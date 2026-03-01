namespace GakunguWater.Models;

public class MeterReading
{
    public int Id { get; set; }
    public int MeterId { get; set; }
    public int BillingMonth { get; set; }   // 1-12
    public int BillingYear { get; set; }
    public double PreviousReading { get; set; }
    public double CurrentReading { get; set; }
    public double Consumption => CurrentReading - PreviousReading;
    public DateTime ReadingDate { get; set; } = DateTime.Now;
    public int EnteredByUserId { get; set; }

    // Navigation
    public string? MeterNumber { get; set; }
    public string? CustomerName { get; set; }
}
