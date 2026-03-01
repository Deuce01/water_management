using GakunguWater.Models;
using GakunguWater.Services;
using GakunguWater.Views.Dialogs;
using System.Windows;
using System.Windows.Controls;

namespace GakunguWater.Views.Pages;

public partial class InvoicesPage : Page
{
    private readonly BillingService _billing;
    private readonly ReportService _reports;
    private Invoice? _selected;

    public InvoicesPage()
    {
        InitializeComponent();
        _billing = App.Resolve<BillingService>();
        _reports = App.Resolve<ReportService>();

        for (int m = 1; m <= 12; m++)
            CboMonth.Items.Add(new ComboBoxItem { Content = new DateTime(2000, m, 1).ToString("MMMM"), Tag = m });
        CboMonth.Items.Insert(0, new ComboBoxItem { Content = "All Months", Tag = 0 });
        CboMonth.SelectedIndex = 0;

        for (int y = DateTime.Now.Year; y >= DateTime.Now.Year - 5; y--)
            CboYear.Items.Add(new ComboBoxItem { Content = y, Tag = y });
        CboYear.Items.Insert(0, new ComboBoxItem { Content = "All Years", Tag = 0 });
        CboYear.SelectedIndex = 0;

        Loaded += (_, _) => LoadData();
    }

    private void Filter_Changed(object s, SelectionChangedEventArgs e) { if (IsLoaded) LoadData(); }
    private void BtnRefresh_Click(object s, RoutedEventArgs e) => LoadData();

    private void LoadData()
    {
        SetBusy(true);
        try
        {
            int month = Convert.ToInt32((CboMonth.SelectedItem as ComboBoxItem)?.Tag ?? 0);
            int year = Convert.ToInt32((CboYear.SelectedItem as ComboBoxItem)?.Tag ?? 0);
            var status = (CboStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString();

            var items = _billing.GetInvoices(
                month == 0 ? null : month,
                year == 0 ? null : year,
                string.IsNullOrEmpty(status) ? null : status) ?? new();
            GridInvoices.ItemsSource = items;
            TxtCount.Text = $"{items.Count} invoice(s)";
        }
        catch (Exception ex)
        {
            AppLogger.Error("InvoicesPage.LoadData failed", ex);
            MessageBox.Show($"Could not load invoices:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    private void Grid_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        _selected = GridInvoices.SelectedItem as Invoice;
        bool has = _selected != null;
        BtnPrint.IsEnabled = has;
        BtnPay.IsEnabled = has && _selected?.Status != "Paid";
    }

    private void BtnPrint_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null) return;
        BtnPrint.IsEnabled = false;
        try { _reports.PrintInvoice(_selected); }
        catch (Exception ex)
        {
            AppLogger.Error("PrintInvoice failed", ex);
            MessageBox.Show(ex.Message, "Print Error");
        }
        finally { BtnPrint.IsEnabled = _selected != null; }
    }

    private void BtnPay_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null) return;
        BtnPay.IsEnabled = false;
        try
        {
            var dlg = new PaymentDialog(_selected);
            if (dlg.ShowDialog() == true) { _selected = null; LoadData(); }
        }
        finally { BtnPay.IsEnabled = _selected != null && _selected.Status != "Paid"; }
    }

    private void SetBusy(bool busy) => BusyIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
}
