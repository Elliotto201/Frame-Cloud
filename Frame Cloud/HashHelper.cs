using System;
using System.Security.Cryptography;
using System.Text;

namespace FrameCloud;

public static class HashHelper
{
    public static string HashString(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        byte[] bytes = Encoding.UTF8.GetBytes(input);
        byte[] hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}