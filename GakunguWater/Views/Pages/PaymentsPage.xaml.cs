using GakunguWater.Models;
using GakunguWater.Services;
using GakunguWater.Views.Dialogs;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace GakunguWater.Views.Pages;

public partial class PaymentsPage : Page
{
    private readonly PaymentService _svc;
    private readonly ReportService _reports;
    private Payment? _selected;

    public PaymentsPage()
    {
        InitializeComponent();
        _svc = App.Resolve<PaymentService>();
        _reports = App.Resolve<ReportService>();
        DpFrom.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        DpTo.SelectedDate = DateTime.Today;
        Loaded += (_, _) => LoadData();
    }

    private void BtnRefresh_Click(object s, RoutedEventArgs e) => LoadData();

    private void LoadData()
    {
        SetBusy(true);
        try
        {
            var items = _svc.GetPayments(DpFrom.SelectedDate, DpTo.SelectedDate) ?? new();
            GridPayments.ItemsSource = items;
            var total = items.Sum(p => p.Amount);
            TxtTotals.Text = $"Total: KES {total:N2}";
        }
        catch (Exception ex)
        {
            AppLogger.Error("PaymentsPage.LoadData failed", ex);
            MessageBox.Show($"Could not load payments:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    private void Grid_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        _selected = GridPayments.SelectedItem as Payment;
        BtnPrintReceipt.IsEnabled = _selected != null;
    }

    private void BtnPrintReceipt_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null) return;
        BtnPrintReceipt.IsEnabled = false;
        try { _reports.PrintReceipt(_selected); }
        catch (Exception ex)
        {
            AppLogger.Error("PrintReceipt failed", ex);
            MessageBox.Show(ex.Message, "Print Error");
        }
        finally { BtnPrintReceipt.IsEnabled = _selected != null; }
    }

    private void BtnMpesa_Click(object s, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title = "Select M-Pesa CSV Statement",
            Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
        };
        if (ofd.ShowDialog() != true) return;

        BtnMpesa.IsEnabled = false;
        try
        {
            var rows = _svc.ParseMpesaCsv(ofd.FileName);
            if (rows.Count == 0)
            {
                MessageBox.Show("No valid rows found in the CSV file.\nCheck that it is a Safaricom M-Pesa statement.",
                    "Empty Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _svc.MatchMpesaRows(rows);

            var dlg = new MpesaImportDialog(rows);
            if (dlg.ShowDialog() == true)
            {
                var confirmed = dlg.ConfirmedRows ?? new();
                if (confirmed.Count == 0)
                {
                    MessageBox.Show("No rows were confirmed for posting.", "Nothing Posted",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _svc.PostMpesaPayments(confirmed, App.CurrentUser!.Id);
                    MessageBox.Show($"✅ {confirmed.Count} payment(s) posted.", "M-Pesa Import",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("M-Pesa import failed", ex);
            MessageBox.Show($"Error reading CSV:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnMpesa.IsEnabled = true; }
    }

    private void SetBusy(bool busy) => BusyIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
}
