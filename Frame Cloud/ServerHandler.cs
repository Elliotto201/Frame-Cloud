using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrameCloud
{
    public class ServerHandler
    {
        private Server server;
        private string password;

        private const int MAX_WAIT_ACCEPT_CLIENT_TIME = 5000;
        public const string PASSWORD_REQUEST_MESSAGE = "Require Password";
        public const string PASSWORD_RESPONSE_MESSAGE = "Password: ";

        private class PotentialClient
        {
            public int ClientId;
            public Stopwatch Stopwatch;

            public PotentialClient()
            {
                Stopwatch = Stopwatch.StartNew();
            }
        }
        private List<PotentialClient> potentialClients = [];
        private List<int> actualClients = [];

        public ServerHandler(Server server, string password)
        {
            this.server = server;
            this.password = password;
        }

        public async Task AcceptLoop()
        {
            {
                int clientId = await server.CheckAccepts();
                if (clientId != -1)
                {
                    Console.WriteLine($"Accepted client with id: {clientId}");

                    byte[] data = Encoding.UTF8.GetBytes(PASSWORD_REQUEST_MESSAGE);
                    await server.SendDataToClient(clientId, data);
                }
                else
                {
                    Console.WriteLine("Failed to accept client");
                }
            }

            foreach(var client in potentialClients.ToArray())
            {
                var data = await server.TryGetDataFromClient(client.ClientId);
                if(data != null)
                {
                    //TODO: a client could theoretically spam a bunch of invalid password till they guess right
                    //this SHOULD not be an issue considering its hard to guess but still should have a counter messure

                    var str = Encoding.UTF8.GetString(data);
                    if(str != null && str.StartsWith(PASSWORD_RESPONSE_MESSAGE))
                    {
                        str = str.Remove(0, PASSWORD_RESPONSE_MESSAGE.Length);
                        if(str != null && str.Length > 0)
                        {
                            if(str == password)
                            {
                                potentialClients.Remove(client);
                                actualClients.Add(client.ClientId);
                            }
                        }
                    }
                }

                //TODO: Potentially unsafe cast
                int timeElapsed = (int)client.Stopwatch.ElapsedMilliseconds;

                if(timeElapsed > MAX_WAIT_ACCEPT_CLIENT_TIME)
                {
                    potentialClients.Remove(client);
                    server.DisconnectClient(client.ClientId);
                }
            }
        }
    }
}
