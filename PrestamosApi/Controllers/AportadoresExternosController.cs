using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.DTOs;
using PrestamosApi.Models;
using PrestamosApi.Services;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AportadoresExternosController : ControllerBase
{
    private readonly PrestamosDbContext _context;
    private readonly ICierreMesService _cierreMesService;

    public AportadoresExternosController(PrestamosDbContext context, ICierreMesService cierreMesService)
    {
        _context = context;
        _cierreMesService = cierreMesService;
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
            MontoTotalAportado = dto.MontoTotalAportado,
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
        
        // Recalcular saldo pendiente si cambia el monto aportado
        if (aportador.MontoTotalAportado != dto.MontoTotalAportado)
        {
            aportador.MontoTotalAportado = dto.MontoTotalAportado;
            // Asumimos que el saldo pendiente es (MontoAportado - MontoPagado) + InteresesPendientes?
            // Por simplicidad y consistencia con el modelo actual: SaldoPendiente = MontoAportado - MontoPagado (Capital)
            // Nota: Esto no considera intereses acumulados si se manejan aparte. Revisar si SaldoPendiente incluye intereses.
            // Si SaldoPendiente es solo capital:
            aportador.SaldoPendiente = aportador.MontoTotalAportado - aportador.MontoPagado; 
        }

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

    [HttpPost("force-cierre-mes")]
    public async Task<IActionResult> ForceCierreMes()
    {
        try
        {
            // Por defecto, trata de cerrar el mes ANTERIOR
            var now = DateTime.UtcNow;
            var prevMonth = now.AddMonths(-1);
            
            var result = await _cierreMesService.EjecutarCierreMes(prevMonth.Month, prevMonth.Year, force: true);
            return Ok(new { message = result });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
