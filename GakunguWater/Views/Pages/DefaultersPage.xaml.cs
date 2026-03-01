using GakunguWater.Services;
using System.Windows;
using System.Windows.Controls;

namespace GakunguWater.Views.Pages;

public partial class DefaultersPage : Page
{
    private readonly PaymentService _svc;

    public DefaultersPage()
    {
        InitializeComponent();
        _svc = App.Resolve<PaymentService>();
        Loaded += (_, _) => LoadData();
    }

    private void CboDays_Changed(object s, SelectionChangedEventArgs e) { if (IsLoaded) LoadData(); }

    private void LoadData()
    {
        SetBusy(true);
        try
        {
            int days = Convert.ToInt32((CboDays.SelectedItem as ComboBoxItem)?.Tag ?? 30);
            var items = _svc.GetDefaulters(days) ?? new();
            GridDefaulters.ItemsSource = items;
            var totalOwed = items.Sum(i => i.Balance);
            TxtSummary.Text = $"{items.Count} defaulter(s) — Total owed: KES {totalOwed:N2}";
        }
        catch (Exception ex)
        {
            AppLogger.Error("DefaultersPage.LoadData failed", ex);
            MessageBox.Show($"Could not load defaulters:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy) => BusyIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
}
