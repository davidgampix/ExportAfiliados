using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AfiliadosExportWeb.Models;

namespace AfiliadosExportWeb.Services;

public interface IAuthService
{
    LoginResponse? ValidateUser(LoginRequest request);
}

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly AuthSettings _authSettings;

    public AuthService(IConfiguration configuration, ILogger<AuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _authSettings = configuration.GetSection("AuthSettings").Get<AuthSettings>() ?? new AuthSettings();
        
        // Si no hay configuración, usar valores por defecto
        if (string.IsNullOrEmpty(_authSettings.Username))
        {
            _authSettings.Username = "soporte";
            _authSettings.Password = "Export2024!";
            _authSettings.JwtSecret = "AfiliadosExportSecretKey2024_MustBeAtLeast32Characters!";
            _authSettings.ExpirationDays = 30;
        }
    }

    public LoginResponse? ValidateUser(LoginRequest request)
    {
        // Buscar usuario en la lista de usuarios configurados
        UserCredentials? user = null;

        // Primero buscar en la lista de usuarios (nueva forma)
        if (_authSettings.Users?.Count > 0)
        {
            user = _authSettings.Users.FirstOrDefault(u =>
                u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase) &&
                u.Password == request.Password);
        }

        // Si no se encontró, verificar el usuario legacy (compatibilidad hacia atrás)
        if (user == null &&
            !string.IsNullOrEmpty(_authSettings.Username) &&
            request.Username.Equals(_authSettings.Username, StringComparison.OrdinalIgnoreCase) &&
            request.Password == _authSettings.Password)
        {
            user = new UserCredentials
            {
                Username = _authSettings.Username,
                Password = _authSettings.Password,
                Role = "Admin",
                DisplayName = _authSettings.Username
            };
        }

        if (user == null)
        {
            _logger.LogWarning($"Intento de login fallido para usuario: {request.Username}");
            return null;
        }

        // Generar token JWT
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_authSettings.JwtSecret);
        var expirationDate = DateTime.UtcNow.AddDays(_authSettings.ExpirationDays);

        var displayName = !string.IsNullOrEmpty(user.DisplayName) ? user.DisplayName : user.Username;

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("DisplayName", displayName),
                new Claim("LoginTime", DateTime.UtcNow.ToString())
            }),
            Expires = expirationDate,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        _logger.LogInformation($"Login exitoso para usuario: {user.Username} ({displayName})");

        return new LoginResponse
        {
            Token = tokenString,
            Username = displayName,
            ExpiresAt = expirationDate
        };
    }
}