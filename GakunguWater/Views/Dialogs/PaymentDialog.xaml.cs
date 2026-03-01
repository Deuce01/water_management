using GakunguWater.Models;
using GakunguWater.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GakunguWater.Views.Dialogs;

public partial class PaymentDialog : Window
{
    private readonly PaymentService _svc;
    private readonly Invoice _invoice;

    public PaymentDialog(Invoice invoice)
    {
        InitializeComponent();
        _svc = App.Resolve<PaymentService>();
        _invoice = invoice;
        var period = new DateTime(invoice.BillingYear, invoice.BillingMonth, 1).ToString("MMMM yyyy");
        TxtInvoiceInfo.Text = $"{invoice.CustomerName}  |  {period}  |  Balance: KES {invoice.Balance:N2}";
        TxtAmount.Text = invoice.Balance.ToString("F2");
    }

    private void CboMethod_Changed(object s, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        bool isMpesa = (CboMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() == "MPesa";
        PanelMpesa.Visibility = isMpesa ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnSave_Click(object s, RoutedEventArgs e)
    {
        // ── Validation ──────────────────────────────────────────
        if (!decimal.TryParse(TxtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show("Enter a valid amount greater than 0.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtAmount.Focus();
            return;
        }
        if (amount > _invoice.Balance * 2)   // sanity cap: no more than 2× balance
        {
            MessageBox.Show($"Amount of KES {amount:N2} is unexpectedly large.\nPlease verify.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var method = (CboMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";
        if (method == "MPesa")
        {
            var ref2 = TxtMpesaRef.Text.Trim();
            if (string.IsNullOrWhiteSpace(ref2))
            {
                MessageBox.Show("M-Pesa reference is required for MPesa payments.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtMpesaRef.Focus();
                return;
            }

            // ── Duplicate receipt check ─────────────────────────
            if (_svc.MpesaReceiptExists(ref2))
            {
                MessageBox.Show($"M-Pesa reference '{ref2}' has already been posted.", "Duplicate Receipt",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        BtnSave.IsEnabled = false;
        try
        {
            _svc.LogPayment(_invoice.Id, _invoice.CustomerId, amount,
                App.CurrentUser!.Id, method,
                method == "MPesa" ? TxtMpesaRef.Text.Trim() : null,
                TxtNotes.Text.Trim());
            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("PaymentDialog.BtnSave failed", ex);
            MessageBox.Show($"Error recording payment:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnSave.IsEnabled = true; }
    }

    // Decimal-only input guard
    private void DecimalOnly_PreviewTextInput(object s, TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '.');

    private void BtnCancel_Click(object s, RoutedEventArgs e) => DialogResult = false;
}
