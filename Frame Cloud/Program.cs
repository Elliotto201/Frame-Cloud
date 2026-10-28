using System.Globalization;
using System.Net;
using System.Text;

namespace FrameCloud
{
    internal class Program
    {
        const string ProjectHashPrefix = "ProjectHash: ";

        public static bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            char[] invalidChars = Path.GetInvalidFileNameChars();

            return fileName.IndexOfAny(invalidChars) < 0 && fileName != "." && fileName != ".."
                && !fileName.Contains('/') && !fileName.Contains('\\');
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("-s for server, -c for client or -d to delete a project");

            string? str = Console.ReadLine();
            if (str == null)
                return;

            if (str.Contains("-s"))
            {
                var project = SetupProject(true);
                if (project == null)
                    return;

                var cts = StartSession();

                Logger.Log("Starting the server", LogLevel.Low);
                await RunServerAsync(project.Value.password, project.Value.projectFolder, cts.Token);
            }
            else if (str.Contains("-c"))
            {
                var project = SetupProject(false);
                if (project == null)
                    return;

                var cts = StartSession();

                Logger.Log("Starting the client", LogLevel.Low);
                await RunClientAsync(project.Value.password, project.Value.projectFolder, cts.Token);
            }
            else if (str.Contains("-d"))
            {
                Console.WriteLine("Delete from -s server projects or -c client projects?");
                string? mode = Console.ReadLine();

                if (mode == null || (!mode.Contains("-s") && !mode.Contains("-c")))
                {
                    Console.WriteLine("Invalid command");
                    return;
                }

                DeleteProject(mode.Contains("-s"));
            }
            else
            {
                Console.WriteLine("Invalid command");
            }
        }

        /// <summary>
        /// Enables Ctrl+C shutdown and the runtime command input (e.g. -loglevel).
        /// </summary>
        private static CancellationTokenSource StartSession()
        {
            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                Logger.Log("Shutting down...", LogLevel.Low);
                cts.Cancel();
            };

            CommandHandler.Start(cts.Token);

            return cts;
        }

        private static string[] GetProjects()
        {
            string availableProjects = PersistentData.ReadFromFileText("Projects");

            return availableProjects.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(project => project.TrimEnd('\r'))
                .Where(project => !string.IsNullOrWhiteSpace(project))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void PrintProjects(string[] projects)
        {
            Console.WriteLine("Available projects:");

            var englishComparer = StringComparer.Create(new CultureInfo("en-US"), true);
            foreach (string project in projects.OrderBy(project => project, englishComparer))
            {
                Console.WriteLine($"-{project}");
            }
        }

        private static (string password, string projectFolder)? SetupProject(bool isServer)
        {
            PersistentData.SetMode(isServer);

            string[] projects = GetProjects();

            Console.WriteLine("Select project from available list or create a new one");
            PrintProjects(projects);

            string? selectedProject = Console.ReadLine();
            if (selectedProject == null || selectedProject == string.Empty || !IsValidFileName(selectedProject))
            {
                Console.WriteLine("Invalid project");
                return null;
            }

            bool projectExists = projects.Contains(selectedProject, StringComparer.OrdinalIgnoreCase);
            string projectFolder = PersistentData.GetFolderFromName(selectedProject);

            PersistentData.CreateFolder(selectedProject);

            if (!projectExists)
            {
                projects = projects.Append(selectedProject).ToArray();
                PersistentData.WriteToFileText("Projects", string.Join(Environment.NewLine, projects));
            }

            string password = PersistentData.ReadFromFileTextEncryptedDPAPI($"{selectedProject}/Password");

            if (password.Length < 8)
            {
                if (isServer)
                {
                    if (!projectExists)
                        Console.WriteLine("Write the password to use permanently for this project\nMust be atleast 8 characters");
                    else
                        Console.WriteLine("Saved password could not be found for this project, please create a new one, atleast 8 characters");
                }
                else
                {
                    Console.WriteLine("Enter the password for this project\nMust be atleast 8 characters");
                }

                password = Console.ReadLine() ?? string.Empty;
                if (password.Length < 8)
                {
                    Console.WriteLine("Invalid password!");
                    return null;
                }

                PersistentData.WriteToFileTextEncryptedDPAPI($"{selectedProject}/Password", password);
            }

            return (password, projectFolder);
        }

        private static void DeleteProject(bool isServer)
        {
            PersistentData.SetMode(isServer);

            string[] projects = GetProjects();
            if (projects.Length == 0)
            {
                Console.WriteLine("There are no projects to delete.");
                return;
            }

            Console.WriteLine("Select a project to delete");
            PrintProjects(projects);

            string? selectedProject = Console.ReadLine();
            if (selectedProject == null || !IsValidFileName(selectedProject))
            {
                Console.WriteLine("Invalid project");
                return;
            }

            string? projectName = projects.FirstOrDefault(project => string.Equals(project, selectedProject, StringComparison.OrdinalIgnoreCase));
            if (projectName == null)
            {
                Console.WriteLine("Invalid project");
                return;
            }

            Console.WriteLine($"Are you sure you want to permanently delete project '{projectName}' and all its files? (y/n)");
            string? confirmation = Console.ReadLine();

            if (!string.Equals(confirmation, "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Deletion cancelled.");
                return;
            }

            string projectFolder = Path.GetFullPath(PersistentData.GetFolderFromName(projectName));
            string localDataFolder = Path.GetFullPath(PersistentData.GetFolderFromName(""));

            if (!string.Equals(Path.GetDirectoryName(projectFolder), localDataFolder.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Invalid project path.");
                return;
            }

            if (Directory.Exists(projectFolder))
                Directory.Delete(projectFolder, true);

            projects = projects.Where(project => !string.Equals(project, projectName, StringComparison.OrdinalIgnoreCase)).ToArray();
            PersistentData.WriteToFileText("Projects", string.Join(Environment.NewLine, projects));

            Console.WriteLine($"Project '{projectName}' deleted.");
        }

        private static async Task RunServerAsync(string password, string projectFolder, CancellationToken token)
        {
            using Server server = new Server(true);
            ServerHandler handler = new ServerHandler(server, password, 3);

            handler.ClientAuthenticated = async client =>
            {
                var projectHash = await HashHelper.HashProject(projectFolder);
                var hashMsg = new ProjectHashMessage { Hash = projectHash };

                if (await client.SendAsync(Encoding.UTF8.GetBytes(MessageSerializer.SerializeMessage(hashMsg))))
                {
                    Logger.Log($"Sent project hash to client {client.Name}", LogLevel.High);
                }
            };

            try
            {
                await handler.RunAsync(token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Log($"Server stopped unexpectedly: {ex}", LogLevel.Low);
            }

            Logger.Log("Server stopped", LogLevel.Low);
        }

        private static async Task RunClientAsync(string password, string projectFolder, CancellationToken token)
        {
            using Client client = new Client();
            ClientHandler handler = new ClientHandler(client, password, projectFolder);

            try
            {
                if (!await client.ConnectAsync(IPAddress.Loopback, token))
                    return;

                await handler.RunAsync(token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Log($"Client stopped unexpectedly: {ex}", LogLevel.Low);
            }

            Logger.Log("Client stopped", LogLevel.Low);
        }
    }
}
