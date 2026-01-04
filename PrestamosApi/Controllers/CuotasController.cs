using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.DTOs;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CuotasController : ControllerBase
{
    private readonly PrestamosDbContext _context;

    public CuotasController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet("prestamo/{prestamoId}")]
    public async Task<ActionResult<IEnumerable<CuotaDto>>> GetCuotasByPrestamo(int prestamoId)
    {
        var cuotas = await _context.CuotasPrestamo
            .Where(c => c.PrestamoId == prestamoId)
            .OrderBy(c => c.NumeroCuota)
            .Select(c => new CuotaDto(
                c.Id,
                c.PrestamoId,
                c.NumeroCuota,
                c.FechaCobro,
                c.MontoCuota,
                c.MontoPagado,
                c.SaldoPendiente,
                c.EstadoCuota,
                c.FechaPago,
                c.Observaciones,
                false
            ))
            .ToListAsync();

        return Ok(cuotas);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCuota(int id, [FromBody] UpdateCuotaFechaDto dto)
    {
        var cuota = await _context.CuotasPrestamo.FindAsync(id);
        if (cuota == null)
            return NotFound(new { message = "Cuota no encontrada" });

        cuota.FechaCobro = dto.FechaCobro;

        // Actualizar estado si la nueva fecha ya pasó
        if (dto.FechaCobro.Date < DateTime.Today && cuota.SaldoPendiente > 0)
        {
            cuota.EstadoCuota = "Vencida";
        }
        else if (cuota.SaldoPendiente > 0 && cuota.MontoPagado == 0)
        {
            cuota.EstadoCuota = "Pendiente";
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id}/fecha")]
    public async Task<IActionResult> UpdateCuotaFecha(int id, [FromBody] UpdateCuotaFechaDto dto)
    {
        return await UpdateCuota(id, dto);
    }

    [HttpGet("proximas-vencer")]
    public async Task<ActionResult<IEnumerable<CuotaProximaDetalleDto>>> GetCuotasProximasVencer([FromQuery] int dias = 7)
    {
        var fechaLimite = DateTime.Today.AddDays(dias);
        
        var cuotas = await _context.CuotasPrestamo
            .Include(c => c.Prestamo)
            .ThenInclude(p => p!.Cliente)
            .Where(c => c.FechaCobro <= fechaLimite && 
                       (c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida"))
            .OrderBy(c => c.FechaCobro)
            .Select(c => new CuotaProximaDetalleDto(
                c.Id,
                c.PrestamoId,
                c.Prestamo!.Cliente!.Nombre,
                c.FechaCobro,
                c.SaldoPendiente,
                c.EstadoCuota,
                (int)(c.FechaCobro.Date - DateTime.Today).TotalDays
            ))
            .ToListAsync();

        return Ok(cuotas);
    }

    [HttpPost("actualizar-vencidas")]
    public async Task<IActionResult> ActualizarCuotasVencidas()
    {
        var cuotasVencidas = await _context.CuotasPrestamo
            .Where(c => c.FechaCobro.Date < DateTime.Today && 
                       c.SaldoPendiente > 0 && 
                       c.EstadoCuota != "Vencida")
            .ToListAsync();

        foreach (var cuota in cuotasVencidas)
        {
            cuota.EstadoCuota = "Vencida";
        }

        // Actualizar estado de préstamos con cuotas vencidas
        var prestamosConVencidas = await _context.Prestamos
            .Include(p => p.Cuotas)
            .Where(p => p.EstadoPrestamo == "Activo" && 
                       p.Cuotas.Any(c => c.EstadoCuota == "Vencida"))
            .ToListAsync();

        foreach (var prestamo in prestamosConVencidas)
        {
            prestamo.EstadoPrestamo = "Vencido";
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = $"Se actualizaron {cuotasVencidas.Count} cuotas vencidas" });
    }
}
