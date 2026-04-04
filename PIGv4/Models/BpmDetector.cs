using System.Diagnostics;
using System.Globalization;

namespace PIGv4.Models;

public static class BpmDetector
{
    /// <summary>
    /// Uses aubio to detect BPM from an MP3 file.
    /// Returns null if aubio isn't installed or detection fails.
    /// </summary>
    public static int? Detect(string filePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "aubio",
                Arguments = $"tempo -i \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            // Read both streams async to avoid deadlock
            var stderrTask = process.StandardError.ReadToEndAsync();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            
            process.WaitForExit(30000);
            
            var stderr = stderrTask.Result;
            var stdout = stdoutTask.Result;

            // BPM is output on stderr in format "63.13 bpm"
            var allOutput = (stderr ?? "") + "\n" + (stdout ?? "");
            foreach (var line in allOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.EndsWith("bpm", StringComparison.OrdinalIgnoreCase))
                {
                    var numPart = trimmed.Replace("bpm", "", StringComparison.OrdinalIgnoreCase).Trim();
                    if (double.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var bpm) && bpm > 0)
                        return (int)Math.Round(bpm);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
