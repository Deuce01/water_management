using Dapper;
using GakunguWater.Tests.Helpers;
using Xunit;

namespace GakunguWater.Tests;

public class DatabaseServiceTests
{
    [Fact]
    public void Initialize_CreatesAllExpectedTables()
    {
        var db = TestDbFactory.Create();
        using var conn = db.GetConnection();
        conn.Open();

        var tables = conn.Query<string>(
            "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name").ToList();

        Assert.Contains("Customers", tables);
        Assert.Contains("Meters", tables);
        Assert.Contains("Tariffs", tables);
        Assert.Contains("Users", tables);
        Assert.Contains("MeterReadings", tables);
        Assert.Contains("Invoices", tables);
        Assert.Contains("Payments", tables);
        Assert.Contains("Expenses", tables);
        Assert.Contains("BackupLog", tables);
        Assert.Contains("SchemaVersion", tables);
    }

    [Fact]
    public void Initialize_SeedsDefaultAdminUser()
    {
        var db = TestDbFactory.Create();
        using var conn = db.GetConnection();

        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Users WHERE Username='admin' AND Role='Admin'");
        Assert.Equal(1, count);
    }

    [Fact]
    public void Initialize_SeedsDefaultCashierUser()
    {
        var db = TestDbFactory.Create();
        using var conn = db.GetConnection();

        var count = conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Users WHERE Username='cashier' AND Role='Cashier'");
        Assert.Equal(1, count);
    }

    [Fact]
    public void Initialize_SeedsDefaultTariff()
    {
        var db = TestDbFactory.Create();
        using var conn = db.GetConnection();

        var count = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Tariffs");
        Assert.Equal(1, count);
    }

    [Fact]
    public void GetSchemaVersion_ReturnsOne_AfterInit()
    {
        var db = TestDbFactory.Create();
        Assert.Equal(1, db.GetSchemaVersion());
    }

    [Fact]
    public void MeterNumberExists_ReturnsFalse_WhenNoMeterExists()
    {
        var db = TestDbFactory.Create();
        Assert.False(db.MeterNumberExists("MTR-9999"));
    }

    [Fact]
    public void MeterNumberExists_ReturnsTrue_WhenMeterExists()
    {
        var db = TestDbFactory.Create();
        using var conn = db.GetConnection();

        // Insert a customer first (FK)
        var custId = conn.ExecuteScalar<int>(@"
            INSERT INTO Customers (FullName, PhoneNumber, Location)
            VALUES ('Test', '0700000000', 'Loc');
            SELECT last_insert_rowid();");

        conn.Execute(@"INSERT INTO Meters (MeterNumber, CustomerId) VALUES ('MTR-001', @cid)",
            new { cid = custId });

        Assert.True(db.MeterNumberExists("MTR-001"));
    }

    [Fact]
    public void MeterNumberExists_ExcludesSpecifiedId()
    {
        var db = TestDbFactory.Create();
        using var conn = db.GetConnection();

        var custId = conn.ExecuteScalar<int>(@"
            INSERT INTO Customers (FullName, PhoneNumber, Location)
            VALUES ('Test', '0700000001', 'Loc');
            SELECT last_insert_rowid();");

        var meterId = conn.ExecuteScalar<int>(@"
            INSERT INTO Meters (MeterNumber, CustomerId) VALUES ('MTR-002', @cid);
            SELECT last_insert_rowid();", new { cid = custId });

        // Excluding the meter's own ID should return false (it's the same record)
        Assert.False(db.MeterNumberExists("MTR-002", meterId));
    }
}
