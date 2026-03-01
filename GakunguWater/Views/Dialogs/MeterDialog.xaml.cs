using GakunguWater.Data;
using GakunguWater.Models;
using GakunguWater.Services;
using System.Windows;
using System.Windows.Input;

namespace GakunguWater.Views.Dialogs;

public partial class MeterDialog : Window
{
    private readonly CustomerService _svc;
    private readonly DatabaseService _db;
    private readonly Customer _customer;
    private Meter? _existing;

    public MeterDialog(Customer customer)
    {
        InitializeComponent();
        _svc = App.Resolve<CustomerService>();
        _db = App.Resolve<DatabaseService>();
        _customer = customer;
        TxtCustomerName.Text = $"Customer: {customer.FullName}";
        DpInstall.SelectedDate = DateTime.Today;

        try
        {
            var meters = _svc.GetMeters(customer.Id);
            _existing = meters.FirstOrDefault(m => m.IsActive);
            if (_existing != null)
            {
                TxtTitle.Text = "Edit Meter";
                TxtMeterNumber.Text = _existing.MeterNumber;
                DpInstall.SelectedDate = _existing.InstallDate;
                ChkActive.IsChecked = _existing.IsActive;
                TxtNotes.Text = _existing.Notes;
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("MeterDialog: failed to load existing meter", ex);
            MessageBox.Show($"Could not load meter data:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnSave_Click(object s, RoutedEventArgs e)
    {
        var meterNum = TxtMeterNumber.Text.Trim();

        if (string.IsNullOrWhiteSpace(meterNum))
        {
            MessageBox.Show("Meter number is required.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtMeterNumber.Focus();
            return;
        }

        // ── Duplicate detection ──────────────────────────────────
        int excludeId = _existing?.Id ?? 0;
        if (_db.MeterNumberExists(meterNum, excludeId))
        {
            MessageBox.Show($"Meter number '{meterNum}' is already assigned to another customer.",
                "Duplicate Meter", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtMeterNumber.Focus();
            return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            var m = _existing ?? new Meter { CustomerId = _customer.Id };
            m.MeterNumber = meterNum;
            m.InstallDate = DpInstall.SelectedDate ?? DateTime.Today;
            m.IsActive = ChkActive.IsChecked == true;
            m.Notes = TxtNotes.Text.Trim();

            if (_existing == null) _svc.AddMeter(m);
            else _svc.UpdateMeter(m);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("MeterDialog.BtnSave failed", ex);
            MessageBox.Show($"Error saving meter:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnSave.IsEnabled = true; }
    }

    private void BtnCancel_Click(object s, RoutedEventArgs e) => DialogResult = false;
}
