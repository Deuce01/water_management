using GakunguWater.Models;
using GakunguWater.Services;
using System.Windows;
using System.Windows.Controls;

namespace GakunguWater.Views.Pages;

public partial class TariffsPage : Page
{
    private readonly BillingService _billing;
    private Tariff? _editing;

    public TariffsPage()
    {
        InitializeComponent();
        _billing = App.Resolve<BillingService>();
        Loaded += (_, _) => LoadData();
    }

    private void LoadData()
    {
        try
        {
            GridTariffs.ItemsSource = _billing.GetTariffs() ?? new();
        }
        catch (Exception ex)
        {
            AppLogger.Error("TariffsPage.LoadData failed", ex);
            MessageBox.Show($"Could not load tariffs:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Grid_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        _editing = GridTariffs.SelectedItem as Tariff;
        if (_editing == null) return;
        TxtFormTitle.Text = $"Editing: {_editing.Name}";
        TxtName.Text = _editing.Name;
        CboType.SelectedIndex = _editing.Type == "FlatRate" ? 1 : 0;
        TxtPricePerM3.Text = _editing.PricePerCubicMeter.ToString();
        TxtMinUnits.Text = _editing.MinUnits.ToString();
        TxtMinCharge.Text = _editing.MinCharge.ToString();
        TxtFlatAmount.Text = _editing.FlatAmount.ToString();
        ChkActive.IsChecked = _editing.IsActive;
    }

    private void CboType_Changed(object s, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        bool isFlat = (CboType.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "FlatRate";
        PanelFlat.Visibility = isFlat ? Visibility.Visible : Visibility.Collapsed;
        PanelVolume.Visibility = isFlat ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BtnNew_Click(object s, RoutedEventArgs e)
    {
        _editing = null;
        TxtFormTitle.Text = "New Tariff";
        TxtName.Text = "";
        TxtPricePerM3.Text = "";
        TxtMinUnits.Text = "0";
        TxtMinCharge.Text = "0";
        TxtFlatAmount.Text = "";
        ChkActive.IsChecked = true;
        CboType.SelectedIndex = 0;
    }

    private void BtnSave_Click(object s, RoutedEventArgs e)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("Tariff name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtName.Focus();
            return;
        }

        var type = (CboType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Volumetric";
        var t = _editing ?? new Tariff();
        t.Name = TxtName.Text.Trim();
        t.Type = type;
        t.IsActive = ChkActive.IsChecked == true;
        t.EffectiveFrom = DateTime.Today;

        if (type == "FlatRate")
        {
            if (!decimal.TryParse(TxtFlatAmount.Text, out var flat) || flat <= 0)
            {
                MessageBox.Show("Enter a valid flat amount (must be > 0).", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtFlatAmount.Focus();
                return;
            }
            t.FlatAmount = flat;
        }
        else
        {
            if (!decimal.TryParse(TxtPricePerM3.Text, out var ppm) || ppm <= 0)
            {
                MessageBox.Show("Enter a valid price per m³ (must be > 0).", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPricePerM3.Focus();
                return;
            }
            decimal.TryParse(TxtMinUnits.Text, out var minU);
            decimal.TryParse(TxtMinCharge.Text, out var minC);
            t.PricePerCubicMeter = ppm;
            t.MinUnits = (double)minU;
            t.MinCharge = minC;
        }

        BtnSave.IsEnabled = false;
        try
        {
            _billing.SaveTariff(t);
            LoadData();
            BtnNew_Click(s, e);
            MessageBox.Show("✅ Tariff saved.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("SaveTariff failed", ex);
            MessageBox.Show($"Error saving tariff:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnSave.IsEnabled = true; }
    }

    // Decimal-only input guard for amount fields
    private void DecimalOnly_PreviewTextInput(object s, System.Windows.Input.TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '.');
}
