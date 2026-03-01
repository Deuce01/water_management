using GakunguWater.Models;
using System.Windows;

namespace GakunguWater.Views.Dialogs;

// Wrapper for DataGrid binding (adds editable checkbox)
public class MpesaRowViewModel
{
    private readonly MpesaCsvRow _row;
    public MpesaRowViewModel(MpesaCsvRow row) => _row = row;

    public bool IsMatchedEditable { get; set; }
    public string ReceiptNo => _row.ReceiptNo;
    public decimal PaidIn => _row.PaidIn;
    public string PhoneNumber => _row.PhoneNumber;
    public string FirstName => $"{_row.FirstName} {_row.LastName}".Trim();
    public string? MatchedCustomerName => _row.MatchedCustomerName;
    public int? MatchedInvoiceId => _row.MatchedInvoiceId;
    public MpesaCsvRow Row => _row;
}

public partial class MpesaImportDialog : Window
{
    private readonly List<MpesaRowViewModel> _vms;
    public List<MpesaCsvRow> ConfirmedRows { get; private set; } = new();

    public MpesaImportDialog(List<MpesaCsvRow> rows)
    {
        InitializeComponent();
        _vms = rows.Select(r => new MpesaRowViewModel(r) { IsMatchedEditable = r.IsMatched && r.MatchedInvoiceId.HasValue }).ToList();
        GridRows.ItemsSource = _vms;

        int matched = _vms.Count(v => v.IsMatchedEditable);
        int unmatched = _vms.Count - matched;
        TxtSummary.Text = $"{matched} matched / {unmatched} unmatched";
    }

    private void BtnPost_Click(object s, RoutedEventArgs e)
    {
        ConfirmedRows = _vms
            .Where(v => v.IsMatchedEditable && v.MatchedInvoiceId.HasValue)
            .Select(v => v.Row)
            .ToList();

        if (ConfirmedRows.Count == 0)
        {
            MessageBox.Show("No rows selected to post.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void BtnCancel_Click(object s, RoutedEventArgs e) => DialogResult = false;
}
