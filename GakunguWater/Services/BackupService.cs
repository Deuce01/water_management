using GakunguWater.Data;
using GakunguWater.Models;
using Dapper;
using System.IO;

namespace GakunguWater.Services;

public class BackupService
{
    private readonly DatabaseService _db;
    private readonly string _dbPath;
    private System.Threading.Timer? _timer;

    public string BackupTargetPath { get; set; } = @"D:\GakunguWaterBackup";
    public DateTime? LastBackupTime { get; private set; }
    public bool LastBackupSuccess { get; private set; }

    public BackupService(DatabaseService db, string dbPath)
    {
        _db = db;
        _dbPath = dbPath;
    }

    public void StartAutoBackup()
    {
        // First backup 30 seconds after startup, then every 24 hours
        _timer = new System.Threading.Timer(_ => PerformBackup(),
            null, TimeSpan.FromSeconds(30), TimeSpan.FromHours(24));
    }

    public void StopAutoBackup() => _timer?.Dispose();

    public BackupResult PerformBackup(string? targetPathOverride = null)
    {
        var targetDir = targetPathOverride ?? BackupTargetPath;
        var result = new BackupResult();
        try
        {
            Directory.CreateDirectory(targetDir);
            var fileName = $"GakunguWater_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var destPath = Path.Combine(targetDir, fileName);

            File.Copy(_dbPath, destPath, overwrite: false);

            var info = new FileInfo(destPath);
            result.Success = true;
            result.Path = destPath;
            result.SizeBytes = info.Length;

            LastBackupTime = DateTime.Now;
            LastBackupSuccess = true;

            // Cleanup old backups (keep last 30)
            CleanupOldBackups(targetDir, 30);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            LastBackupSuccess = false;
        }

        // Log to database
        try
        {
            using var conn = _db.GetConnection();
            conn.Execute("""
                INSERT INTO BackupLog (BackupPath,Success,ErrorMessage,FileSizeBytes)
                VALUES (@p,@s,@e,@fs)
                """,
                new { p = result.Path ?? targetDir, s = result.Success ? 1 : 0, e = result.ErrorMessage, fs = result.SizeBytes });
        }
        catch { /* don't crash if logging fails */ }

        return result;
    }

    public List<BackupLog> GetBackupHistory(int limit = 50)
    {
        using var conn = _db.GetConnection();
        return conn.Query<BackupLog>(
            "SELECT * FROM BackupLog ORDER BY BackupAt DESC LIMIT @limit", new { limit }).ToList();
    }

    private void CleanupOldBackups(string dir, int keep)
    {
        var files = Directory.GetFiles(dir, "GakunguWater_*.db")
                             .OrderByDescending(f => f).Skip(keep);
        foreach (var f in files)
        {
            try { File.Delete(f); } catch { }
        }
    }
}

public class BackupResult
{
    public bool Success { get; set; }
    public string? Path { get; set; }
    public string? ErrorMessage { get; set; }
    public long SizeBytes { get; set; }
}
