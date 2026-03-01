using GakunguWater.Models;
using GakunguWater.Services;
using GakunguWater.Views.Dialogs;
using System.Windows;
using System.Windows.Controls;

namespace GakunguWater.Views.Pages;

public partial class ExpensesPage : Page
{
    private readonly ExpenseService _svc;
    private Expense? _selected;

    public ExpensesPage()
    {
        InitializeComponent();
        _svc = App.Resolve<ExpenseService>();

        for (int m = 1; m <= 12; m++)
            CboMonth.Items.Add(new ComboBoxItem { Content = new DateTime(2000, m, 1).ToString("MMMM"), Tag = m });
        CboMonth.Items.Insert(0, new ComboBoxItem { Content = "All Months", Tag = 0 });
        CboMonth.SelectedIndex = DateTime.Now.Month; // current month

        for (int y = DateTime.Now.Year; y >= DateTime.Now.Year - 5; y--)
            CboYear.Items.Add(new ComboBoxItem { Content = y, Tag = y });
        CboYear.Items.Insert(0, new ComboBoxItem { Content = "All Years", Tag = 0 });
        CboYear.SelectedIndex = 0;

        Loaded += (_, _) => LoadData();
    }

    private void Filter_Changed(object s, SelectionChangedEventArgs e) { if (IsLoaded) LoadData(); }

    private void LoadData()
    {
        SetBusy(true);
        try
        {
            int month = Convert.ToInt32((CboMonth.SelectedItem as ComboBoxItem)?.Tag ?? 0);
            int year = Convert.ToInt32((CboYear.SelectedItem as ComboBoxItem)?.Tag ?? 0);
            var cat = (CboCategory.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var items = _svc.GetAll(month == 0 ? null : month, year == 0 ? null : year,
                string.IsNullOrEmpty(cat) ? null : cat) ?? new();
            GridExpenses.ItemsSource = items;
            TxtTotal.Text = $"Total: KES {items.Sum(e => e.Amount):N2}";
        }
        catch (Exception ex)
        {
            AppLogger.Error("ExpensesPage.LoadData failed", ex);
            MessageBox.Show($"Could not load expenses:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    private void Grid_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        _selected = GridExpenses.SelectedItem as Expense;
        bool has = _selected != null;
        BtnEdit.IsEnabled = has;
        BtnDelete.IsEnabled = has;
    }

    private void BtnAdd_Click(object s, RoutedEventArgs e)
    {
        BtnAdd.IsEnabled = false;
        try
        {
            var dlg = new ExpenseDialog(null);
            if (dlg.ShowDialog() == true) LoadData();
        }
        catch (Exception ex)
        {
            AppLogger.Error("BtnAdd expense dialog failed", ex);
            MessageBox.Show($"Could not open expense dialog:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnAdd.IsEnabled = true; }
    }

    private void BtnEdit_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null) return;
        BtnEdit.IsEnabled = false;
        try
        {
            var dlg = new ExpenseDialog(_selected);
            if (dlg.ShowDialog() == true) LoadData();
        }
        catch (Exception ex)
        {
            AppLogger.Error("BtnEdit expense dialog failed", ex);
            MessageBox.Show($"Could not open expense dialog:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnEdit.IsEnabled = _selected != null; }
    }

    private void BtnDelete_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var r = MessageBox.Show($"Delete expense: {_selected.Description}?", "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        try { _svc.Delete(_selected.Id); LoadData(); }
        catch (Exception ex)
        {
            AppLogger.Error("Delete expense failed", ex);
            MessageBox.Show($"Could not delete expense:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetBusy(bool busy) => BusyIndicator.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
}
