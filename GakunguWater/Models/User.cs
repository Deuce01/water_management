namespace GakunguWater.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "Cashier"; // Admin, Cashier
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLogin { get; set; }
}

public class UserSession
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Role { get; set; } = "";
    public string? FullName { get; set; }
    public bool IsAdmin => Role == "Admin";
}
