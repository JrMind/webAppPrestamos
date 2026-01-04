using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.DTOs;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PagosController : ControllerBase
{
    private readonly PrestamosDbContext _context;

    public PagosController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet("prestamo/{prestamoId}")]
    public async Task<ActionResult<IEnumerable<PagoDto>>> GetPagosByPrestamo(int prestamoId)
    {
        var pagos = await _context.Pagos
            .Include(p => p.Cuota)
            .Where(p => p.PrestamoId == prestamoId)
            .OrderByDescending(p => p.FechaPago)
            .Select(p => new PagoDto(
                p.Id,
                p.PrestamoId,
                p.CuotaId,
                p.Cuota != null ? p.Cuota.NumeroCuota : null,
                p.MontoPago,
                p.FechaPago,
                p.MetodoPago,
                p.Comprobante,
                p.Observaciones
            ))
            .ToListAsync();

        return Ok(pagos);
    }

    [HttpPost]
    public async Task<ActionResult<PagoDto>> CreatePago(CreatePagoDto dto)
    {
        // Validar préstamo existe
        var prestamo = await _context.Prestamos
            .Include(p => p.Cuotas)
            .FirstOrDefaultAsync(p => p.Id == dto.PrestamoId);
            
        if (prestamo == null)
            return BadRequest(new { message = "Préstamo no encontrado" });

        var pago = new Pago
        {
            PrestamoId = dto.PrestamoId,
            CuotaId = dto.CuotaId,
            MontoPago = dto.MontoPago,
            FechaPago = dto.FechaPago,
            MetodoPago = dto.MetodoPago,
            Comprobante = dto.Comprobante,
            Observaciones = dto.Observaciones
        };

        _context.Pagos.Add(pago);

        // Si el pago está asociado a una cuota, actualizar la cuota
        if (dto.CuotaId.HasValue)
        {
            var cuota = await _context.CuotasPrestamo.FindAsync(dto.CuotaId.Value);
            if (cuota != null)
            {
                cuota.MontoPagado += dto.MontoPago;
                cuota.SaldoPendiente = cuota.MontoCuota - cuota.MontoPagado;

                if (cuota.SaldoPendiente <= 0)
                {
                    cuota.SaldoPendiente = 0;
                    cuota.EstadoCuota = "Pagada";
                    cuota.FechaPago = dto.FechaPago;
                }
                else if (cuota.MontoPagado > 0)
                {
                    cuota.EstadoCuota = "Parcial";
                }
            }
        }

        // Verificar si todas las cuotas están pagadas
        var todasPagadas = prestamo.Cuotas.All(c => 
            c.Id == dto.CuotaId ? (c.MontoCuota - c.MontoPagado - dto.MontoPago <= 0) : c.EstadoCuota == "Pagada");
        
        if (todasPagadas)
        {
            prestamo.EstadoPrestamo = "Pagado";
        }

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPagosByPrestamo), new { prestamoId = pago.PrestamoId },
            new PagoDto(pago.Id, pago.PrestamoId, pago.CuotaId, null, pago.MontoPago,
                pago.FechaPago, pago.MetodoPago, pago.Comprobante, pago.Observaciones));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePago(int id)
    {
        var pago = await _context.Pagos
            .Include(p => p.Cuota)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pago == null)
            return NotFound(new { message = "Pago no encontrado" });

        // Revertir el pago en la cuota si aplica
        if (pago.CuotaId.HasValue && pago.Cuota != null)
        {
            pago.Cuota.MontoPagado -= pago.MontoPago;
            pago.Cuota.SaldoPendiente = pago.Cuota.MontoCuota - pago.Cuota.MontoPagado;

            if (pago.Cuota.MontoPagado <= 0)
            {
                pago.Cuota.MontoPagado = 0;
                pago.Cuota.SaldoPendiente = pago.Cuota.MontoCuota;
                pago.Cuota.EstadoCuota = pago.Cuota.FechaCobro.Date < DateTime.Today ? "Vencida" : "Pendiente";
                pago.Cuota.FechaPago = null;
            }
            else
            {
                pago.Cuota.EstadoCuota = "Parcial";
            }
        }

        // Actualizar estado del préstamo si estaba pagado
        var prestamo = await _context.Prestamos.FindAsync(pago.PrestamoId);
        if (prestamo != null && prestamo.EstadoPrestamo == "Pagado")
        {
            prestamo.EstadoPrestamo = "Activo";
        }

        _context.Pagos.Remove(pago);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
