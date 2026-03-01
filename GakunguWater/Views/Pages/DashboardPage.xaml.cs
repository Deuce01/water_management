using Dapper;
using GakunguWater.Data;
using GakunguWater.Services;
using System.Windows;
using System.Windows.Controls;

namespace GakunguWater.Views.Pages;

public partial class DashboardPage : Page
{
    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadData();
    }

    private void LoadData()
    {
        SetBusy(true);
        try
        {
            var db = App.Resolve<DatabaseService>();
            var paymentSvc = App.Resolve<PaymentService>();

            using var conn = db.GetConnection();

            var totalCustomers = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Customers");
            var activeCustomers = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Customers WHERE ConnectionStatus='Active'");
            TxtTotalCustomers.Text = totalCustomers.ToString();
            TxtActiveCustomers.Text = $"{activeCustomers} active";

            var now = DateTime.Now;
            var revenue = conn.ExecuteScalar<decimal>("""
                SELECT COALESCE(SUM(Amount),0) FROM Payments
                WHERE strftime('%m',PaidAt)=@m AND strftime('%Y',PaidAt)=@y
                """, new { m = now.Month.ToString("D2"), y = now.Year.ToString() });
            TxtRevenue.Text = $"KES {revenue:N0}";

            var unpaid = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Invoices WHERE Status!='Paid'");
            var unpaidTotal = conn.ExecuteScalar<decimal>("SELECT COALESCE(SUM(AmountDue-AmountPaid),0) FROM Invoices WHERE Status!='Paid'");
            TxtUnpaidInvoices.Text = unpaid.ToString();
            TxtUnpaidTotal.Text = $"KES {unpaidTotal:N0} outstanding";

            var defaulters = paymentSvc.GetDefaulters(30) ?? new();
            TxtDefaulters.Text = defaulters.Count.ToString();
            GridDefaulters.ItemsSource = defaulters.Take(10).ToList();

            var payments = paymentSvc.GetPayments(
                from: new DateTime(now.Year, now.Month, 1), to: now) ?? new();
            GridRecentPayments.ItemsSource = payments.Take(10).ToList();
        }
        catch (Exception ex)
        {
            AppLogger.Error("DashboardPage.LoadData failed", ex);
            ShowError("Could not load dashboard data. Check the log for details.");
        }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy) => BusyIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    private void ShowError(string msg) => TxtError.Text = msg; // bound TextBlock in XAML
}
