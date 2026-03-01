namespace GakunguWater.Models;

public class BackupLog
{
    public int Id { get; set; }
    public string BackupPath { get; set; } = "";
    public DateTime BackupAt { get; set; } = DateTime.Now;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long FileSizeBytes { get; set; }
}

public class MpesaCsvRow
{
    public string ReceiptNo { get; set; } = "";
    public string CompletionTime { get; set; } = "";
    public string Details { get; set; } = "";
    public decimal PaidIn { get; set; }
    public string AccountNumber { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";

    // Matched after import
    public int? MatchedCustomerId { get; set; }
    public string? MatchedCustomerName { get; set; }
    public int? MatchedInvoiceId { get; set; }
    public bool IsMatched => MatchedCustomerId.HasValue;
}
