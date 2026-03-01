using System.IO;

namespace GakunguWater;

/// <summary>Writes errors to %LocalAppData%\GakunguWater\app.log (max 2 MB, auto-rotated).</summary>
public static class AppLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GakunguWater", "app.log");

    private const long MaxBytes = 2 * 1024 * 1024; // 2 MB

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex == null ? message : $"{message}\n  {ex}");

    public static void Info(string message) => Write("INFO", message);

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

            // Rotate if too large
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxBytes)
                File.Move(LogPath, LogPath + ".bak", overwrite: true);

            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}\n");
        }
        catch { /* never let logging crash the app */ }
    }
}
