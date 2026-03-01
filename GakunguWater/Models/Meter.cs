namespace GakunguWater.Models;

public class Meter
{
    public int Id { get; set; }
    public string MeterNumber { get; set; } = "";
    public int CustomerId { get; set; }
    public DateTime InstallDate { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    // Navigation
    public string? CustomerName { get; set; }
}
