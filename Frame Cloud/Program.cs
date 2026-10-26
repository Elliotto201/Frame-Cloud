namespace FrameCloud
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("-s for server or -c for client");

            var str = Console.ReadLine();
            if (str.Contains("-s"))
            {
                Console.WriteLine("Write the password to use permanently for this device, this can be modified in AppData/Local/FrameCloudPassword.txt \n Must be atleast 8 characters");
                string password = Console.ReadLine();

                if(password == null || password.Length < 8)
                {
                    Console.WriteLine("Invalid password!");
                    Main(args);
                }

                _ = Server(password);
            }
            else if(str.Contains("-c"))
            {

            }
            else
            {
                Console.WriteLine("Invalid command");
                Main(args);
            }
        }

        private static async Task Server(string password)
        {
            Server server = new Server();
            ServerHandler handler = new ServerHandler(server, password);

            try
            {
                while (true)
                {
                    await handler.AcceptLoop();
                }
            }
            catch (Exception ex) 
            {
                Console.WriteLine($"Server failed with exception: {ex.Message}");
            }
            finally
            {
                server.Dispose();
            }
        }
    }
}
