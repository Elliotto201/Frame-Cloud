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
    public class Server : IDisposable
    {
        public const int SERVER_PORT = 9865;

        private TcpListener listener;

        private int clientIdCurrent = 1;
        private ConcurrentDictionary<int, TcpClient> clients = [];

        public Server()
        {
            listener = new TcpListener(IPAddress.Any, SERVER_PORT);
            listener.Start();
        }

        public async Task<int> CheckAccepts()
        {
            if (listener.Pending())
            {
                clientIdCurrent++;

                var client = await listener.AcceptTcpClientAsync();

                if(!clients.TryAdd(clientIdCurrent, client))
                {
                    throw new Exception($"Failed to create a new client");
                }

                return clientIdCurrent;
            }

            return -1;
        }

        public void Dispose()
        {
            listener.Dispose();
            foreach(var client in clients.Values)
            {
                client.Dispose();
            }
        }

        public void DisconnectClient(int clientId)
        {
            if(clients.TryGetValue(clientId, out var client))
            {
                clients.Remove(clientId, out var tcpClient);
                tcpClient?.Close();
            }
        }

        public async Task<bool> SendDataToClient(int clientId, Memory<byte> data)
        {
            if(clients.TryGetValue(clientId, out var client))
            {
                Memory<byte> bytes = new byte[data.Length + 4];
                Memory<byte> dataBuffer = bytes.Slice(4, data.Length);
                data.CopyTo(dataBuffer);

                BinaryPrimitives.WriteInt32BigEndian(bytes.Span, data.Length);
                await client.GetStream().WriteAsync(bytes);

                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<byte[]?> TryGetDataFromClient(int clientId)
        {
            if(clients.TryGetValue(clientId, out var client))
            {
                Memory<byte> lengthData = new byte[4];
                try
                {
                    await client.GetStream().ReadExactlyAsync(lengthData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Reading server-data failed beacuse of invalid message length prefix: {ex.Message}");
                    return null;
                }
                int length = BinaryPrimitives.ReadInt32BigEndian(lengthData.Span);

                byte[] data = new byte[length];
                int readLength = 0;

                int triedReads = 0;
                const int maxTriedReads = 1000;

                while(readLength < length)
                {
                    if(triedReads > maxTriedReads)
                    {
                        return null;
                    }

                    readLength += await client.GetStream().ReadAsync(data);
                    triedReads++;

                    await Task.Delay(100);
                }

                return data;
            }
            else
            {
                return null;
            }
        }
    }
}
