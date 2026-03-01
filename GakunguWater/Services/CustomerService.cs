using Dapper;
using GakunguWater.Data;
using GakunguWater.Models;

namespace GakunguWater.Services;

public class CustomerService
{
    private readonly DatabaseService _db;

    public CustomerService(DatabaseService db) => _db = db;

    public List<Customer> GetAll(string? search = null, string? statusFilter = null)
    {
        using var conn = _db.GetConnection();
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            where.Add("(c.FullName LIKE @s OR c.PhoneNumber LIKE @s OR c.Location LIKE @s)");
        if (!string.IsNullOrWhiteSpace(statusFilter))
            where.Add("c.ConnectionStatus=@st");

        var sql = $"""
            SELECT c.*, m.MeterNumber,
                COALESCE((SELECT SUM(AmountDue-AmountPaid) FROM Invoices i WHERE i.CustomerId=c.Id AND i.Status!='Paid'),0) AS OutstandingBalance
            FROM Customers c
            LEFT JOIN Meters m ON m.CustomerId=c.Id AND m.IsActive=1
            {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
            ORDER BY c.FullName
            """;
        return conn.Query<Customer>(sql, new { s = $"%{search}%", st = statusFilter }).ToList();
    }

    public Customer? GetById(int id)
    {
        using var conn = _db.GetConnection();
        return conn.QueryFirstOrDefault<Customer>(
            "SELECT c.*, m.MeterNumber FROM Customers c LEFT JOIN Meters m ON m.CustomerId=c.Id AND m.IsActive=1 WHERE c.Id=@id",
            new { id });
    }

    public int Add(Customer c)
    {
        using var conn = _db.GetConnection();
        return conn.ExecuteScalar<int>("""
            INSERT INTO Customers (FullName, PhoneNumber, Location, ConnectionStatus, Notes)
            VALUES (@FullName,@PhoneNumber,@Location,@ConnectionStatus,@Notes);
            SELECT last_insert_rowid();
            """, c);
    }

    public void Update(Customer c)
    {
        using var conn = _db.GetConnection();
        conn.Execute("""
            UPDATE Customers SET FullName=@FullName, PhoneNumber=@PhoneNumber,
                Location=@Location, ConnectionStatus=@ConnectionStatus, Notes=@Notes
            WHERE Id=@Id
            """, c);
    }

    public void SetStatus(int id, string status)
    {
        using var conn = _db.GetConnection();
        conn.Execute("UPDATE Customers SET ConnectionStatus=@s WHERE Id=@id", new { s = status, id });
    }

    public void Delete(int id)
    {
        using var conn = _db.GetConnection();
        conn.Execute("DELETE FROM Customers WHERE Id=@id", new { id });
    }

    // ── Meter management ──────────────────────────────────────
    public List<Meter> GetMeters(int? customerId = null)
    {
        using var conn = _db.GetConnection();
        var sql = customerId.HasValue
            ? "SELECT m.*, c.FullName AS CustomerName FROM Meters m JOIN Customers c ON c.Id=m.CustomerId WHERE m.CustomerId=@cid ORDER BY m.MeterNumber"
            : "SELECT m.*, c.FullName AS CustomerName FROM Meters m JOIN Customers c ON c.Id=m.CustomerId ORDER BY m.MeterNumber";
        return conn.Query<Meter>(sql, new { cid = customerId }).ToList();
    }

    public Meter? GetMeterById(int id)
    {
        using var conn = _db.GetConnection();
        return conn.QueryFirstOrDefault<Meter>("SELECT * FROM Meters WHERE Id=@id", new { id });
    }

    public int AddMeter(Meter m)
    {
        using var conn = _db.GetConnection();
        return conn.ExecuteScalar<int>("""
            INSERT INTO Meters (MeterNumber, CustomerId, InstallDate, IsActive, Notes)
            VALUES (@MeterNumber,@CustomerId,@InstallDate,@IsActive,@Notes);
            SELECT last_insert_rowid();
            """, m);
    }

    public void UpdateMeter(Meter m)
    {
        using var conn = _db.GetConnection();
        conn.Execute("""
            UPDATE Meters SET MeterNumber=@MeterNumber, CustomerId=@CustomerId,
                InstallDate=@InstallDate, IsActive=@IsActive, Notes=@Notes
            WHERE Id=@Id
            """, m);
    }
}
