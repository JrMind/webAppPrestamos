using Microsoft.AspNetCore.Mvc;
using PrestamosApi.Models;
using PrestamosApi.Services;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("[controller]")]
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
        var (usuario, token) = await _authService.LoginAsync(dto.Email, dto.Password);
        if (usuario == null || token == null)
        {
            return Unauthorized(new { message = "Email o contraseña incorrectos" });
        }

        return Ok(new
        {
            token,
            usuario = new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Email,
                Rol = usuario.Rol.ToString(),
                usuario.Telefono
            }
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!Enum.TryParse<RolUsuario>(dto.Rol, out var rol))
        {
            return BadRequest(new { message = "Rol inválido" });
        }

        var usuario = await _authService.RegisterAsync(dto.Nombre, dto.Email, dto.Password, dto.Telefono, rol);
        if (usuario == null)
        {
            return BadRequest(new { message = "El email ya está registrado" });
        }

        return Ok(new
        {
            message = "Usuario registrado exitosamente",
            usuario = new
            {
                usuario.Id,
                usuario.Nombre,
                usuario.Email,
                Rol = usuario.Rol.ToString()
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
    public string Rol { get; set; } = "Socio";
}
