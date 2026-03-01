namespace GakunguWater.Models;

public class Payment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash"; // Cash, MPesa
    public string? MPesaRef { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.Now;
    public int ReceivedByUserId { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public string? CustomerName { get; set; }
    public string? ReceivedByUsername { get; set; }
    public int? BillingMonth { get; set; }
    public int? BillingYear { get; set; }
}
