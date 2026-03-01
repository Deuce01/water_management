using GakunguWater.Models;
using GakunguWater.Services;
using System.Windows;
using System.Windows.Controls;

namespace GakunguWater.Views.Pages;

public partial class UsersPage : Page
{
    private readonly AuthService _auth;
    private User? _selected;

    public UsersPage()
    {
        InitializeComponent();
        _auth = App.Resolve<AuthService>();
        Loaded += (_, _) => LoadData();
    }

    private void LoadData()
    {
        try
        {
            GridUsers.ItemsSource = _auth.GetAllUsers() ?? new();
        }
        catch (Exception ex)
        {
            AppLogger.Error("UsersPage.LoadData failed", ex);
            MessageBox.Show($"Could not load users:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Grid_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        _selected = GridUsers.SelectedItem as User;
        bool has = _selected != null;
        BtnDisable.IsEnabled = has && _selected!.IsActive;
        BtnEnable.IsEnabled = has && !_selected!.IsActive;
        BtnResetPwd.IsEnabled = has;
    }

    private void BtnDisable_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null) return;
        if (_selected.Username == "admin")
        {
            MessageBox.Show("The admin account cannot be disabled.", "Not Allowed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try { _auth.SetUserActive(_selected.Id, false); LoadData(); }
        catch (Exception ex)
        {
            AppLogger.Error("Disable user failed", ex);
            MessageBox.Show(ex.Message, "Error");
        }
    }

    private void BtnEnable_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null) return;
        try { _auth.SetUserActive(_selected.Id, true); LoadData(); }
        catch (Exception ex)
        {
            AppLogger.Error("Enable user failed", ex);
            MessageBox.Show(ex.Message, "Error");
        }
    }

    private void BtnReset_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null) return;
        // Use the inline reset panel instead of VB InputBox
        PanelResetPwd.Visibility = Visibility.Visible;
        TxtResetLabel.Text = $"New password for '{_selected.Username}':";
        PwdReset.Focus();
    }

    private void BtnConfirmReset_Click(object s, RoutedEventArgs e)
    {
        if (_selected == null)
        {
            PwdReset.Clear();
            PanelResetPwd.Visibility = Visibility.Collapsed;
            MessageBox.Show("No user selected. Please select a user and try again.", "Selection Lost",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var pwd = PwdReset.Password;
        if (pwd.Length < 6)
        {
            MessageBox.Show("Password must be at least 6 characters.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var confirmBtn = s as System.Windows.Controls.Button;
        if (confirmBtn != null) confirmBtn.IsEnabled = false;
        try
        {
            _auth.ChangePassword(_selected.Id, pwd);
            PwdReset.Clear();
            PanelResetPwd.Visibility = Visibility.Collapsed;
            MessageBox.Show("✅ Password changed.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Reset password failed", ex);
            MessageBox.Show(ex.Message, "Error");
        }
        finally { if (confirmBtn != null) confirmBtn.IsEnabled = true; }
    }

    private void BtnCancelReset_Click(object s, RoutedEventArgs e)
    {
        PwdReset.Clear();
        PanelResetPwd.Visibility = Visibility.Collapsed;
    }

    private void BtnCreate_Click(object s, RoutedEventArgs e)
    {
        var username = TxtUsername.Text.Trim();
        var password = PwdNew.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("Username is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtUsername.Focus();
            return;
        }
        if (password.Length < 6)
        {
            MessageBox.Show("Password must be at least 6 characters.", "Validation",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            PwdNew.Focus();
            return;
        }

        BtnCreate.IsEnabled = false;
        try
        {
            _auth.CreateUser(username, password,
                (CboRole.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cashier",
                TxtFullName.Text.Trim());
            TxtUsername.Text = "";
            TxtFullName.Text = "";
            PwdNew.Clear();
            LoadData();
            MessageBox.Show("✅ User created.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Create user failed", ex);
            MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnCreate.IsEnabled = true; }
    }
}
