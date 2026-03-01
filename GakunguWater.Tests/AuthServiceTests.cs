using GakunguWater.Services;
using GakunguWater.Tests.Helpers;
using Xunit;

namespace GakunguWater.Tests;

public class AuthServiceTests
{
    private static AuthService Setup() => new AuthService(TestDbFactory.Create());

    // ── Login ────────────────────────────────────────────────────
    [Fact]
    public void Login_ValidAdminCredentials_ReturnsTrue()
    {
        var svc = Setup();
        Assert.True(svc.Login("admin", "Admin@123"));
    }

    [Fact]
    public void Login_ValidCashierCredentials_ReturnsTrue()
    {
        var svc = Setup();
        Assert.True(svc.Login("cashier", "Cashier@123"));
    }

    [Fact]
    public void Login_WrongPassword_ReturnsFalse()
    {
        var svc = Setup();
        Assert.False(svc.Login("admin", "wrongPassword"));
    }

    [Fact]
    public void Login_UnknownUser_ReturnsFalse()
    {
        var svc = Setup();
        Assert.False(svc.Login("nobody", "anything"));
    }

    [Fact]
    public void Login_EmptyUsername_ReturnsFalse()
    {
        var svc = Setup();
        Assert.False(svc.Login("", "Admin@123"));
    }

    // ── CurrentUser after login ───────────────────────────────────
    [Fact]
    public void Login_SetsCurrentUser()
    {
        var svc = Setup();
        svc.Login("admin", "Admin@123");

        Assert.NotNull(svc.CurrentUser);
        Assert.Equal("admin", svc.CurrentUser!.Username);
    }

    [Fact]
    public void Login_SetsCorrectRole()
    {
        var svc = Setup();
        svc.Login("admin", "Admin@123");
        Assert.Equal("Admin", svc.CurrentUser!.Role);
    }

    // ── Logout ────────────────────────────────────────────────────
    [Fact]
    public void Logout_ClearsCurrentUser()
    {
        var svc = Setup();
        svc.Login("admin", "Admin@123");
        svc.Logout();

        Assert.Null(svc.CurrentUser);
    }

    // ── IsAdmin ───────────────────────────────────────────────────
    [Fact]
    public void IsAdmin_ReturnsTrueForAdminRole()
    {
        var svc = Setup();
        svc.Login("admin", "Admin@123");
        Assert.True(svc.IsAdmin());
    }

    [Fact]
    public void IsAdmin_ReturnsFalseForCashierRole()
    {
        var svc = Setup();
        svc.Login("cashier", "Cashier@123");
        Assert.False(svc.IsAdmin());
    }

    [Fact]
    public void IsAdmin_ReturnsFalseWhenNotLoggedIn()
    {
        var svc = Setup();
        Assert.False(svc.IsAdmin());
    }

    // ── RequireAdmin ──────────────────────────────────────────────
    [Fact]
    public void RequireAdmin_ThrowsWhenNotAdmin()
    {
        var svc = Setup();
        svc.Login("cashier", "Cashier@123");
        Assert.Throws<UnauthorizedAccessException>(() => svc.RequireAdmin());
    }

    [Fact]
    public void RequireAdmin_DoesNotThrowForAdmin()
    {
        var svc = Setup();
        svc.Login("admin", "Admin@123");
        // Should not throw
        svc.RequireAdmin();
    }

    // ── CreateUser / ChangePassword / SetActive ───────────────────
    [Fact]
    public void CreateUser_And_Login_RoundTrip()
    {
        var svc = Setup();
        svc.CreateUser("newuser", "NewPass@1", "Cashier", "New User");
        Assert.True(svc.Login("newuser", "NewPass@1"));
    }

    [Fact]
    public void ChangePassword_AllowsNewPassword()
    {
        var svc  = Setup();
        svc.Login("admin", "Admin@123");
        int uid = svc.CurrentUser!.Id;

        svc.ChangePassword(uid, "NewAdmin@456");
        svc.Logout();

        Assert.False(svc.Login("admin", "Admin@123"));   // old password fails
        Assert.True(svc.Login("admin",  "NewAdmin@456")); // new password works
    }

    [Fact]
    public void SetUserActive_False_BlocksLogin()
    {
        var svc  = Setup();
        svc.Login("cashier", "Cashier@123");
        int uid = svc.CurrentUser!.Id;
        svc.Logout();

        svc.SetUserActive(uid, false);

        Assert.False(svc.Login("cashier", "Cashier@123"));
    }

    [Fact]
    public void SetUserActive_True_RestoresLogin()
    {
        var svc  = Setup();
        svc.Login("cashier", "Cashier@123");
        int uid = svc.CurrentUser!.Id;
        svc.Logout();

        svc.SetUserActive(uid, false);
        svc.SetUserActive(uid, true);

        Assert.True(svc.Login("cashier", "Cashier@123"));
    }

    [Fact]
    public void GetAllUsers_ReturnsAtLeastTwoSeededUsers()
    {
        var svc = Setup();
        var users = svc.GetAllUsers();
        Assert.True(users.Count >= 2);
    }
}
