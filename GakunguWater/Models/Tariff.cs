namespace GakunguWater.Models;

public class Tariff
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Volumetric"; // FlatRate or Volumetric
    public decimal FlatAmount { get; set; }
    public decimal PricePerCubicMeter { get; set; }
    public double MinUnits { get; set; }
    public decimal MinCharge { get; set; }
    public DateTime EffectiveFrom { get; set; } = DateTime.Today;
    public bool IsActive { get; set; } = true;
}
