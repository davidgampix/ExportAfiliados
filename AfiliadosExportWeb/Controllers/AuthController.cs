using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AfiliadosExportWeb.Models;
using AfiliadosExportWeb.Services;

namespace AfiliadosExportWeb.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = _authService.ValidateUser(request);
            
            if (response == null)
            {
                return Unauthorized(new { message = "Usuario o contraseña incorrectos" });
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante el login");
            return StatusCode(500, new { message = "Error en el servidor" });
        }
    }

    [HttpGet("validate")]
    [Authorize]
    public IActionResult ValidateToken()
    {
        // Este endpoint se usa para verificar si el token es válido
        // Si llega aquí y tiene [Authorize], el token es válido
        return Ok(new { valid = true, username = User.Identity?.Name });
    }
}