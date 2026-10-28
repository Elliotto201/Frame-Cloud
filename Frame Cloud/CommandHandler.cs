namespace FrameCloud
{
    public static class CommandHandler
    {
        public static void Start(CancellationToken token)
        {
            _ = Task.Run(() => ReadLoop(token));
        }

        private static void ReadLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                string? line = Console.ReadLine();
                if (line == null)
                    return;

                Execute(line.Trim());
            }
        }

        public static void Execute(string line)
        {
            if (line.Length == 0)
                return;

            if (line.StartsWith("-loglevel", StringComparison.OrdinalIgnoreCase))
            {
                string argument = line["-loglevel".Length..].Trim();

                if (Enum.TryParse(argument, true, out LogLevel level) && Enum.IsDefined(level))
                {
                    Logger.CurrentLevel = level;
                    Console.WriteLine($"Log level set to {level}");
                }
                else
                {
                    Console.WriteLine("Usage: -loglevel None|Low|High");
                }
            }
            else
            {
                Console.WriteLine($"Unknown command: {line}");
            }
        }
    }
}