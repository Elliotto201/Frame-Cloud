using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FrameCloud
{
    public sealed class Client : IDisposable
    {
        private Connection? connection;

        public bool IsConnected => connection?.IsConnected ?? false;

        public async Task<bool> ConnectAsync(IPAddress address, CancellationToken token = default)
        {
            TcpClient tcp = new TcpClient(address.AddressFamily);

            try
            {
                await tcp.ConnectAsync(address, Server.SERVER_PORT, token);
            }
            catch (SocketException ex)
            {
                Logger.Log($"Connection failed: {ex.Message}", LogLevel.Low);
                tcp.Dispose();
                return false;
            }
            catch (OperationCanceledException)
            {
                tcp.Dispose();
                throw;
            }

            connection = new Connection(tcp);
            Logger.Log("Connected to server", LogLevel.Low);

            return true;
        }

        public Task<bool> SendAsync(ReadOnlyMemory<byte> data)
        {
            return connection?.SendAsync(data) ?? Task.FromResult(false);
        }

        /// <summary>
        /// Returns null if the server connection was lost.
        /// </summary>
        public Task<byte[]?> ReceiveAsync(CancellationToken token = default)
        {
            return connection?.ReceiveAsync(token) ?? Task.FromResult<byte[]?>(null);
        }

        public void Dispose()
        {
            connection?.Dispose();
        }
    }
}
