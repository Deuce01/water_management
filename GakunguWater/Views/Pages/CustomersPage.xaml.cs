using GakunguWater.Models;
using GakunguWater.Services;
using GakunguWater.Views.Dialogs;
using System.Windows;
using System.Windows.Controls;

namespace GakunguWater.Views.Pages;

public partial class CustomersPage : Page
{
    private readonly CustomerService _svc;
    private Customer? _selected;

    public CustomersPage()
    {
        InitializeComponent();
        _svc = App.Resolve<CustomerService>();
        Loaded += (_, _) => LoadData();
    }

    private void LoadData()
    {
        SetBusy(true);
        try
        {
            var status = (CboStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var items = _svc.GetAll(TxtSearch.Text, string.IsNullOrEmpty(status) ? null : status)
                        ?? new();
            GridCustomers.ItemsSource = items;
            TxtCount.Text = $"{items.Count} customer(s) found";
        }
        catch (Exception ex)
        {
            AppLogger.Error("CustomersPage.LoadData failed", ex);
            MessageBox.Show($"Could not load customers:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    private void TxtSearch_TextChanged(object s, TextChangedEventArgs e) { if (IsLoaded) LoadData(); }
    private void CboStatus_SelectionChanged(object s, SelectionChangedEventArgs e) { if (IsLoaded) LoadData(); }
    private void BtnRefresh_Click(object s, RoutedEventArgs e) => LoadData();

    private void GridCustomers_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        _selected = GridCustomers.SelectedItem as Customer;
        bool has = _selected != null;
        BtnEdit.IsEnabled = has;
        BtnMeter.IsEnabled = has;
        BtnActive.IsEnabled = has;
        BtnSuspend.IsEnabled = has;
    }

    private void BtnAdd_Click(object s, RoutedEventArgs e)
    {
        BtnAdd.IsEnabled = false;
        try
        {
            var dlg = new CustomerDialog(null);
            if (dlg.ShowDialog() == true) LoadData();
        }
        finally { BtnAdd.IsEnabled = true; }
    }

    private void BtnEdit_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null) return;
        BtnEdit.IsEnabled = false;
        try
        {
            var dlg = new CustomerDialog(_selected);
            if (dlg.ShowDialog() == true) LoadData();
        }
        finally { BtnEdit.IsEnabled = true; }
    }

    private void BtnMeter_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null) return;
        BtnMeter.IsEnabled = false;
        try
        {
            var dlg = new MeterDialog(_selected);
            if (dlg.ShowDialog() == true) LoadData();
        }
        catch (Exception ex)
        {
            AppLogger.Error("BtnMeter_Click failed", ex);
            MessageBox.Show($"Could not open meter dialog:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnMeter.IsEnabled = _selected != null; }
    }

    private void BtnActive_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null) return;
        BtnActive.IsEnabled = false;
        try { _svc.SetStatus(_selected.Id, "Active"); LoadData(); }
        catch (Exception ex)
        {
            AppLogger.Error("Set Active failed", ex);
            MessageBox.Show($"Could not update status:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { BtnActive.IsEnabled = _selected != null; }
    }

    private void BtnSuspend_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null) return;
        BtnSuspend.IsEnabled = false;
        try { _svc.SetStatus(_selected.Id, "Suspended"); LoadData(); }
        catch (Exception ex)
        {
            AppLogger.Error("Set Suspended failed", ex);
            MessageBox.Show($"Could not update status:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { BtnSuspend.IsEnabled = _selected != null; }
    }

    private void SetBusy(bool busy) => BusyIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
}
