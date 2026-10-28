using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace FrameCloud
{
    /// <summary>
    /// Length-prefixed message connection. Never throws on connection loss:
    /// sends return false, receives return null, and the connection closes itself.
    /// </summary>
    public class Connection : IDisposable
    {
        private const int MaxMessageLength = 16 * 1024 * 1024;

        private readonly TcpClient tcp;
        private readonly SemaphoreSlim sendLock = new(1, 1);
        private int disposed;

        public string Name { get; }
        public IPAddress? Address { get; }
        public bool IsConnected => Volatile.Read(ref disposed) == 0;

        public event Action? Disconnected;

        public Connection(TcpClient tcp)
        {
            this.tcp = tcp;

            IPEndPoint? endPoint = null;
            try
            {
                endPoint = tcp.Client.RemoteEndPoint as IPEndPoint;
            }
            catch (Exception ex) when (IsConnectionError(ex))
            {
            }

            Address = endPoint?.Address;
            Name = endPoint?.ToString() ?? "unknown";
        }

        public async Task<bool> SendAsync(ReadOnlyMemory<byte> data)
        {
            if (!IsConnected)
                return false;

            byte[] buffer = new byte[data.Length + 4];
            BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
            data.CopyTo(buffer.AsMemory(4));

            try
            {
                await sendLock.WaitAsync();
                try
                {
                    await tcp.GetStream().WriteAsync(buffer);
                }
                finally
                {
                    sendLock.Release();
                }

                return true;
            }
            catch (Exception ex) when (IsConnectionError(ex))
            {
                Logger.Log($"Failed to send to {Name}: {ex.Message}", LogLevel.High);
                Dispose();
                return false;
            }
        }

        /// <summary>
        /// Returns null if the connection was lost or sent invalid data.
        /// Only throws OperationCanceledException.
        /// </summary>
        public async Task<byte[]?> ReceiveAsync(CancellationToken token = default)
        {
            try
            {
                NetworkStream stream = tcp.GetStream();

                byte[] header = new byte[4];
                await stream.ReadExactlyAsync(header, token);

                int length = BinaryPrimitives.ReadInt32BigEndian(header);
                if (length < 0 || length > MaxMessageLength)
                {
                    Logger.Log($"{Name} sent an invalid message length ({length})", LogLevel.Low);
                    Dispose();
                    return null;
                }

                byte[] data = new byte[length];
                await stream.ReadExactlyAsync(data, token);

                return data;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && IsConnectionError(ex))
            {
                Logger.Log($"Lost connection to {Name}: {ex.Message}", LogLevel.High);
                Dispose();
                return null;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            try
            {
                tcp.Close();
            }
            catch (Exception ex) when (IsConnectionError(ex))
            {
            }

            Logger.Log($"Closed connection to {Name}", LogLevel.High);
            Disconnected?.Invoke();
        }

        private static bool IsConnectionError(Exception ex)
        {
            return ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException;
        }
    }

    public sealed class ConnectedClient : Connection
    {
        public DateTime ConnectedAtUtc { get; } = DateTime.UtcNow;

        public ConnectedClient(TcpClient tcp) : base(tcp)
        {
        }
    }
}