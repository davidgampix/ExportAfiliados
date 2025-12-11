namespace AfiliadosExportWeb.Models;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class UserCredentials
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public string DisplayName { get; set; } = string.Empty;
}

public class AuthSettings
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string JwtSecret { get; set; } = string.Empty;
    public int ExpirationDays { get; set; } = 30;
    public List<UserCredentials> Users { get; set; } = new();
}