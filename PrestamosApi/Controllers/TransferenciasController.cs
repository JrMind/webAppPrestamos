using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.DTOs;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransferenciasController : ControllerBase
{
    private readonly PrestamosDbContext _context;

    public TransferenciasController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransferenciaDto>>> GetAll()
    {
        var items = await _context.Transferencias
            .OrderByDescending(t => t.Fecha)
            .Select(t => new TransferenciaDto(t.Id, t.Origen, t.Destino, t.Monto, t.Fecha, t.Descripcion))
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<TransferenciaDto>> Create([FromBody] CreateTransferenciaDto dto)
    {
        if (dto.Monto <= 0)
            return BadRequest(new { message = "El monto debe ser mayor a 0" });
        if (dto.Origen == dto.Destino)
            return BadRequest(new { message = "El origen y destino no pueden ser iguales" });
        var mediosValidos = new[] { "Nequi", "Efectivo" };
        if (!mediosValidos.Contains(dto.Origen) || !mediosValidos.Contains(dto.Destino))
            return BadRequest(new { message = "Medio inválido. Use Nequi o Efectivo" });

        var t = new Transferencia
        {
            Origen = dto.Origen,
            Destino = dto.Destino,
            Monto = dto.Monto,
            Fecha = dto.Fecha.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dto.Fecha, DateTimeKind.Utc)
                : dto.Fecha.ToUniversalTime(),
            Descripcion = dto.Descripcion
        };

        _context.Transferencias.Add(t);
        await _context.SaveChangesAsync();

        return Ok(new TransferenciaDto(t.Id, t.Origen, t.Destino, t.Monto, t.Fecha, t.Descripcion));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var t = await _context.Transferencias.FindAsync(id);
        if (t == null) return NotFound();
        _context.Transferencias.Remove(t);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
