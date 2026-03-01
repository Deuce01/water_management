using GakunguWater.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GakunguWater.Reports;

public class ReceiptDocument : IDocument
{
    private readonly Payment _payment;

    public ReceiptDocument(Payment payment) => _payment = payment;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(80, 200, Unit.Millimetre); // Thermal 80mm width
            page.Margin(5, Unit.Millimetre);
            page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

            page.Content().Element(ComposeContent);
        });
    }

    private void ComposeContent(IContainer c)
    {
        c.Column(col =>
        {
            col.Item().AlignCenter().Text("GAKUNGU WATER PROJECT")
                .Bold().FontSize(11);
            col.Item().AlignCenter().Text("PAYMENT RECEIPT").FontSize(10).Italic();
            col.Item().LineHorizontal(0.5f);
            col.Item().Height(4);

            void Row(string label, string value)
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text(label).Bold();
                    r.RelativeItem().AlignRight().Text(value);
                });
            }

            Row("Receipt No:", $"REC-{_payment.Id:D6}");
            Row("Date:", _payment.PaidAt.ToString("dd/MM/yyyy HH:mm"));
            col.Item().Height(4);
            Row("Customer:", _payment.CustomerName ?? "");
            Row("Method:", _payment.PaymentMethod);
            if (!string.IsNullOrEmpty(_payment.MPesaRef))
                Row("M-Pesa Ref:", _payment.MPesaRef);
            col.Item().LineHorizontal(0.5f);

            // Billing period
            if (_payment.BillingMonth.HasValue && _payment.BillingYear.HasValue)
            {
                var period = new DateTime(_payment.BillingYear.Value, _payment.BillingMonth.Value, 1);
                Row("Bill Period:", period.ToString("MMMM yyyy"));
            }

            col.Item().LineHorizontal(0.5f);
            col.Item().Row(r =>
            {
                r.RelativeItem().Text("AMOUNT PAID:").Bold().FontSize(11);
                r.RelativeItem().AlignRight().Text($"KES {_payment.Amount:N2}")
                    .Bold().FontSize(11).FontColor(Colors.Green.Darken2);
            });

            col.Item().LineHorizontal(0.5f);
            col.Item().Height(4);
            col.Item().AlignCenter().Text("Thank you!").Italic();
            col.Item().AlignCenter().Text($"Served by: {_payment.ReceivedByUsername}").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }
}
