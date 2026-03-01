using GakunguWater.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GakunguWater.Reports;

public class FinancialSummaryDocument : IDocument
{
    private readonly int _month;
    private readonly int _year;
    private readonly decimal _revenue;
    private readonly decimal _expenses;
    private readonly List<Expense> _expenseDetails;
    private readonly List<Payment> _paymentDetails;

    public FinancialSummaryDocument(int month, int year, decimal revenue, decimal expenses,
        List<Expense> expenseDetails, List<Payment> paymentDetails)
    {
        _month = month;
        _year = year;
        _revenue = revenue;
        _expenses = expenses;
        _expenseDetails = expenseDetails;
        _paymentDetails = paymentDetails;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().AlignRight().Text(t =>
            {
                t.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                t.CurrentPageNumber().FontSize(8);
            });
        });
    }

    private void ComposeHeader(IContainer c)
    {
        c.Column(col =>
        {
            col.Item().Text("GAKUNGU COMMUNITY WATER PROJECT")
                .FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
            col.Item().Text("Financial Summary Report")
                .FontSize(12).Italic().FontColor(Colors.Grey.Darken2);
            col.Item().Text($"Period: {new DateTime(_year, _month, 1):MMMM yyyy}")
                .Bold().FontSize(11);
            col.Item().LineHorizontal(1.5f).LineColor(Colors.Blue.Darken2);
            col.Item().Height(8);
        });
    }

    private void ComposeContent(IContainer c)
    {
        var net = _revenue - _expenses;
        c.Column(col =>
        {
            // KPI summary boxes
            col.Item().Row(row =>
            {
                KpiBox(row, "Total Revenue", $"KES {_revenue:N2}", Colors.Green.Darken1);
                row.ConstantItem(10);
                KpiBox(row, "Total Expenses", $"KES {_expenses:N2}", Colors.Red.Darken1);
                row.ConstantItem(10);
                KpiBox(row, "Net Surplus / (Deficit)", $"KES {net:N2}",
                    net >= 0 ? Colors.Blue.Darken2 : Colors.Orange.Darken2);
            });

            col.Item().Height(16);

            // Revenue details
            col.Item().Text("Revenue Breakdown (Payments Received)").Bold().FontSize(11);
            col.Item().Height(4);
            if (_paymentDetails.Count == 0)
            {
                col.Item().Text("No payments recorded for this period.").Italic().FontColor(Colors.Grey.Darken1);
            }
            else
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                    });
                    void Hdr(string t) => table.Cell().Background(Colors.Blue.Darken2).Padding(4)
                        .Text(t).FontColor(Colors.White).Bold().FontSize(9);
                    Hdr("Customer"); Hdr("Date"); Hdr("Method"); Hdr("Amount (KES)");

                    foreach (var p in _paymentDetails)
                    {
                        bool odd = _paymentDetails.IndexOf(p) % 2 == 0;
                        var bg = odd ? Colors.White : Colors.Blue.Lighten5;
                        table.Cell().Background(bg).Padding(3).Text(p.CustomerName ?? "");
                        table.Cell().Background(bg).Padding(3).Text(p.PaidAt.ToString("dd/MM/yyyy"));
                        table.Cell().Background(bg).Padding(3).Text(p.PaymentMethod);
                        table.Cell().Background(bg).Padding(3).AlignRight().Text($"{p.Amount:N2}");
                    }
                    // Total row
                    table.Cell().ColumnSpan(3).Padding(4).AlignRight().Text("TOTAL REVENUE:").Bold();
                    table.Cell().Background(Colors.Green.Lighten4).Padding(4).AlignRight()
                        .Text($"KES {_revenue:N2}").Bold().FontColor(Colors.Green.Darken2);
                });
            }

            col.Item().Height(16);

            // Expense details
            col.Item().Text("Expense Breakdown").Bold().FontSize(11);
            col.Item().Height(4);
            if (_expenseDetails.Count == 0)
            {
                col.Item().Text("No expenses recorded for this period.").Italic().FontColor(Colors.Grey.Darken1);
            }
            else
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(4);
                        cols.RelativeColumn();
                        cols.RelativeColumn();
                    });
                    void Hdr(string t) => table.Cell().Background(Colors.Red.Darken1).Padding(4)
                        .Text(t).FontColor(Colors.White).Bold().FontSize(9);
                    Hdr("Category"); Hdr("Description"); Hdr("Date"); Hdr("Amount (KES)");

                    foreach (var e in _expenseDetails)
                    {
                        bool odd = _expenseDetails.IndexOf(e) % 2 == 0;
                        var bg = odd ? Colors.White : Colors.Red.Lighten5;
                        table.Cell().Background(bg).Padding(3).Text(e.Category);
                        table.Cell().Background(bg).Padding(3).Text(e.Description);
                        table.Cell().Background(bg).Padding(3).Text(e.ExpenseDate.ToString("dd/MM/yyyy"));
                        table.Cell().Background(bg).Padding(3).AlignRight().Text($"{e.Amount:N2}");
                    }
                    table.Cell().ColumnSpan(3).Padding(4).AlignRight().Text("TOTAL EXPENSES:").Bold();
                    table.Cell().Background(Colors.Red.Lighten4).Padding(4).AlignRight()
                        .Text($"KES {_expenses:N2}").Bold().FontColor(Colors.Red.Darken2);
                });
            }

            col.Item().Height(16);

            // Net result highlight
            col.Item().Background(net >= 0 ? Colors.Green.Lighten4 : Colors.Orange.Lighten4)
                .Padding(12).Row(row =>
                {
                    row.RelativeItem().Text(net >= 0 ? "✓ SURPLUS" : "⚠ DEFICIT")
                        .Bold().FontSize(13)
                        .FontColor(net >= 0 ? Colors.Green.Darken2 : Colors.Orange.Darken2);
                    row.RelativeItem().AlignRight().Text($"KES {Math.Abs(net):N2}")
                        .Bold().FontSize(13)
                        .FontColor(net >= 0 ? Colors.Green.Darken2 : Colors.Orange.Darken2);
                });
        });
    }

    private static void KpiBox(RowDescriptor row, string label, string value, string color)
    {
        row.RelativeItem().Background(color).Padding(12).Column(col =>
        {
            col.Item().Text(label).FontColor(Colors.White).FontSize(9);
            col.Item().Text(value).FontColor(Colors.White).Bold().FontSize(12);
        });
    }
}
