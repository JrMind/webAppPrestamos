using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;
using PrestamosApi.Services;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : BaseApiController
{
    private readonly PrestamosDbContext _context;
    private readonly IAuthService _authService;

    public UsuariosController(PrestamosDbContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetUsuarios()
    {
        var usuarios = await _context.Usuarios
            .Where(u => u.Activo)
            .Select(u => new
            {
                u.Id,
                u.Nombre,
                u.Email,
                u.Telefono,
                Rol = u.Rol.ToString(),
                u.PorcentajeParticipacion,
                u.TasaInteresMensual,
                u.Activo
            })
            .ToListAsync();

        return Ok(usuarios);
    }

    [HttpGet("cobradores")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<object>>> GetCobradores()
    {
        var cobradores = await _context.Usuarios
            .Where(u => u.Activo && u.Rol == RolUsuario.Cobrador)
            .Select(u => new
            {
                u.Id,
                u.Nombre,
                u.Telefono
            })
            .ToListAsync();

        return Ok(cobradores);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetUsuario(int id)
    {
        var usuario = await _context.Usuarios
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.Nombre,
                u.Email,
                u.Telefono,
                Rol = u.Rol.ToString(),
                u.PorcentajeParticipacion,
                u.TasaInteresMensual,
                u.Activo
            })
            .FirstOrDefaultAsync();

        if (usuario == null)
        {
            return NotFound();
        }

        return Ok(usuario);
    }

    [HttpPost]
    public async Task<ActionResult<object>> CreateUsuario([FromBody] CreateUsuarioDto dto)
    {
        if (!Enum.TryParse<RolUsuario>(dto.Rol, out var rol))
        {
            return BadRequest(new { message = "Rol inválido" });
        }

        if (await _context.Usuarios.AnyAsync(u => u.Email == dto.Email))
        {
            return BadRequest(new { message = "El email ya está registrado" });
        }

        var usuario = new Usuario
        {
            Nombre = dto.Nombre,
            Email = dto.Email,
            PasswordHash = _authService.HashPassword(dto.Password),
            Telefono = dto.Telefono,
            Rol = rol,
            PorcentajeParticipacion = dto.PorcentajeParticipacion,
            TasaInteresMensual = dto.TasaInteresMensual,
            Activo = true
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUsuario), new { id = usuario.Id }, new
        {
            usuario.Id,
            usuario.Nombre,
            usuario.Email,
            Rol = usuario.Rol?.ToString() ?? "Pendiente"
        });
    }

    /// <summary>
    /// Obtener usuarios pendientes de asignación de rol (solo para admin)
    /// </summary>
    [HttpGet("pendientes")]
    public async Task<ActionResult<IEnumerable<object>>> GetUsuariosPendientes()
    {
        var currentRole = GetCurrentUserRole();
        if (currentRole != RolUsuario.Admin)
        {
            return Forbid();
        }

        var pendientes = await _context.Usuarios
            .Where(u => u.Rol == null && u.Activo)
            .Select(u => new
            {
                u.Id,
                u.Nombre,
                u.Email,
                u.Telefono,
                Rol = "Pendiente"
            })
            .ToListAsync();

        return Ok(pendientes);
    }

    /// <summary>
    /// Asignar rol a un usuario (solo admin)
    /// </summary>
    [HttpPut("{id}/asignar-rol")]
    public async Task<IActionResult> AsignarRol(int id, [FromBody] AsignarRolDto dto)
    {
        var currentRole = GetCurrentUserRole();
        if (currentRole != RolUsuario.Admin)
        {
            return Forbid();
        }

        if (!Enum.TryParse<RolUsuario>(dto.Rol, out var rol))
        {
            return BadRequest(new { message = "Rol inválido" });
        }

        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
        {
            return NotFound(new { message = "Usuario no encontrado" });
        }

        usuario.Rol = rol;
        usuario.PorcentajeParticipacion = dto.PorcentajeParticipacion ?? 0;
        usuario.TasaInteresMensual = dto.TasaInteresMensual ?? 3;

        await _context.SaveChangesAsync();

        return Ok(new { 
            message = $"Rol '{rol}' asignado exitosamente a {usuario.Nombre}",
            usuario = new { usuario.Id, usuario.Nombre, usuario.Email, Rol = rol.ToString() }
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUsuario(int id, [FromBody] UpdateUsuarioDto dto)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
        {
            return NotFound();
        }

        if (dto.Nombre != null) usuario.Nombre = dto.Nombre;
        if (dto.Telefono != null) usuario.Telefono = dto.Telefono;
        if (dto.PorcentajeParticipacion.HasValue) usuario.PorcentajeParticipacion = dto.PorcentajeParticipacion.Value;
        if (dto.TasaInteresMensual.HasValue) usuario.TasaInteresMensual = dto.TasaInteresMensual.Value;
        if (dto.Activo.HasValue) usuario.Activo = dto.Activo.Value;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUsuario(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null)
        {
            return NotFound();
        }

        usuario.Activo = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public class CreateUsuarioDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Rol { get; set; }
    public decimal PorcentajeParticipacion { get; set; }
    public decimal TasaInteresMensual { get; set; } = 3;
}

public class UpdateUsuarioDto
{
    public string? Nombre { get; set; }
    public string? Telefono { get; set; }
    public decimal? PorcentajeParticipacion { get; set; }
    public decimal? TasaInteresMensual { get; set; }
    public bool? Activo { get; set; }
}

public class AsignarRolDto
{
    public string Rol { get; set; } = string.Empty;
    public decimal? PorcentajeParticipacion { get; set; }
    public decimal? TasaInteresMensual { get; set; }
}

