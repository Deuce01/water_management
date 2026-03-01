using GakunguWater.Services;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace GakunguWater.Views.Pages;

public partial class BackupPage : Page
{
    private readonly BackupService _backup;

    public BackupPage()
    {
        InitializeComponent();
        _backup = App.Resolve<BackupService>();
        TxtPath.Text = _backup.BackupTargetPath;
        Loaded += (_, _) => LoadHistory();
    }

    private void LoadHistory()
    {
        try
        {
            GridHistory.ItemsSource = _backup.GetBackupHistory() ?? new();
            TxtLastBackup.Text = _backup.LastBackupTime.HasValue
                ? $"Last Backup: {_backup.LastBackupTime:dd/MM/yyyy HH:mm:ss} — {(_backup.LastBackupSuccess ? "✅ Success" : "❌ Failed")}"
                : "No backup has been run yet in this session.";
        }
        catch (Exception ex)
        {
            AppLogger.Error("BackupPage.LoadHistory failed", ex);
        }
    }

    private void BtnBrowse_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select backup target folder",
            InitialDirectory = string.IsNullOrWhiteSpace(TxtPath.Text) ? "C:\\" : TxtPath.Text
        };
        if (dlg.ShowDialog() == true)
            TxtPath.Text = dlg.FolderName;
    }

    private void BtnSavePath_Click(object s, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtPath.Text))
        {
            MessageBox.Show("Please select or enter a backup folder path.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _backup.BackupTargetPath = TxtPath.Text.Trim();
        MessageBox.Show("✅ Backup path saved.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnBackup_Click(object s, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtPath.Text))
        {
            MessageBox.Show("Please select a backup folder first.", "No Folder Set",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnBackup.IsEnabled = false;
        BtnBackup.Content = "⏳ Backing up…";
        try
        {
            var result = _backup.PerformBackup(TxtPath.Text.Trim());
            if (result.Success)
            {
                AppLogger.Info($"Manual backup succeeded: {result.Path}");
                MessageBox.Show($"✅ Backup successful!\n{result.Path}", "Backup",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                AppLogger.Error($"Manual backup failed: {result.ErrorMessage}");
                MessageBox.Show($"❌ Backup failed:\n{result.ErrorMessage}", "Backup Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            LoadHistory();
        }
        catch (Exception ex)
        {
            AppLogger.Error("BtnBackup_Click unhandled", ex);
            MessageBox.Show($"Unexpected error during backup:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnBackup.IsEnabled = true;
            BtnBackup.Content = "💾 Run Backup Now";
        }
    }
}
