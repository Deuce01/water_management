using GakunguWater.Models;
using GakunguWater.Services;
using System.Windows;
using System.Windows.Controls;

namespace GakunguWater.Views.Pages;

public partial class MeterReadingsPage : Page
{
    private readonly BillingService _billing;
    private List<MeterReading> _readings = new();

    public MeterReadingsPage()
    {
        InitializeComponent();
        _billing = App.Resolve<BillingService>();

        for (int m = 1; m <= 12; m++)
            CboMonth.Items.Add(new ComboBoxItem
            {
                Content = new DateTime(2000, m, 1).ToString("MMMM"),
                Tag = m
            });
        CboMonth.SelectedIndex = DateTime.Now.Month - 1;

        for (int y = DateTime.Now.Year; y >= DateTime.Now.Year - 5; y--)
            CboYear.Items.Add(new ComboBoxItem { Content = y, Tag = y });
        CboYear.SelectedIndex = 0;

        Loaded += (_, _) => LoadReadings();
    }

    private void Period_Changed(object s, SelectionChangedEventArgs e) { if (IsLoaded) LoadReadings(); }
    private void BtnLoad_Click(object s, RoutedEventArgs e) => LoadReadings();

    private void LoadReadings()
    {
        SetBusy(true);
        try
        {
            int month = GetMonth();
            int year = GetYear();
            _readings = _billing.PrepareBatchReadings(month, year, App.CurrentUser!.Id) ?? new();
            GridReadings.ItemsSource = _readings;
            TxtStatus.Text = $"{_readings.Count} meter(s) loaded for {new DateTime(year, month, 1):MMMM yyyy}.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("MeterReadingsPage.LoadReadings failed", ex);
            TxtStatus.Text = $"❌ Error loading readings: {ex.Message}";
        }
        finally { SetBusy(false); }
    }

    private void BtnSave_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var bad = _readings.Where(r => r.CurrentReading < r.PreviousReading).ToList();
            if (bad.Any())
            {
                MessageBox.Show($"Current reading cannot be less than previous reading for:\n" +
                    string.Join("\n", bad.Select(b => $"  • {b.CustomerName} ({b.MeterNumber})")),
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnSave.IsEnabled = false;
            _billing.SaveReadings(_readings);
            TxtStatus.Text = $"✅ Readings saved for {_readings.Count} meter(s).";
            LoadReadings();
        }
        catch (Exception ex)
        {
            AppLogger.Error("SaveReadings failed", ex);
            MessageBox.Show($"Error saving readings:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnSave.IsEnabled = true; }
    }

    private void BtnGenerate_Click(object s, RoutedEventArgs e)
    {
        int month = GetMonth();
        int year = GetYear();
        var result = MessageBox.Show(
            $"Generate invoices for {new DateTime(year, month, 1):MMMM yyyy}?\nExisting invoices will be skipped.",
            "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        BtnGenerate.IsEnabled = false;
        try
        {
            int count = _billing.GenerateInvoices(month, year);
            TxtStatus.Text = $"✅ {count} invoice(s) generated for {new DateTime(year, month, 1):MMMM yyyy}.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("GenerateInvoices failed", ex);
            MessageBox.Show($"Error generating invoices:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnGenerate.IsEnabled = true; }
    }

    private int GetMonth() => CboMonth.SelectedItem is ComboBoxItem item && item.Tag is int m ? m : DateTime.Now.Month;
    private int GetYear() => CboYear.SelectedItem is ComboBoxItem item && item.Tag is int y ? y : DateTime.Now.Year;
    private void SetBusy(bool busy) => BusyIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
}
