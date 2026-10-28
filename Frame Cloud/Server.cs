using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace FrameCloud
{
    public sealed class Server : IDisposable
    {
        public const int SERVER_PORT = 9865;

        private readonly TcpListener listener;
        private readonly ConcurrentDictionary<ConnectedClient, byte> clients = new();

        public IEnumerable<ConnectedClient> Clients => clients.Keys;

        public Server(bool local)
        {
            listener = new TcpListener(local ? IPAddress.Loopback : IPAddress.Any, SERVER_PORT);
            listener.Start();

            Logger.Log($"Listening on port {SERVER_PORT}", LogLevel.Low);
        }

        /// <summary>
        /// Waits for the next client. Returns null if the accept failed. Throws OperationCanceledException on cancellation.
        /// </summary>
        public async Task<ConnectedClient?> AcceptAsync(CancellationToken token)
        {
            try
            {
                TcpClient tcp = await listener.AcceptTcpClientAsync(token);

                var client = new ConnectedClient(tcp);
                clients.TryAdd(client, 0);
                client.Disconnected += () => clients.TryRemove(client, out _);

                return client;
            }
            catch (SocketException ex)
            {
                Logger.Log($"Failed to accept a client: {ex.Message}", LogLevel.Low);
                return null;
            }
        }

        public void Dispose()
        {
            listener.Stop();

            foreach (var client in clients.Keys)
            {
                client.Dispose();
            }
        }
    }
}
