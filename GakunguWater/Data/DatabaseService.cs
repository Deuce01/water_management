using Dapper;
using GakunguWater.Models;
using Microsoft.Data.Sqlite;

namespace GakunguWater.Data;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string dbPath)
    {
        _connectionString = $"Data Source={dbPath};Pooling=True;Cache=Shared";
    }

    public SqliteConnection GetConnection() => new(_connectionString);

    public void Initialize()
    {
        using var conn = GetConnection();
        conn.Open();

        // WAL mode and FK enforcement must be set OUTSIDE any transaction
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
            cmd.ExecuteNonQuery();
        }

        using var tx = conn.BeginTransaction();
        try
        {


            // ── Customers ──────────────────────────────────────
            conn.Execute("""
                CREATE TABLE IF NOT EXISTS Customers (
                    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName         TEXT    NOT NULL,
                    PhoneNumber      TEXT    NOT NULL DEFAULT '',
                    Location         TEXT    NOT NULL DEFAULT '',
                    ConnectionStatus TEXT    NOT NULL DEFAULT 'Active',
                    Notes            TEXT,
                    CreatedAt        TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );
                """, transaction: tx);

            // ── Meters ─────────────────────────────────────────
            conn.Execute("""
                CREATE TABLE IF NOT EXISTS Meters (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    MeterNumber  TEXT    NOT NULL UNIQUE,
                    CustomerId   INTEGER NOT NULL REFERENCES Customers(Id),
                    InstallDate  TEXT    NOT NULL DEFAULT (date('now','localtime')),
                    IsActive     INTEGER NOT NULL DEFAULT 1,
                    Notes        TEXT
                );
                """, transaction: tx);

            // ── Tariffs ────────────────────────────────────────
            conn.Execute("""
                CREATE TABLE IF NOT EXISTS Tariffs (
                    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name                TEXT    NOT NULL,
                    Type                TEXT    NOT NULL DEFAULT 'Volumetric',
                    FlatAmount          REAL    NOT NULL DEFAULT 0,
                    PricePerCubicMeter  REAL    NOT NULL DEFAULT 0,
                    MinUnits            REAL    NOT NULL DEFAULT 0,
                    MinCharge           REAL    NOT NULL DEFAULT 0,
                    EffectiveFrom       TEXT    NOT NULL,
                    IsActive            INTEGER NOT NULL DEFAULT 1
                );
                """, transaction: tx);

            // ── Users ──────────────────────────────────────────
            conn.Execute("""
                CREATE TABLE IF NOT EXISTS Users (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username     TEXT    NOT NULL UNIQUE,
                    PasswordHash TEXT    NOT NULL,
                    Role         TEXT    NOT NULL DEFAULT 'Cashier',
                    FullName     TEXT,
                    IsActive     INTEGER NOT NULL DEFAULT 1,
                    CreatedAt    TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                    LastLogin    TEXT
                );
                """, transaction: tx);

            // ── MeterReadings ──────────────────────────────────
            conn.Execute("""
                CREATE TABLE IF NOT EXISTS MeterReadings (
                    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                    MeterId          INTEGER NOT NULL REFERENCES Meters(Id),
                    BillingMonth     INTEGER NOT NULL,
                    BillingYear      INTEGER NOT NULL,
                    PreviousReading  REAL    NOT NULL DEFAULT 0,
                    CurrentReading   REAL    NOT NULL DEFAULT 0,
                    ReadingDate      TEXT    NOT NULL DEFAULT (date('now','localtime')),
                    EnteredByUserId  INTEGER NOT NULL REFERENCES Users(Id),
                    UNIQUE(MeterId, BillingMonth, BillingYear)
                );
                """, transaction: tx);

            // ── Invoices ───────────────────────────────────────
            conn.Execute("""
                CREATE TABLE IF NOT EXISTS Invoices (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    CustomerId      INTEGER NOT NULL REFERENCES Customers(Id),
                    MeterId         INTEGER NOT NULL REFERENCES Meters(Id),
                    MeterReadingId  INTEGER NOT NULL REFERENCES MeterReadings(Id),
                    TariffId        INTEGER NOT NULL REFERENCES Tariffs(Id),
                    BillingMonth    INTEGER NOT NULL,
                    BillingYear     INTEGER NOT NULL,
                    Consumption     REAL    NOT NULL DEFAULT 0,
                    AmountDue       REAL    NOT NULL DEFAULT 0,
                    AmountPaid      REAL    NOT NULL DEFAULT 0,
                    Status          TEXT    NOT NULL DEFAULT 'Unpaid',
                    GeneratedAt     TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                    DueDate         TEXT
                );
                """, transaction: tx);

            // ── Payments ───────────────────────────────────────
            conn.Execute("""
                CREATE TABLE IF NOT EXISTS Payments (
                    Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
                    InvoiceId          INTEGER NOT NULL REFERENCES Invoices(Id),
                    CustomerId         INTEGER NOT NULL REFERENCES Customers(Id),
                    Amount             REAL    NOT NULL,
                    PaymentMethod      TEXT    NOT NULL DEFAULT 'Cash',
                    MPesaRef           TEXT,
                    PaidAt             TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                    ReceivedByUserId   INTEGER NOT NULL REFERENCES Users(Id),
                    Notes              TEXT
                );
                """, transaction: tx);

            // ── Expenses ───────────────────────────────────────
            conn.Execute("""
                CREATE TABLE IF NOT EXISTS Expenses (
                    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                    Category         TEXT    NOT NULL DEFAULT 'Other',
                    Description      TEXT    NOT NULL DEFAULT '',
                    Amount           REAL    NOT NULL,
                    ExpenseDate      TEXT    NOT NULL DEFAULT (date('now','localtime')),
                    EnteredByUserId  INTEGER NOT NULL REFERENCES Users(Id),
                    Receipt          TEXT
                );
                """, transaction: tx);

            // ── BackupLog ──────────────────────────────────────
            conn.Execute("""
                CREATE TABLE IF NOT EXISTS BackupLog (
                    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                    BackupPath   TEXT    NOT NULL,
                    BackupAt     TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                    Success      INTEGER NOT NULL DEFAULT 0,
                    ErrorMessage TEXT,
                    FileSizeBytes INTEGER NOT NULL DEFAULT 0
                );
                """, transaction: tx);


            // ── Schema version tracking ────────────────────────
            conn.Execute("""
                CREATE TABLE IF NOT EXISTS SchemaVersion (
                    Version     INTEGER NOT NULL,
                    AppliedAt   TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                );
                """, transaction: tx);

            var currentVersion = conn.ExecuteScalar<int>(
                "SELECT COALESCE(MAX(Version),0) FROM SchemaVersion", transaction: tx);

            if (currentVersion < 1)
                conn.Execute("INSERT INTO SchemaVersion (Version) VALUES (1)", transaction: tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        SeedDefaults();
    }

    /// <summary>Returns the current schema version stored in the database.</summary>
    public int GetSchemaVersion()
    {
        using var conn = GetConnection();
        try { return conn.ExecuteScalar<int>("SELECT COALESCE(MAX(Version),0) FROM SchemaVersion"); }
        catch { return 0; }
    }

    /// <summary>Checks whether a meter number already exists (optionally excluding a given meter ID).</summary>
    public bool MeterNumberExists(string meterNumber, int excludeId = 0)
    {
        using var conn = GetConnection();
        return conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Meters WHERE MeterNumber=@n AND Id!=@id",
            new { n = meterNumber, id = excludeId }) > 0;
    }


    private void SeedDefaults()
    {
        using var conn = GetConnection();
        conn.Open();

        // Seed admin user
        var adminExists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Users WHERE Username='admin'");
        if (adminExists == 0)
        {
            conn.Execute("""
                INSERT INTO Users (Username, PasswordHash, Role, FullName)
                VALUES ('admin', @hash, 'Admin', 'System Administrator');
                """,
                new { hash = BCrypt.Net.BCrypt.HashPassword("Admin@123") });
        }

        // Seed cashier user
        var cashierExists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Users WHERE Username='cashier'");
        if (cashierExists == 0)
        {
            conn.Execute("""
                INSERT INTO Users (Username, PasswordHash, Role, FullName)
                VALUES ('cashier', @hash, 'Cashier', 'Default Cashier');
                """,
                new { hash = BCrypt.Net.BCrypt.HashPassword("Cashier@123") });
        }

        // Seed a default volumetric tariff
        var tariffExists = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Tariffs");
        if (tariffExists == 0)
        {
            conn.Execute("""
                INSERT INTO Tariffs (Name, Type, PricePerCubicMeter, MinUnits, MinCharge, EffectiveFrom)
                VALUES ('Standard Volumetric', 'Volumetric', 50, 0, 200, date('now'));
                """);
        }
    }
}
