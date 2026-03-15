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
public class PagosController : BaseApiController
{
    private readonly PrestamosDbContext _context;
    private readonly ITwilioService _twilioService;
    private readonly IDistribucionGananciasService _distribucionService;
    private readonly IPrestamoService _prestamoService;
    private readonly ILogger<PagosController> _logger;

    public PagosController(
        PrestamosDbContext context, 
        ITwilioService twilioService, 
        IDistribucionGananciasService distribucionService,
        IPrestamoService prestamoService,
        ILogger<PagosController> logger)
    {
        _context = context;
        _twilioService = twilioService;
        _distribucionService = distribucionService;
        _prestamoService = prestamoService;
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

    /// <summary>
    /// Obtener pagos agrupados por día con totales
    /// </summary>
    [HttpGet("por-dia")]
    public async Task<ActionResult<object>> GetPagosPorDia([FromQuery] string? fechaInicio = null, [FromQuery] string? fechaFin = null)
    {
        DateTime inicio;
        DateTime fin;
        
        // Parsear fechas como UTC explícitamente
        if (!string.IsNullOrEmpty(fechaInicio))
        {
            if (!DateTime.TryParseExact(fechaInicio, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out inicio))
            {
                return BadRequest(new { message = "Formato de fechaInicio inválido. Use yyyy-MM-dd" });
            }
        }
        else
        {
            inicio = DateTime.UtcNow.AddDays(-30);
        }
        
        if (!string.IsNullOrEmpty(fechaFin))
        {
            if (!DateTime.TryParseExact(fechaFin, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out fin))
            {
                return BadRequest(new { message = "Formato de fechaFin inválido. Use yyyy-MM-dd" });
            }
        }
        else
        {
            fin = DateTime.UtcNow;
        }
        
        // Ajustar a inicio y fin del día en UTC
        inicio = inicio.Date;
        fin = fin.Date.AddDays(1).AddTicks(-1);

        var pagos = await _context.Pagos
            .Include(p => p.Prestamo)
                .ThenInclude(pr => pr!.Cliente)
            .Where(p => p.FechaPago >= inicio && p.FechaPago <= fin)
            .OrderByDescending(p => p.FechaPago)
            .Select(p => new {
                p.Id,
                p.PrestamoId,
                ClienteNombre = p.Prestamo!.Cliente!.Nombre,
                p.MontoPago,
                p.FechaPago,
                p.MetodoPago,
                p.CuotaId,
                p.Observaciones
            })
            .ToListAsync();

        // Agrupar por fecha
        var porDia = pagos
            .GroupBy(p => p.FechaPago.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new {
                Fecha = g.Key,
                TotalDia = g.Sum(p => p.MontoPago),
                CantidadPagos = g.Count(),
                Pagos = g.Select(p => new {
                    p.Id,
                    p.PrestamoId,
                    p.ClienteNombre,
                    p.MontoPago,
                    p.FechaPago,
                    p.MetodoPago,
                    p.Observaciones
                }).ToList()
            })
            .ToList();

        return Ok(new {
            fechaInicio = inicio,
            fechaFin = fin,
            totalGeneral = pagos.Sum(p => p.MontoPago),
            totalPagos = pagos.Count,
            diasConPagos = porDia.Count,
            porDia
        });
    }

    [HttpPost]
    public async Task<ActionResult<PagoDto>> CreatePago(CreatePagoDto dto)
    {
        // Solo socios pueden registrar pagos
        if (!IsSocio() && GetCurrentUserRole() != RolUsuario.Admin)
        {
            return Forbid();
        }

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
                // Para préstamos congelados, manejar lógica especial de sobrepago
                if (prestamo.EsCongelado)
                {
                    // Calcular el interés que corresponde a esta cuota
                    decimal interesCuota = cuota.MontoCuota; // En préstamos congelados, la cuota ES el interés
                    
                    if (dto.MontoPago > interesCuota)
                    {
                        // Sobrepago: el exceso reduce el capital
                        decimal abonoCapital = dto.MontoPago - interesCuota;
                        prestamo.MontoPrestado -= abonoCapital;
                        
                        if (prestamo.MontoPrestado <= 0)
                        {
                            // Se pagó todo el capital, préstamo saldado
                            prestamo.MontoPrestado = 0;
                            prestamo.EstadoPrestamo = "Pagado";
                            
                            // Marcar todas las cuotas pendientes como pagadas
                            foreach (var c in prestamo.Cuotas.Where(c => c.EstadoCuota != "Pagada"))
                            {
                                c.EstadoCuota = "Pagada";
                                c.FechaPago = fechaPagoUtc;
                                c.SaldoPendiente = 0;
                            }
                        }
                        else
                        {
                            
                            // Recalcular MontoCuota del préstamo para el futuro
                            decimal factorFrecuencia = prestamo.FrecuenciaPago switch
                            {
                                "Diario" => 1m / 30m,
                                "Semanal" => 7m / 30m,
                                "Quincenal" => 15m / 30m,
                                "Mensual" => 1m,
                                _ => 1m
                            };
                            decimal nuevaCuotaBase = Math.Round(prestamo.MontoPrestado * (prestamo.TasaInteres / 100m) * factorFrecuencia, 0);
                            prestamo.MontoCuota = nuevaCuotaBase;
                            
                            _logger.LogInformation("Préstamo congelado #{PrestamoId}: Abono capital ${AbonoCapital}, Nuevo capital ${NuevoCapital}, Nueva cuota base ${NuevaCuota}", 
                                prestamo.Id, abonoCapital, prestamo.MontoPrestado, nuevaCuotaBase);
                        }
                    }
                    
                    // Marcar la cuota actual como pagada
                    cuota.MontoPagado = cuota.MontoCuota;
                    cuota.SaldoPendiente = 0;
                    cuota.EstadoCuota = "Pagada";
                    cuota.FechaPago = fechaPagoUtc;

                    // IMPORTANTE: Generar la cuota del PRÓXIMO MES automáticamente
                    if (prestamo.EstadoPrestamo == "Activo")
                    {
                        var nuevaCuotaCongelada = _prestamoService.GenerarSiguienteCuotaCongelada(prestamo, cuota);
                        _context.CuotasPrestamo.Add(nuevaCuotaCongelada);
                    }
                }
                else
                {
                    // Lógica normal para préstamos no congelados CON DISTRIBUCIÓN A CUOTAS FUTURAS
                    decimal montoRestante = dto.MontoPago;
                    
                    // Primero, aplicar a la cuota actual
                    decimal abonoActual = Math.Min(montoRestante, cuota.SaldoPendiente);
                    cuota.MontoPagado += abonoActual;
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
                    
                    montoRestante -= abonoActual;
                    
                    // Si queda dinero, distribuir a cuotas futuras
                    if (montoRestante > 0)
                    {
                        var cuotasPendientes = prestamo.Cuotas
                            .Where(c => c.Id != cuota.Id && 
                                       (c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida"))
                            .OrderBy(c => c.FechaCobro)
                            .ToList();
                        
                        foreach (var cuotaFutura in cuotasPendientes)
                        {
                            if (montoRestante <= 0) break;
                            
                            decimal abonoFuturo = Math.Min(montoRestante, cuotaFutura.SaldoPendiente);
                            cuotaFutura.MontoPagado += abonoFuturo;
                            cuotaFutura.SaldoPendiente = cuotaFutura.MontoCuota - cuotaFutura.MontoPagado;
                            
                            if (cuotaFutura.SaldoPendiente <= 0)
                            {
                                cuotaFutura.SaldoPendiente = 0;
                                cuotaFutura.EstadoCuota = "Pagada";
                                cuotaFutura.FechaPago = fechaPagoUtc;
                            }
                            else if (cuotaFutura.MontoPagado > 0)
                            {
                                cuotaFutura.EstadoCuota = "Parcial";
                            }
                            
                            montoRestante -= abonoFuturo;
                        }
                        
                        _logger.LogInformation("Pago con exceso distribuido: Préstamo #{PrestamoId}, Cuota #{CuotaId}, Monto original ${Monto}, Distribuido a cuotas futuras",
                            prestamo.Id, cuota.Id, dto.MontoPago);
                    }
                }
            }
        }

        // Verificar si todas las cuotas están pagadas (solo para préstamos no congelados)
        if (!prestamo.EsCongelado)
        {
            var todasPagadas = prestamo.Cuotas.All(c => 
                c.Id == dto.CuotaId ? (c.MontoCuota - c.MontoPagado - dto.MontoPago <= 0) : c.EstadoCuota == "Pagada");
            
            if (todasPagadas)
            {
                prestamo.EstadoPrestamo = "Pagado";
            }
        }

        await _context.SaveChangesAsync();

        // ACTUALIZAR RESERVA: Agregar capital recuperado de todas las cuotas afectadas
        decimal capitalTotalRecuperado = 0;
        
        // Calcular capital recuperado de la cuota principal
        if (dto.CuotaId.HasValue)
        {
            var cuotaPrincipal = await _context.CuotasPrestamo.FindAsync(dto.CuotaId.Value);
            if (cuotaPrincipal != null && cuotaPrincipal.MontoCuota > 0)
            {
                var ratioCapital = cuotaPrincipal.MontoCapital / cuotaPrincipal.MontoCuota;
                
                // Aplicar ratio solo al monto que efectivamente se aplicó a esta cuota
                decimal montoAplicadoACuota = Math.Min(dto.MontoPago, cuotaPrincipal.MontoPagado);
                capitalTotalRecuperado += montoAplicadoACuota * ratioCapital;
            }
        }
        else
        {
            // Si no hay cuota específica, calcular sobre todas las cuotas que recibieron el pago
            // (en caso de distribución automática a futuras cuotas)
            var cuotasAfectadas = prestamo.Cuotas
                .Where(c => c.MontoPagado > 0 && c.MontoCuota > 0)
                .ToList();
            
            foreach (var cuota in cuotasAfectadas)
            {
                var ratioCapital = cuota.MontoCapital / cuota.MontoCuota;
                // Aproximado: asumimos que el pago reciente contribuyó proporcionalmente
                capitalTotalRecuperado += (dto.MontoPago / cuotasAfectadas.Count) * ratioCapital;
            }
        }
        
        // Actualizar reserva con el capital recuperado
        if (capitalTotalRecuperado > 0)
        {
            var gananciasService = new GananciasService(_context);
            await gananciasService.ActualizarReservaAsync(capitalTotalRecuperado, $"Pago préstamo #{prestamo.Id} - ${dto.MontoPago:N0}");
        }

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
        // Solo socios pueden eliminar pagos
        if (!IsSocio() && GetCurrentUserRole() != RolUsuario.Admin)
        {
            return Forbid();
        }

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

    /// <summary>
    /// Abono al capital para préstamos congelados - reduce el capital adeudado
    /// </summary>
    [HttpPost("abono-capital/{prestamoId}")]
    public async Task<IActionResult> AbonoCapital(int prestamoId, [FromBody] AbonoCapitalDto dto)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .FirstOrDefaultAsync(p => p.Id == prestamoId);

        if (prestamo == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        if (!prestamo.EsCongelado)
            return BadRequest(new { message = "Solo préstamos congelados pueden recibir abonos al capital" });

        if (dto.Monto <= 0)
            return BadRequest(new { message = "El monto debe ser mayor a 0" });

        if (dto.Monto > prestamo.MontoPrestado)
            return BadRequest(new { message = $"El abono ({dto.Monto:N0}) no puede ser mayor al capital adeudado ({prestamo.MontoPrestado:N0})" });

        // Reducir el capital
        var capitalAnterior = prestamo.MontoPrestado;
        prestamo.MontoPrestado -= dto.Monto;

        // Registrar el pago como abono al capital
        var pago = new Pago
        {
            PrestamoId = prestamoId,
            MontoPago = dto.Monto,
            FechaPago = DateTime.UtcNow,
            MetodoPago = dto.MetodoPago ?? "Efectivo",
            Observaciones = $"Abono al capital. Capital anterior: ${capitalAnterior:N0}, Nuevo capital: ${prestamo.MontoPrestado:N0}"
        };
        _context.Pagos.Add(pago);

        // MANTENER SINCRONIZADO EL CAPITAL QUIETO CON LA TABLA DE CUOTAS
        // Como el Capital Quieto / Circulante lee de MontoCapital de las cuotas Pendientes, necesitamos actualizar la cuota actual
        var cuotaActiva = prestamo.Cuotas.FirstOrDefault(c => c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida");
        if (cuotaActiva != null)
        {
            // El capital quieto del préstamo congelado se va a leer de aquí. Debe ser igual al nuevo MontoPrestado.
            cuotaActiva.MontoCapital = prestamo.MontoPrestado;
        }

        if (prestamo.MontoPrestado <= 0)
        {
            // Capital pagado completamente
            prestamo.MontoPrestado = 0;
            prestamo.EstadoPrestamo = "Pagado";
            
            if (cuotaActiva != null)
            {
                 // Si se saldó el capital pero no había pagado el interés de este mes...
                 // Para este modelo simple, asumimos que liquida.
                 cuotaActiva.EstadoCuota = "Pagada";
                 cuotaActiva.MontoCapital = 0;
            }
            
            _logger.LogInformation("Préstamo congelado #{PrestamoId} liquidado con abono de ${Monto}", prestamoId, dto.Monto);
        }
        else
        {
            // Recalcular el monto de cuota para el nuevo capital
            decimal factorFrecuencia = prestamo.FrecuenciaPago switch
            {
                "Diario" => 1m / 30m,
                "Semanal" => 7m / 30m,
                "Quincenal" => 15m / 30m,
                "Mensual" => 1m,
                _ => 1m
            };
            decimal nuevaCuota = Math.Round(prestamo.MontoPrestado * (prestamo.TasaInteres / 100m) * factorFrecuencia, 0);
            prestamo.MontoCuota = nuevaCuota;
            
            // NO TOCAMOS cuotaActiva.MontoCuota ni SaldoPendiente porque el cliente todavía debe el interés original del mes anterior
            // La nueva cuota más baja aplicará a partir del SIGUIENTE MES que se genere.

            _logger.LogInformation("Préstamo congelado #{PrestamoId}: Abono capital ${Monto}, Nuevo capital ${NuevoCapital}, Nueva cuota interés ${NuevaCuota}", 
                prestamoId, dto.Monto, prestamo.MontoPrestado, nuevaCuota);
        }

        await _context.SaveChangesAsync();

        return Ok(new { 
            message = $"Abono de ${dto.Monto:N0} aplicado exitosamente",
            capitalAnterior = capitalAnterior,
            nuevoCapital = prestamo.MontoPrestado,
            nuevaCuota = prestamo.MontoCuota,
            estadoPrestamo = prestamo.EstadoPrestamo
        });
    }
}

public record AbonoCapitalDto(decimal Monto, string? MetodoPago = null);
