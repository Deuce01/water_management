using GakunguWater.Models;
using GakunguWater.Reports;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.IO;

namespace GakunguWater.Services;

public class ReportService
{
    static ReportService()
    {
        // QuestPDF community license (free for small orgs)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void PrintInvoice(Invoice invoice)
    {
        var path = GetTempPath($"Invoice_INV{invoice.Id:D5}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
        new InvoiceDocument(invoice).GeneratePdf(path);
        OpenFile(path);
    }

    public void PrintReceipt(Payment payment)
    {
        var path = GetTempPath($"Receipt_REC{payment.Id:D6}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
        new ReceiptDocument(payment).GeneratePdf(path);
        OpenFile(path);
    }

    public void PrintFinancialSummary(int month, int year, decimal revenue, decimal expenses,
        List<Expense> expenseDetails, List<Payment> paymentDetails)
    {
        var path = GetTempPath($"FinancialSummary_{year}{month:D2}.pdf");
        new FinancialSummaryDocument(month, year, revenue, expenses, expenseDetails, paymentDetails)
            .GeneratePdf(path);
        OpenFile(path);
    }

    private string GetTempPath(string fileName)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GakunguWater", "Reports");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, fileName);
    }

    private static void OpenFile(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
