using GakunguWater.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GakunguWater.Reports;

public class InvoiceDocument : IDocument
{
    private readonly Invoice _invoice;

    public InvoiceDocument(Invoice invoice) => _invoice = invoice;

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer c)
    {
        c.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().Text("GAKUNGU COMMUNITY WATER PROJECT")
                        .FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                    inner.Item().Text("Water Bill / Invoice").FontSize(11).Italic();
                });
                row.ConstantItem(100).AlignRight().Column(inner =>
                {
                    inner.Item().Text($"Invoice #").Bold();
                    inner.Item().Text($"INV-{_invoice.Id:D5}").FontColor(Colors.Blue.Darken1);
                });
            });
            col.Item().LineHorizontal(1).LineColor(Colors.Blue.Darken1);
            col.Item().Height(8);
        });
    }

    private void ComposeContent(IContainer c)
    {
        c.Column(col =>
        {
            // Customer details
            col.Item().Background(Colors.Blue.Lighten5).Padding(8).Table(table =>
            {
                table.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.RelativeColumn(); });
                void Row(string l, string v, bool bold = false)
                {
                    table.Cell().Text(l).Bold();
                    var cell = table.Cell().Text(v);
                    if (bold) cell.Bold();
                }
                Row("Customer:", _invoice.CustomerName ?? "");
                Row("Phone:", _invoice.PhoneNumber ?? "");
                Row("Location:", _invoice.Location ?? "");
                Row("Meter No:", _invoice.MeterNumber ?? "");
            });

            col.Item().Height(10);

            // Billing period
            var month = new DateTime(_invoice.BillingYear, _invoice.BillingMonth, 1).ToString("MMMM yyyy");
            col.Item().Text($"Billing Period: {month}").Bold().FontSize(11);
            col.Item().Height(6);

            // Charges table
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn();
                    cols.RelativeColumn();
                });

                table.Header(h =>
                {
                    h.Cell().Background(Colors.Blue.Darken2).Padding(4)
                        .Text("Description").FontColor(Colors.White).Bold();
                    h.Cell().Background(Colors.Blue.Darken2).Padding(4).AlignRight()
                        .Text("Units").FontColor(Colors.White).Bold();
                    h.Cell().Background(Colors.Blue.Darken2).Padding(4).AlignRight()
                        .Text("Amount (KES)").FontColor(Colors.White).Bold();
                });

                table.Cell().BorderBottom(0.5f).Padding(4).Text("Water Consumption");
                table.Cell().BorderBottom(0.5f).Padding(4).AlignRight()
                    .Text($"{_invoice.Consumption:F2} m³");
                table.Cell().BorderBottom(0.5f).Padding(4).AlignRight()
                    .Text($"{_invoice.AmountDue:N2}");

                // Total row
                table.Cell().ColumnSpan(2).Background(Colors.Grey.Lighten3).Padding(4)
                    .Text("TOTAL DUE").Bold();
                table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight()
                    .Text($"KES {_invoice.AmountDue:N2}").Bold().FontColor(Colors.Red.Darken1);
            });

            col.Item().Height(8);

            // Paid / Balance
            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"Amount Paid: KES {_invoice.AmountPaid:N2}").FontColor(Colors.Green.Darken1).Bold();
                row.RelativeItem().AlignRight().Text($"Balance Due: KES {_invoice.Balance:N2}")
                    .Bold().FontColor(_invoice.Balance > 0 ? Colors.Red.Darken2 : Colors.Green.Darken2);
            });

            col.Item().Height(6);

            // Status badge
            var status = _invoice.Status ?? "Unpaid";
            var statusColor = status switch
            {
                "Paid" => Colors.Green.Darken2,
                "PartiallyPaid" => Colors.Orange.Darken2,
                _ => Colors.Red.Darken2
            };
            col.Item().Width(100).Background(statusColor).Padding(4)
                .Text(status.ToUpper()).FontColor(Colors.White).Bold().FontSize(9);

            col.Item().Height(10);
            if (_invoice.DueDate.HasValue)
                col.Item().Text($"Due Date: {_invoice.DueDate.Value:dd MMM yyyy}").Italic().FontColor(Colors.Grey.Darken1);
        });
    }

    private void ComposeFooter(IContainer c)
    {
        c.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
            col.Item().Height(4);
            col.Item().Row(row =>
            {
                row.RelativeItem().Text("Thank you for your payment!").Italic().FontSize(9);
                row.RelativeItem().AlignRight().Text($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
            col.Item().Text("For queries, please contact your local water project office.")
                .FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }
}
