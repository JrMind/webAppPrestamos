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
public class DashboardController : BaseApiController
{
    private readonly PrestamosDbContext _context;
    private readonly IGananciasService _gananciasService;

    public DashboardController(PrestamosDbContext context, IGananciasService gananciasService)
    {
        _context = context;
        _gananciasService = gananciasService;
    }

    // Aplica filtros de scope (fecha y cobradores) a queries de Prestamos
    private IQueryable<Prestamo> ScopePrestamos(IQueryable<Prestamo> q, DateTime? fechaScope, List<int>? cobsScope)
    {
        if (fechaScope.HasValue)
            q = q.Where(p => p.FechaPrestamo >= fechaScope.Value);
        if (cobsScope != null)
            q = q.Where(p => p.CobradorId.HasValue && cobsScope.Contains(p.CobradorId.Value));
        return q;
    }

    // Aplica filtros de scope a queries de CuotasPrestamo
    private IQueryable<CuotaPrestamo> ScopeCuotas(IQueryable<CuotaPrestamo> q, DateTime? fechaScope, List<int>? cobsScope)
    {
        if (fechaScope.HasValue)
            q = q.Where(c => c.Prestamo!.FechaPrestamo >= fechaScope.Value);
        if (cobsScope != null)
            q = q.Where(c => c.Prestamo!.CobradorId.HasValue && cobsScope.Contains(c.Prestamo!.CobradorId.Value));
        return q;
    }

    // Aplica filtros de scope a queries de Pagos
    private IQueryable<Pago> ScopePagos(IQueryable<Pago> q, DateTime? fechaScope, List<int>? cobsScope)
    {
        if (fechaScope.HasValue)
            q = q.Where(p => p.Prestamo!.FechaPrestamo >= fechaScope.Value);
        if (cobsScope != null)
            q = q.Where(p => p.Prestamo!.CobradorId.HasValue && cobsScope.Contains(p.Prestamo!.CobradorId.Value));
        return q;
    }

    [HttpGet("metricas")]
    public async Task<ActionResult<DashboardMetricasDto>> GetMetricas()
    {
        try
        {
            // Scope para rol Administrador
            var fechaScope = IsAdministrador() ? GetFechaInicioAcceso() : null;
            var cobsScope  = IsAdministrador() ? GetCobradorIdsPermitidos() : null;

            // Usar UTC para PostgreSQL
            var hoy = DateTime.UtcNow.Date;
            var en7Dias = hoy.AddDays(7);

            // KPIs básicos
            var totalPrestado = await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope)
                .Where(p => p.EstadoPrestamo == "Activo")
                .SumAsync(p => p.MontoPrestado);
            
            // Total a cobrar = suma de saldos pendientes de cuotas de préstamos NORMALES activos
            var totalACobrar = await ScopeCuotas(_context.CuotasPrestamo.Include(c => c.Prestamo), fechaScope, cobsScope)
                .Where(c => c.Prestamo!.EstadoPrestamo == "Activo" && c.Prestamo.EsCongelado == false)
                .SumAsync(c => c.SaldoPendiente);

            // Nuevos KPIs de Congelados
            var capitalCongelado = await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope)
                .Where(p => p.EsCongelado == true)
                .SumAsync(p => p.MontoPrestado);

            // Renta de Congelados Mes (Sumatoria del valor cuota de cada préstamo congelado activo)
            var rentaCongeladosMes = await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope)
                .Where(p => p.EsCongelado == true && p.EstadoPrestamo == "Activo")
                .SumAsync(p => p.MontoCuota);

            var prestamosActivos = await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope)
                .Where(p => p.EstadoPrestamo == "Activo")
                .CountAsync();

            var montoPrestamosActivos = await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope)
                .Where(p => p.EstadoPrestamo == "Activo")
                .SumAsync(p => p.MontoPrestado);

            // Flujo de Capital
            var totalCobrado = await ScopePagos(_context.Pagos.Include(p => p.Prestamo), fechaScope, cobsScope)
                .SumAsync(p => p.MontoPago);

            // Capital de préstamos activos que aún no se ha recuperado
            var capitalPrestamosActivos = await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope)
                .Where(p => p.EstadoPrestamo == "Activo")
                .SumAsync(p => p.MontoPrestado);
            var totalAPagarActivos = await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope)
                .Where(p => p.EstadoPrestamo == "Activo")
                .SumAsync(p => p.MontoTotal);
            
            // Proporción de capital en cada pago (capital / total)
            var proporcionCapital = totalAPagarActivos > 0 ? capitalPrestamosActivos / totalAPagarActivos : 0;
            var capitalRecuperado = totalCobrado * proporcionCapital;
            var dineroCirculando = capitalPrestamosActivos - capitalRecuperado;
            if (dineroCirculando < 0) dineroCirculando = 0;
            
            // Usar el método completo de GananciasService para calcular la reserva correctamente
            var reservaDisponible = await _gananciasService.CalcularReservaDisponibleAsync();


            // Cuotas próximos 7 días (solo de préstamos activos o vencidos)
            var cuotasProximas = await ScopeCuotas(_context.CuotasPrestamo.Include(c => c.Prestamo), fechaScope, cobsScope)
                .Where(c => c.FechaCobro.Date > hoy && c.FechaCobro.Date <= en7Dias &&
                        (c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial") &&
                        (c.Prestamo!.EstadoPrestamo == "Activo" || c.Prestamo.EstadoPrestamo == "Vencido"))
                .ToListAsync();
            var cantidadCuotasProximas = cuotasProximas.Count;
            var montoCuotasProximas = cuotasProximas.Sum(c => c.SaldoPendiente);

            // Tasa promedio
            var tasaPromedioInteres = await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope)
                .Where(p => p.EstadoPrestamo == "Activo")
                .AverageAsync(p => (decimal?)p.TasaInteres) ?? 0;

            // Morosidad (solo cuotas de préstamos activos o vencidos)
            var totalCuotas = await ScopeCuotas(_context.CuotasPrestamo, fechaScope, cobsScope)
                .Where(c => c.Prestamo!.EstadoPrestamo == "Activo" || c.Prestamo.EstadoPrestamo == "Vencido")
                .CountAsync();
            var cuotasVencidas = await ScopeCuotas(_context.CuotasPrestamo, fechaScope, cobsScope)
                .Where(c => c.EstadoCuota == "Vencida" &&
                           (c.Prestamo!.EstadoPrestamo == "Activo" || c.Prestamo.EstadoPrestamo == "Vencido"))
                .CountAsync();
            var porcentajeMorosidad = totalCuotas > 0 ? (decimal)cuotasVencidas / totalCuotas * 100 : 0;

            // Evolución préstamos (últimos 12 meses)
            var hace12Meses = hoy.AddMonths(-12);
            var evolucion = await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope)
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

            var pagosEvolucion = await ScopePagos(_context.Pagos.Include(p => p.Prestamo), fechaScope, cobsScope)
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

            // Top 10 clientes (dentro del scope)
            var prestamosParaTop = await ScopePrestamos(_context.Prestamos.Include(p => p.Cliente), fechaScope, cobsScope)
                .Select(p => new { p.ClienteId, ClienteNombre = p.Cliente!.Nombre, p.MontoPrestado })
                .ToListAsync();
            var topClientes = prestamosParaTop
                .GroupBy(p => new { p.ClienteId, p.ClienteNombre })
                .OrderByDescending(g => g.Sum(p => p.MontoPrestado))
                .Take(10)
                .Select(g => new TopClienteDto(g.Key.ClienteNombre, g.Sum(p => p.MontoPrestado)))
                .ToList();

            // Distribución por estado (dentro del scope)
            var distribucion = new DistribucionEstadosDto(
                await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope).CountAsync(p => p.EstadoPrestamo == "Activo"),
                await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope).CountAsync(p => p.EstadoPrestamo == "Pagado"),
                await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope).CountAsync(p => p.EstadoPrestamo == "Vencido"),
                await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope).CountAsync(p => p.EstadoPrestamo == "Terminado")
            );

            // Ingresos mensuales
            var ingresosMensuales = new List<IngresoMensualDto>();
            for (int i = 5; i >= 0; i--)
            {
                var mesInicio = DateTime.SpecifyKind(new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-i), DateTimeKind.Utc);
                var mesFin = mesInicio.AddMonths(1);
                
                var pagosDelMes = await ScopePagos(_context.Pagos.Include(p => p.Cuota).Include(p => p.Prestamo), fechaScope, cobsScope)
                    .Where(p => p.FechaPago >= mesInicio && p.FechaPago < mesFin)
                    .ToListAsync();

                // Simplificación: dividir proporcionalmente entre capital e intereses
                var totalPagado = pagosDelMes.Sum(p => p.MontoPago);

                var prestamosDelMes = await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope)
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

            // Cuotas próximas detalle (solo de préstamos activos o vencidos)
            var cuotasProximasDetalle = await ScopeCuotas(
                    _context.CuotasPrestamo.Include(c => c.Prestamo).ThenInclude(p => p!.Cliente),
                    fechaScope, cobsScope)
                .Where(c => c.FechaCobro.Date >= hoy && c.FechaCobro.Date <= hoy.AddDays(15) &&
                        (c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida") &&
                        (c.Prestamo!.EstadoPrestamo == "Activo" || c.Prestamo.EstadoPrestamo == "Vencido"))
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

            // NUEVO CÁLCULO DE CAPITAL EN LA CALLE / CIRCULANTE (EXACTO Y DIRECTO A BD)
            decimal capitalInicial = 0;
            try
            {
                var capitalCongelados = await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope)
                    .Where(p => p.EstadoPrestamo == "Activo" && p.EsCongelado == true)
                    .SumAsync(p => p.MontoPrestado);

                var capitalNormales = await ScopeCuotas(_context.CuotasPrestamo, fechaScope, cobsScope)
                    .Where(c => c.Prestamo!.EstadoPrestamo == "Activo"
                             && c.Prestamo.EsCongelado == false
                             && (c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida" || c.EstadoCuota == "Mora"))
                    .SumAsync(c => c.MontoCapital);

                capitalInicial = capitalCongelados + capitalNormales;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculando capital exacto: {ex.Message}");
                capitalInicial = 0;
            }

            return Ok(new DashboardMetricasDto(
                TotalPrestado: totalPrestado,
                TotalACobrar: totalACobrar,
                PrestamosActivos: prestamosActivos,
                MontoPrestamosActivos: montoPrestamosActivos,
                CuotasProximas7Dias: cantidadCuotasProximas,
                MontoCuotasProximas7Dias: montoCuotasProximas,
                TasaPromedioInteres: (decimal)Math.Round(tasaPromedioInteres, 2),
                PorcentajeMorosidad: (decimal)Math.Round(porcentajeMorosidad, 2),
                EvolucionPrestamos: evolucionPrestamos,
                TopClientes: topClientes,
                DistribucionEstados: distribucion,
                IngresosMensuales: ingresosMensuales,
                CuotasProximasDetalle: cuotasProximasDetalle,
                CapitalCongelado: capitalCongelado,
                RentaCongeladosMes: rentaCongeladosMes,
                DineroCirculando: dineroCirculando,
                ReservaDisponible: reservaDisponible,
                CapitalInicial: (decimal)Math.Round(capitalInicial, 2)
            ));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error interno calculando métricas", error = ex.Message, stackDetails = ex.StackTrace });
        }
    }

    [HttpGet("metricas-cobradores")]
    [AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin, RolUsuario.Administrador)]
    public async Task<ActionResult<MetricasGeneralesDto>> GetMetricasCobradores()
    {
        try
        {
            var fechaScope = IsAdministrador() ? GetFechaInicioAcceso() : null;
            var cobsScope  = IsAdministrador() ? GetCobradorIdsPermitidos() : null;

            var prestamosActivos = await ScopePrestamos(_context.Prestamos.Include(p => p.Cobrador), fechaScope, cobsScope)
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

    [HttpGet("mantenimiento/reparar-prestamos")]
    public async Task<IActionResult> RepararStatusPrestamosPagados()
    {
        try
        {
            // Busca préstamos "Activos"
            var prestamosReparar = await _context.Prestamos
                .Include(p => p.Cuotas)
                .Where(p => p.EstadoPrestamo == "Activo")
                .ToListAsync();

            int arreglados = 0;

            foreach (var prestamo in prestamosReparar)
            {
                // Si TODAS sus cuotas existen y están pagadas, el préstamo en realidad está finalizado
                if (prestamo.Cuotas != null && prestamo.Cuotas.Any() && prestamo.Cuotas.All(c => c.EstadoCuota == "Pagada"))
                {
                    prestamo.EstadoPrestamo = "Pagado";
                    arreglados++;
                }
            }

            if (arreglados > 0)
            {
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = $"Se encontraron y repararon {arreglados} préstamos que estaban en 'Activo' pero realmente ya tenían todas sus cuotas en 'Pagada'." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error reparando", error = ex.Message });
        }
    }
}
