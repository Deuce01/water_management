namespace GakunguWater.Models;

public class Expense
{
    public int Id { get; set; }
    public string Category { get; set; } = "Other"; // Electricity, Diesel, Salary, Repairs, Other
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.Today;
    public int EnteredByUserId { get; set; }
    public string? Receipt { get; set; }

    // Navigation
    public string? EnteredByUsername { get; set; }
}
