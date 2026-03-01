using GakunguWater.Views.Pages;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GakunguWater.Views;

public partial class MainWindow : Window
{
    private Button? _activeButton;
    private readonly DispatcherTimer _clock;

    public MainWindow()
    {
        InitializeComponent();

        var user = App.CurrentUser;
        TxtCurrentUser.Text = $"👤 {user?.FullName ?? user?.Username}\n{user?.Role}";

        // Hide admin-only nav items for cashiers
        if (user?.IsAdmin != true)
        {
            BtnUsers.Visibility = Visibility.Collapsed;
            AdminSection.Visibility = Visibility.Collapsed;
        }

        // Clock in status bar
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => TxtDateTime.Text = DateTime.Now.ToString("ddd, dd MMM yyyy  HH:mm:ss");
        _clock.Start();
        TxtDateTime.Text = DateTime.Now.ToString("ddd, dd MMM yyyy  HH:mm:ss");

        TxtStatus.Text = "  Ready";

        // Navigate to dashboard by default
        Navigate(BtnDashboard);
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn) Navigate(btn);
    }

    private void Navigate(Button btn)
    {
        // Update active style
        if (_activeButton != null) _activeButton.Style = (Style)FindResource("NavButton");
        btn.Style = (Style)FindResource("NavButtonActive");
        _activeButton = btn;

        Page? page = null;
        try
        {
            page = btn.Tag?.ToString() switch
            {
                "Dashboard"  => new DashboardPage(),
                "Customers"  => new CustomersPage(),
                "Readings"   => new MeterReadingsPage(),
                "Invoices"   => new InvoicesPage(),
                "Payments"   => new PaymentsPage(),
                "Defaulters" => new DefaultersPage(),
                "Expenses"   => new ExpensesPage(),
                "Finance"    => new FinanceReportPage(),
                "Tariffs"    => new TariffsPage(),
                "Users"      => new UsersPage(),
                "Backup"     => new BackupPage(),
                _ => null
            };
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Navigate failed for '{btn.Tag}'", ex);
            TxtStatus.Text = $"  ❌ Could not load page: {ex.Message}";
            // Restore previous active button so the nav bar remains consistent
            btn.Style = (Style)FindResource("NavButton");
            _activeButton = null;
            return;
        }

        if (page != null) FrameContent.Navigate(page);
        TxtStatus.Text = $"  {btn.Content}";
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        _clock.Stop();
        try { App.Resolve<GakunguWater.Services.AuthService>().Logout(); } catch { }
        App.CurrentUser = null;
        var login = new LoginWindow();
        login.Show();
        Close();
    }
}
