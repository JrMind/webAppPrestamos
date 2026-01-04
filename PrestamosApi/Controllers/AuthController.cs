using Microsoft.AspNetCore.Mvc;
using PrestamosApi.Models;
using PrestamosApi.Services;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var (usuario, token, error) = await _authService.LoginAsync(dto.Email, dto.Password);
        
        if (error != null)
        {
            return Unauthorized(new { message = error });
        }

        return Ok(new
        {
            token,
            usuario = new
            {
                usuario!.Id,
                usuario.Nombre,
                usuario.Email,
                Rol = usuario.Rol?.ToString() ?? "Pendiente",
                usuario.Telefono
            }
        });
    }

    /// <summary>
    /// Registro público - usuario queda pendiente de asignación de rol por admin
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        // Registro público: sin rol asignado (pendiente)
        var usuario = await _authService.RegisterAsync(dto.Nombre, dto.Email, dto.Password, dto.Telefono, null);
        
        if (usuario == null)
        {
            return BadRequest(new { message = "El email ya está registrado" });
        }

        return Ok(new
        {
            message = "Registro exitoso. Tu cuenta está pendiente de aprobación por un administrador.",
            usuario = new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Email,
                Rol = "Pendiente"
            }
        });
    }
}

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Telefono { get; set; }
}

