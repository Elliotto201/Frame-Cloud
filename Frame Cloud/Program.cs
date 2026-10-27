using System.Globalization;

namespace FrameCloud
{
    internal class Program
    {
        public static bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            // Get the array of characters forbidden in file names
            char[] invalidChars = Path.GetInvalidFileNameChars();

            // Check if the filename contains any invalid characters
            return fileName.IndexOfAny(invalidChars) < 0;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("-s for server, -c for client or -d to delete a project");

            var str = Console.ReadLine();
            if (str.Contains("-s"))
            {
                Console.WriteLine("Select project from available list or create a new one");
                string availableProjects = PersistentData.ReadFromFileText("Projects");
                string[] projectsArr = availableProjects.Split('\n');

                Console.WriteLine("Available projects: ");

                if(availableProjects.Length > 0)
                {
                    var englishComparer = StringComparer.Create(new CultureInfo("en-US"), ignoreCase: true);
                    foreach (var project in projectsArr.OrderBy(t => t, englishComparer))
                    {
                        Console.WriteLine($"-{project}");
                    }
                }

                string selectedProject = Console.ReadLine();
                if(selectedProject == null || selectedProject == string.Empty || !IsValidFileName(selectedProject))
                {
                    Console.WriteLine("Invalid project");
                    Main(args);
                }

                string password = PersistentData.ReadFromFileTextEncryptedDPAPI($"{selectedProject}/Password");
                bool newProject = false;
                if (projectsArr.Contains(selectedProject))
                {
                    newProject = false;
                }
                else
                {
                    PersistentData.CreateFolder(selectedProject);
                    PersistentData.AppendToFileText("Projects", selectedProject + "\n");

                    newProject = true;
                }

                bool passwordNeedsSaving = false;
                if(password.Length < 8)
                {
                    if (newProject)
                    {
                        Console.WriteLine("Write the password to use permanently for this project\nMust be atleast 8 characters");
                        passwordNeedsSaving = true;
                    }
                    else
                    {
                        Console.WriteLine("Saved password could not be found for this project, please create a new one, atleast 8 characters");
                        passwordNeedsSaving = true;
                    }
                    password = Console.ReadLine();

                    if (password == null || password.Length < 8)
                    {
                        Console.WriteLine("Invalid password!");
                        Main(args);
                    }
                }

                if (passwordNeedsSaving)
                {
                    PersistentData.WriteToFileTextEncryptedDPAPI($"{selectedProject}/Password", password);
                }

                Console.WriteLine("Starting the server");
                _ = Server(password);
            }
            else if(str.Contains("-c"))
            {

            }
            else if (str.Contains("-d"))
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
            ServerHandler handler = new ServerHandler(server, password, 3);

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

        private static async Task Client(string password)
        {

        }
    }
}
