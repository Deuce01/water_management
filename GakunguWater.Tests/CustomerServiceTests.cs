using GakunguWater.Models;
using GakunguWater.Services;
using GakunguWater.Tests.Helpers;
using Xunit;

namespace GakunguWater.Tests;

public class CustomerServiceTests
{
    private static (GakunguWater.Data.DatabaseService db, CustomerService svc) Setup()
    {
        var db = TestDbFactory.Create();
        return (db, new CustomerService(db));
    }

    private static Customer MakeCustomer(string name = "Alice Wanjiku", string phone = "0712345678",
        string location = "Nairobi", string status = "Active") =>
        new() { FullName = name, PhoneNumber = phone, Location = location, ConnectionStatus = status };

    // ── Add / GetById ────────────────────────────────────────────
    [Fact]
    public void Add_And_GetById_RoundTrip()
    {
        var (_, svc) = Setup();
        var c = MakeCustomer();
        int id = svc.Add(c);

        var fetched = svc.GetById(id);
        Assert.NotNull(fetched);
        Assert.Equal("Alice Wanjiku", fetched.FullName);
        Assert.Equal("0712345678", fetched.PhoneNumber);
        Assert.Equal("Active", fetched.ConnectionStatus);
    }

    [Fact]
    public void GetById_ReturnsNull_ForNonExistentId()
    {
        var (_, svc) = Setup();
        Assert.Null(svc.GetById(9999));
    }

    // ── GetAll ───────────────────────────────────────────────────
    [Fact]
    public void GetAll_ReturnsAllCustomers()
    {
        var (_, svc) = Setup();
        svc.Add(MakeCustomer("Alice Wanjiku", "0700000001"));
        svc.Add(MakeCustomer("Bob Kamau",    "0700000002"));

        var all = svc.GetAll();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void GetAll_WithSearch_FiltersByName()
    {
        var (_, svc) = Setup();
        svc.Add(MakeCustomer("Alice Wanjiku"));
        svc.Add(MakeCustomer("Bob Kamau"));

        var result = svc.GetAll(search: "Alice");
        Assert.Single(result);
        Assert.Equal("Alice Wanjiku", result[0].FullName);
    }

    [Fact]
    public void GetAll_WithSearch_FiltersbyPhone()
    {
        var (_, svc) = Setup();
        svc.Add(MakeCustomer("Alice", "0711111111"));
        svc.Add(MakeCustomer("Bob",   "0722222222"));

        var result = svc.GetAll(search: "0711111111");
        Assert.Single(result);
        Assert.Equal("Alice", result[0].FullName);
    }

    [Fact]
    public void GetAll_WithStatusFilter_ReturnsOnlyMatchingStatus()
    {
        var (_, svc) = Setup();
        svc.Add(MakeCustomer("Alice", status: "Active"));
        svc.Add(MakeCustomer("Bob",   status: "Disconnected"));

        var active = svc.GetAll(statusFilter: "Active");
        Assert.Single(active);
        Assert.Equal("Alice", active[0].FullName);
    }

    // ── Update ───────────────────────────────────────────────────
    [Fact]
    public void Update_ChangesCustomerFields()
    {
        var (_, svc) = Setup();
        var c = MakeCustomer();
        int id = svc.Add(c);
        c.Id = id;

        c.FullName = "Alice Mwangi";
        c.Location = "Mombasa";
        svc.Update(c);

        var updated = svc.GetById(id)!;
        Assert.Equal("Alice Mwangi", updated.FullName);
        Assert.Equal("Mombasa", updated.Location);
    }

    // ── SetStatus ────────────────────────────────────────────────
    [Fact]
    public void SetStatus_UpdatesConnectionStatus()
    {
        var (_, svc) = Setup();
        int id = svc.Add(MakeCustomer(status: "Active"));

        svc.SetStatus(id, "Disconnected");

        Assert.Equal("Disconnected", svc.GetById(id)!.ConnectionStatus);
    }

    // ── Delete ───────────────────────────────────────────────────
    [Fact]
    public void Delete_RemovesCustomer()
    {
        var (_, svc) = Setup();
        int id = svc.Add(MakeCustomer());

        svc.Delete(id);

        Assert.Null(svc.GetById(id));
    }

    // ── Meters ───────────────────────────────────────────────────
    [Fact]
    public void AddMeter_And_GetMeters_RoundTrip()
    {
        var (_, svc) = Setup();
        int custId = svc.Add(MakeCustomer());

        var meter = new Meter
        {
            MeterNumber = "MTR-500",
            CustomerId  = custId,
            InstallDate = DateTime.Today,
            IsActive    = true
        };
        svc.AddMeter(meter);

        var meters = svc.GetMeters(custId);
        Assert.Single(meters);
        Assert.Equal("MTR-500", meters[0].MeterNumber);
    }

    [Fact]
    public void GetMeters_WithNullCustomerId_ReturnsAllMeters()
    {
        var (_, svc) = Setup();
        int cid1 = svc.Add(MakeCustomer("Cust1", "0700000003"));
        int cid2 = svc.Add(MakeCustomer("Cust2", "0700000004"));

        svc.AddMeter(new Meter { MeterNumber = "MTR-A", CustomerId = cid1, InstallDate = new DateTime(2024, 1, 1), IsActive = true });
        svc.AddMeter(new Meter { MeterNumber = "MTR-B", CustomerId = cid2, InstallDate = new DateTime(2024, 1, 1), IsActive = true });

        Assert.Equal(2, svc.GetMeters().Count);
    }

    [Fact]
    public void AddMeter_DuplicateMeterNumber_ThrowsException()
    {
        var (_, svc) = Setup();
        int custId = svc.Add(MakeCustomer());

        svc.AddMeter(new Meter { MeterNumber = "MTR-DUP", CustomerId = custId, InstallDate = new DateTime(2024, 1, 1), IsActive = true });

        Assert.ThrowsAny<Exception>(() =>
            svc.AddMeter(new Meter { MeterNumber = "MTR-DUP", CustomerId = custId, InstallDate = new DateTime(2024, 1, 1), IsActive = true }));
    }

    [Fact]
    public void UpdateMeter_PersistsChanges()
    {
        var (_, svc) = Setup();
        int custId = svc.Add(MakeCustomer());
        int metId = svc.AddMeter(new Meter { MeterNumber = "MTR-UPD", CustomerId = custId, InstallDate = new DateTime(2024, 1, 1), IsActive = true });

        var m = svc.GetMeterById(metId)!;
        m.IsActive = false;
        svc.UpdateMeter(m);

        Assert.False(svc.GetMeterById(metId)!.IsActive);
    }
}
