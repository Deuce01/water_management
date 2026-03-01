using GakunguWater.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GakunguWater.Views.Pages;

public partial class FinanceReportPage : Page
{
    private readonly ExpenseService _expSvc;
    private readonly PaymentService _paymentSvc;
    private readonly ReportService _reports;
    private int _month, _year;
    private decimal _revenue, _expenses;

    public FinanceReportPage()
    {
        InitializeComponent();
        _expSvc = App.Resolve<ExpenseService>();
        _paymentSvc = App.Resolve<PaymentService>();
        _reports = App.Resolve<ReportService>();

        for (int m = 1; m <= 12; m++)
            CboMonth.Items.Add(new ComboBoxItem { Content = new DateTime(2000, m, 1).ToString("MMMM"), Tag = m });
        CboMonth.SelectedIndex = DateTime.Now.Month - 1;

        for (int y = DateTime.Now.Year; y >= DateTime.Now.Year - 5; y--)
            CboYear.Items.Add(new ComboBoxItem { Content = y, Tag = y });
        CboYear.SelectedIndex = 0;
    }

    private void BtnCalc_Click(object s, RoutedEventArgs e)
    {
        BtnCalc.IsEnabled = false;
        try
        {
            _month = Convert.ToInt32((CboMonth.SelectedItem as ComboBoxItem)?.Tag ?? 0);
            _year = Convert.ToInt32((CboYear.SelectedItem as ComboBoxItem)?.Tag ?? 0);
            (_revenue, _expenses, var net) = _expSvc.GetFinancialSummary(_month, _year);

            TxtRevenue.Text = $"KES {_revenue:N2}";
            TxtExpenses.Text = $"KES {_expenses:N2}";
            TxtNet.Text = $"KES {net:N2}";

            bool surplus = net >= 0;
            TxtNet.Foreground = surplus
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563A8"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12"));
            TxtNetLabel.Text = surplus ? "✓ SURPLUS" : "⚠ DEFICIT";
            TxtNetLabel.Foreground = TxtNet.Foreground;
            BtnExport.IsEnabled = true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("FinanceReportPage calculation failed", ex);
            MessageBox.Show($"Could not calculate summary:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnCalc.IsEnabled = true; }
    }

    private void BtnExport_Click(object s, RoutedEventArgs e)
    {
        BtnExport.IsEnabled = false;
        try
        {
            var expenses = _expSvc.GetAll(_month, _year) ?? new();
            var payments = _paymentSvc.GetPayments(
                new DateTime(_year, _month, 1),
                new DateTime(_year, _month, DateTime.DaysInMonth(_year, _month))) ?? new();
            _reports.PrintFinancialSummary(_month, _year, _revenue, _expenses, expenses, payments);
        }
        catch (Exception ex)
        {
            AppLogger.Error("FinanceReport export failed", ex);
            MessageBox.Show($"Error generating report:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnExport.IsEnabled = true; }
    }
}
