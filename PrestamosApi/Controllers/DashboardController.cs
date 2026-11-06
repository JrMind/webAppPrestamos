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
    private readonly IMoraService _moraService;

    public DashboardController(PrestamosDbContext context, IGananciasService gananciasService, IMoraService moraService)
    {
        _context = context;
        _gananciasService = gananciasService;
        _moraService = moraService;
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
                .Where(p => !TiposPago.Cargos.Contains(p.TipoPago))
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

            // Balances por medio de pago (con fallback si la migración aún no se aplicó)
            var balanceNequi = 0m;
            var balanceEfectivo = 0m;
            try
            {
                balanceNequi = await _gananciasService.CalcularBalanceMedioAsync("Nequi");
                balanceEfectivo = await _gananciasService.CalcularBalanceMedioAsync("Efectivo");
            }
            catch (Exception)
            {
                // Columnas nuevas aún no migradas en producción — se muestran en 0 hasta aplicar la migración
            }


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
                .Where(p => p.FechaPago >= hace12Meses && !TiposPago.Cargos.Contains(p.TipoPago))
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
                    .Where(p => p.FechaPago >= mesInicio && p.FechaPago < mesFin && !TiposPago.Cargos.Contains(p.TipoPago))
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

            // CAPITAL CIRCULANTE:
            // - Préstamos normales (Activo/Vencido): suma SaldoPendiente de cada cuota por cobrar
            // - Préstamos congelados (Activo/Vencido): usa MontoPrestado porque MontoCapital
            //   en sus cuotas es 0 (el capital nunca se reduce en préstamos congelados)
            decimal capitalInicial = 0;
            try
            {
                var capitalNormales = await ScopeCuotas(_context.CuotasPrestamo, fechaScope, cobsScope)
                    .Where(c => (c.Prestamo!.EstadoPrestamo == "Activo" || c.Prestamo.EstadoPrestamo == "Vencido")
                             && c.Prestamo.EsCongelado == false
                             && (c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida" || c.EstadoCuota == "Mora"))
                    // Proporcional a lo que falta: una cuota abonada a medias no tiene pendiente todo su capital
                    .SumAsync(c => c.MontoCuota > 0 ? c.MontoCapital * c.SaldoPendiente / c.MontoCuota : c.MontoCapital);

                var capitalCongelados = await ScopePrestamos(_context.Prestamos, fechaScope, cobsScope)
                    .Where(p => (p.EstadoPrestamo == "Activo" || p.EstadoPrestamo == "Vencido")
                             && p.EsCongelado == true)
                    .SumAsync(p => p.MontoPrestado);

                capitalInicial = capitalNormales + capitalCongelados;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculando capital circulante: {ex.Message}");
                capitalInicial = 0;
            }

            // Mora acumulada de todas las cuotas vencidas (informativa: aún no cobrada)
            decimal moraAcumuladaTotal = 0;
            try
            {
                moraAcumuladaTotal = await _moraService.CalcularMoraDeCuotasAsync(
                    ScopeCuotas(_context.CuotasPrestamo, fechaScope, cobsScope));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculando mora global: {ex.Message}");
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
                CapitalInicial: (decimal)Math.Round(capitalInicial, 2),
                BalanceNequi: balanceNequi,
                BalanceEfectivo: balanceEfectivo,
                CapitalTotal: (decimal)Math.Round(capitalInicial + reservaDisponible, 2),
                MoraAcumuladaTotal: moraAcumuladaTotal
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
                    EstadisticasCobradores = new List<EstadisticasCobradorDto>(),
                    SalariosCobradores = await ObtenerSalariosCobradoresAsync(cobsScope)
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

            // 4. Intereses Exactos por Mes (Últimos 12 meses)
            var hoy = DateTime.UtcNow.Date;
            var hace12Meses = hoy.AddMonths(-11);
            var mesInicioBusqueda = DateTime.SpecifyKind(new DateTime(hace12Meses.Year, hace12Meses.Month, 1), DateTimeKind.Utc);

            var cuotasPagadasUltimosMeses = await ScopeCuotas(
                _context.CuotasPrestamo
                    .Include(c => c.Prestamo).ThenInclude(p => p!.Cliente)
                    .Include(c => c.Prestamo).ThenInclude(p => p!.Cobrador),
                fechaScope, cobsScope)
                .Where(c => c.FechaPago.HasValue
                        && c.FechaPago.Value >= mesInicioBusqueda
                        && c.MontoPagado > 0
                        && (c.EstadoCuota == "Pagada" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida"))
                .ToListAsync();

            // Cuotas programadas (por fecha de cobro) en los mismos 12 meses, para comparar programado vs pagado
            var mesFinBusqueda = DateTime.SpecifyKind(new DateTime(hoy.Year, hoy.Month, 1).AddMonths(1), DateTimeKind.Utc);
            var cuotasProgramadasUltimosMeses = await ScopeCuotas(
                _context.CuotasPrestamo
                    .Include(c => c.Prestamo).ThenInclude(p => p!.Cobrador),
                fechaScope, cobsScope)
                .Where(c => c.FechaCobro >= mesInicioBusqueda && c.FechaCobro < mesFinBusqueda)
                .ToListAsync();

            // Pagos que no pasan por las cuotas: abono a capital (congelados), mora y solo interés
            var tiposFueraDeCuota = new[] { "AbonoCapital", "Mora", "SoloInteres" };
            var otrosPagosUltimosMeses = await ScopePagos(
                _context.Pagos
                    .Include(p => p.Prestamo).ThenInclude(p => p!.Cobrador),
                fechaScope, cobsScope)
                .Where(p => tiposFueraDeCuota.Contains(p.TipoPago)
                        && p.FechaPago >= mesInicioBusqueda && p.FechaPago < mesFinBusqueda)
                .ToListAsync();

            var interesesMensuales = new List<InteresMensualDto>();
            for (int i = 11; i >= 0; i--)
            {
                var mesFoco    = DateTime.SpecifyKind(new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-i), DateTimeKind.Utc);
                var mesFocoFin = mesFoco.AddMonths(1);

        var cuotasMes = cuotasPagadasUltimosMeses
            .Where(c => c.FechaPago!.Value >= mesFoco && c.FechaPago.Value < mesFocoFin);

        // Interés proporcional a lo pagado: MontoInteres exacto de la cuota (en congelados la cuota completa es interés)
        static decimal InteresDeCuota(CuotaPrestamo c)
        {
            var proporcion = c.MontoCuota > 0 ? c.MontoPagado / c.MontoCuota : 1m;
            return c.MontoInteres * proporcion;
        }

        var prestamosMes = cuotasMes
            .GroupBy(c => c.PrestamoId)
            .Select(g =>
            {
                var p = g.First().Prestamo;
                return new InteresMensualPrestamoDto
                {
                    PrestamoId     = g.Key,
                    ClienteNombre  = p?.Cliente?.Nombre ?? $"Préstamo #{g.Key}",
                    CobradorId     = p?.CobradorId,
                    CobradorNombre = p?.Cobrador?.Nombre,
                    EsCongelado    = p?.EsCongelado == true,
                    CuotasPagadas  = g.Count(),
                    MontoPagado    = Math.Round(g.Sum(c => c.MontoPagado), 2),
                    // Lo que no es interés es capital: capital + interés = lo que el cobrador debe entregar
                    CapitalCobrado = Math.Round(g.Sum(c => c.MontoPagado) - g.Sum(InteresDeCuota), 2),
                    InteresCobrado = Math.Round(g.Sum(InteresDeCuota), 2)
                };
            })
            .Where(x => x.MontoPagado > 0)
            .OrderByDescending(x => x.InteresCobrado)
            .ToList();

        var programadoMes = cuotasProgramadasUltimosMeses
            .Where(c => c.FechaCobro >= mesFoco && c.FechaCobro < mesFocoFin)
            .GroupBy(c => c.Prestamo?.CobradorId)
            .Select(g =>
            {
                static decimal PagadoDe(CuotaPrestamo c) => Math.Min(c.MontoPagado, c.MontoCuota);
                static bool EsTerminado(CuotaPrestamo c) => c.Prestamo?.EstadoPrestamo == "Terminado";
                // Misma regla que la pantalla de Cobros para contar una cuota como pendiente/vencida
                static decimal PendienteDe(CuotaPrestamo c) =>
                    !EsTerminado(c) && c.EstadoCuota != "Pagada" && (!c.Cobrado || c.SaldoPendiente > 0.01m)
                        ? c.SaldoPendiente : 0m;
                // Lo que no se pagó ni queda pendiente: cuotas de préstamos terminados, o cuotas
                // dadas por saldadas sin abono (p. ej. congelado liquidado con abono a capital)
                static decimal RestoDe(CuotaPrestamo c) => Math.Max(0m, c.MontoCuota - PagadoDe(c) - PendienteDe(c));

                var total = g.Sum(c => c.MontoCuota);
                return new ProgramadoCobradorDto
                {
                    CobradorId        = g.Key,
                    CobradorNombre    = g.First().Prestamo?.Cobrador?.Nombre,
                    Prestamos         = g.Select(c => c.PrestamoId).Distinct().Count(),
                    Cuotas            = g.Count(),
                    CuotasPagadas     = g.Count(c => c.MontoCuota > 0 && c.MontoPagado >= c.MontoCuota),
                    CapitalProgramado = Math.Round(g.Sum(c => c.MontoCuota - c.MontoInteres), 2),
                    InteresProgramado = Math.Round(g.Sum(c => c.MontoInteres), 2),
                    TotalProgramado   = Math.Round(total, 2),
                    Pagado            = Math.Round(g.Sum(PagadoDe), 2),
                    Pendiente         = Math.Round(g.Sum(PendienteDe), 2),
                    Terminados        = Math.Round(g.Where(EsTerminado).Sum(RestoDe), 2),
                    SaldadoSinPago    = Math.Round(g.Where(c => !EsTerminado(c)).Sum(RestoDe), 2)
                };
            })
            .OrderByDescending(x => x.TotalProgramado)
            .ToList();

        var otrosPagosMes = otrosPagosUltimosMeses
            .Where(p => p.FechaPago >= mesFoco && p.FechaPago < mesFocoFin)
            .GroupBy(p => p.Prestamo?.CobradorId)
            .Select(g => new OtrosPagosCobradorDto
            {
                CobradorId     = g.Key,
                CobradorNombre = g.First().Prestamo?.Cobrador?.Nombre,
                Prestamos      = g.Select(p => p.PrestamoId).Distinct().Count(),
                AbonosCapital  = Math.Round(g.Where(p => p.TipoPago == "AbonoCapital").Sum(p => p.MontoPago), 2),
                Mora           = Math.Round(g.Where(p => p.TipoPago == "Mora").Sum(p => p.MontoPago), 2),
                SoloInteres    = Math.Round(g.Where(p => p.TipoPago == "SoloInteres").Sum(p => p.MontoPago), 2)
            })
            .ToList();

        interesesMensuales.Add(new InteresMensualDto
        {
            Mes            = mesFoco.ToString("MMM yyyy"),
            InteresCobrado = Math.Round(cuotasMes.Sum(InteresDeCuota), 2),
            Prestamos      = prestamosMes,
            Programado     = programadoMes,
            OtrosPagos     = otrosPagosMes
        });
    }
            var resultado = new MetricasGeneralesDto
            {
                PromedioTasasActivos = Math.Round(promedioTasasActivos, 2),
                CapitalFantasma = Math.Round(capitalFantasma, 2),
                TotalPrestamosActivos = prestamosActivos.Count,
                EstadisticasCobradores = cobradores,
                InteresesMensuales = interesesMensuales,
                SalariosCobradores = await ObtenerSalariosCobradoresAsync(cobsScope)
            };

            return Ok(resultado);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error calculando métricas de cobradores", error = ex.Message });
        }
    }

    /// <summary>
    /// Fondos acumulados: Sistema, Papelería y Renovación (descontados al desembolsar),
    /// Cobro de intereses (pagos de interés mensual / solo interés) y Cobro de mora.
    /// </summary>
    [HttpGet("fondos")]
    [AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin, RolUsuario.Administrador)]
    public async Task<ActionResult<List<FondoDto>>> GetFondos()
    {
        var fechaScope = IsAdministrador() ? GetFechaInicioAcceso() : null;
        var cobsScope  = IsAdministrador() ? GetCobradorIdsPermitidos() : null;

        var hoy = DateTime.UtcNow.Date;
        var inicioMes = DateTime.SpecifyKind(new DateTime(hoy.Year, hoy.Month, 1), DateTimeKind.Utc);

        var tipos = new[] { TiposPago.Sistema, TiposPago.Papeleria, TiposPago.Renovacion, TiposPago.SoloInteres, TiposPago.Mora };
        var pagos = await ScopePagos(
                _context.Pagos
                    .Include(p => p.Prestamo).ThenInclude(p => p!.Cliente)
                    .Include(p => p.Prestamo).ThenInclude(p => p!.Cobrador),
                fechaScope, cobsScope)
            .Where(p => tipos.Contains(p.TipoPago))
            .ToListAsync();

        MovimientoFondoDto Movimiento(Pago p) => new()
        {
            Fecha          = p.FechaPago,
            PrestamoId     = p.PrestamoId,
            ClienteNombre  = p.Prestamo?.Cliente?.Nombre ?? $"Préstamo #{p.PrestamoId}",
            CobradorNombre = p.Prestamo?.Cobrador?.Nombre,
            Monto          = p.MontoPago,
            MedioPago      = p.MetodoPago,
            Observaciones  = p.Observaciones
        };

        var movimientos = tipos.ToDictionary(t => t, t => pagos.Where(p => p.TipoPago == t).Select(Movimiento).ToList());

        // Préstamos anteriores a este cambio: Sistema/Renovación marcados como cobrados sin Pago registrado
        var prestamosConCargoSinPago = await ScopePrestamos(
                _context.Prestamos.Include(p => p.Cliente).Include(p => p.Cobrador),
                fechaScope, cobsScope)
            .Where(p => (p.ValorSistema > 0 && p.SistemaCobrado
                            && !p.Pagos.Any(pg => pg.TipoPago == TiposPago.Sistema))
                     || (p.ValorRenovacion > 0 && p.RenovacionCobrada
                            && !p.Pagos.Any(pg => pg.TipoPago == TiposPago.Renovacion)))
            .Select(p => new
            {
                p.Id, p.ValorSistema, p.SistemaCobrado, p.FechaSistemaCobrado,
                p.ValorRenovacion, p.RenovacionCobrada, p.FechaRenovacionCobrada, p.FechaPrestamo,
                Cliente = p.Cliente != null ? p.Cliente.Nombre : null,
                Cobrador = p.Cobrador != null ? p.Cobrador.Nombre : null,
                TienePagoSistema = p.Pagos.Any(pg => pg.TipoPago == TiposPago.Sistema),
                TienePagoRenovacion = p.Pagos.Any(pg => pg.TipoPago == TiposPago.Renovacion)
            })
            .ToListAsync();

        foreach (var p in prestamosConCargoSinPago)
        {
            if (p.ValorSistema > 0 && p.SistemaCobrado && !p.TienePagoSistema)
                movimientos[TiposPago.Sistema].Add(new MovimientoFondoDto
                {
                    Fecha = p.FechaSistemaCobrado ?? p.FechaPrestamo, PrestamoId = p.Id,
                    ClienteNombre = p.Cliente ?? $"Préstamo #{p.Id}", CobradorNombre = p.Cobrador,
                    Monto = p.ValorSistema, Observaciones = "Registro anterior (marcado como cobrado)"
                });
            if (p.ValorRenovacion > 0 && p.RenovacionCobrada && !p.TienePagoRenovacion)
                movimientos[TiposPago.Renovacion].Add(new MovimientoFondoDto
                {
                    Fecha = p.FechaRenovacionCobrada ?? p.FechaPrestamo, PrestamoId = p.Id,
                    ClienteNombre = p.Cliente ?? $"Préstamo #{p.Id}", CobradorNombre = p.Cobrador,
                    Monto = p.ValorRenovacion, Observaciones = "Registro anterior (marcado como cobrado)"
                });
        }

        FondoDto Fondo(string clave, string nombre, string tipo)
        {
            var lista = movimientos[tipo];
            return new FondoDto
            {
                Clave = clave,
                Nombre = nombre,
                Total = Math.Round(lista.Sum(m => m.Monto), 2),
                EsteMes = Math.Round(lista.Where(m => m.Fecha >= inicioMes).Sum(m => m.Monto), 2),
                CantidadMovimientos = lista.Count,
                Movimientos = lista.OrderByDescending(m => m.Fecha).Take(200).ToList()
            };
        }

        return Ok(new List<FondoDto>
        {
            Fondo("Sistema", "Sistema", TiposPago.Sistema),
            Fondo("Papeleria", "Papelería", TiposPago.Papeleria),
            Fondo("Renovacion", "Renovación", TiposPago.Renovacion),
            Fondo("Intereses", "Cobro de intereses", TiposPago.SoloInteres),
            Fondo("Mora", "Cobro de mora", TiposPago.Mora),
        });
    }

    /// <summary>
    /// Reparte el capital prestado de cada cobrador en: ya recuperado, atrasado de meses pasados,
    /// pendiente de este mes, meses futuros y congelados. Préstamos activos y vencidos, a hoy.
    /// </summary>
    [HttpGet("composicion-capital")]
    [AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin, RolUsuario.Administrador)]
    public async Task<ActionResult<List<ComposicionCapitalDto>>> GetComposicionCapital()
    {
        var fechaScope = IsAdministrador() ? GetFechaInicioAcceso() : null;
        var cobsScope  = IsAdministrador() ? GetCobradorIdsPermitidos() : null;

        var hoy = DateTime.UtcNow.Date;
        var inicioMes = DateTime.SpecifyKind(new DateTime(hoy.Year, hoy.Month, 1), DateTimeKind.Utc);
        var finMes = inicioMes.AddMonths(1);

        var prestamos = await ScopePrestamos(
                _context.Prestamos.Include(p => p.Cobrador).Include(p => p.Cuotas)
                    .Include(p => p.Pagos.Where(pg => pg.TipoPago == TiposPago.AbonoCapital)),
                fechaScope, cobsScope)
            .Where(p => p.EstadoPrestamo == "Activo" || p.EstadoPrestamo == "Vencido")
            .ToListAsync();

        var filas = prestamos
            .GroupBy(p => new { p.CobradorId, Nombre = p.Cobrador != null ? p.Cobrador.Nombre : "Sin cobrador" })
            .Select(g =>
            {
                decimal recuperado = 0, atrasado = 0, esteMes = 0, futuros = 0, congelados = 0;
                foreach (var p in g)
                {
                    if (p.EsCongelado)
                    {
                        congelados += p.MontoPrestado;                       // Capital actual
                        recuperado += p.Pagos.Sum(pg => pg.MontoPago);      // Abonos a capital
                        continue;
                    }
                    foreach (var c in p.Cuotas)
                    {
                        // Cuota pagada (o dada por saldada): todo su capital está recuperado.
                        // Si no, se recupera en proporción a lo abonado y el resto queda pendiente.
                        var pagadoCapital = c.EstadoCuota == "Pagada"
                            ? c.MontoCapital
                            : c.MontoCuota > 0 ? c.MontoCapital * Math.Min(c.MontoPagado / c.MontoCuota, 1m) : 0m;
                        var pendienteCapital = c.MontoCapital - pagadoCapital;
                        recuperado += pagadoCapital;
                        if (pendienteCapital <= 0) continue;
                        if (c.FechaCobro < inicioMes) atrasado += pendienteCapital;
                        else if (c.FechaCobro < finMes) esteMes += pendienteCapital;
                        else futuros += pendienteCapital;
                    }
                }
                return new ComposicionCapitalDto
                {
                    CobradorId = g.Key.CobradorId,
                    CobradorNombre = g.Key.Nombre,
                    Prestamos = g.Count(),
                    Recuperado = Math.Round(recuperado, 0),
                    AtrasadoMesesPasados = Math.Round(atrasado, 0),
                    PendienteEsteMes = Math.Round(esteMes, 0),
                    MesesFuturos = Math.Round(futuros, 0),
                    Congelados = Math.Round(congelados, 0),
                    CapitalPrestado = Math.Round(recuperado + atrasado + esteMes + futuros + congelados, 0)
                };
            })
            .OrderBy(f => f.CobradorId == null)
            .ThenByDescending(f => f.CapitalPrestado)
            .ToList();

        return Ok(filas);
    }

    /// <summary>
    /// Préstamos vencidos (se muestran en Cobros): sin cuotas este mes ni posteriores y con saldo de meses pasados.
    /// </summary>
    [HttpGet("prestamos-vencidos")]
    [AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin, RolUsuario.Administrador)]
    public async Task<ActionResult<List<PrestamoVencidoDto>>> GetPrestamosVencidos()
    {
        var fechaScope = IsAdministrador() ? GetFechaInicioAcceso() : null;
        var cobsScope  = IsAdministrador() ? GetCobradorIdsPermitidos() : null;
        return Ok(await ObtenerPrestamosVencidosAsync(fechaScope, cobsScope));
    }

    // Préstamos vencidos: todas sus cuotas tenían fecha de cobro antes de este mes (no le queda
    // ninguna cuota de este mes ni posterior) y todavía deben saldo de esas cuotas pasadas.
    private async Task<List<PrestamoVencidoDto>> ObtenerPrestamosVencidosAsync(DateTime? fechaScope, List<int>? cobsScope)
    {
        var hoy = DateTime.UtcNow.Date;
        var inicioMes = DateTime.SpecifyKind(new DateTime(hoy.Year, hoy.Month, 1), DateTimeKind.Utc);

        var prestamos = await ScopePrestamos(
                _context.Prestamos
                    .Include(p => p.Cliente)
                    .Include(p => p.Cobrador)
                    .Include(p => p.Cuotas),
                fechaScope, cobsScope)
            .Where(p => p.EstadoPrestamo == "Activo" || p.EstadoPrestamo == "Vencido")
            .Where(p => p.Cuotas.Any() && p.Cuotas.All(c => c.FechaCobro < inicioMes))
            .Where(p => p.Cuotas.Any(c => c.SaldoPendiente > 0))
            .ToListAsync();

        return prestamos
            .Select(p =>
            {
                var pendientes = p.Cuotas.Where(c => c.SaldoPendiente > 0).ToList();
                // Parte del saldo pendiente que es interés, en la misma proporción que la cuota
                decimal InteresPendiente(CuotaPrestamo c) =>
                    c.MontoCuota > 0 ? c.MontoInteres * c.SaldoPendiente / c.MontoCuota : 0m;
                var masAntigua = pendientes.Min(c => c.FechaCobro);
                var saldo = pendientes.Sum(c => c.SaldoPendiente);
                var interes = pendientes.Sum(InteresPendiente);
                return new PrestamoVencidoDto
                {
                    PrestamoId              = p.Id,
                    ClienteNombre           = p.Cliente?.Nombre ?? $"Préstamo #{p.Id}",
                    CobradorId              = p.CobradorId,
                    CobradorNombre          = p.Cobrador?.Nombre,
                    EsCongelado             = p.EsCongelado,
                    UltimaFechaCobro        = p.Cuotas.Max(c => c.FechaCobro),
                    CuotaMasAntiguaSinPagar = masAntigua,
                    DiasVencido             = Math.Max(0, (hoy - masAntigua.Date).Days),
                    CuotasPendientes        = pendientes.Count,
                    SaldoPendiente          = Math.Round(saldo, 2),
                    CapitalPendiente        = Math.Round(saldo - interes, 2),
                    InteresPendiente        = Math.Round(interes, 2)
                };
            })
            .OrderByDescending(v => v.DiasVencido)
            .ToList();
    }

    // Salario mensual de cada cobrador, guardado en ConfiguracionesSistema con la clave "SalarioCobrador:{id}"
    // (sin cambio de esquema). Se usa para ver si los intereses que cobró en el mes cubren su salario.
    private const string PrefijoSalarioCobrador = "SalarioCobrador:";

    private async Task<List<SalarioCobradorDto>> ObtenerSalariosCobradoresAsync(List<int>? cobsScope)
    {
        var cobradores = await _context.Usuarios
            .Where(u => u.Rol == RolUsuario.Cobrador && u.Activo)
            .Where(u => cobsScope == null || cobsScope.Contains(u.Id))
            .OrderBy(u => u.Nombre)
            .Select(u => new { u.Id, u.Nombre })
            .ToListAsync();

        var salarios = (await _context.ConfiguracionesSistema
                .Where(c => c.Clave.StartsWith(PrefijoSalarioCobrador))
                .ToListAsync())
            .ToDictionary(c => c.Clave, c => decimal.TryParse(c.Valor, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m);

        return cobradores.Select(c => new SalarioCobradorDto
        {
            CobradorId = c.Id,
            CobradorNombre = c.Nombre,
            SalarioMensual = salarios.GetValueOrDefault($"{PrefijoSalarioCobrador}{c.Id}")
        }).ToList();
    }

    [HttpPut("salarios-cobradores/{cobradorId:int}")]
    [AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin)]
    public async Task<IActionResult> ActualizarSalarioCobrador(int cobradorId, ActualizarSalarioCobradorDto dto)
    {
        if (dto.SalarioMensual < 0)
            return BadRequest(new { message = "El salario no puede ser negativo" });

        var cobrador = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == cobradorId && u.Rol == RolUsuario.Cobrador);
        if (cobrador == null)
            return NotFound(new { message = "Cobrador no encontrado" });

        var clave = $"{PrefijoSalarioCobrador}{cobradorId}";
        var valor = dto.SalarioMensual.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var config = await _context.ConfiguracionesSistema.FirstOrDefaultAsync(c => c.Clave == clave);
        if (config == null)
        {
            _context.ConfiguracionesSistema.Add(new ConfiguracionSistema
            {
                Clave = clave,
                Valor = valor,
                FechaActualizacion = DateTime.UtcNow,
                Descripcion = $"Salario mensual del cobrador {cobrador.Nombre}"
            });
        }
        else
        {
            config.Valor = valor;
            config.FechaActualizacion = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(new SalarioCobradorDto { CobradorId = cobradorId, CobradorNombre = cobrador.Nombre, SalarioMensual = dto.SalarioMensual });
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
