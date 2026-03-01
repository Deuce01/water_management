using GakunguWater.Models;
using GakunguWater.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GakunguWater.Views.Dialogs;

public partial class ExpenseDialog : Window
{
    private readonly ExpenseService _svc;
    private readonly Expense? _existing;

    public ExpenseDialog(Expense? expense)
    {
        InitializeComponent();
        _svc = App.Resolve<ExpenseService>();
        _existing = expense;
        DpDate.SelectedDate = DateTime.Today;

        if (expense != null)
        {
            TxtTitle.Text = "Edit Expense";
            TxtDescription.Text = expense.Description;
            TxtAmount.Text = expense.Amount.ToString("F2");
            DpDate.SelectedDate = expense.ExpenseDate;
            foreach (ComboBoxItem item in CboCategory.Items)
                if (item.Content?.ToString() == expense.Category) { item.IsSelected = true; break; }
        }
    }

    private void BtnSave_Click(object s, RoutedEventArgs e)
    {
        // ── Validation ──────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(TxtDescription.Text))
        {
            MessageBox.Show("Description is required.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtDescription.Focus();
            return;
        }
        if (!decimal.TryParse(TxtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show("Enter a valid amount greater than 0.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtAmount.Focus();
            return;
        }

        BtnSave.IsEnabled = false;
        try
        {
            var exp = _existing ?? new Expense { EnteredByUserId = App.CurrentUser!.Id };
            exp.Category = (CboCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Other";
            exp.Description = TxtDescription.Text.Trim();
            exp.Amount = amount;
            exp.ExpenseDate = DpDate.SelectedDate ?? DateTime.Today;

            if (_existing == null) _svc.Add(exp);
            else _svc.Update(exp);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            AppLogger.Error("ExpenseDialog.BtnSave failed", ex);
            MessageBox.Show($"Error saving expense:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnSave.IsEnabled = true; }
    }

    // Decimal-only input guard for amount field
    private void DecimalOnly_PreviewTextInput(object s, TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '.');

    private void BtnCancel_Click(object s, RoutedEventArgs e) => DialogResult = false;
}
