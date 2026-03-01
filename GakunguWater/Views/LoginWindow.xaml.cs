using GakunguWater.Services;
using System.Windows;
using System.Windows.Input;

namespace GakunguWater.Views;

public partial class LoginWindow : Window
{
    private readonly AuthService _auth;

    public LoginWindow()
    {
        InitializeComponent();
        _auth = App.Resolve<AuthService>();
        TxtUsername.Focus();
    }

    private void BtnLogin_Click(object sender, RoutedEventArgs e) => TryLogin();
    private void TxtUsername_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Return) PwdPassword.Focus(); }
    private void PwdPassword_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Return) TryLogin(); }

    private void TryLogin()
    {
        TxtError.Visibility = Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(TxtUsername.Text) || PwdPassword.SecurePassword.Length == 0)
        {
            ShowError("Please enter username and password.");
            return;
        }

        BtnLogin.IsEnabled = false;
        try
        {
            if (_auth.Login(TxtUsername.Text.Trim(), PwdPassword.Password))
            {
                App.CurrentUser = _auth.CurrentUser;
                var main = new MainWindow();
                main.Show();
                Close();
            }
            else
            {
                ShowError("Invalid username or password.");
                PwdPassword.Clear();
                PwdPassword.Focus();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Login failed unexpectedly", ex);
            ShowError($"Login error: {ex.Message}");
            PwdPassword.Clear();
        }
        finally { BtnLogin.IsEnabled = true; }
    }

    private void ShowError(string msg)
    {
        TxtError.Text = msg;
        TxtError.Visibility = Visibility.Visible;
    }
}
