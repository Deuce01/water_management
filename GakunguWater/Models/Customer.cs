namespace GakunguWater.Models;

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Location { get; set; } = "";
    public string ConnectionStatus { get; set; } = "Active"; // Active, Suspended, Disconnected
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? Notes { get; set; }

    // Navigation (not stored in DB, populated by join queries)
    public string? MeterNumber { get; set; }
    public decimal OutstandingBalance { get; set; }
}
