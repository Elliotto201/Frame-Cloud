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
    public class Client : IDisposable
    {
        private TcpClient client;

        public async Task<bool> ConnectAsync(IPAddress address)
        {
            try
            {
                client = new TcpClient(address.AddressFamily);
                await client.ConnectAsync(address, Server.SERVER_PORT);

                Console.WriteLine("Connection successful to server");

                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Connection failed {ex.Message}");

                return false;
            }
        }

        public void Dispose()
        {
            client.Close();
        }

        public async Task TrySendDataToServerAsync(Memory<byte> data)
        {
            Memory<byte> bytes = new byte[data.Length + 4];
            Memory<byte> dataBuffer = bytes.Slice(4, data.Length);
            data.CopyTo(dataBuffer);

            BinaryPrimitives.WriteInt32BigEndian(bytes.Span, data.Length);
            await client.GetStream().WriteAsync(bytes);
        }

        public async Task<byte[]?> TryGetServerDataAsync()
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

            while (readLength < length)
            {
                if (triedReads > maxTriedReads)
                {
                    return null;
                }

                readLength += await client.GetStream().ReadAsync(data);
                triedReads++;

                await Task.Delay(100);
            }

            return data;
        }
    }
}
