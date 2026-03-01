using GakunguWater.Data;
using GakunguWater.Models;
using GakunguWater.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GakunguWater.Views.Dialogs;

public partial class CustomerDialog : Window
{
    private readonly CustomerService _svc;
    private readonly Customer? _existing;

    public CustomerDialog(Customer? customer)
    {
        InitializeComponent();
        _svc = App.Resolve<CustomerService>();
        _existing = customer;

        if (customer != null)
        {
            TxtTitle.Text = "Edit Customer";
            TxtName.Text = customer.FullName;
            TxtPhone.Text = customer.PhoneNumber;
            TxtLocation.Text = customer.Location;
            TxtNotes.Text = customer.Notes;

            foreach (ComboBoxItem item in CboStatus.Items)
                if (item.Tag?.ToString() == customer.ConnectionStatus)
                { item.IsSelected = true; break; }
        }
    }

    private void BtnSave_Click(object s, RoutedEventArgs e)
    {
        // ── Validation ─────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("Full Name is required.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtName.Focus();
            return;
        }

        // Phone — optional but must be numeric if provided
        var phone = TxtPhone.Text.Trim();
        if (!string.IsNullOrEmpty(phone) && !phone.All(c => char.IsDigit(c) || c == '+' || c == '-' || c == ' '))
        {
            MessageBox.Show("Phone number contains invalid characters.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtPhone.Focus();
            return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            var c = _existing ?? new Customer();
            c.FullName = TxtName.Text.Trim();
            c.PhoneNumber = phone;
            c.Location = TxtLocation.Text.Trim();
            c.ConnectionStatus = (CboStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Active";
            c.Notes = string.IsNullOrWhiteSpace(TxtNotes.Text) ? null : TxtNotes.Text.Trim();

            if (_existing == null) _svc.Add(c);
            else _svc.Update(c);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("CustomerDialog.BtnSave failed", ex);
            MessageBox.Show($"Error saving customer:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnSave.IsEnabled = true; }
    }

    private void BtnCancel_Click(object s, RoutedEventArgs e) => DialogResult = false;
}
