using Dapper;
using GakunguWater.Data;
using GakunguWater.Models;
using System.Globalization;
using System.IO;

namespace GakunguWater.Services;

public class PaymentService
{
    private readonly DatabaseService _db;

    public PaymentService(DatabaseService db) => _db = db;

    public List<Payment> GetPayments(DateTime? from = null, DateTime? to = null, int? customerId = null)
    {
        using var conn = _db.GetConnection();
        var where = new List<string>();
        if (from.HasValue) where.Add("DATE(p.PaidAt)>=@from");
        if (to.HasValue) where.Add("DATE(p.PaidAt)<=@to");
        if (customerId.HasValue) where.Add("p.CustomerId=@customerId");

        var sql = $"""
            SELECT p.*, c.FullName AS CustomerName, u.Username AS ReceivedByUsername,
                   i.BillingMonth, i.BillingYear
            FROM Payments p
            JOIN Customers c ON c.Id=p.CustomerId
            JOIN Users u ON u.Id=p.ReceivedByUserId
            JOIN Invoices i ON i.Id=p.InvoiceId
            {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}
            ORDER BY p.PaidAt DESC
            """;
        return conn.Query<Payment>(sql,
            new { from = from?.ToString("yyyy-MM-dd"), to = to?.ToString("yyyy-MM-dd"), customerId }).ToList();
    }

    public Payment? GetById(int id)
    {
        using var conn = _db.GetConnection();
        return conn.QueryFirstOrDefault<Payment>("""
            SELECT p.*, c.FullName AS CustomerName, u.Username AS ReceivedByUsername,
                   i.BillingMonth, i.BillingYear
            FROM Payments p
            JOIN Customers c ON c.Id=p.CustomerId
            JOIN Users u ON u.Id=p.ReceivedByUserId
            JOIN Invoices i ON i.Id=p.InvoiceId
            WHERE p.Id=@id
            """, new { id });
    }

    /// <summary>Returns true if this M-Pesa receipt has already been posted.</summary>
    public bool MpesaReceiptExists(string mpesaRef)
    {
        using var conn = _db.GetConnection();
        return conn.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Payments WHERE MPesaRef=@r", new { r = mpesaRef }) > 0;
    }

    public int LogPayment(int invoiceId, int customerId, decimal amount,
                          int receivedByUserId, string method = "Cash", string? mpesaRef = null, string? notes = null)
    {

        using var conn = _db.GetConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var payId = conn.ExecuteScalar<int>("""
            INSERT INTO Payments (InvoiceId,CustomerId,Amount,PaymentMethod,MPesaRef,ReceivedByUserId,Notes)
            VALUES (@invoiceId,@customerId,@amount,@method,@mpesaRef,@receivedByUserId,@notes);
            SELECT last_insert_rowid();
            """,
            new { invoiceId, customerId, amount, method, mpesaRef, receivedByUserId, notes }, tx);

        // Update invoice paid amount and status
        conn.Execute("UPDATE Invoices SET AmountPaid=AmountPaid+@amount WHERE Id=@invoiceId",
            new { amount, invoiceId }, tx);

        var inv = conn.QueryFirstOrDefault<Invoice>(
            "SELECT AmountDue, AmountPaid FROM Invoices WHERE Id=@invoiceId",
            new { invoiceId }, tx);

        if (inv != null)
        {
            string newStatus = inv.AmountPaid >= inv.AmountDue ? "Paid"
                             : inv.AmountPaid > 0 ? "PartiallyPaid"
                             : "Unpaid";
            conn.Execute("UPDATE Invoices SET Status=@s WHERE Id=@id",
                new { s = newStatus, id = invoiceId }, tx);
        }

        tx.Commit();
        return payId;
    }

    // ── M-Pesa CSV Import ─────────────────────────────────────
    public List<MpesaCsvRow> ParseMpesaCsv(string filePath)
    {
        var rows = new List<MpesaCsvRow>();
        using var reader = new StreamReader(filePath);

        // Read header line
        var header = reader.ReadLine();
        if (header == null) return rows;

        // Parse column indices (flexible)
        var cols = header.Split(',').Select(h => h.Trim('"', ' ')).ToArray();
        int Idx(params string[] names) => names.Select(n => Array.FindIndex(cols, c => c.Equals(n, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(i => i >= 0);

        int idxReceipt = Idx("Receipt No", "ReceiptNo");
        int idxTime = Idx("Completion Time", "CompletionTime");
        int idxPaidIn = Idx("Paid In", "PaidIn");
        int idxPhone = Idx("Phone Number", "PhoneNumber");
        int idxAcc = Idx("A/C No", "Account", "Acc No", "AccountNumber");
        int idxFirst = Idx("First Name", "FirstName");
        int idxLast = Idx("Last Name", "LastName");

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = SplitCsvLine(line);
            if (parts.Length <= Math.Max(idxReceipt, idxPaidIn)) continue;

            decimal.TryParse(parts.ElementAtOrDefault(idxPaidIn) ?? "", NumberStyles.Any, CultureInfo.InvariantCulture, out var paidIn);
            if (paidIn <= 0) continue; // Skip reversals / zero-value rows

            rows.Add(new MpesaCsvRow
            {
                ReceiptNo = parts.ElementAtOrDefault(idxReceipt) ?? "",
                CompletionTime = parts.ElementAtOrDefault(idxTime) ?? "",
                PaidIn = paidIn,
                PhoneNumber = NormalizePhone(parts.ElementAtOrDefault(idxPhone) ?? ""),
                AccountNumber = parts.ElementAtOrDefault(idxAcc) ?? "",
                FirstName = parts.ElementAtOrDefault(idxFirst) ?? "",
                LastName = parts.ElementAtOrDefault(idxLast) ?? ""
            });
        }
        return rows;
    }

    public void MatchMpesaRows(List<MpesaCsvRow> rows)
    {
        using var conn = _db.GetConnection();
        var customers = conn.Query<Customer>("SELECT Id, FullName, PhoneNumber FROM Customers").ToList();

        foreach (var row in rows)
        {
            // Match by phone number
            var phone = NormalizePhone(row.PhoneNumber);
            var matched = customers.FirstOrDefault(c => NormalizePhone(c.PhoneNumber) == phone);

            // Match by account number (if numeric, try customer ID)
            if (matched == null && int.TryParse(row.AccountNumber, out var accId))
                matched = customers.FirstOrDefault(c => c.Id == accId);

            if (matched == null) continue;

            row.MatchedCustomerId = matched.Id;
            row.MatchedCustomerName = matched.FullName;

            // Find oldest unpaid invoice
            var invoice = conn.QueryFirstOrDefault<Invoice>("""
                SELECT * FROM Invoices WHERE CustomerId=@cid AND Status!='Paid'
                ORDER BY BillingYear, BillingMonth LIMIT 1
                """, new { cid = matched.Id });
            row.MatchedInvoiceId = invoice?.Id;
        }
    }

    public void PostMpesaPayments(List<MpesaCsvRow> rows, int receivedByUserId)
    {
        foreach (var row in rows.Where(r => r.IsMatched && r.MatchedInvoiceId.HasValue))
        {
            // Check if this M-Pesa receipt was already posted
            using var conn = _db.GetConnection();
            var exists = conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM Payments WHERE MPesaRef=@ref", new { @ref = row.ReceiptNo });
            if (exists > 0) continue;

            LogPayment(row.MatchedInvoiceId!.Value, row.MatchedCustomerId!.Value,
                       row.PaidIn, receivedByUserId, "MPesa", row.ReceiptNo);
        }
    }

    // ── Defaulters ────────────────────────────────────────────
    public List<Invoice> GetDefaulters(int daysOverdue = 30)
    {
        using var conn = _db.GetConnection();
        return conn.Query<Invoice>("""
            SELECT i.*, c.FullName AS CustomerName, c.PhoneNumber, c.Location, m.MeterNumber
            FROM Invoices i
            JOIN Customers c ON c.Id=i.CustomerId
            JOIN Meters m ON m.Id=i.MeterId
            WHERE i.Status!='Paid'
              AND DATE(i.DueDate) <= DATE('now','-' || @days || ' days','localtime')
            ORDER BY i.DueDate, c.FullName
            """, new { days = daysOverdue }).ToList();
    }

    private static string NormalizePhone(string phone)
    {
        phone = phone.Trim().Replace(" ", "").Replace("-", "");
        if (phone.StartsWith("0") && phone.Length == 10) phone = "254" + phone[1..];
        if (phone.StartsWith("+")) phone = phone[1..];
        return phone;
    }

    private static string[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') { inQuotes = !inQuotes; }
            else if (c == ',' && !inQuotes) { result.Add(current.ToString().Trim()); current.Clear(); }
            else current.Append(c);
        }
        result.Add(current.ToString().Trim());
        return result.ToArray();
    }
}
