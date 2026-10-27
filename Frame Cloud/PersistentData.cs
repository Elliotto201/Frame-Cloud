using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FrameCloud
{
    public static class PersistentData
    {
        private static string LocalData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FrameCloud");

        /// <summary>
        /// Dont include file extension
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="text"></param>
        public static void WriteToFileText(string fileName, string text)
        {
            string path = Path.Combine(LocalData, fileName + ".txt");
            Console.WriteLine($"Writing to text file: {path}");

            File.WriteAllText(path, text);
        }

        public static async Task WriteToFileTextAsync(string fileName, string text)
        {
            string path = Path.Combine(LocalData, fileName + ".txt");
            Console.WriteLine($"Writing to text file: {path}");

            await File.WriteAllTextAsync(path, text);
        }

        public static void AppendToFileText(string fileName, string text)
        {
            string path = Path.Combine(LocalData, fileName + ".txt");
            Console.WriteLine($"Appending to text file: {path}");

            if (!File.Exists(path))
            {
                File.Create(path).Dispose();
            }

            File.AppendAllText(path, text);
        }

        public static string ReadFromFileText(string fileName)
        {
            string path = Path.Combine(LocalData, fileName + ".txt");
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            Console.WriteLine($"Read text from file {path}");

            return File.ReadAllText(path);
        }

        public static async Task<string> ReadFromFileTextAsync(string fileName)
        {
            string path = Path.Combine(LocalData, fileName + ".txt");
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            Console.WriteLine($"Read text from file {path}");

            return await File.ReadAllTextAsync(path);
        }

        public static void CreateFolder(string folder)
        {
            Directory.CreateDirectory(Path.Combine(LocalData, folder));
        }

        public static void WriteToFileTextEncryptedDPAPI(string fileName, string text)
        {
            string keyPath = Path.Combine(LocalData, fileName + ".dat");
            byte[] data = Encoding.UTF8.GetBytes(text);

            var protect = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(keyPath, protect);
        }

        public static string ReadFromFileTextEncryptedDPAPI(string fileName)
        {
            string keyPath = Path.Combine(LocalData, fileName + ".dat");
            if (!File.Exists(keyPath))
            {
                return string.Empty;
            }

            byte[] data = File.ReadAllBytes(keyPath);

            var unprotect = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
            if(unprotect == null)
            {
                return string.Empty;
            }

            return Encoding.UTF8.GetString(unprotect);
        }
    }
}
