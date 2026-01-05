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
public class AportadoresExternosController : ControllerBase
{
    private readonly PrestamosDbContext _context;

    public AportadoresExternosController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AportadorExternoDto>>> GetAll()
    {
        var aportadores = await _context.AportadoresExternos
            .OrderBy(a => a.Nombre)
            .Select(a => new AportadorExternoDto(
                a.Id, a.Nombre, a.Telefono, a.Email,
                a.TasaInteres, a.DiasParaPago,
                a.MontoTotalAportado, a.MontoPagado, a.SaldoPendiente,
                a.Estado, a.FechaCreacion, a.Notas
            ))
            .ToListAsync();

        return Ok(aportadores);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AportadorExternoDto>> GetById(int id)
    {
        var a = await _context.AportadoresExternos.FindAsync(id);
        if (a == null) return NotFound();

        return Ok(new AportadorExternoDto(
            a.Id, a.Nombre, a.Telefono, a.Email,
            a.TasaInteres, a.DiasParaPago,
            a.MontoTotalAportado, a.MontoPagado, a.SaldoPendiente,
            a.Estado, a.FechaCreacion, a.Notas
        ));
    }

    [HttpPost]
    public async Task<ActionResult<AportadorExternoDto>> Create(CreateAportadorExternoDto dto)
    {
        var aportador = new AportadorExterno
        {
            Nombre = dto.Nombre,
            Telefono = dto.Telefono,
            Email = dto.Email,
            TasaInteres = dto.TasaInteres,
            DiasParaPago = dto.DiasParaPago,
            Notas = dto.Notas,
            FechaCreacion = DateTime.UtcNow
        };

        _context.AportadoresExternos.Add(aportador);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = aportador.Id },
            new AportadorExternoDto(
                aportador.Id, aportador.Nombre, aportador.Telefono, aportador.Email,
                aportador.TasaInteres, aportador.DiasParaPago,
                aportador.MontoTotalAportado, aportador.MontoPagado, aportador.SaldoPendiente,
                aportador.Estado, aportador.FechaCreacion, aportador.Notas
            ));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateAportadorExternoDto dto)
    {
        var aportador = await _context.AportadoresExternos.FindAsync(id);
        if (aportador == null) return NotFound();

        aportador.Nombre = dto.Nombre;
        aportador.Telefono = dto.Telefono;
        aportador.Email = dto.Email;
        aportador.TasaInteres = dto.TasaInteres;
        aportador.DiasParaPago = dto.DiasParaPago;
        aportador.Estado = dto.Estado;
        aportador.Notas = dto.Notas;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var aportador = await _context.AportadoresExternos.FindAsync(id);
        if (aportador == null) return NotFound();

        // Solo permitir eliminar si no tiene préstamos asociados
        var tienePrestamos = await _context.FuentesCapitalPrestamo
            .AnyAsync(f => f.AportadorExternoId == id);
        
        if (tienePrestamos)
            return BadRequest(new { message = "No se puede eliminar un aportador con préstamos asociados" });

        _context.AportadoresExternos.Remove(aportador);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
