using Dapper;
using GakunguWater.Models;
using GakunguWater.Services;
using GakunguWater.Tests.Helpers;
using System.IO;
using Xunit;

namespace GakunguWater.Tests;

/// <summary>
/// Focused resiliency tests for M-Pesa CSV parsing and customer matching.
/// Each test writes a temporary file and cleans up after itself.
/// </summary>
public class MpesaCsvResiliencyTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string WriteTempCsv(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mpesa_test_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            try { File.Delete(f); } catch { }
    }

    private static PaymentService BuildPaymentService() =>
        new PaymentService(TestDbFactory.Create());

    // ── Parsing: valid CSV ────────────────────────────────────────
    [Fact]
    public void ParseMpesaCsv_ValidRows_ParsesAllFields()
    {
        var svc = BuildPaymentService();
        var path = WriteTempCsv(
            "Receipt No,Completion Time,Paid In,Phone Number,A/C No,First Name,Last Name\n" +
            "QZA001,2025-01-15 10:30:00,500,0712345678,1,Alice,Wanjiku\n" +
            "QZA002,2025-01-16 11:00:00,1000,0722222222,2,Bob,Kamau\n");

        var rows = svc.ParseMpesaCsv(path);

        Assert.Equal(2, rows.Count);
        Assert.Equal("QZA001", rows[0].ReceiptNo);
        Assert.Equal(500m, rows[0].PaidIn);
        Assert.Equal("Alice", rows[0].FirstName);
    }

    // ── Parsing: edge-case files ──────────────────────────────────
    [Fact]
    public void ParseMpesaCsv_EmptyFile_ReturnsEmptyList()
    {
        var svc  = BuildPaymentService();
        var path = WriteTempCsv("");

        var rows = svc.ParseMpesaCsv(path);
        Assert.Empty(rows);
    }

    [Fact]
    public void ParseMpesaCsv_HeaderOnlyFile_ReturnsEmptyList()
    {
        var svc  = BuildPaymentService();
        var path = WriteTempCsv(
            "Receipt No,Completion Time,Paid In,Phone Number,A/C No,First Name,Last Name\n");

        var rows = svc.ParseMpesaCsv(path);
        Assert.Empty(rows);
    }

    [Fact]
    public void ParseMpesaCsv_ZeroPaidInRows_AreSkipped()
    {
        var svc  = BuildPaymentService();
        var path = WriteTempCsv(
            "Receipt No,Completion Time,Paid In,Phone Number,A/C No,First Name,Last Name\n" +
            "QZA003,2025-01-17 09:00:00,0,0711111111,3,Carol,Omondi\n");

        var rows = svc.ParseMpesaCsv(path);
        Assert.Empty(rows);
    }

    [Fact]
    public void ParseMpesaCsv_NegativePaidInRows_AreSkipped()
    {
        var svc  = BuildPaymentService();
        var path = WriteTempCsv(
            "Receipt No,Completion Time,Paid In,Phone Number,A/C No,First Name,Last Name\n" +
            "REV001,2025-01-18 08:00:00,-300,0711111111,4,Dan,Otieno\n");

        var rows = svc.ParseMpesaCsv(path);
        Assert.Empty(rows);
    }

    [Fact]
    public void ParseMpesaCsv_QuotedCommasInFields_ParsedCorrectly()
    {
        var svc  = BuildPaymentService();
        var path = WriteTempCsv(
            "Receipt No,Completion Time,Paid In,Phone Number,A/C No,First Name,Last Name\n" +
            "\"QZA,004\",2025-01-19 07:00:00,750,0733333333,5,Eve,Muthoni\n");

        // Despite comma in ReceiptNo, it should still extract an amount of 750
        var rows = svc.ParseMpesaCsv(path);
        Assert.Single(rows);
        Assert.Equal(750m, rows[0].PaidIn);
    }

    [Fact]
    public void ParseMpesaCsv_MissingOptionalColumns_DoesNotThrow()
    {
        var svc  = BuildPaymentService();
        // Only the essential columns; First Name and Last Name are absent
        var path = WriteTempCsv(
            "Receipt No,Paid In,Phone Number\n" +
            "QZA005,250,0744444444\n");

        var rows = svc.ParseMpesaCsv(path);
        // Amount is 250 > 0, should be parsed; missing fields gracefully default
        Assert.Single(rows);
        Assert.Equal(250m, rows[0].PaidIn);
    }

    // ── Phone normalisation ───────────────────────────────────────
    [Fact]
    public void ParseMpesaCsv_LocalFormatPhone_NormalisedTo254()
    {
        var svc  = BuildPaymentService();
        var path = WriteTempCsv(
            "Receipt No,Completion Time,Paid In,Phone Number,A/C No,First Name,Last Name\n" +
            "QZA006,2025-01-20 08:00:00,300,0712345678,6,Frank,Otieno\n");

        var rows = svc.ParseMpesaCsv(path);
        // NormalizePhone turns 0712345678 → 254712345678
        Assert.Equal("254712345678", rows[0].PhoneNumber);
    }

    [Fact]
    public void ParseMpesaCsv_PlusFormatPhone_StripsPlus()
    {
        var svc  = BuildPaymentService();
        var path = WriteTempCsv(
            "Receipt No,Completion Time,Paid In,Phone Number,A/C No,First Name,Last Name\n" +
            "QZA007,2025-01-21 08:00:00,400,+254712345678,7,Grace,Njeri\n");

        var rows = svc.ParseMpesaCsv(path);
        Assert.Equal("254712345678", rows[0].PhoneNumber);
    }

    // ── MatchMpesaRows ────────────────────────────────────────────
    [Fact]
    public void MatchMpesaRows_MatchesByPhoneNumber()
    {
        var db  = TestDbFactory.Create();
        var custSvc = new CustomerService(db);
        var paySvc  = new PaymentService(db);

        int custId = custSvc.Add(new Customer
        {
            FullName = "Matched Mary", PhoneNumber = "0712345678",
            Location = "Nairobi", ConnectionStatus = "Active"
        });

        var rows = new List<MpesaCsvRow>
        {
            new() { ReceiptNo = "QZA008", PaidIn = 500m, PhoneNumber = "254712345678" }
        };
        paySvc.MatchMpesaRows(rows);

        Assert.Equal(custId, rows[0].MatchedCustomerId);
        Assert.Equal("Matched Mary", rows[0].MatchedCustomerName);
    }

    [Fact]
    public void MatchMpesaRows_NoMatch_LeavesMatchedCustomerIdNull()
    {
        var db     = TestDbFactory.Create();
        var paySvc = new PaymentService(db);

        var rows = new List<MpesaCsvRow>
        {
            new() { ReceiptNo = "QZA009", PaidIn = 800m, PhoneNumber = "254700000000" }
        };
        paySvc.MatchMpesaRows(rows);

        Assert.Null(rows[0].MatchedCustomerId);
    }

    [Fact]
    public void MatchMpesaRows_MatchesByAccountNumber()
    {
        var db      = TestDbFactory.Create();
        var custSvc = new CustomerService(db);
        var paySvc  = new PaymentService(db);

        int custId = custSvc.Add(new Customer
        {
            FullName = "Account Andy", PhoneNumber = "0799999999",
            Location = "Kisumu", ConnectionStatus = "Active"
        });

        var rows = new List<MpesaCsvRow>
        {
            new() { ReceiptNo = "QZA010", PaidIn = 200m,
                    PhoneNumber = "254888888888", // won't match by phone
                    AccountNumber = custId.ToString() }
        };
        paySvc.MatchMpesaRows(rows);

        Assert.Equal(custId, rows[0].MatchedCustomerId);
    }

    // ── PostMpesaPayments duplicate guard ─────────────────────────
    [Fact]
    public void PostMpesaPayments_SkipsDuplicateReceipts()
    {
        var db      = TestDbFactory.Create();
        var custSvc = new CustomerService(db);
        var billSvc = new BillingService(db);
        var paySvc  = new PaymentService(db);
        using var conn = db.GetConnection();

        int custId = custSvc.Add(new Customer
        {
            FullName = "Duplicate Dave", PhoneNumber = "0711222333",
            Location = "Eldoret", ConnectionStatus = "Active"
        });
        int meterId = custSvc.AddMeter(new Meter
        {
            MeterNumber = "MTR-DUP2", CustomerId = custId,
            InstallDate = new DateTime(2024, 1, 1), IsActive = true
        });
        int userId = conn.ExecuteScalar<int>("SELECT Id FROM Users WHERE Username='admin'");

        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = 2, BillingYear = 2025,
                    PreviousReading = 0, CurrentReading = 10, EnteredByUserId = userId }
        });
        billSvc.GenerateInvoices(2, 2025);

        var invoice = billSvc.GetInvoices(2, 2025).Single();
        var rows = new List<MpesaCsvRow>
        {
            new() { ReceiptNo = "SAME_REF_123", PaidIn = 200m,
                    PhoneNumber = "254711222333",
                    MatchedCustomerId = custId,
                    MatchedInvoiceId  = invoice.Id }
        };

        // Post twice
        paySvc.PostMpesaPayments(rows, userId);
        paySvc.PostMpesaPayments(rows, userId);

        // Should only be one payment record
        var payments = paySvc.GetPayments(customerId: custId);
        Assert.Single(payments);
    }
}
