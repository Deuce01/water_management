using Dapper;
using GakunguWater.Models;
using GakunguWater.Services;
using GakunguWater.Tests.Helpers;
using Xunit;

namespace GakunguWater.Tests;

public class BillingServiceTests
{
    // ── Setup helpers ────────────────────────────────────────────
    private static (GakunguWater.Data.DatabaseService db,
                    CustomerService custSvc,
                    BillingService billSvc,
                    int custId, int meterId, int userId) Setup()
    {
        var db = TestDbFactory.Create();
        var custSvc = new CustomerService(db);
        var billSvc = new BillingService(db);

        int custId = custSvc.Add(new Customer
        {
            FullName = "Test Customer", PhoneNumber = "0700000001",
            Location = "Town", ConnectionStatus = "Active"
        });
        int meterId = custSvc.AddMeter(new Meter
        {
            MeterNumber = "MTR-TEST", CustomerId = custId,
            InstallDate = new DateTime(2024, 1, 1), IsActive = true
        });

        using var conn = db.GetConnection();
        int userId = conn.ExecuteScalar<int>("SELECT Id FROM Users WHERE Username='admin'");

        return (db, custSvc, billSvc, custId, meterId, userId);
    }

    // ── Tariffs ──────────────────────────────────────────────────
    [Fact]
    public void GetActiveTariff_ReturnsSeededTariff()
    {
        var (_, _, billSvc, _, _, _) = Setup();
        var tariff = billSvc.GetActiveTariff();
        Assert.NotNull(tariff);
        Assert.True(tariff.IsActive);
    }

    [Fact]
    public void SaveTariff_Insert_AddsNewTariff()
    {
        var (_, _, billSvc, _, _, _) = Setup();
        int before = billSvc.GetTariffs().Count;

        billSvc.SaveTariff(new Tariff
        {
            Name = "New Tariff", Type = "Volumetric",
            PricePerCubicMeter = 60m, MinCharge = 100m,
            EffectiveFrom = new DateTime(2025, 1, 1), IsActive = false
        });

        Assert.Equal(before + 1, billSvc.GetTariffs().Count);
    }

    [Fact]
    public void SaveTariff_Update_ModifiesTariff()
    {
        var (_, _, billSvc, _, _, _) = Setup();
        var tariff = billSvc.GetActiveTariff()!;
        tariff.Name = "Updated Name";
        billSvc.SaveTariff(tariff);

        Assert.Equal("Updated Name", billSvc.GetActiveTariff()!.Name);
    }

    // ── Readings ─────────────────────────────────────────────────
    [Fact]
    public void PrepareBatchReadings_ReturnsOneRowPerActiveMeter()
    {
        var (_, custSvc, billSvc, custId, _, userId) = Setup();
        // Add a second active meter
        custSvc.AddMeter(new Meter
        {
            MeterNumber = "MTR-2", CustomerId = custId,
            InstallDate = new DateTime(2024, 1, 1), IsActive = true
        });

        var rows = billSvc.PrepareBatchReadings(1, 2025, userId);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void PrepareBatchReadings_PopulatesPreviousReading()
    {
        var (_, _, billSvc, _, meterId, userId) = Setup();

        // Save a reading for Dec 2024
        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = 12, BillingYear = 2024,
                    PreviousReading = 0, CurrentReading = 100, EnteredByUserId = userId }
        });

        var rows = billSvc.PrepareBatchReadings(1, 2025, userId);
        Assert.Equal(100, rows[0].PreviousReading);
    }

    [Fact]
    public void SaveReadings_And_GetReadings_RoundTrip()
    {
        var (_, _, billSvc, _, meterId, userId) = Setup();

        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = 3, BillingYear = 2025,
                    PreviousReading = 50, CurrentReading = 80, EnteredByUserId = userId }
        });

        var readings = billSvc.GetReadings(3, 2025);
        Assert.Single(readings);
        Assert.Equal(80, readings[0].CurrentReading);
    }

    [Fact]
    public void SaveReadings_Update_ModifiesExistingReading()
    {
        var (_, _, billSvc, _, meterId, userId) = Setup();

        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = 4, BillingYear = 2025,
                    PreviousReading = 0, CurrentReading = 20, EnteredByUserId = userId }
        });

        var saved = billSvc.GetReadings(4, 2025).Single();
        saved.CurrentReading = 35;
        billSvc.SaveReadings(new List<MeterReading> { saved });

        Assert.Equal(35, billSvc.GetReadings(4, 2025).Single().CurrentReading);
    }

    // ── Invoice Generation ───────────────────────────────────────
    [Fact]
    public void GenerateInvoices_Volumetric_CalculatesCorrectAmount()
    {
        var (_, _, billSvc, _, meterId, userId) = Setup();

        // 10 m³ at KES 50 = 500, min charge is 200 → should be 500
        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = 5, BillingYear = 2025,
                    PreviousReading = 0, CurrentReading = 10, EnteredByUserId = userId }
        });

        int generated = billSvc.GenerateInvoices(5, 2025);
        Assert.Equal(1, generated);

        var invoices = billSvc.GetInvoices(5, 2025);
        Assert.Single(invoices);
        Assert.Equal(500m, invoices[0].AmountDue);
    }

    [Fact]
    public void GenerateInvoices_Volumetric_AppliesMinCharge()
    {
        var (db, _, billSvc, _, meterId, userId) = Setup();

        // Consumption = 2 m³, 2 × 50 = 100 < min charge 200 → invoice should be 200
        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = 6, BillingYear = 2025,
                    PreviousReading = 0, CurrentReading = 2, EnteredByUserId = userId }
        });

        billSvc.GenerateInvoices(6, 2025);

        var invoices = billSvc.GetInvoices(6, 2025);
        Assert.Equal(200m, invoices[0].AmountDue);
    }

    [Fact]
    public void GenerateInvoices_FlatRate_UsesFlatAmount()
    {
        var (db, _, billSvc, _, meterId, userId) = Setup();

        // Set active tariff to flat rate
        var tariff = billSvc.GetActiveTariff()!;
        tariff.Type = "FlatRate";
        tariff.FlatAmount = 350m;
        billSvc.SaveTariff(tariff);

        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = 7, BillingYear = 2025,
                    PreviousReading = 0, CurrentReading = 20, EnteredByUserId = userId }
        });

        billSvc.GenerateInvoices(7, 2025);
        var invoices = billSvc.GetInvoices(7, 2025);
        Assert.Equal(350m, invoices[0].AmountDue);
    }

    [Fact]
    public void GenerateInvoices_IsIdempotent_SecondCallAddsNoInvoices()
    {
        var (_, _, billSvc, _, meterId, userId) = Setup();

        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = 8, BillingYear = 2025,
                    PreviousReading = 0, CurrentReading = 15, EnteredByUserId = userId }
        });

        billSvc.GenerateInvoices(8, 2025);
        int second = billSvc.GenerateInvoices(8, 2025);
        Assert.Equal(0, second);
    }

    [Fact]
    public void GenerateInvoices_NoActiveTariff_ThrowsInvalidOperationException()
    {
        var (db, _, billSvc, _, meterId, userId) = Setup();

        // Deactivate the tariff
        using var conn = db.GetConnection();
        conn.Execute("UPDATE Tariffs SET IsActive=0");

        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = 9, BillingYear = 2025,
                    PreviousReading = 0, CurrentReading = 10, EnteredByUserId = userId }
        });

        Assert.Throws<InvalidOperationException>(() => billSvc.GenerateInvoices(9, 2025));
    }

    [Fact]
    public void GetInvoices_FiltersbyStatus()
    {
        var (_, _, billSvc, _, meterId, userId) = Setup();

        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = 10, BillingYear = 2025,
                    PreviousReading = 0, CurrentReading = 10, EnteredByUserId = userId }
        });
        billSvc.GenerateInvoices(10, 2025);

        var unpaid = billSvc.GetInvoices(status: "Unpaid");
        var paid   = billSvc.GetInvoices(status: "Paid");

        Assert.NotEmpty(unpaid);
        Assert.Empty(paid);
    }

    [Fact]
    public void GetInvoiceById_ReturnsCorrectInvoice()
    {
        var (_, _, billSvc, _, meterId, userId) = Setup();

        billSvc.SaveReadings(new List<MeterReading>
        {
            new() { MeterId = meterId, BillingMonth = 11, BillingYear = 2025,
                    PreviousReading = 0, CurrentReading = 10, EnteredByUserId = userId }
        });
        billSvc.GenerateInvoices(11, 2025);

        var invoice = billSvc.GetInvoices(11, 2025).Single();
        var fetched = billSvc.GetInvoiceById(invoice.Id);

        Assert.NotNull(fetched);
        Assert.Equal(invoice.Id, fetched.Id);
    }
}
