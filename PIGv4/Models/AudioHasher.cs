using System.Security.Cryptography;

namespace PIGv4.Models;

public static class AudioHasher
{
    /// <summary>
    /// Computes SHA256 hash of audio-only data (skips ID3 tags).
    /// Uses TagLib to find where the actual audio starts/ends.
    /// </summary>
    public static string ComputeHash(string filePath)
    {
        using var tagFile = TagLib.File.Create(filePath);
        var start = tagFile.InvariantStartPosition;
        var end = tagFile.InvariantEndPosition;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        fs.Seek(start, SeekOrigin.Begin);

        var length = end - start;
        var buffer = new byte[length];
        fs.ReadExactly(buffer, 0, (int)length);

        var hashBytes = SHA256.HashData(buffer);
        return Convert.ToHexStringLower(hashBytes);
    }
}
