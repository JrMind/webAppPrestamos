using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.DTOs;
using PrestamosApi.Services;
using PrestamosApi.Models.DTOs;
using PrestamosApi.Attributes;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly PrestamosDbContext _context;
    private readonly IGananciasService _gananciasService;

    public DashboardController(PrestamosDbContext context, IGananciasService gananciasService)
    {
        _context = context;
        _gananciasService = gananciasService;
    }

    [HttpGet("metricas")]
    public async Task<ActionResult<DashboardMetricasDto>> GetMetricas()
    {
        try 
        {
            // Usar UTC para PostgreSQL
            var hoy = DateTime.UtcNow.Date;
            var en7Dias = hoy.AddDays(7);

            // KPIs básicos
            var totalPrestado = await _context.Prestamos.SumAsync(p => p.MontoPrestado);
            
            // Total a cobrar = suma de saldos pendientes de cuotas de préstamos activos
            var totalACobrar = await _context.CuotasPrestamo
                .Include(c => c.Prestamo)
                .Where(c => c.Prestamo!.EstadoPrestamo == "Activo")
                .SumAsync(c => c.SaldoPendiente);

            // Interés del mes - suma de intereses de cuotas del mes actual
            var inicioMes = DateTime.SpecifyKind(new DateTime(hoy.Year, hoy.Month, 1), DateTimeKind.Utc);
            var finMes = DateTime.SpecifyKind(inicioMes.AddMonths(1), DateTimeKind.Utc);
            var interesMes = await _context.CuotasPrestamo
                .Where(c => c.FechaCobro >= inicioMes && c.FechaCobro < finMes)
                .SumAsync(c => (decimal?)c.MontoInteres) ?? 0;

            // Total de cuotas a cobrar del mes vigente (ganancia total del mes)
            var gananciaTotalMes = await _context.CuotasPrestamo
                .Where(c => c.FechaCobro >= inicioMes && c.FechaCobro < finMes
                        && (c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial"))
                .SumAsync(c => (decimal?)c.SaldoPendiente) ?? 0;

            var prestamosActivos = await _context.Prestamos
                .Where(p => p.EstadoPrestamo == "Activo")
                .CountAsync();

            var montoPrestamosActivos = await _context.Prestamos
                .Where(p => p.EstadoPrestamo == "Activo")
                .SumAsync(p => p.MontoPrestado);

            // Flujo de Capital
            var totalCobrado = await _context.Pagos.SumAsync(p => p.MontoPago);
            
            // Saldo pendiente de préstamos activos (lo que falta por cobrar - capital + intereses)
            var saldoPendienteActivos = await _context.CuotasPrestamo
                .Include(c => c.Prestamo)
                .Where(c => c.Prestamo!.EstadoPrestamo == "Activo")
                .SumAsync(c => c.SaldoPendiente);
            
            // Capital de préstamos activos que aún no se ha recuperado
            // Dinero Circulando = Capital prestado activo - proporción de capital cobrado
            var capitalPrestamosActivos = await _context.Prestamos
                .Where(p => p.EstadoPrestamo == "Activo")
                .SumAsync(p => p.MontoPrestado);
            var totalAPagarActivos = await _context.Prestamos
                .Where(p => p.EstadoPrestamo == "Activo")
                .SumAsync(p => p.MontoTotal);
            
            // Proporción de capital en cada pago (capital / total)
            var proporcionCapital = totalAPagarActivos > 0 ? capitalPrestamosActivos / totalAPagarActivos : 0;
            var capitalRecuperado = totalCobrado * proporcionCapital;
            var dineroCirculando = capitalPrestamosActivos - capitalRecuperado;
            if (dineroCirculando < 0) dineroCirculando = 0;
            
            // Usar el método completo de GananciasService para calcular la reserva correctamente
            var reservaDisponible = await _gananciasService.CalcularReservaDisponibleAsync();

            // Cuotas vencidas hoy - comparar solo la parte de fecha
            var cuotasVencidasHoy = await _context.CuotasPrestamo
                .Where(c => c.FechaCobro.Date == hoy && 
                        (c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial"))
                .ToListAsync();
            var cantidadCuotasVencidasHoy = cuotasVencidasHoy.Count;
            var montoCuotasVencidasHoy = cuotasVencidasHoy.Sum(c => c.SaldoPendiente);

            // Cuotas próximos 7 días
            var cuotasProximas = await _context.CuotasPrestamo
                .Where(c => c.FechaCobro.Date > hoy && c.FechaCobro.Date <= en7Dias &&
                        (c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial"))
                .ToListAsync();
            var cantidadCuotasProximas = cuotasProximas.Count;
            var montoCuotasProximas = cuotasProximas.Sum(c => c.SaldoPendiente);

            // Tasa promedio
            var tasaPromedioInteres = await _context.Prestamos
                .Where(p => p.EstadoPrestamo == "Activo")
                .AverageAsync(p => (decimal?)p.TasaInteres) ?? 0;

            // Morosidad
            var totalCuotas = await _context.CuotasPrestamo.CountAsync();
            var cuotasVencidas = await _context.CuotasPrestamo
                .Where(c => c.EstadoCuota == "Vencida")
                .CountAsync();
            var porcentajeMorosidad = totalCuotas > 0 ? (decimal)cuotasVencidas / totalCuotas * 100 : 0;

            // Evolución préstamos (últimos 12 meses)
            var hace12Meses = hoy.AddMonths(-12);
            var evolucion = await _context.Prestamos
                .Where(p => p.FechaPrestamo >= hace12Meses)
                .GroupBy(p => new { p.FechaPrestamo.Year, p.FechaPrestamo.Month })
                .Select(g => new 
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MontoPrestado = g.Sum(p => p.MontoPrestado)
                })
                .OrderBy(e => e.Year).ThenBy(e => e.Month)
                .ToListAsync();

            var pagosEvolucion = await _context.Pagos
                .Where(p => p.FechaPago >= hace12Meses)
                .GroupBy(p => new { p.FechaPago.Year, p.FechaPago.Month })
                .Select(g => new 
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MontoCobrado = g.Sum(p => p.MontoPago)
                })
                .OrderBy(e => e.Year).ThenBy(e => e.Month)
                .ToListAsync();

            decimal acumuladoPrestado = 0;
            decimal acumuladoCobrado = 0;
            var evolucionPrestamos = new List<EvolucionPrestamosDto>();
            
            for (var fecha = hace12Meses; fecha <= hoy; fecha = fecha.AddMonths(1))
            {
                var prestadoMes = evolucion.FirstOrDefault(e => e.Year == fecha.Year && e.Month == fecha.Month)?.MontoPrestado ?? 0;
                var cobradoMes = pagosEvolucion.FirstOrDefault(e => e.Year == fecha.Year && e.Month == fecha.Month)?.MontoCobrado ?? 0;
                
                acumuladoPrestado += prestadoMes;
                acumuladoCobrado += cobradoMes;
                
                evolucionPrestamos.Add(new EvolucionPrestamosDto(
                    DateTime.SpecifyKind(new DateTime(fecha.Year, fecha.Month, 1), DateTimeKind.Utc), 
                    acumuladoPrestado, 
                    acumuladoCobrado));
            }

            // Top 10 clientes
            var topClientes = await _context.Clientes
                .Include(c => c.Prestamos)
                .OrderByDescending(c => c.Prestamos.Sum(p => p.MontoPrestado))
                .Take(10)
                .Select(c => new TopClienteDto(c.Nombre, c.Prestamos.Sum(p => p.MontoPrestado)))
                .ToListAsync();

            // Distribución por estado
            var distribucion = new DistribucionEstadosDto(
                await _context.Prestamos.CountAsync(p => p.EstadoPrestamo == "Activo"),
                await _context.Prestamos.CountAsync(p => p.EstadoPrestamo == "Pagado"),
                await _context.Prestamos.CountAsync(p => p.EstadoPrestamo == "Vencido")
            );

            // Ingresos mensuales
            var ingresosMensuales = new List<IngresoMensualDto>();
            for (int i = 5; i >= 0; i--)
            {
                var mesInicio = DateTime.SpecifyKind(new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-i), DateTimeKind.Utc);
                var mesFin = mesInicio.AddMonths(1);
                
                var pagosDelMes = await _context.Pagos
                    .Include(p => p.Cuota)
                    .Where(p => p.FechaPago >= mesInicio && p.FechaPago < mesFin)
                    .ToListAsync();

                // Simplificación: dividir proporcionalmente entre capital e intereses
                var totalPagado = pagosDelMes.Sum(p => p.MontoPago);
                
                var prestamosDelMes = await _context.Prestamos
                    .Where(p => p.FechaPrestamo >= mesInicio && p.FechaPrestamo < mesFin)
                    .ToListAsync();
                
                var promedioInteres = prestamosDelMes.Any(p => p.MontoTotal > 0)
                    ? prestamosDelMes.Where(p => p.MontoTotal > 0).Average(p => p.MontoIntereses / p.MontoTotal) 
                    : 0.15m;

                var interesesGanados = totalPagado * promedioInteres;
                var capitalRecuperadoMes = totalPagado - interesesGanados;

                ingresosMensuales.Add(new IngresoMensualDto(
                    mesInicio.ToString("MMM yyyy"),
                    Math.Round(interesesGanados, 2),
                    Math.Round(capitalRecuperadoMes, 2)
                ));
            }

            // Cuotas próximas detalle
            var cuotasProximasDetalle = await _context.CuotasPrestamo
                .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cliente)
                .Where(c => c.FechaCobro.Date >= hoy && c.FechaCobro.Date <= hoy.AddDays(15) &&
                        (c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida"))
                .OrderBy(c => c.FechaCobro)
                .Take(20)
                .Select(c => new CuotaProximaDetalleDto(
                    c.Id,
                    c.PrestamoId,
                    c.Prestamo != null && c.Prestamo.Cliente != null ? c.Prestamo.Cliente.Nombre : "Sin Cliente",
                    c.FechaCobro,
                    c.SaldoPendiente,
                    c.EstadoCuota,
                    (int)(c.FechaCobro.Date - hoy).TotalDays
                ))
                .ToListAsync();

            // Capital = Capital Activo (Saldo de capital por cobrar)
            // Congelados: MontoPrestado es el saldo actual
            // Normales: Cálculo proporcional (Capital = Principal - (Pagado * (Principal/Total)))
            // ya que la columna MontoCapital no existe en la BD.
            var prestamosActivosData = await _context.Prestamos
                .Where(p => p.EstadoPrestamo == "Activo")
                .Select(p => new {
                    p.Id,
                    p.MontoPrestado,
                    p.MontoTotal,
                    p.EsCongelado,
                    TotalPagado = p.Cuotas.Sum(c => c.MontoPagado)
                })
                .ToListAsync();

            decimal capitalEnCalle = 0;
            foreach (var p in prestamosActivosData)
            {
                if (p.EsCongelado == true) 
                {
                     capitalEnCalle += p.MontoPrestado; 
                }
                else 
                {
                    // Si MontoTotal es 0 (error datos), asumimos ratio 1 (todo es capital)
                    decimal ratio = p.MontoTotal > 0 ? p.MontoPrestado / p.MontoTotal : 1;
                    decimal capitalAmortizado = p.TotalPagado * ratio;
                    capitalEnCalle += (p.MontoPrestado - capitalAmortizado);
                }
            }
            var capitalInicial = capitalEnCalle;

            return Ok(new DashboardMetricasDto(
                totalPrestado,
                totalACobrar,
                interesMes,
                gananciaTotalMes,
                prestamosActivos,
                montoPrestamosActivos,
                cantidadCuotasVencidasHoy,
                montoCuotasVencidasHoy,
                cantidadCuotasProximas,
                montoCuotasProximas,
                Math.Round(tasaPromedioInteres, 2),
                Math.Round(porcentajeMorosidad, 2),
                evolucionPrestamos,
                topClientes,
                distribucion,
                ingresosMensuales,
                cuotasProximasDetalle,
                totalCobrado,
                dineroCirculando,
                reservaDisponible,
                Math.Round(capitalInicial, 2)
            ));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error interno calculando métricas", error = ex.Message, stackDetails = ex.StackTrace });
        }
    }

    [HttpGet("metricas-cobradores")]
    [AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin)]
    public async Task<ActionResult<MetricasGeneralesDto>> GetMetricasCobradores()
    {
        try
        {
            var prestamosActivos = await _context.Prestamos
                .Include(p => p.Cobrador)
                .Where(p => p.EstadoPrestamo == "Activo")
                .ToListAsync();

            if (!prestamosActivos.Any())
            {
                return Ok(new MetricasGeneralesDto
                {
                    PromedioTasasActivos = 0,
                    CapitalFantasma = 0,
                    TotalPrestamosActivos = 0,
                    EstadisticasCobradores = new List<EstadisticasCobradorDto>()
                });
            }

            // 1. Promedio de tasas de interés de todos los préstamos activos
            var promedioTasasActivos = prestamosActivos.Average(p => p.TasaInteres);

            // 2. Capital Fantasma: suma de todo el MontoPrestado de préstamos activos
            var capitalFantasma = prestamosActivos.Sum(p => p.MontoPrestado);

            // 3. Estadísticas por cobrador
            var cobradores = prestamosActivos
                .Where(p => p.CobradorId.HasValue)
                .GroupBy(p => new { p.CobradorId, CobradorNombre = p.Cobrador!.Nombre })
                .OrderBy(g => g.Key.CobradorId)
                .Select((g, index) => new EstadisticasCobradorDto
                {
                    CobradorId = g.Key.CobradorId!.Value,
                    CobradorNombre = g.Key.CobradorNombre,
                    Alias = $"Cobrador {index + 1}", // Cobrador 1, Cobrador 2, etc.
                    PromedioTasaInteres = Math.Round(g.Average(p => p.TasaInteres), 2),
                    PromedioTasaInteresNeto = Math.Round(g.Average(p => p.TasaInteres) - 8, 2), // Restando 8%
                    CapitalTotalPrestado = Math.Round(g.Sum(p => p.MontoPrestado), 2),
                    TotalCreditosActivos = g.Count()
                })
                .ToList();

            var resultado = new MetricasGeneralesDto
            {
                PromedioTasasActivos = Math.Round(promedioTasasActivos, 2),
                CapitalFantasma = Math.Round(capitalFantasma, 2),
                TotalPrestamosActivos = prestamosActivos.Count,
                EstadisticasCobradores = cobradores
            };

            return Ok(resultado);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error calculando métricas de cobradores", error = ex.Message });
        }
    }
}
