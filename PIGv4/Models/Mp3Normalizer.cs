using System.Diagnostics;

namespace PIGv4.Models;

public static class Mp3Normalizer
{
    /// <summary>
    /// Normalizes an MP3 file to the target dB using mp3gain.
    /// mp3gain default target is 89.0 dB. The -d flag adjusts relative to that.
    /// Modifies the file in-place (lossless frame adjustment, no re-encoding).
    /// Returns true if successful.
    /// </summary>
    public static bool Normalize(string filePath, double targetDb = 89.0)
    {
        try
        {
            // mp3gain uses 89.0 as its baseline.
            // -d <adjustment> shifts the target: -d 2 means 91 dB, -d -2 means 87 dB
            var adjustment = targetDb - 89.0;
            var dArg = adjustment != 0 ? $"-d {adjustment:F1}" : "";

            var psi = new ProcessStartInfo
            {
                FileName = "mp3gain",
                // -r: apply Track gain (per-file normalization)
                // -c: skip asking to clip
                // -q: quiet
                Arguments = $"-r -c -q {dArg} \"{filePath}\"".Trim(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            process.WaitForExit(60000); // 60 second timeout
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
