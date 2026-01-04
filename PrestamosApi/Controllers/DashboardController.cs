using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.DTOs;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("[controller]")]
public class DashboardController : ControllerBase
{
    private readonly PrestamosDbContext _context;

    public DashboardController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet("metricas")]
    public async Task<ActionResult<DashboardMetricasDto>> GetMetricas()
    {
        var hoy = DateTime.Today;
        var en7Dias = hoy.AddDays(7);

        // KPIs básicos
        var totalPrestado = await _context.Prestamos.SumAsync(p => p.MontoPrestado);
        
        var totalACobrar = await _context.Prestamos
            .Where(p => p.EstadoPrestamo == "Activo")
            .SumAsync(p => p.MontoTotal);

        var totalGanadoIntereses = await _context.Prestamos
            .Where(p => p.EstadoPrestamo == "Pagado")
            .SumAsync(p => p.MontoIntereses);

        var prestamosActivos = await _context.Prestamos
            .Where(p => p.EstadoPrestamo == "Activo")
            .CountAsync();

        var montoPrestamosActivos = await _context.Prestamos
            .Where(p => p.EstadoPrestamo == "Activo")
            .SumAsync(p => p.MontoPrestado);

        // Flujo de Capital
        var totalCobrado = await _context.Pagos.SumAsync(p => p.MontoPago);
        // Dinero circulando = Capital prestado que aún no se ha recuperado
        var dineroCirculando = totalPrestado - totalCobrado;
        if (dineroCirculando < 0) dineroCirculando = 0;
        // Reserva disponible = Cuotas cobradas menos el capital prestado = dinero disponible para prestar
        var reservaDisponible = totalCobrado - totalPrestado;
        if (reservaDisponible < 0) reservaDisponible = 0;

        // Cuotas vencidas hoy
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
                Fecha = new DateTime(g.Key.Year, g.Key.Month, 1),
                MontoPrestado = g.Sum(p => p.MontoPrestado)
            })
            .OrderBy(e => e.Fecha)
            .ToListAsync();

        var pagosEvolucion = await _context.Pagos
            .Where(p => p.FechaPago >= hace12Meses)
            .GroupBy(p => new { p.FechaPago.Year, p.FechaPago.Month })
            .Select(g => new 
            {
                Fecha = new DateTime(g.Key.Year, g.Key.Month, 1),
                MontoCobrado = g.Sum(p => p.MontoPago)
            })
            .OrderBy(e => e.Fecha)
            .ToListAsync();

        decimal acumuladoPrestado = 0;
        decimal acumuladoCobrado = 0;
        var evolucionPrestamos = new List<EvolucionPrestamosDto>();
        
        for (var fecha = hace12Meses; fecha <= hoy; fecha = fecha.AddMonths(1))
        {
            var prestadoMes = evolucion.FirstOrDefault(e => e.Fecha.Year == fecha.Year && e.Fecha.Month == fecha.Month)?.MontoPrestado ?? 0;
            var cobradoMes = pagosEvolucion.FirstOrDefault(e => e.Fecha.Year == fecha.Year && e.Fecha.Month == fecha.Month)?.MontoCobrado ?? 0;
            
            acumuladoPrestado += prestadoMes;
            acumuladoCobrado += cobradoMes;
            
            evolucionPrestamos.Add(new EvolucionPrestamosDto(fecha, acumuladoPrestado, acumuladoCobrado));
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
            var mesInicio = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-i);
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
            
            var promedioInteres = prestamosDelMes.Any() 
                ? prestamosDelMes.Average(p => p.MontoIntereses / p.MontoTotal) 
                : 0.15m;

            var interesesGanados = totalPagado * promedioInteres;
            var capitalRecuperado = totalPagado - interesesGanados;

            ingresosMensuales.Add(new IngresoMensualDto(
                mesInicio.ToString("MMM yyyy"),
                Math.Round(interesesGanados, 2),
                Math.Round(capitalRecuperado, 2)
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
                c.Prestamo!.Cliente!.Nombre,
                c.FechaCobro,
                c.SaldoPendiente,
                c.EstadoCuota,
                (int)(c.FechaCobro.Date - hoy).TotalDays
            ))
            .ToListAsync();

        return Ok(new DashboardMetricasDto(
            totalPrestado,
            totalACobrar,
            totalGanadoIntereses,
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
            reservaDisponible
        ));
    }
}
