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
public class GastosController : BaseApiController
{
    private readonly PrestamosDbContext _context;

    public GastosController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<GastoDto>>> GetAll()
    {
        var gastos = await _context.Gastos
            .Include(g => g.Usuario)
            .OrderByDescending(g => g.Fecha)
            .Select(g => new GastoDto(g.Id, g.Descripcion, g.Monto, g.MedioPago, g.Fecha, g.Categoria, g.Usuario != null ? g.Usuario.Nombre : null))
            .ToListAsync();
        return Ok(gastos);
    }

    [HttpPost]
    public async Task<ActionResult<GastoDto>> Create([FromBody] CreateGastoDto dto)
    {
        if (dto.Monto <= 0)
            return BadRequest(new { message = "El monto debe ser mayor a 0" });
        if (dto.MedioPago != "Nequi" && dto.MedioPago != "Efectivo")
            return BadRequest(new { message = "El medio de pago debe ser Nequi o Efectivo" });

        var usuarioId = GetCurrentUserId();

        var gasto = new Gasto
        {
            Descripcion = dto.Descripcion,
            Monto = dto.Monto,
            MedioPago = dto.MedioPago,
            Fecha = dto.Fecha.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dto.Fecha, DateTimeKind.Utc)
                : dto.Fecha.ToUniversalTime(),
            Categoria = dto.Categoria,
            UsuarioId = usuarioId
        };

        _context.Gastos.Add(gasto);
        await _context.SaveChangesAsync();

        var nombreUsuario = usuarioId.HasValue
            ? (await _context.Usuarios.FindAsync(usuarioId.Value))?.Nombre
            : null;

        return Ok(new GastoDto(gasto.Id, gasto.Descripcion, gasto.Monto, gasto.MedioPago, gasto.Fecha, gasto.Categoria, nombreUsuario));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var gasto = await _context.Gastos.FindAsync(id);
        if (gasto == null) return NotFound();

        _context.Gastos.Remove(gasto);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
