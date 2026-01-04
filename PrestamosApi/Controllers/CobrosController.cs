using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class CobrosController : ControllerBase
{
    private readonly PrestamosDbContext _context;

    public CobrosController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet("hoy")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> GetCobrosHoy()
    {
        var today = DateTime.UtcNow.Date;

        // Cuotas de hoy
        var cuotasHoy = await _context.CuotasPrestamo
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cliente)
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cobrador)
            .Where(c => c.FechaCobro.Date == today && c.EstadoCuota != "Pagada")
            .OrderBy(c => c.Cobrado)
            .ThenBy(c => c.Prestamo!.Cliente!.Nombre)
            .Select(c => new
            {
                c.Id,
                c.PrestamoId,
                c.NumeroCuota,
                c.FechaCobro,
                c.MontoCuota,
                c.MontoPagado,
                c.SaldoPendiente,
                c.EstadoCuota,
                c.Cobrado,
                ClienteNombre = c.Prestamo!.Cliente!.Nombre,
                ClienteTelefono = c.Prestamo.Cliente.Telefono,
                CobradorNombre = c.Prestamo.Cobrador != null ? c.Prestamo.Cobrador.Nombre : null,
                Vencido = false
            })
            .ToListAsync();

        // Cuotas vencidas (días anteriores no pagadas)
        var cuotasVencidas = await _context.CuotasPrestamo
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cliente)
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cobrador)
            .Where(c => c.FechaCobro.Date < today && c.EstadoCuota != "Pagada" && !c.Cobrado)
            .OrderBy(c => c.FechaCobro)
            .ThenBy(c => c.Prestamo!.Cliente!.Nombre)
            .Select(c => new
            {
                c.Id,
                c.PrestamoId,
                c.NumeroCuota,
                c.FechaCobro,
                c.MontoCuota,
                c.MontoPagado,
                c.SaldoPendiente,
                c.EstadoCuota,
                c.Cobrado,
                ClienteNombre = c.Prestamo!.Cliente!.Nombre,
                ClienteTelefono = c.Prestamo.Cliente.Telefono,
                CobradorNombre = c.Prestamo.Cobrador != null ? c.Prestamo.Cobrador.Nombre : null,
                Vencido = true
            })
            .ToListAsync();

        var montoTotalHoy = cuotasHoy.Sum(c => c.SaldoPendiente);
        var montoTotalVencido = cuotasVencidas.Sum(c => c.SaldoPendiente);

        return Ok(new
        {
            fecha = today,
            cuotasHoy,
            cuotasVencidas,
            resumen = new
            {
                totalCuotasHoy = cuotasHoy.Count,
                totalCuotasVencidas = cuotasVencidas.Count,
                montoTotalHoy,
                montoTotalVencido,
                montoPendienteTotal = montoTotalHoy + montoTotalVencido
            }
        });
    }

    [HttpPut("{cuotaId}/marcar")]
    public async Task<IActionResult> MarcarCobrado(int cuotaId, [FromBody] MarcarCobradoDto dto)
    {
        var cuota = await _context.CuotasPrestamo.FindAsync(cuotaId);
        if (cuota == null)
        {
            return NotFound();
        }

        cuota.Cobrado = dto.Cobrado;
        
        // Si se marca como cobrado y no tiene fecha de pago, actualizar
        if (dto.Cobrado && !cuota.FechaPago.HasValue)
        {
            cuota.FechaPago = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = dto.Cobrado ? "Cuota marcada como cobrada" : "Cuota desmarcada" });
    }

    [HttpGet("resumen-diario")]
    public async Task<ActionResult<object>> GetResumenDiario([FromQuery] DateTime? fecha)
    {
        var targetDate = fecha?.Date ?? DateTime.UtcNow.Date;

        var cuotasCobradas = await _context.CuotasPrestamo
            .Where(c => c.FechaPago.HasValue && c.FechaPago.Value.Date == targetDate && c.Cobrado)
            .SumAsync(c => c.MontoPagado);

        var cuotasPorCobrar = await _context.CuotasPrestamo
            .Where(c => c.FechaCobro.Date == targetDate && c.EstadoCuota != "Pagada")
            .SumAsync(c => c.SaldoPendiente);

        var cuotasVencidas = await _context.CuotasPrestamo
            .Where(c => c.FechaCobro.Date < targetDate && c.EstadoCuota != "Pagada" && !c.Cobrado)
            .SumAsync(c => c.SaldoPendiente);

        return Ok(new
        {
            fecha = targetDate,
            montoCobrado = cuotasCobradas,
            montoPorCobrar = cuotasPorCobrar,
            montoVencido = cuotasVencidas
        });
    }
}

public class MarcarCobradoDto
{
    public bool Cobrado { get; set; }
}
