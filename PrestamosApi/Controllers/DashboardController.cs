using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.DTOs;
using PrestamosApi.Services;

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

            // Capital = Suma del capital prestado de préstamos activos (dinero en la calle)
            // Se actualiza dinámicamente al crear préstamos o recibir pagos
            var capitalInicial = await _context.Prestamos
                .Where(p => p.EstadoPrestamo == "Activo")
                .SumAsync(p => p.MontoPrestado);

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
}
