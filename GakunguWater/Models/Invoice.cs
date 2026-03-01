namespace GakunguWater.Models;

public class Invoice
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int MeterId { get; set; }
    public int MeterReadingId { get; set; }
    public int TariffId { get; set; }
    public int BillingMonth { get; set; }
    public int BillingYear { get; set; }
    public double Consumption { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance => AmountDue - AmountPaid;
    public string Status { get; set; } = "Unpaid"; // Unpaid, PartiallyPaid, Paid
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public DateTime? DueDate { get; set; }

    // Navigation
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? MeterNumber { get; set; }
    public string? Location { get; set; }
    public string? TariffName { get; set; }
}
