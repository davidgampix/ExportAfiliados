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
        // Validar credenciales
        if (request.Username != _authSettings.Username || request.Password != _authSettings.Password)
        {
            _logger.LogWarning($"Intento de login fallido para usuario: {request.Username}");
            return null;
        }

        // Generar token JWT
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_authSettings.JwtSecret);
        var expirationDate = DateTime.UtcNow.AddDays(_authSettings.ExpirationDays);
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, request.Username),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("LoginTime", DateTime.UtcNow.ToString())
            }),
            Expires = expirationDate,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), 
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        _logger.LogInformation($"Login exitoso para usuario: {request.Username}");

        return new LoginResponse
        {
            Token = tokenString,
            Username = request.Username,
            ExpiresAt = expirationDate
        };
    }
}