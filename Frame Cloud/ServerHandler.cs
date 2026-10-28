using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FrameCloud
{
    public class ServerHandler
    {
        private const int MaxWaitAcceptClientTime = 10000;

        private readonly Server server;
        private readonly string password;
        private readonly int maxFailedConnectionsPerClientIp;
        private readonly ConcurrentDictionary<IPAddress, int> failedAttempts = new();

        /// <summary>
        /// Called once for every client that authenticated successfully.
        /// </summary>
        public Func<ConnectedClient, Task>? ClientAuthenticated { get; set; }

        public ServerHandler(Server server, string password, int maxFailedConnectionsPerClientIp)
        {
            this.server = server;
            this.password = password;
            this.maxFailedConnectionsPerClientIp = maxFailedConnectionsPerClientIp;
        }

        public async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                ConnectedClient? client = await server.AcceptAsync(token);
                if (client == null)
                {
                    await Task.Delay(50, token);
                    continue;
                }

                Logger.Log($"Accepted client {client.Name}", LogLevel.Low);

                _ = HandleClientAsync(client, token);
            }
        }

        private async Task HandleClientAsync(ConnectedClient client, CancellationToken token)
        {
            try
            {
                if (client.Address != null
                    && failedAttempts.TryGetValue(client.Address, out int failures)
                    && failures >= maxFailedConnectionsPerClientIp)
                {
                    Logger.Log($"Rejected {client.Name}: too many failed attempts", LogLevel.Low);
                    client.Dispose();
                    return;
                }

                if (!await AuthenticateAsync(client, token))
                {
                    if (client.Address != null)
                    {
                        failedAttempts.AddOrUpdate(client.Address, 1, (_, count) => count + 1);
                    }

                    client.Dispose();
                    return;
                }

                Logger.Log($"Client {client.Name} authenticated", LogLevel.Low);

                if (ClientAuthenticated != null)
                {
                    await ClientAuthenticated(client);
                }
            }
            catch (OperationCanceledException)
            {
                client.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error while handling client {client.Name}: {ex.Message}", LogLevel.Low);
                client.Dispose();
            }
        }

        private async Task<bool> AuthenticateAsync(ConnectedClient client, CancellationToken token)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(MaxWaitAcceptClientTime);

            try
            {
                byte[] request = Encoding.UTF8.GetBytes(MessageSerializer.SerializeMessage(new PasswordRequestMessage()));
                if (!await client.SendAsync(request))
                    return false;

                byte[]? data = await client.ReceiveAsync(timeout.Token);
                if (data == null)
                    return false;

                string text = Encoding.UTF8.GetString(data);

                if (MessageSerializer.GetMessageType(text) != MessageType.PasswordResponse)
                {
                    Logger.Log($"Client {client.Name} sent an unexpected message during authentication", LogLevel.Low);
                    return false;
                }

                var response = MessageSerializer.DeserializeMessage<PasswordResponseMessage>(text);
                if (response.Password != password)
                {
                    Logger.Log($"Client {client.Name} sent a wrong password", LogLevel.Low);
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                Logger.Log($"Client {client.Name} timed out during authentication", LogLevel.Low);
                return false;
            }
            catch (JsonException ex)
            {
                Logger.Log($"Client {client.Name} sent an invalid message: {ex.Message}", LogLevel.Low);
                return false;
            }
        }
    }
}
