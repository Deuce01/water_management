using Dapper;
using GakunguWater.Models;
using GakunguWater.Services;
using GakunguWater.Tests.Helpers;
using Xunit;

namespace GakunguWater.Tests;

public class PaymentServiceTests
{
    // ── Setup ────────────────────────────────────────────────────
    private static (GakunguWater.Data.DatabaseService db,
                    BillingService billSvc,
                    PaymentService paySvc,
                    int custId, int invoiceId, int userId) Setup(
                        double prevReading = 0, double currReading = 10,
                        int month = 1, int year = 2025)
    {
        var db = TestDbFactory.Create();
        var custSvc  = new CustomerService(db);
        var billSvc  = new BillingService(db);
        var paySvc   = new PaymentService(db);

        int custId = custSvc.Add(new Customer
        {
            FullName = "Test Payer", PhoneNumber = "0711000001",
            Location = "Nairobi", ConnectionStatus = "Active"
        });
        int meterId = custSvc.AddMeter(new Meter
        {
            MeterNumber = "MTR-PAY", CustomerId = custId,
            InstallDate = new DateTime(2024, 1, 1), IsActive = true
        });

        using var conn = db.GetConnection();
        int userId = conn.ExecuteScalar<int>("SELECT Id FROM Users WHERE Username='admin'");

        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = month, BillingYear = year,
                    PreviousReading = prevReading, CurrentReading = currReading,
                    EnteredByUserId = userId }
        });
        billSvc.GenerateInvoices(month, year);

        var invoice = billSvc.GetInvoices(month, year).Single();
        return (db, billSvc, paySvc, custId, invoice.Id, userId);
    }

    // ── LogPayment status transitions ────────────────────────────
    [Fact]
    public void LogPayment_PartialPayment_SetsStatusToPartiallyPaid()
    {
        var (db, billSvc, paySvc, custId, invoiceId, userId) = Setup();
        var invoiceBefore = billSvc.GetInvoiceById(invoiceId)!;

        paySvc.LogPayment(invoiceId, custId, invoiceBefore.AmountDue / 2,
                          userId, "Cash");

        var updated = billSvc.GetInvoiceById(invoiceId)!;
        Assert.Equal("PartiallyPaid", updated.Status);
    }

    [Fact]
    public void LogPayment_FullPayment_SetsStatusToPaid()
    {
        var (db, billSvc, paySvc, custId, invoiceId, userId) = Setup();
        var invoice = billSvc.GetInvoiceById(invoiceId)!;

        paySvc.LogPayment(invoiceId, custId, invoice.AmountDue, userId, "Cash");

        Assert.Equal("Paid", billSvc.GetInvoiceById(invoiceId)!.Status);
    }

    [Fact]
    public void LogPayment_Overpayment_StatusIsPaid()
    {
        var (db, billSvc, paySvc, custId, invoiceId, userId) = Setup();
        var invoice = billSvc.GetInvoiceById(invoiceId)!;

        paySvc.LogPayment(invoiceId, custId, invoice.AmountDue + 500, userId, "Cash");

        Assert.Equal("Paid", billSvc.GetInvoiceById(invoiceId)!.Status);
    }

    [Fact]
    public void LogPayment_TwoPartialPayments_AccumulateCorrectly()
    {
        var (db, billSvc, paySvc, custId, invoiceId, userId) = Setup(currReading: 20); // 1000 KES
        var invoice = billSvc.GetInvoiceById(invoiceId)!;
        decimal half = invoice.AmountDue / 2;

        paySvc.LogPayment(invoiceId, custId, half, userId, "Cash");
        paySvc.LogPayment(invoiceId, custId, half, userId, "Cash");

        Assert.Equal("Paid", billSvc.GetInvoiceById(invoiceId)!.Status);
    }

    // ── M-Pesa duplicate detection ───────────────────────────────
    [Fact]
    public void MpesaReceiptExists_ReturnsFalse_ForNewRef()
    {
        var (_, _, paySvc, _, _, _) = Setup();
        Assert.False(paySvc.MpesaReceiptExists("QZX999NOTREAL"));
    }

    [Fact]
    public void MpesaReceiptExists_ReturnsTrue_AfterPayment()
    {
        var (db, billSvc, paySvc, custId, invoiceId, userId) = Setup();
        var invoice = billSvc.GetInvoiceById(invoiceId)!;

        paySvc.LogPayment(invoiceId, custId, invoice.AmountDue,
                          userId, "MPesa", "QZX123MPESA");

        Assert.True(paySvc.MpesaReceiptExists("QZX123MPESA"));
    }

    // ── GetById ──────────────────────────────────────────────────
    [Fact]
    public void GetById_ReturnsPaymentDetails()
    {
        var (db, billSvc, paySvc, custId, invoiceId, userId) = Setup();
        var invoice = billSvc.GetInvoiceById(invoiceId)!;

        int payId = paySvc.LogPayment(invoiceId, custId, 100m, userId, "Cash");
        var pay   = paySvc.GetById(payId);

        Assert.NotNull(pay);
        Assert.Equal(100m, pay.Amount);
        Assert.Equal("Cash", pay.PaymentMethod);
    }

    // ── GetPayments date filtering ───────────────────────────────
    [Fact]
    public void GetPayments_WithNoFilter_ReturnsAll()
    {
        var (db, billSvc, paySvc, custId, invoiceId, userId) = Setup();
        paySvc.LogPayment(invoiceId, custId, 50m, userId, "Cash");

        Assert.Single(paySvc.GetPayments());
    }

    [Fact]
    public void GetPayments_FilterByCustomerId_ReturnsOnlyTheirPayments()
    {
        var (db, billSvc, paySvc, custId, invoiceId, userId) = Setup();
        paySvc.LogPayment(invoiceId, custId, 50m, userId, "Cash");

        var filtered = paySvc.GetPayments(customerId: custId);
        Assert.Single(filtered);

        var none = paySvc.GetPayments(customerId: 9999);
        Assert.Empty(none);
    }

    // ── Defaulters ───────────────────────────────────────────────
    [Fact]
    public void GetDefaulters_ReturnsOverdueUnpaidInvoices()
    {
        var db = TestDbFactory.Create();
        var custSvc = new CustomerService(db);
        var billSvc = new BillingService(db);
        var paySvc  = new PaymentService(db);

        int custId = custSvc.Add(new Customer
        {
            FullName = "Defaulter Dan", PhoneNumber = "0722000001",
            Location = "Kisumu", ConnectionStatus = "Active"
        });
        int meterId = custSvc.AddMeter(new Meter
        {
            MeterNumber = "MTR-DEF", CustomerId = custId,
            InstallDate = new DateTime(2024, 1, 1), IsActive = true
        });
        using var conn = db.GetConnection();
        int userId = conn.ExecuteScalar<int>("SELECT Id FROM Users WHERE Username='admin'");

        // Create an invoice for an old month so it is definitely overdue
        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = 1, BillingYear = 2024,
                    PreviousReading = 0, CurrentReading = 10, EnteredByUserId = userId }
        });
        billSvc.GenerateInvoices(1, 2024);

        // Force DueDate to the past so GetDefaulters picks it up
        // (GenerateInvoices sets DueDate = now+30d via SQLite)
        conn.Execute("UPDATE Invoices SET DueDate=date('now','-60 days','localtime')");

        var defaulters = paySvc.GetDefaulters(0); // 0 days = anything past due date
        Assert.NotEmpty(defaulters);
        Assert.Contains(defaulters, d => d.CustomerName == "Defaulter Dan");
    }

    [Fact]
    public void GetDefaulters_ExcludesPaidInvoices()
    {
        var (db, billSvc, paySvc, custId, invoiceId, userId) = Setup(month: 1, year: 2024);
        var invoice = billSvc.GetInvoiceById(invoiceId)!;
        paySvc.LogPayment(invoiceId, custId, invoice.AmountDue, userId, "Cash");

        var defaulters = paySvc.GetDefaulters(0);
        // Backdate the due date for the test
        using var conn2 = db.GetConnection();
        conn2.Execute("UPDATE Invoices SET DueDate=date('now','-60 days','localtime')");
        defaulters = paySvc.GetDefaulters(0);
        Assert.DoesNotContain(defaulters, d => d.Id == invoiceId);
    }
}
