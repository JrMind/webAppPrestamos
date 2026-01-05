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
public class PagosController : ControllerBase
{
    private readonly PrestamosDbContext _context;
    private readonly ITwilioService _twilioService;
    private readonly IDistribucionGananciasService _distribucionService;
    private readonly ILogger<PagosController> _logger;

    public PagosController(
        PrestamosDbContext context, 
        ITwilioService twilioService, 
        IDistribucionGananciasService distribucionService,
        ILogger<PagosController> logger)
    {
        _context = context;
        _twilioService = twilioService;
        _distribucionService = distribucionService;
        _logger = logger;
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
        // Validar préstamo existe - IMPORTANTE: incluir Cliente para SMS
        var prestamo = await _context.Prestamos
            .Include(p => p.Cliente)  // <-- CRÍTICO: para enviar SMS al cliente correcto
            .Include(p => p.Cuotas)
            .FirstOrDefaultAsync(p => p.Id == dto.PrestamoId);
            
        if (prestamo == null)
            return BadRequest(new { message = "Préstamo no encontrado" });

        // Convertir fecha a UTC para PostgreSQL
        var fechaPagoUtc = DateTime.SpecifyKind(dto.FechaPago, DateTimeKind.Utc);

        var pago = new Pago
        {
            PrestamoId = dto.PrestamoId,
            CuotaId = dto.CuotaId,
            MontoPago = dto.MontoPago,
            FechaPago = fechaPagoUtc,
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
                    cuota.FechaPago = fechaPagoUtc;
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

        // *** DISTRIBUIR GANANCIAS SEGÚN FUENTES DE CAPITAL ***
        try
        {
            await _distribucionService.DistribuirGananciasPago(prestamo.Id, dto.MontoPago);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error distribuyendo ganancias para préstamo {PrestamoId}", prestamo.Id);
        }

        // *** ENVIAR SMS AL CLIENTE ***
        // CRÍTICO: El teléfono es del CLIENTE del préstamo, NO del usuario logueado
        await EnviarSmsAlCliente(prestamo, dto.MontoPago);

        return CreatedAtAction(nameof(GetPagosByPrestamo), new { prestamoId = pago.PrestamoId },
            new PagoDto(pago.Id, pago.PrestamoId, pago.CuotaId, null, pago.MontoPago,
                pago.FechaPago, pago.MetodoPago, pago.Comprobante, pago.Observaciones));
    }

    /// <summary>
    /// Envia SMS al CLIENTE del préstamo con su balance actualizado
    /// </summary>
    private async Task EnviarSmsAlCliente(Prestamo prestamo, decimal montoPagado)
    {
        try
        {
            // Validar que el cliente tiene teléfono
            if (prestamo.Cliente == null || string.IsNullOrEmpty(prestamo.Cliente.Telefono))
            {
                _logger.LogWarning("Cliente {ClienteId} no tiene teléfono registrado, no se envía SMS", 
                    prestamo.ClienteId);
                return;
            }

            // Calcular balance del cliente
            var cuotasPagadas = prestamo.Cuotas.Count(c => c.EstadoCuota == "Pagada");
            var cuotasRestantes = prestamo.NumeroCuotas - cuotasPagadas;
            var saldoPendiente = prestamo.Cuotas.Sum(c => c.SaldoPendiente);

            var mensaje = $"📱 Hola {prestamo.Cliente.Nombre}\n" +
                $"✅ Pago recibido: ${montoPagado:N0}\n" +
                $"📊 Cuotas pagadas: {cuotasPagadas}/{prestamo.NumeroCuotas}\n" +
                $"📝 Cuotas restantes: {cuotasRestantes}\n" +
                $"💰 Saldo pendiente: ${saldoPendiente:N0}\n" +
                $"💵 Capital prestado: ${prestamo.MontoPrestado:N0}";

            _logger.LogInformation("Enviando SMS a cliente {ClienteNombre} ({Telefono})", 
                prestamo.Cliente.Nombre, prestamo.Cliente.Telefono);

            await _twilioService.SendSmsAsync(prestamo.Cliente.Telefono, mensaje);

            _logger.LogInformation("SMS enviado exitosamente a {Telefono}", prestamo.Cliente.Telefono);
        }
        catch (Exception ex)
        {
            // No fallar el pago si el SMS falla, solo loguear
            _logger.LogError(ex, "Error enviando SMS al cliente {ClienteId}", prestamo.ClienteId);
        }
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
                pago.Cuota.EstadoCuota = pago.Cuota.FechaCobro.Date < DateTime.UtcNow.Date ? "Vencida" : "Pendiente";
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
