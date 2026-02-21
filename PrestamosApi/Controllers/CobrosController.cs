using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.DTOs;
using PrestamosApi.Models;
using PrestamosApi.Attributes;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CobrosController : BaseApiController
{
    private readonly PrestamosDbContext _context;
    private readonly Services.ITwilioService _twilioService;
    private readonly Services.IDistribucionGananciasService _distribucionService;

    public CobrosController(
        PrestamosDbContext context, 
        Services.ITwilioService twilioService,
        Services.IDistribucionGananciasService distribucionService)
    {
        _context = context;
        _twilioService = twilioService;
        _distribucionService = distribucionService;
    }

    [HttpGet("hoy")]
    public async Task<ActionResult<object>> GetCobrosHoy([FromQuery(Name = "cobradorId")] int? cobradorId)
    {
        var today = DateTime.UtcNow.Date;
        var userId = GetCurrentUserId();
        var isCobrador = IsCobrador();
        var isAdmin = IsAdmin();

        // Determine cobrador filter - this will be applied to all queries
        int? effectiveCobradorId = null;
        bool hasCobradorFilter = false;
        
        if (isCobrador && userId.HasValue)
        {
            // Cobradores solo ven sus propios cobros
            effectiveCobradorId = userId.Value;
            hasCobradorFilter = true;
        }
        else if (cobradorId.HasValue)
        {
            // Permitir filtrar por cobrador a cualquier usuario no-cobrador (Admins, Socios, etc)
            // Ya que por defecto ven TODO, filtrar es una restricción, no una elevación de privilegios.
            effectiveCobradorId = cobradorId.Value;
            hasCobradorFilter = true;
        }

        // Build query based on filter
        IQueryable<CuotaPrestamo> baseQuery;
        
        if (hasCobradorFilter && effectiveCobradorId.HasValue)
        {
            // Apply cobrador filter explicitly
            var targetCobradorId = effectiveCobradorId.Value;
            baseQuery = _context.CuotasPrestamo
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cliente)
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cobrador)
                .Where(c => c.Prestamo!.CobradorId == targetCobradorId);
        }
        else
        {
            // No cobrador filter - show all
            baseQuery = _context.CuotasPrestamo
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cliente)
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cobrador);
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
    [AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin)]
    public async Task<IActionResult> MarcarCobrado(int cuotaId, [FromBody] MarcarCobradoDto dto)
    {
        var cuota = await _context.CuotasPrestamo
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cliente)
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cuotas)
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.FuentesCapital) // Include FuentesCapital for distribution
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
            cuota.SaldoPendiente = 0; // CRÍTICO: Poner en 0 para que la reserva se actualice
            cuota.EstadoCuota = "Pagada";
            
            // Distribuir ganancias automáticamente
            if (cuota.Prestamo != null)
            {
                await _distribucionService.DistribuirGananciasPago(cuota.PrestamoId, cuota.MontoPagado);
            }
            
            // ACTUALIZAR RESERVA: Agregar capital recuperado (no intereses)
            // Capital recuperado = MontoPagado * (MontoCapital / MontoCuota)
            if (cuota.MontoCuota > 0)
            {
                var ratioCapital = cuota.MontoCapital / cuota.MontoCuota;
                var capitalRecuperado = cuota.MontoPagado * ratioCapital;
                
                var gananciasService = new Services.GananciasService(_context);
                await gananciasService.ActualizarReservaAsync(capitalRecuperado, $"Pago cuota #{cuota.NumeroCuota} préstamo #{cuota.PrestamoId}");
            }
        }
        else if (!dto.Cobrado)
        {
            // Revertir ganancias antes de desmarcar para evitar duplicación
            if (cuota.FechaPago.HasValue && cuota.MontoPagado > 0)
            {
                await _distribucionService.RevertirGananciasPago(cuota.PrestamoId, cuota.MontoPagado);
            }
            
            cuota.FechaPago = null;
            cuota.MontoPagado = 0;
            cuota.SaldoPendiente = cuota.MontoCuota; // Restaurar saldo pendiente
            cuota.Cobrado = false;
            // Verificar si la cuota está vencida
            cuota.EstadoCuota = cuota.FechaCobro.Date < DateTime.UtcNow.Date ? "Vencida" : "Pendiente";
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
    public async Task<ActionResult<object>> GetCobrosDelMes([FromQuery(Name = "cobradorId")] int? cobradorId)
    {
        var today = DateTime.UtcNow.Date;
        var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
        var userId = GetCurrentUserId();
        var isCobrador = IsCobrador();
        var isAdmin = IsAdmin();

        // Determine cobrador filter - this will be applied to all queries
        int? effectiveCobradorId = null;
        bool hasCobradorFilter = false;
        
        if (isCobrador && userId.HasValue)
        {
            // Cobradores solo ven sus propios cobros
            effectiveCobradorId = userId.Value;
            hasCobradorFilter = true;
        }
        else if (cobradorId.HasValue)
        {
            // Permitir filtrar por cobrador a cualquier usuario no-cobrador
            effectiveCobradorId = cobradorId.Value;
            hasCobradorFilter = true;
        }

        // Build query based on filter
        IQueryable<CuotaPrestamo> baseQuery;
        
        if (hasCobradorFilter && effectiveCobradorId.HasValue)
        {
            // Apply cobrador filter explicitly
            var targetCobradorId = effectiveCobradorId.Value;
            baseQuery = _context.CuotasPrestamo
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cliente)
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cobrador)
                .Where(c => c.EstadoCuota != "Pagada")
                .Where(c => c.Prestamo!.CobradorId == targetCobradorId);
        }
        else
        {
            // No cobrador filter - show all
            baseQuery = _context.CuotasPrestamo
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cliente)
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cobrador)
                .Where(c => c.EstadoCuota != "Pagada");
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

    [HttpPost("abonar")]
    public async Task<IActionResult> AbonarPrestamo([FromBody] AbonarPrestamoDto dto)
    {
        if (dto.Monto <= 0)
            return BadRequest(new { message = "El monto debe ser mayor a 0" });

        var prestamo = await _context.Prestamos
            .Include(p => p.Cuotas)
            .Include(p => p.FuentesCapital)
            .Include(p => p.Cliente)
            .Include(p => p.Cobrador)
            .FirstOrDefaultAsync(p => p.Id == dto.PrestamoId);

        if (prestamo == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        // Obtener cuotas pendientes ordenadas por fecha
        var cuotasPendientes = prestamo.Cuotas
            .Where(c => c.EstadoCuota != "Pagada")
            .OrderBy(c => c.FechaCobro)
            .ToList();

        if (!cuotasPendientes.Any())
            return BadRequest(new { message = "El préstamo no tiene cuotas pendientes" });

        decimal montoRestante = dto.Monto;
        decimal capitalRecuperadoTotal = 0;
        int cuotasPagadasCount = 0;
        var cuotasAfectadas = 0;

        var gananciasService = new Services.GananciasService(_context);

        foreach (var cuota in cuotasPendientes)
        {
            if (montoRestante <= 0) break;

            decimal montoAbonar = Math.Min(montoRestante, cuota.SaldoPendiente);
            
            // Actualizar estado de la cuota
            cuota.MontoPagado += montoAbonar;
            cuota.SaldoPendiente -= montoAbonar;
            cuota.FechaPago = DateTime.UtcNow; // Actualizar fecha último pago
            cuotasAfectadas++;

            // Crear registro de PAgo
            var pago = new Pago
            {
                PrestamoId = prestamo.Id,
                CuotaId = cuota.Id,
                MontoPago = montoAbonar,
                FechaPago = DateTime.UtcNow,
                MetodoPago = "Abono",
                Observaciones = "Abono a cuota futura/pendiente"
            };
            _context.Pagos.Add(pago);

            // Calcular capital recuperado de este abono
            if (cuota.MontoCuota > 0)
            {
                var ratioCapital = cuota.MontoCapital / cuota.MontoCuota;
                var capitalRecuperadoCuota = montoAbonar * ratioCapital;
                capitalRecuperadoTotal += capitalRecuperadoCuota;
            }

            // Verificar si se completó la cuota
            if (cuota.SaldoPendiente <= 0.01m) // Margen por redondeo
            {
                cuota.SaldoPendiente = 0;
                cuota.EstadoCuota = "Pagada";
                cuota.Cobrado = true;
                cuotasPagadasCount++;

                // Distribuir ganancias si se completó
                await _distribucionService.DistribuirGananciasPago(prestamo.Id, cuota.MontoPagado);
            }
            else
            {
                cuota.EstadoCuota = "Parcial";
            }

            montoRestante -= montoAbonar;
        }

        // Actualizar Reserva con el capital total recuperado
        if (capitalRecuperadoTotal > 0)
        {
            await gananciasService.ActualizarReservaAsync(capitalRecuperadoTotal, $"Abono a préstamo #{prestamo.Id}");
        }

        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            message = $"Abono aplicado exitosamente. {cuotasPagadasCount} cuotas completadas.",
            montoAbonado = dto.Monto - montoRestante,
            montoRestante = montoRestante, // Si el abono excedió la deuda total
            cuotasAfectadas
        });
    }
    // ──────────────────────────────────────────────────────────────
    // LIQUIDACIÓN / COMISIONES DE COBRADORES
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Devuelve la liquidación acumulada de TODOS los cobradores.
    /// Solo Admin y Socio pueden acceder.
    /// GET /api/cobros/comisiones
    /// GET /api/cobros/comisiones?incluirParciales=true
    /// </summary>
    [HttpGet("comisiones")]
    [AuthorizeRoles(RolUsuario.Admin, RolUsuario.Socio)]
    public async Task<ActionResult<List<LiquidacionCobradorDto>>> GetComisionesTodosCobradores(
        [FromQuery] bool incluirParciales = false)
    {
        var liquidaciones = await BuildLiquidaciones(cobradorId: null, incluirParciales);
        return Ok(liquidaciones);
    }

    /// <summary>
    /// Devuelve la liquidación acumulada de UN cobrador específico.
    /// El propio cobrador también puede consultar la suya.
    /// GET /api/cobros/comisiones/{cobradorId}
    /// GET /api/cobros/comisiones/{cobradorId}?incluirParciales=true
    /// </summary>
    [HttpGet("comisiones/{cobradorId:int}")]
    public async Task<ActionResult<LiquidacionCobradorDto>> GetComisionCobrador(
        int cobradorId,
        [FromQuery] bool incluirParciales = false)
    {
        // Un cobrador solo puede ver la suya propia; admin/socio pueden ver cualquiera
        var currentUserId = GetCurrentUserId();
        if (IsCobrador() && currentUserId != cobradorId)
            return Forbid();

        var liquidaciones = await BuildLiquidaciones(cobradorId, incluirParciales);

        if (!liquidaciones.Any())
            return NotFound(new { message = "Cobrador no encontrado o sin préstamos asignados" });

        return Ok(liquidaciones.First());
    }

    // ── Método privado compartido por los dos endpoints ──────────
    private async Task<List<LiquidacionCobradorDto>> BuildLiquidaciones(
        int? cobradorId, bool incluirParciales)
    {
        // Traer cobradores con sus préstamos y cuotas en una sola consulta
        var cobradores = await _context.Usuarios
            .Where(u => u.Rol == RolUsuario.Cobrador && u.Activo
                        && (cobradorId == null || u.Id == cobradorId))
            .Include(u => u.PrestamosComoCobrador)
                .ThenInclude(p => p.Cliente)
            .Include(u => u.PrestamosComoCobrador)
                .ThenInclude(p => p.Cuotas)
            .AsNoTracking()
            .ToListAsync();

        // Traer todas las liquidaciones de esos cobradores de una vez
        var cobradorIds = cobradores.Select(c => c.Id).ToList();
        var liquidacionesPorCobrador = await _context.LiquidacionesCobrador
            .Include(l => l.RealizadoPorUsuario)
            .Where(l => cobradorIds.Contains(l.CobradorId))
            .OrderByDescending(l => l.FechaLiquidacion)
            .AsNoTracking()
            .ToListAsync();

        var resultado = new List<LiquidacionCobradorDto>();

        foreach (var cobrador in cobradores)
        {
            var prestamosDto = new List<ComisionPrestamoDto>();

            foreach (var prestamo in cobrador.PrestamosComoCobrador)
            {
                var pct = prestamo.PorcentajeCobrador;

                // Cuotas completamente pagadas o abonadas
                var cuotasPagadas = prestamo.Cuotas
                    .Where(c => c.EstadoCuota == "Pagada" || c.EstadoCuota == "Abonada")
                    .ToList();

                // Cuotas con abono parcial (decisión del admin si incluirlas)
                var cuotasParciales = incluirParciales
                    ? prestamo.Cuotas.Where(c => c.EstadoCuota == "Parcial" && c.MontoPagado > 0).ToList()
                    : new();

                // Detalle de cuotas pagadas
                var detallePagadas = cuotasPagadas.Select(c => new ComisionCuotaDto(
                    CuotaId          : c.Id,
                    NumeroCuota      : c.NumeroCuota,
                    FechaPago        : c.FechaPago,
                    MontoCuota       : Math.Round(c.MontoCuota, 2),
                    MontoPagado      : Math.Round(c.MontoPagado, 2),
                    MontoCapital     : Math.Round(c.MontoCapital, 2),
                    MontoInteres     : Math.Round(c.MontoInteres, 2),
                    PorcentajeCobrador: pct,
                    ComisionCuota    : Math.Round(c.MontoPagado * pct / 100m, 2)
                )).OrderBy(c => c.NumeroCuota).ToList();

                var totalRecaudado  = cuotasPagadas.Sum(c => c.MontoPagado);
                var comisionPrestamo = Math.Round(totalRecaudado * pct / 100m, 2);

                var totalParcial    = cuotasParciales.Sum(c => c.MontoPagado);
                var comisionParcial = Math.Round(totalParcial * pct / 100m, 2);

                prestamosDto.Add(new ComisionPrestamoDto(
                    PrestamoId          : prestamo.Id,
                    ClienteNombre       : prestamo.Cliente?.Nombre ?? "—",
                    ClienteCedula       : prestamo.Cliente?.Cedula ?? "—",
                    MontoPrestado       : prestamo.MontoPrestado,
                    EstadoPrestamo      : prestamo.EstadoPrestamo,
                    PorcentajeCobrador  : pct,
                    CuotasTotales       : prestamo.Cuotas.Count,
                    CuotasPagadas       : cuotasPagadas.Count,
                    CuotasParciales     : cuotasParciales.Count,
                    TotalRecaudado      : Math.Round(totalRecaudado, 2),
                    TotalRecaudadoParcial: Math.Round(totalParcial, 2),
                    ComisionPrestamo    : comisionPrestamo,
                    ComisionParcial     : comisionParcial,
                    CuotasPagadasDetalle: detallePagadas
                ));
            }

            var totalRec         = prestamosDto.Sum(p => p.TotalRecaudado);
            var totalRecParcial  = prestamosDto.Sum(p => p.TotalRecaudadoParcial);
            var totalCom         = prestamosDto.Sum(p => p.ComisionPrestamo);
            var totalComParcial  = prestamosDto.Sum(p => p.ComisionParcial);

            var totalComisionGeneral = Math.Round(totalCom + totalComParcial, 2);
            var totalLiquidado = Math.Round(
                liquidacionesPorCobrador.Where(l => l.CobradorId == cobrador.Id).Sum(l => l.MontoLiquidado), 2);
            var saldoPendiente = Math.Round(totalComisionGeneral - totalLiquidado, 2);

            var historial = liquidacionesPorCobrador
                .Where(l => l.CobradorId == cobrador.Id)
                .Select(l => new LiquidacionRegistroDto(
                    Id: l.Id,
                    MontoLiquidado: l.MontoLiquidado,
                    FechaLiquidacion: l.FechaLiquidacion,
                    Observaciones: l.Observaciones,
                    RealizadoPorNombre: l.RealizadoPorUsuario?.Nombre
                )).ToList();

            resultado.Add(new LiquidacionCobradorDto(
                CobradorId          : cobrador.Id,
                CobradorNombre      : cobrador.Nombre,
                CobradorTelefono    : cobrador.Telefono,
                TotalPrestamos      : prestamosDto.Count,
                TotalCuotasPagadas  : prestamosDto.Sum(p => p.CuotasPagadas),
                TotalCuotasParciales: prestamosDto.Sum(p => p.CuotasParciales),
                TotalRecaudado      : Math.Round(totalRec, 2),
                TotalRecaudadoParcial: Math.Round(totalRecParcial, 2),
                TotalComision       : Math.Round(totalCom, 2),
                TotalComisionParcial: Math.Round(totalComParcial, 2),
                TotalComisionGeneral: totalComisionGeneral,
                TotalLiquidado      : totalLiquidado,
                SaldoPendiente      : saldoPendiente,
                FechaConsulta       : DateTime.UtcNow,
                Prestamos           : prestamosDto.OrderBy(p => p.ClienteNombre).ToList(),
                HistorialLiquidaciones: historial
            ));
        }

        return resultado.OrderBy(c => c.CobradorNombre).ToList();
    }

    /// <summary>
    /// Registra un pago al cobrador (liquidación parcial o total).
    /// POST /api/cobros/liquidar
    /// </summary>
    [HttpPost("liquidar")]
    [AuthorizeRoles(RolUsuario.Admin, RolUsuario.Socio)]
    public async Task<ActionResult<object>> LiquidarCobrador([FromBody] RegistrarLiquidacionDto dto)
    {
        if (dto.Monto <= 0)
            return BadRequest(new { message = "El monto a liquidar debe ser mayor que cero" });

        var cobrador = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Id == dto.CobradorId && u.Rol == RolUsuario.Cobrador);

        if (cobrador == null)
            return NotFound(new { message = "Cobrador no encontrado" });

        var liquidacion = new LiquidacionCobrador
        {
            CobradorId       = dto.CobradorId,
            MontoLiquidado   = Math.Round(dto.Monto, 2),
            FechaLiquidacion = DateTime.UtcNow,
            Observaciones    = dto.Observaciones,
            RealizadoPor     = GetCurrentUserId()
        };

        _context.LiquidacionesCobrador.Add(liquidacion);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = $"Liquidación de ${dto.Monto:N2} registrada para {cobrador.Nombre}",
            liquidacionId = liquidacion.Id,
            cobradorNombre = cobrador.Nombre,
            monto = liquidacion.MontoLiquidado,
            fecha = liquidacion.FechaLiquidacion
        });
    }
}

public class MarcarCobradoDto
{
    public bool Cobrado { get; set; }
}

