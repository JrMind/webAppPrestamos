using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CobrosController : BaseApiController
{
    private readonly PrestamosDbContext _context;
    private readonly Services.ITwilioService _twilioService;

    public CobrosController(PrestamosDbContext context, Services.ITwilioService twilioService)
    {
        _context = context;
        _twilioService = twilioService;
    }

    [HttpGet("hoy")]
    public async Task<ActionResult<object>> GetCobrosHoy()
    {
        var today = DateTime.UtcNow.Date;
        var userId = GetCurrentUserId();
        var isCobrador = IsCobrador();

        // Base query
        var baseQuery = _context.CuotasPrestamo
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cliente)
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cobrador)
            .AsQueryable();

        // Si es cobrador, filtrar solo sus préstamos asignados
        if (isCobrador && userId.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.Prestamo!.CobradorId == userId.Value);
        }

        // Cuotas de hoy
        var cuotasHoy = await baseQuery
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
        var cuotasVencidas = await baseQuery
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
        var cuota = await _context.CuotasPrestamo
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cliente)
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cuotas)
            .FirstOrDefaultAsync(c => c.Id == cuotaId);

        if (cuota == null)
        {
            return NotFound();
        }

        cuota.Cobrado = dto.Cobrado;
        
        // Si se marca como cobrado y no tiene fecha de pago, actualizar
        if (dto.Cobrado && !cuota.FechaPago.HasValue)
        {
            cuota.FechaPago = DateTime.UtcNow;
            cuota.MontoPagado = cuota.MontoCuota; // Asumir pago completo por defecto al marcar
            cuota.EstadoCuota = "Pagada";
        }
        else if (!dto.Cobrado)
        {
            cuota.FechaPago = null;
            cuota.MontoPagado = 0;
            cuota.EstadoCuota = "Pendiente";
        }

        await _context.SaveChangesAsync();

        // Enviar SMS si es marcado como cobrado
        if (dto.Cobrado && cuota.Prestamo?.Cliente?.Telefono != null)
        {
            try 
            {
                var campaign = await _context.SmsCampaigns
                    .FirstOrDefaultAsync(c => c.Activo && c.TipoDestinatario == TipoDestinatarioSms.ConfirmacionPago);

                if (campaign != null)
                {
                    var prestamo = cuota.Prestamo;
                    var cuotasTotales = prestamo.Cuotas.Count;
                    var cuotasPagadas = prestamo.Cuotas.Count(c => c.Cobrado);
                    var cuotasRestantes = cuotasTotales - cuotasPagadas;
                    
                    var proximaCuotaEntity = prestamo.Cuotas
                        .Where(c => !c.Cobrado && c.Id != cuota.Id)
                        .OrderBy(c => c.FechaCobro)
                        .FirstOrDefault();
                    
                    var saldoPendiente = prestamo.Cuotas.Where(c => !c.Cobrado).Sum(c => c.SaldoPendiente);

                    var mensaje = campaign.Mensaje
                        .Replace("{cliente}", prestamo.Cliente.Nombre)
                        .Replace("{monto}", cuota.MontoPagado.ToString("N0"))
                        .Replace("{cuotasPagadas}", cuotasPagadas.ToString())
                        .Replace("{cuotasRestantes}", cuotasRestantes.ToString())
                        .Replace("{proximaCuota}", proximaCuotaEntity?.MontoCuota.ToString("N0") ?? "0")
                        .Replace("{fechaProxima}", proximaCuotaEntity?.FechaCobro.ToString("dd/MM/yyyy") ?? "-")
                        .Replace("{saldoPendiente}", saldoPendiente.ToString("N0"));

                    var sent = await _twilioService.SendSmsAsync(prestamo.Cliente.Telefono, mensaje);

                    var history = new SmsHistory
                    {
                        SmsCampaignId = campaign.Id,
                        ClienteId = prestamo.ClienteId,
                        NumeroTelefono = prestamo.Cliente.Telefono,
                        Mensaje = mensaje,
                        FechaEnvio = DateTime.UtcNow,
                        Estado = sent ? EstadoSms.Enviado : EstadoSms.Fallido,
                        TwilioSid = sent ? "SentAPI" : null
                    };

                    _context.SmsHistories.Add(history);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the request
                Console.WriteLine($"Error sending SMS: {ex.Message}");
            }
        }

        return Ok(new { message = dto.Cobrado ? "Cuota marcada como cobrada" : "Cuota desmarcada" });
    }

    [HttpGet("resumen-diario")]
    public async Task<ActionResult<object>> GetResumenDiario([FromQuery] DateTime? fecha)
    {
        var targetDate = fecha.HasValue 
            ? DateTime.SpecifyKind(fecha.Value.Date, DateTimeKind.Utc) 
            : DateTime.UtcNow.Date;

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

    [HttpGet("mes")]
    public async Task<ActionResult<object>> GetCobrosDelMes()
    {
        var today = DateTime.UtcNow.Date;
        var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
        var userId = GetCurrentUserId();
        var isCobrador = IsCobrador();

        // Base query
        var baseQuery = _context.CuotasPrestamo
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cliente)
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cobrador)
            .Where(c => c.EstadoCuota != "Pagada")
            .AsQueryable();

        // Si es cobrador, filtrar solo sus préstamos asignados
        if (isCobrador && userId.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.Prestamo!.CobradorId == userId.Value);
        }

        // Cuotas de hoy
        var cuotasHoy = await baseQuery
            .Where(c => c.FechaCobro.Date == today)
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
                DiasParaVencer = 0
            })
            .ToListAsync();

        // Cuotas vencidas (días anteriores no pagadas)
        var cuotasVencidas = await baseQuery
            .Where(c => c.FechaCobro.Date < today && !c.Cobrado)
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
                DiasParaVencer = (int)(today - c.FechaCobro.Date).TotalDays * -1
            })
            .ToListAsync();

        // Cuotas próximas del mes (después de hoy, hasta fin de mes)
        var cuotasProximas = await baseQuery
            .Where(c => c.FechaCobro.Date > today && c.FechaCobro.Date <= endOfMonth)
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
                DiasParaVencer = (int)(c.FechaCobro.Date - today).TotalDays
            })
            .ToListAsync();

        return Ok(new
        {
            fecha = today,
            mesActual = today.ToString("MMMM yyyy"),
            cuotasHoy,
            cuotasVencidas,
            cuotasProximas,
            resumen = new
            {
                totalCuotasHoy = cuotasHoy.Count,
                totalCuotasVencidas = cuotasVencidas.Count,
                totalCuotasProximas = cuotasProximas.Count,
                montoTotalHoy = cuotasHoy.Sum(c => c.SaldoPendiente),
                montoTotalVencido = cuotasVencidas.Sum(c => c.SaldoPendiente),
                montoTotalProximas = cuotasProximas.Sum(c => c.SaldoPendiente)
            }
        });
    }

    /// <summary>
    /// Envía un SMS recordatorio al cliente para una cuota específica
    /// </summary>
    [HttpPost("{cuotaId}/enviar-recordatorio")]
    public async Task<IActionResult> EnviarRecordatorio(int cuotaId)
    {
        var cuota = await _context.CuotasPrestamo
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cliente)
            .FirstOrDefaultAsync(c => c.Id == cuotaId);

        if (cuota == null)
            return NotFound(new { message = "Cuota no encontrada" });

        var prestamo = cuota.Prestamo;
        var cliente = prestamo?.Cliente;

        if (cliente == null || string.IsNullOrEmpty(cliente.Telefono))
            return BadRequest(new { message = "El cliente no tiene teléfono registrado" });

        var diasParaVencer = (cuota.FechaCobro.Date - DateTime.UtcNow.Date).Days;
        var estadoTiempo = diasParaVencer < 0 
            ? $"⚠️ Vencida hace {Math.Abs(diasParaVencer)} días" 
            : diasParaVencer == 0 
                ? "📅 Vence HOY" 
                : $"📆 Vence en {diasParaVencer} días";

        var mensaje = $"📱 Recordatorio de pago\n" +
            $"Hola {cliente.Nombre},\n" +
            $"{estadoTiempo}\n" +
            $"💰 Monto: ${cuota.SaldoPendiente:N0}\n" +
            $"📊 Cuota #{cuota.NumeroCuota} de {prestamo!.NumeroCuotas}\n" +
            $"📅 Fecha: {cuota.FechaCobro:dd/MM/yyyy}";

        try
        {
            var sent = await _twilioService.SendSmsAsync(cliente.Telefono, mensaje);

            // Registrar en historial
            var history = new SmsHistory
            {
                ClienteId = cliente.Id,
                NumeroTelefono = cliente.Telefono,
                Mensaje = mensaje,
                FechaEnvio = DateTime.UtcNow,
                Estado = sent ? EstadoSms.Enviado : EstadoSms.Fallido
            };
            _context.SmsHistories.Add(history);
            await _context.SaveChangesAsync();

            return sent 
                ? Ok(new { message = "SMS enviado exitosamente" })
                : BadRequest(new { message = "Error al enviar SMS" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Envía un SMS con el balance actual del préstamo al cliente
    /// </summary>
    [HttpPost("{prestamoId}/enviar-balance")]
    public async Task<IActionResult> EnviarBalanceSms(int prestamoId)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .FirstOrDefaultAsync(p => p.Id == prestamoId);

        if (prestamo == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        var cliente = prestamo.Cliente;
        if (cliente == null || string.IsNullOrEmpty(cliente.Telefono))
            return BadRequest(new { message = "El cliente no tiene teléfono registrado" });

        var cuotasPagadas = prestamo.Cuotas.Count(c => c.EstadoCuota == "Pagada");
        var cuotasRestantes = prestamo.NumeroCuotas - cuotasPagadas;
        var saldoPendiente = prestamo.Cuotas.Sum(c => c.SaldoPendiente);
        var totalPagado = prestamo.Cuotas.Sum(c => c.MontoPagado);

        var proximaCuota = prestamo.Cuotas
            .Where(c => c.EstadoCuota != "Pagada")
            .OrderBy(c => c.FechaCobro)
            .FirstOrDefault();

        var mensaje = $"📊 Balance de su préstamo\n" +
            $"Hola {cliente.Nombre},\n" +
            $"💵 Capital: ${prestamo.MontoPrestado:N0}\n" +
            $"✅ Pagado: ${totalPagado:N0}\n" +
            $"📝 Pendiente: ${saldoPendiente:N0}\n" +
            $"📊 Cuotas: {cuotasPagadas}/{prestamo.NumeroCuotas}";

        if (proximaCuota != null)
        {
            mensaje += $"\n📅 Próxima: ${proximaCuota.SaldoPendiente:N0} el {proximaCuota.FechaCobro:dd/MM/yyyy}";
        }

        try
        {
            var sent = await _twilioService.SendSmsAsync(cliente.Telefono, mensaje);

            var history = new SmsHistory
            {
                ClienteId = cliente.Id,
                NumeroTelefono = cliente.Telefono,
                Mensaje = mensaje,
                FechaEnvio = DateTime.UtcNow,
                Estado = sent ? EstadoSms.Enviado : EstadoSms.Fallido
            };
            _context.SmsHistories.Add(history);
            await _context.SaveChangesAsync();

            return sent 
                ? Ok(new { message = "SMS de balance enviado exitosamente" })
                : BadRequest(new { message = "Error al enviar SMS" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Error: {ex.Message}" });
        }
    }
}

public class MarcarCobradoDto
{
    public bool Cobrado { get; set; }
}

