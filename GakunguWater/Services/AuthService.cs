using Dapper;
using GakunguWater.Data;
using GakunguWater.Models;

namespace GakunguWater.Services;

public class AuthService
{
    private readonly DatabaseService _db;
    public UserSession? CurrentUser { get; private set; }

    public AuthService(DatabaseService db)
    {
        _db = db;
    }

    public bool Login(string username, string password)
    {
        using var conn = _db.GetConnection();
        var user = conn.QueryFirstOrDefault<User>(
            "SELECT * FROM Users WHERE Username=@u AND IsActive=1",
            new { u = username });

        if (user == null) return false;
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return false;

        // Update last login
        conn.Execute("UPDATE Users SET LastLogin=datetime('now','localtime') WHERE Id=@id", new { id = user.Id });

        CurrentUser = new UserSession
        {
            Id = user.Id,
            Username = user.Username,
            Role = user.Role,
            FullName = user.FullName
        };
        return true;
    }

    public void Logout() => CurrentUser = null;

    public bool IsAdmin() => CurrentUser?.IsAdmin == true;

    public void RequireAdmin()
    {
        if (!IsAdmin()) throw new UnauthorizedAccessException("Admin access required.");
    }

    // ── User Management (admin only) ──────────────────────────
    public List<User> GetAllUsers()
    {
        using var conn = _db.GetConnection();
        return conn.Query<User>("SELECT * FROM Users ORDER BY Username").ToList();
    }

    public void CreateUser(string username, string password, string role, string? fullName)
    {
        using var conn = _db.GetConnection();
        conn.Execute("""
            INSERT INTO Users (Username, PasswordHash, Role, FullName)
            VALUES (@u, @h, @r, @fn)
            """,
            new { u = username, h = BCrypt.Net.BCrypt.HashPassword(password), r = role, fn = fullName });
    }

    public void ChangePassword(int userId, string newPassword)
    {
        using var conn = _db.GetConnection();
        conn.Execute("UPDATE Users SET PasswordHash=@h WHERE Id=@id",
            new { h = BCrypt.Net.BCrypt.HashPassword(newPassword), id = userId });
    }

    public void SetUserActive(int userId, bool active)
    {
        using var conn = _db.GetConnection();
        conn.Execute("UPDATE Users SET IsActive=@a WHERE Id=@id", new { a = active ? 1 : 0, id = userId });
    }
}
