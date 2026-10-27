using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FrameCloud
{
    public class ServerHandler
    {
        private Server server;
        private readonly string password;
        private readonly int maxFailedConnectionsPerClientIp;

        private const int MAX_WAIT_ACCEPT_CLIENT_TIME = 10000;
        public const string PASSWORD_REQUEST_MESSAGE = "Require Password";
        public const string PASSWORD_RESPONSE_MESSAGE = "Password: ";

        private class PotentialClient
        {
            public int ClientId;
            public Stopwatch Stopwatch;
            public int PasswordRetryAttempts = 0;

            public PotentialClient(int clientId)
            {
                ClientId = clientId;
                Stopwatch = Stopwatch.StartNew();
            }
        }
        private List<PotentialClient> potentialClients = [];
        private List<int> actualClients = [];
        private Dictionary<IPAddress, int> clientRetryAttempts = [];

        public ServerHandler(Server server, string password, int maxFailedConnectionsPerClientIp)
        {
            this.server = server;
            this.password = password;
            this.maxFailedConnectionsPerClientIp = maxFailedConnectionsPerClientIp;
        }

        public async Task AcceptLoop()
        {
            {
                //-1 means no client waits for acceptance
                int clientId = await server.CheckAccepts();
                if (clientId != -1)
                {
                    Console.WriteLine($"Accepted client with id: {clientId}");

                    byte[] data = Encoding.UTF8.GetBytes(PASSWORD_REQUEST_MESSAGE);
                    await server.SendDataToClient(clientId, data);

                    potentialClients.Add(new PotentialClient(clientId));
                }
            }

            foreach(var client in potentialClients.ToArray())
            {
                var data = await server.TryGetDataFromClient(client.ClientId);
                if(data != null)
                {
                    //TODO: a client could theoretically spam a bunch of invalid password till they guess right
                    //this SHOULD not be an issue considering its hard to guess but still should have a counter messure

                    const int PASSWORD_RETRY_ATTEMPTS = 3;
                    if(client.PasswordRetryAttempts > PASSWORD_RETRY_ATTEMPTS)
                    {
                        bool wrongPassword = false;
                        var str = Encoding.UTF8.GetString(data);
                        if (str != null && str.StartsWith(PASSWORD_RESPONSE_MESSAGE))
                        {
                            str = str.Remove(0, PASSWORD_RESPONSE_MESSAGE.Length);
                            if (str != null && str.Length > 0)
                            {
                                if (str == password)
                                {
                                    potentialClients.Remove(client);
                                    actualClients.Add(client.ClientId);
                                }
                                else
                                {
                                    wrongPassword = true;
                                }
                            }
                            else
                            {
                                wrongPassword = false;
                            }
                        }
                        else
                        {
                            wrongPassword = false;
                        }

                        if (wrongPassword)
                        {
                            client.PasswordRetryAttempts++;
                        }
                    }
                }

                int timeElapsed = (int)client.Stopwatch.ElapsedMilliseconds;

                if(timeElapsed > MAX_WAIT_ACCEPT_CLIENT_TIME)
                {
                    //Client failed connection
                    potentialClients.Remove(client);
                    server.DisconnectClient(client.ClientId);

                    int oldValue = 0;
                    var address = server.GetClientAddress(client.ClientId);

                    if(address != null)
                    {
                        clientRetryAttempts.TryGetValue(address, out oldValue);
                    }
                    oldValue++;

                    if(address == null)
                    {
                        throw new Exception("Failed to get address from client!");
                    }
                    clientRetryAttempts[address] = oldValue;
                }
            }
        }
    }
}
