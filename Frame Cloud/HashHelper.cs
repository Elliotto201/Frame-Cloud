using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace FrameCloud;

public sealed class ProjectHash
{
    public Dictionary<string, string> FileHashes = [];

    public static bool operator ==(ProjectHash? a, ProjectHash? b)
    {
        if (ReferenceEquals(a, b))
            return true;

        if (a is null || b is null)
            return false;

        return a.FileHashes.Count == b.FileHashes.Count
            && a.FileHashes.All(pair =>
                b.FileHashes.TryGetValue(pair.Key, out var value)
                && pair.Value == value);
    }

    public static bool operator !=(ProjectHash? a, ProjectHash? b)
        => !(a == b);

    public override bool Equals(object? obj)
        => obj is ProjectHash other && this == other;

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var pair in FileHashes.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }
}

public static class HashHelper
{
    public static string HashString(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task<ProjectHash> HashProject(string[] folders)
    {
        Stopwatch stopwatch = null;

        if (Current.Debug)
        {
            stopwatch = Stopwatch.StartNew();
        }

        ProjectHash hash = new ProjectHash();

        foreach(var folder in folders)
        {
            foreach(var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                var data = await File.ReadAllBytesAsync(file);
                var str = Convert.ToBase64String(data);
                var hashedStr = HashString(str);

                hash.FileHashes[file] = hashedStr;
            }
        }

        if (Current.Debug)
        {
            Console.WriteLine($"Hashed project in: {stopwatch.Elapsed.TotalSeconds} seconds");
        }

        return hash;
    }
}
