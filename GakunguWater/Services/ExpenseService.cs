using Dapper;
using GakunguWater.Data;
using GakunguWater.Models;

namespace GakunguWater.Services;

public class ExpenseService
{
    private readonly DatabaseService _db;

    public ExpenseService(DatabaseService db) => _db = db;

    public List<Expense> GetAll(int? month = null, int? year = null, string? category = null)
    {
        using var conn = _db.GetConnection();
        var where = new List<string>();
        if (month.HasValue) where.Add("strftime('%m',ExpenseDate)=@month");
        if (year.HasValue) where.Add("strftime('%Y',ExpenseDate)=@year");
        if (!string.IsNullOrEmpty(category)) where.Add("e.Category=@category");

        var sql = $"""
            SELECT e.*, u.Username AS EnteredByUsername
            FROM Expenses e
            JOIN Users u ON u.Id=e.EnteredByUserId
            {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
            ORDER BY e.ExpenseDate DESC
            """;
        return conn.Query<Expense>(sql,
            new { month = month?.ToString("D2"), year = year?.ToString(), category }).ToList();
    }

    public int Add(Expense e)
    {
        using var conn = _db.GetConnection();
        return conn.ExecuteScalar<int>("""
            INSERT INTO Expenses (Category,Description,Amount,ExpenseDate,EnteredByUserId,Receipt)
            VALUES (@Category,@Description,@Amount,@ExpenseDate,@EnteredByUserId,@Receipt);
            SELECT last_insert_rowid();
            """, e);
    }

    public void Update(Expense e)
    {
        using var conn = _db.GetConnection();
        conn.Execute("""
            UPDATE Expenses SET Category=@Category, Description=@Description,
                Amount=@Amount, ExpenseDate=@ExpenseDate, Receipt=@Receipt
            WHERE Id=@Id
            """, e);
    }

    public void Delete(int id)
    {
        using var conn = _db.GetConnection();
        conn.Execute("DELETE FROM Expenses WHERE Id=@id", new { id });
    }

    public (decimal Revenue, decimal Expenses, decimal Net) GetFinancialSummary(int month, int year)
    {
        using var conn = _db.GetConnection();
        var revenue = conn.ExecuteScalar<decimal>("""
            SELECT COALESCE(SUM(Amount),0) FROM Payments
            WHERE strftime('%m',PaidAt)=@month AND strftime('%Y',PaidAt)=@year
            """, new { month = month.ToString("D2"), year = year.ToString() });

        var expenses = conn.ExecuteScalar<decimal>("""
            SELECT COALESCE(SUM(Amount),0) FROM Expenses
            WHERE strftime('%m',ExpenseDate)=@month AND strftime('%Y',ExpenseDate)=@year
            """, new { month = month.ToString("D2"), year = year.ToString() });

        return (revenue, expenses, revenue - expenses);
    }

    public static IReadOnlyList<string> Categories =>
        new[] { "Electricity", "Diesel", "Salary", "Repairs", "Other" };
}
