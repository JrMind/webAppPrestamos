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
    private readonly Services.IPrestamoService _prestamoService;

    public CobrosController(
        PrestamosDbContext context,
        Services.ITwilioService twilioService,
        Services.IDistribucionGananciasService distribucionService,
        Services.IPrestamoService prestamoService)
    {
        _context = context;
        _twilioService = twilioService;
        _distribucionService = distribucionService;
        _prestamoService = prestamoService;
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
        DateTime? fechaScopeMin = null;

        if (isCobrador && userId.HasValue)
        {
            // Cobradores solo ven sus propios cobros
            effectiveCobradorId = userId.Value;
            hasCobradorFilter = true;
        }
        else if (IsAdministrador())
        {
            // Administrador: scope fijo de cobradores y fecha
            var cobsScope = GetCobradorIdsPermitidos();
            fechaScopeMin = GetFechaInicioAcceso();
            // Si tiene cobradores permitidos, usar el primero como filtro base y manejar el resto con IN
            // Para simplificar: aplicaremos el filtro por lista en la query directamente
            hasCobradorFilter = cobsScope != null;
            // Guardamos en effectiveCobradorId = -1 como señal de usar lista
            effectiveCobradorId = cobsScope != null ? -1 : null;
        }
        else if (cobradorId.HasValue)
        {
            // Permitir filtrar por cobrador a cualquier usuario no-cobrador (Admins, Socios, etc)
            effectiveCobradorId = cobradorId.Value;
            hasCobradorFilter = true;
        }

        // Build query based on filter
        IQueryable<CuotaPrestamo> baseQuery;
        var cobsScopeList = IsAdministrador() ? GetCobradorIdsPermitidos() : null;

        if (IsAdministrador() && cobsScopeList != null)
        {
            // Administrador: filtrar por lista de cobradores permitidos y fecha mínima
            baseQuery = _context.CuotasPrestamo
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cliente)
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cobrador)
                .Where(c => c.Prestamo!.CobradorId.HasValue && cobsScopeList.Contains(c.Prestamo.CobradorId.Value));

            if (fechaScopeMin.HasValue)
                baseQuery = baseQuery.Where(c => c.Prestamo!.FechaPrestamo >= fechaScopeMin.Value);
        }
        else if (hasCobradorFilter && effectiveCobradorId.HasValue)
        {
            var targetCobradorId = effectiveCobradorId.Value;
            baseQuery = _context.CuotasPrestamo
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cliente)
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cobrador)
                .Where(c => targetCobradorId == 0
                            ? c.Prestamo!.CobradorId == null
                            : c.Prestamo!.CobradorId == targetCobradorId);
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
            .Where(c => c.FechaCobro.Date == today && c.EstadoCuota != "Pagada" && c.Prestamo!.EstadoPrestamo != "Terminado")
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
                ClienteId = c.Prestamo!.ClienteId,
                ClienteNombre = c.Prestamo.Cliente!.Nombre,
                ClienteTelefono = c.Prestamo.Cliente.Telefono,
                CobradorNombre = c.Prestamo.Cobrador != null ? c.Prestamo.Cobrador.Nombre : null,
                Vencido = false
            })
            .ToListAsync();

        // Cuotas vencidas (días anteriores no pagadas)
        var cuotasVencidas = await baseQuery
            .Where(c => c.FechaCobro.Date < today && c.EstadoCuota != "Pagada" && (!c.Cobrado || c.SaldoPendiente > 0.01m) && c.Prestamo!.EstadoPrestamo != "Terminado")
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
                ClienteId = c.Prestamo!.ClienteId,
                ClienteNombre = c.Prestamo.Cliente!.Nombre,
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
            cuota.CobradoMedioPago = dto.MedioPago ?? "Efectivo";
            
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

            // Congelados: al cobrar la cuota de interés se genera la del siguiente periodo
            if (cuota.Prestamo != null)
            {
                var nuevaCuotaCongelada = _prestamoService.GenerarCuotaCongeladaSiFalta(cuota.Prestamo);
                if (nuevaCuotaCongelada != null)
                    _context.CuotasPrestamo.Add(nuevaCuotaCongelada);
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
            cuota.CobradoMedioPago = null;
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
            .Where(c => c.FechaCobro.Date < targetDate && c.EstadoCuota != "Pagada" && (!c.Cobrado || c.SaldoPendiente > 0.01m))
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
            var targetCobradorId = effectiveCobradorId.Value;
            baseQuery = _context.CuotasPrestamo
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cliente)
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cobrador)
                .Where(c => c.EstadoCuota != "Pagada")
                .Where(c => c.Prestamo!.EstadoPrestamo != "Terminado")
                .Where(c => targetCobradorId == 0
                            ? c.Prestamo!.CobradorId == null
                            : c.Prestamo!.CobradorId == targetCobradorId);
        }
        else
        {
            // No cobrador filter - show all
            baseQuery = _context.CuotasPrestamo
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cliente)
                .Include(c => c.Prestamo)
                    .ThenInclude(p => p!.Cobrador)
                .Where(c => c.EstadoCuota != "Pagada")
                .Where(c => c.Prestamo!.EstadoPrestamo != "Terminado");
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
                ClienteId = c.Prestamo!.ClienteId,
                ClienteNombre = c.Prestamo.Cliente!.Nombre,
                ClienteTelefono = c.Prestamo.Cliente.Telefono,
                CobradorNombre = c.Prestamo.Cobrador != null ? c.Prestamo.Cobrador.Nombre : null,
                DiasParaVencer = 0
            })
            .ToListAsync();

        // Cuotas vencidas (días anteriores no pagadas). Una cuota marcada "cobrado" que quedó con saldo
        // (pago parcial) también sigue apareciendo, si no desaparecía de todas las listas.
        var cuotasVencidas = await baseQuery
            .Where(c => c.FechaCobro.Date < today && (!c.Cobrado || c.SaldoPendiente > 0.01m))
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
                ClienteId = c.Prestamo!.ClienteId,
                ClienteNombre = c.Prestamo.Cliente!.Nombre,
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
                ClienteId = c.Prestamo!.ClienteId,
                ClienteNombre = c.Prestamo.Cliente!.Nombre,
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
    /// Devuelve los clientes morosos (con cuotas vencidas sin pagar) agrupados por cliente,
    /// sin importar cobrador ni tipo de préstamo, ordenados por total adeudado de mayor a menor.
    /// GET /api/cobros/morosos
    /// </summary>
    [HttpGet("morosos")]
    public async Task<ActionResult<object>> GetClientesMorosos()
    {
        var today = DateTime.UtcNow.Date;

        var cuotasVencidas = await _context.CuotasPrestamo
            .Where(c => c.FechaCobro.Date < today
                        && c.EstadoCuota != "Pagada"
                        && (!c.Cobrado || c.SaldoPendiente > 0.01m)
                        && c.Prestamo!.EstadoPrestamo != "Terminado")
            .Select(c => new
            {
                ClienteId = c.Prestamo!.ClienteId,
                ClienteNombre = c.Prestamo.Cliente!.Nombre,
                ClienteTelefono = c.Prestamo.Cliente.Telefono,
                c.PrestamoId,
                c.SaldoPendiente,
                c.FechaCobro,
                CobradorNombre = c.Prestamo.Cobrador != null ? c.Prestamo.Cobrador.Nombre : null
            })
            .ToListAsync();

        var morosos = cuotasVencidas
            .GroupBy(c => c.ClienteId)
            .Select(g => new
            {
                ClienteId = g.Key,
                ClienteNombre = g.First().ClienteNombre,
                ClienteTelefono = g.First().ClienteTelefono,
                CuotasVencidas = g.Count(),
                TotalAdeudado = g.Sum(x => x.SaldoPendiente),
                DiasMoraMax = (int)(today - g.Min(x => x.FechaCobro).Date).TotalDays,
                Prestamos = g.Select(x => x.PrestamoId).Distinct().OrderBy(id => id).ToList(),
                Cobradores = g.Select(x => x.CobradorNombre)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .ToList()
            })
            .OrderByDescending(m => m.TotalAdeudado)
            .ThenByDescending(m => m.CuotasVencidas)
            .ToList();

        return Ok(new
        {
            fecha = today,
            morosos,
            resumen = new
            {
                totalClientes = morosos.Count,
                totalCuotasVencidas = morosos.Sum(m => m.CuotasVencidas),
                totalAdeudado = morosos.Sum(m => m.TotalAdeudado)
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

        // Congelados: si el abono cubrió la cuota de interés se genera la del siguiente periodo
        var nuevaCuotaCongelada = _prestamoService.GenerarCuotaCongeladaSiFalta(prestamo);
        if (nuevaCuotaCongelada != null)
            _context.CuotasPrestamo.Add(nuevaCuotaCongelada);

        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            message = $"Abono aplicado exitosamente. {cuotasPagadasCount} cuotas completadas.",
            montoAbonado = dto.Monto - montoRestante,
            montoRestante = montoRestante, // Si el abono excedió la deuda total
            cuotasAfectadas
        });
    }
}

public class MarcarCobradoDto
{
    public bool Cobrado { get; set; }
    public string? MedioPago { get; set; } // "Nequi" | "Efectivo"
}

