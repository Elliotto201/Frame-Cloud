using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FrameCloud
{
    public class ServerFileManager
    {
        public sealed class FileReplacement
        {
            public FileReplacement(string path)
            {
                Path = path;
            }

            public string Path { get; private set; }
            public string TmpPath => Path + ".tmp";
        }

        public string ProjectFolder { get; private set; }

        public ServerFileManager(string project)
        {

        }

        public FileReplacement BeginReplacement(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new ArgumentException($"Invalid filepath {filePath}");
            }

            var replacement = new FileReplacement(filePath);
            File.Create(replacement.TmpPath).Dispose();

            return replacement;
        }

        private async Task WaitForFileReleaseAsync(string filePath, int maxRetries = 10, int delayMs = 600)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using (var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        return;
                    }
                }
                catch (IOException)
                {
                    await Task.Delay(delayMs);
                }
            }

            throw new IOException($"File {filePath} is still locked after waiting.");
        }


        public async Task AppendChunkToFileAsync(FileReplacement replacement, Memory<byte> data)
        {
            await WaitForFileReleaseAsync(replacement.TmpPath);

            await File.AppendAllBytesAsync(replacement.TmpPath, data);
        }

        public void FinnishReplacement(FileReplacement replacement)
        {
            File.Move(replacement.TmpPath, replacement.Path, true);
        }
    }
}
