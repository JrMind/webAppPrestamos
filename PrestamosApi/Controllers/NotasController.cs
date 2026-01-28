using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/prestamos/{prestamoId}/notas")]
[Authorize]
public class NotasController : BaseApiController
{
    private readonly PrestamosDbContext _context;

    public NotasController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetNotas(int prestamoId)
    {
        var notas = await _context.NotasPrestamo
            .Include(n => n.Usuario)
            .Where(n => n.PrestamoId == prestamoId)
            .OrderByDescending(n => n.FechaCreacion)
            .Select(n => new
            {
                n.Id,
                n.Contenido,
                n.FechaCreacion,
                UsuarioNombre = n.Usuario != null ? n.Usuario.Nombre : "Sistema",
                EsMio = n.UsuarioId == GetCurrentUserId()
            })
            .ToListAsync();

        return Ok(notas);
    }

    [HttpPost]
    public async Task<ActionResult<object>> CreateNota(int prestamoId, [FromBody] CreateNotaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Contenido))
            return BadRequest(new { message = "El contenido de la nota es requerido" });

        var prestamo = await _context.Prestamos.FindAsync(prestamoId);
        if (prestamo == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        var nota = new NotaPrestamo
        {
            PrestamoId = prestamoId,
            Contenido = dto.Contenido,
            UsuarioId = GetCurrentUserId(),
            FechaCreacion = DateTime.UtcNow
        };

        _context.NotasPrestamo.Add(nota);
        await _context.SaveChangesAsync();

        // Recargar con usuario
        var notaCreada = await _context.NotasPrestamo
            .Include(n => n.Usuario)
            .FirstOrDefaultAsync(n => n.Id == nota.Id);

        return CreatedAtAction(nameof(GetNotas), new { prestamoId }, new
        {
            nota.Id,
            nota.Contenido,
            nota.FechaCreacion,
            UsuarioNombre = notaCreada?.Usuario?.Nombre ?? "Sistema",
            EsMio = true
        });
    }
}

public class CreateNotaDto
{
    public string Contenido { get; set; } = string.Empty;
}
