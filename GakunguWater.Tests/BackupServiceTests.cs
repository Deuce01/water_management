using GakunguWater.Services;
using GakunguWater.Tests.Helpers;
using System.IO;
using Xunit;

namespace GakunguWater.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _tempDir;

    public BackupServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"GakunguBackupTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static (GakunguWater.Data.DatabaseService db, BackupService svc, string dbPath) Setup()
    {
        // Write the in-memory DB to a temp file so BackupService can copy it
        var dbPath = Path.Combine(Path.GetTempPath(), $"gakungu_test_{Guid.NewGuid():N}.db");
        var db = new GakunguWater.Data.DatabaseService(dbPath);
        db.Initialize();
        var svc = new BackupService(db, dbPath);
        return (db, svc, dbPath);
    }

    // ── Successful backup ────────────────────────────────────────
    [Fact]
    public void PerformBackup_ToTempDir_ReturnsSuccess()
    {
        var (db, svc, dbPath) = Setup();

        var result = svc.PerformBackup(_tempDir);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(result.Path), "Backup file not found on disk.");
        Assert.True(result.SizeBytes > 0);

        // Cleanup temp db file
        try { File.Delete(dbPath); } catch { }
    }

    [Fact]
    public void PerformBackup_SetsLastBackupTimeAndSuccess()
    {
        var (db, svc, dbPath) = Setup();
        svc.PerformBackup(_tempDir);

        Assert.NotNull(svc.LastBackupTime);
        Assert.True(svc.LastBackupSuccess);

        try { File.Delete(dbPath); } catch { }
    }

    // ── Failed backup ────────────────────────────────────────────
    [Fact]
    public void PerformBackup_NonExistentSourceFile_ReturnsFailure()
    {
        var db  = TestDbFactory.Create(); // in-memory, no real file
        var svc = new BackupService(db, @"C:\nonexistent\path\fake.db");

        var result = svc.PerformBackup(_tempDir);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.False(svc.LastBackupSuccess);
    }

    // ── Backup log ───────────────────────────────────────────────
    [Fact]
    public void PerformBackup_LogsSuccessResultToDatabase()
    {
        var (db, svc, dbPath) = Setup();
        svc.PerformBackup(_tempDir);

        var history = svc.GetBackupHistory();
        Assert.NotEmpty(history);
        Assert.True(history[0].Success);

        try { File.Delete(dbPath); } catch { }
    }

    [Fact]
    public void PerformBackup_FailedBackup_LogsFailureToDatabase()
    {
        var db  = TestDbFactory.Create();
        var svc = new BackupService(db, @"C:\fake\nope.db");
        svc.PerformBackup(_tempDir);

        var history = svc.GetBackupHistory();
        Assert.NotEmpty(history);
        Assert.False(history[0].Success);
        Assert.NotEmpty(history[0].ErrorMessage ?? "");
    }

    // ── Cleanup old backups ──────────────────────────────────────
    [Fact]
    public void PerformBackup_CleanupKeepsOnly30Files()
    {
        var (db, svc, dbPath) = Setup();

        // Pre-create 35 backup-named files so cleanup has something to prune
        for (int i = 0; i < 35; i++)
        {
            var stamp = DateTime.Now.AddSeconds(-i).ToString("yyyyMMdd_HHmmss");
            File.WriteAllText(Path.Combine(_tempDir, $"GakunguWater_{stamp}{i}.db"), "placeholder");
        }

        svc.PerformBackup(_tempDir);

        // Count .db files matching GakunguWater_*.db pattern
        var remaining = Directory.GetFiles(_tempDir, "GakunguWater_*.db");
        Assert.True(remaining.Length <= 30,
            $"Expected ≤30 backup files but found {remaining.Length}");

        try { File.Delete(dbPath); } catch { }
    }

    // ── GetBackupHistory ─────────────────────────────────────────
    [Fact]
    public void GetBackupHistory_LimitIsRespected()
    {
        var db  = TestDbFactory.Create();
        var svc = new BackupService(db, @"C:\fake\nope.db");

        // Trigger 5 failed backups (they still log)
        for (int i = 0; i < 5; i++) svc.PerformBackup(_tempDir + $"\\sub{i}");

        var history = svc.GetBackupHistory(limit: 3);
        Assert.True(history.Count <= 3);
    }
}
