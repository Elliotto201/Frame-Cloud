using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FrameCloud
{
    public class ClientHandler
    {
        private readonly Client client;
        private readonly string password;
        private readonly string projectFolder;

        public ClientHandler(Client client, string password, string projectFolder)
        {
            this.client = client;
            this.password = password;
            this.projectFolder = projectFolder;
        }

        /// <summary>
        /// Runs until the server disconnects or the token is cancelled.
        /// </summary>
        public async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                byte[]? data = await client.ReceiveAsync(token);
                if (data == null)
                {
                    Logger.Log("Lost connection to the server", LogLevel.Low);
                    return;
                }

                try
                {
                    if (!await HandleMessageAsync(Encoding.UTF8.GetString(data)))
                    {
                        Logger.Log("Lost connection to the server", LogLevel.Low);
                        return;
                    }
                }
                catch (JsonException ex)
                {
                    Logger.Log($"Ignored an invalid message from the server: {ex.Message}", LogLevel.Low);
                }
            }
        }

        /// <summary>
        /// Returns false if the connection was lost while responding.
        /// </summary>
        private async Task<bool> HandleMessageAsync(string text)
        {
            MessageType messageType = MessageSerializer.GetMessageType(text);

            switch (messageType)
            {
                case MessageType.PasswordRequest:
                    Logger.Log("Received password request, sending password", LogLevel.High);

                    string response = MessageSerializer.SerializeMessage(new PasswordResponseMessage { Password = password });
                    return await client.SendAsync(Encoding.UTF8.GetBytes(response));

                case MessageType.ProjectHash:
                    await HandleProjectHashAsync(text);
                    return true;

                default:
                    Logger.Log($"Received unexpected message of type {messageType}", LogLevel.High);
                    return true;
            }
        }

        private async Task HandleProjectHashAsync(string text)
        {
            ProjectHash? serverHash = MessageSerializer.DeserializeMessage<ProjectHashMessage>(text).Hash;
            if (serverHash == null)
            {
                Logger.Log("Received a project hash message without a hash", LogLevel.Low);
                return;
            }

            ProjectHash localHash = await HashHelper.HashProject(projectFolder);

            List<string> failedHashes = new();
            foreach (var kvp in serverHash.FileHashes)
            {
                if (!localHash.FileHashes.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
                {
                    failedHashes.Add(kvp.Key);
                }
            }

            Logger.Log($"Received server hash with {failedHashes.Count} mismatches", LogLevel.High);
        }
    }
}
