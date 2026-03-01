using Dapper;
using GakunguWater.Data;
using GakunguWater.Models;

namespace GakunguWater.Services;

public class BillingService
{
    private readonly DatabaseService _db;

    public BillingService(DatabaseService db) => _db = db;

    // ── Tariffs ───────────────────────────────────────────────
    public List<Tariff> GetTariffs()
    {
        using var conn = _db.GetConnection();
        return conn.Query<Tariff>("SELECT * FROM Tariffs ORDER BY EffectiveFrom DESC").ToList();
    }

    public Tariff? GetActiveTariff()
    {
        using var conn = _db.GetConnection();
        return conn.QueryFirstOrDefault<Tariff>(
            "SELECT * FROM Tariffs WHERE IsActive=1 ORDER BY EffectiveFrom DESC LIMIT 1");
    }

    public void SaveTariff(Tariff t)
    {
        using var conn = _db.GetConnection();
        if (t.Id == 0)
            conn.Execute("""
                INSERT INTO Tariffs (Name,Type,FlatAmount,PricePerCubicMeter,MinUnits,MinCharge,EffectiveFrom,IsActive)
                VALUES (@Name,@Type,@FlatAmount,@PricePerCubicMeter,@MinUnits,@MinCharge,@EffectiveFrom,@IsActive)
                """, t);
        else
            conn.Execute("""
                UPDATE Tariffs SET Name=@Name,Type=@Type,FlatAmount=@FlatAmount,
                    PricePerCubicMeter=@PricePerCubicMeter,MinUnits=@MinUnits,MinCharge=@MinCharge,
                    EffectiveFrom=@EffectiveFrom,IsActive=@IsActive
                WHERE Id=@Id
                """, t);
    }

    // ── Meter Readings ────────────────────────────────────────
    public List<MeterReading> GetReadings(int month, int year)
    {
        using var conn = _db.GetConnection();
        return conn.Query<MeterReading>("""
            SELECT mr.*, m.MeterNumber, c.FullName AS CustomerName
            FROM MeterReadings mr
            JOIN Meters m ON m.Id=mr.MeterId
            JOIN Customers c ON c.Id=m.CustomerId
            WHERE mr.BillingMonth=@month AND mr.BillingYear=@year
            ORDER BY c.FullName
            """, new { month, year }).ToList();
    }

    /// <summary>Returns a list of active meters with their last reading pre-populated for batch entry.</summary>
    public List<MeterReading> PrepareBatchReadings(int month, int year, int enteredByUserId)
    {
        using var conn = _db.GetConnection();

        // Get all active meters
        var meters = conn.Query<Meter>("""
            SELECT m.*, c.FullName AS CustomerName
            FROM Meters m JOIN Customers c ON c.Id=m.CustomerId
            WHERE m.IsActive=1
            ORDER BY c.FullName
            """).ToList();

        var result = new List<MeterReading>();
        foreach (var m in meters)
        {
            // Check if reading already exists for this month
            var existing = conn.QueryFirstOrDefault<MeterReading>(
                "SELECT * FROM MeterReadings WHERE MeterId=@mid AND BillingMonth=@mo AND BillingYear=@yr",
                new { mid = m.Id, mo = month, yr = year });

            if (existing != null)
            {
                existing.MeterNumber = m.MeterNumber;
                existing.CustomerName = m.CustomerName;
                result.Add(existing);
                continue;
            }

            // Find previous reading
            var prevReading = conn.QueryFirstOrDefault<double?>("""
                SELECT CurrentReading FROM MeterReadings
                WHERE MeterId=@mid
                ORDER BY BillingYear DESC, BillingMonth DESC
                LIMIT 1
                """, new { mid = m.Id }) ?? 0;

            result.Add(new MeterReading
            {
                MeterId = m.Id,
                BillingMonth = month,
                BillingYear = year,
                PreviousReading = prevReading,
                CurrentReading = prevReading,
                EnteredByUserId = enteredByUserId,
                MeterNumber = m.MeterNumber,
                CustomerName = m.CustomerName
            });
        }
        return result;
    }

    public void SaveReadings(List<MeterReading> readings)
    {
        using var conn = _db.GetConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();
        foreach (var r in readings)
        {
            if (r.Id == 0)
                conn.Execute("""
                    INSERT OR REPLACE INTO MeterReadings
                        (MeterId,BillingMonth,BillingYear,PreviousReading,CurrentReading,ReadingDate,EnteredByUserId)
                    VALUES (@MeterId,@BillingMonth,@BillingYear,@PreviousReading,@CurrentReading,date('now','localtime'),@EnteredByUserId)
                    """, r, tx);
            else
                conn.Execute("""
                    UPDATE MeterReadings SET PreviousReading=@PreviousReading, CurrentReading=@CurrentReading
                    WHERE Id=@Id
                    """, r, tx);
        }
        tx.Commit();
    }

    // ── Invoice Generation ────────────────────────────────────
    public int GenerateInvoices(int month, int year)
    {
        var tariff = GetActiveTariff() ?? throw new InvalidOperationException("No active tariff found.");
        using var conn = _db.GetConnection();

        var readings = conn.Query<MeterReading>("""
            SELECT mr.*, m.CustomerId
            FROM MeterReadings mr
            JOIN Meters m ON m.Id=mr.MeterId
            WHERE mr.BillingMonth=@month AND mr.BillingYear=@year
            """, new { month, year }).ToList();

        int count = 0;
        conn.Open();
        using var tx = conn.BeginTransaction();

        foreach (var r in readings)
        {
            // Skip if invoice already exists
            var exists = conn.ExecuteScalar<int>("""
                SELECT COUNT(*) FROM Invoices 
                WHERE MeterId=@mid AND BillingMonth=@mo AND BillingYear=@yr
                """,
                new { mid = r.MeterId, mo = month, yr = year }, tx);
            if (exists > 0) continue;

            var consumption = r.CurrentReading - r.PreviousReading;
            decimal amount = tariff.Type == "FlatRate"
                ? tariff.FlatAmount
                : Math.Max(tariff.MinCharge, (decimal)consumption * tariff.PricePerCubicMeter);

            conn.Execute("""
                INSERT INTO Invoices
                    (CustomerId,MeterId,MeterReadingId,TariffId,BillingMonth,BillingYear,Consumption,AmountDue,Status,DueDate)
                VALUES (@cid,@mid,@rid,@tid,@mo,@yr,@cons,@amt,'Unpaid',date('now','+30 days','localtime'))
                """,
                new
                {
                    cid = conn.ExecuteScalar<int>("SELECT CustomerId FROM Meters WHERE Id=@id", new { id = r.MeterId }, tx),
                    mid = r.MeterId,
                    rid = r.Id,
                    tid = tariff.Id,
                    mo = month,
                    yr = year,
                    cons = consumption,
                    amt = amount
                }, tx);
            count++;
        }
        tx.Commit();
        return count;
    }

    public List<Invoice> GetInvoices(int? month = null, int? year = null, string? status = null, int? customerId = null)
    {
        using var conn = _db.GetConnection();
        var where = new List<string>();
        if (month.HasValue) where.Add("i.BillingMonth=@month");
        if (year.HasValue) where.Add("i.BillingYear=@year");
        if (!string.IsNullOrEmpty(status)) where.Add("i.Status=@status");
        if (customerId.HasValue) where.Add("i.CustomerId=@customerId");

        var sql = $"""
            SELECT i.*, c.FullName AS CustomerName, c.PhoneNumber, c.Location,
                   m.MeterNumber, t.Name AS TariffName
            FROM Invoices i
            JOIN Customers c ON c.Id=i.CustomerId
            JOIN Meters m ON m.Id=i.MeterId
            JOIN Tariffs t ON t.Id=i.TariffId
            {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
            ORDER BY i.BillingYear DESC, i.BillingMonth DESC, c.FullName
            """;
        return conn.Query<Invoice>(sql, new { month, year, status, customerId }).ToList();
    }

    public Invoice? GetInvoiceById(int id)
    {
        using var conn = _db.GetConnection();
        return conn.QueryFirstOrDefault<Invoice>("""
            SELECT i.*, c.FullName AS CustomerName, c.PhoneNumber, c.Location,
                   m.MeterNumber, t.Name AS TariffName
            FROM Invoices i
            JOIN Customers c ON c.Id=i.CustomerId
            JOIN Meters m ON m.Id=i.MeterId
            JOIN Tariffs t ON t.Id=i.TariffId
            WHERE i.Id=@id
            """, new { id });
    }
}
