namespace FrameCloud
{
    public enum LogLevel
    {
        None,
        Low,
        High
    }

    public static class Logger
    {
        private static readonly object ConsoleLock = new();

        public static LogLevel CurrentLevel { get; set; } = LogLevel.Low;

        public static void Log(string text, LogLevel level = LogLevel.Low)
        {
            if (level == LogLevel.None || level > CurrentLevel)
                return;

            lock (ConsoleLock)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{level}] {text}");
            }
        }
    }
}