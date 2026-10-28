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

    public static async Task<ProjectHash> HashProject(string projectFolder)
    {
        var stopwatch = Stopwatch.StartNew();
        ProjectHash hash = new ProjectHash();

        foreach (var file in Directory.GetFiles(projectFolder, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(projectFolder, file).Replace('\\', '/');
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            var hashBytes = await SHA256.HashDataAsync(stream);
            hash.FileHashes[relative] = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        Logger.Log($"Hashed {hash.FileHashes.Count} files in {stopwatch.ElapsedMilliseconds}ms", LogLevel.High);

        return hash;
    }
}
