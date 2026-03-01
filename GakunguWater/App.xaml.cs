using GakunguWater.Data;
using GakunguWater.Models;
using GakunguWater.Services;
using GakunguWater.Views;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace GakunguWater;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static UserSession? CurrentUser { get; set; }

    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GakunguWater", "gakungu.db");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Global exception handlers ────────────────────────────
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);

            var services = new ServiceCollection();

            var dbService = new DatabaseService(DbPath);
            dbService.Initialize();

            services.AddSingleton(dbService);
            services.AddSingleton<AuthService>();
            services.AddSingleton<CustomerService>();
            services.AddSingleton<BillingService>();
            services.AddSingleton<PaymentService>();
            services.AddSingleton<ExpenseService>();
            services.AddSingleton(new BackupService(dbService, DbPath));
            services.AddSingleton<ReportService>();

            Services = services.BuildServiceProvider();

            // Start auto-backup
            var backup = Services.GetRequiredService<BackupService>();
            backup.StartAutoBackup();

            // Warn if no backup in 7+ days
            CheckBackupWarning(backup);

            AppLogger.Info("Application started.");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Fatal startup error", ex);
            MessageBox.Show(
                $"Failed to start Gakungu Water:\n\n{ex.Message}\n\nCheck the log at:\n%LocalAppData%\\GakunguWater\\app.log",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void CheckBackupWarning(BackupService backup)
    {
        try
        {
            if (backup.LastBackupTime.HasValue &&
                (DateTime.Now - backup.LastBackupTime.Value).TotalDays < 7)
                return;  // recent backup exists

            // Check DB log for last successful backup
            using var conn = App.Resolve<DatabaseService>().GetConnection();
            var lastOk = Dapper.SqlMapper.ExecuteScalar<string?>(conn,
                "SELECT BackupAt FROM BackupLog WHERE Success=1 ORDER BY BackupAt DESC LIMIT 1");

            if (lastOk == null ||
                (DateTime.TryParse(lastOk, out var dt) && (DateTime.Now - dt).TotalDays >= 7))
            {
                MessageBox.Show(
                    "⚠️ No successful database backup in the last 7 days.\n\nPlease go to Backup page and run a manual backup.",
                    "Backup Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch { /* never crash on backup warning */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { Services?.GetRequiredService<BackupService>().StopAutoBackup(); } catch { }
        AppLogger.Info("Application exited.");
        base.OnExit(e);
    }

    // ── Global exception handlers ────────────────────────────────
    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Error("Unhandled UI exception", e.Exception);
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will continue running.\nDetails logged to: %LocalAppData%\\GakunguWater\\app.log",
            "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true; // prevent crash
    }

    private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        AppLogger.Error("Fatal domain exception", ex);
        MessageBox.Show(
            $"A fatal error occurred:\n\n{ex?.Message}\n\nThe application must close.\nDetails logged to: %LocalAppData%\\GakunguWater\\app.log",
            "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static T Resolve<T>() where T : notnull => Services.GetRequiredService<T>();
}
