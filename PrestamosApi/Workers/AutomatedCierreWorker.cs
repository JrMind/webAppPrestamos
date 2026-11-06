using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;
using PrestamosApi.Services;

namespace PrestamosApi.Workers;

public class AutomatedCierreWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutomatedCierreWorker> _logger;
    private const int CHECK_INTERVAL_HOURS = 1;

    public AutomatedCierreWorker(IServiceProvider serviceProvider, ILogger<AutomatedCierreWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Automated Cierre Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ActualizarEstadosPrestamosAsync();

                var now = DateTime.UtcNow;

                // Ejecutar si estamos en los primeros 5 días del mes (para asegurar que corra si hubo downtime)
                // Y revisar si el mes ANTERIOR ya se cerró
                if (now.Day <= 5)
                {
                    var prevMonthDate = now.AddMonths(-1);
                    int targetMes = prevMonthDate.Month;
                    int targetAnio = prevMonthDate.Year;

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var cierreService = scope.ServiceProvider.GetRequiredService<ICierreMesService>();
                        await cierreService.EjecutarCierreMes(targetMes, targetAnio, force: false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico en AutomatedCierreWorker");
            }

            await Task.Delay(TimeSpan.FromHours(CHECK_INTERVAL_HOURS), stoppingToken);
        }
    }

    /// <summary>
    /// Corrige el EstadoPrestamo de todos los préstamos según su FechaVencimiento real.
    /// Un préstamo es "Vencido" solo cuando su última cuota ya pasó; de lo contrario es "Activo".
    /// </summary>
    public async Task ActualizarEstadosPrestamosAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PrestamosDbContext>();
        var prestamoService = scope.ServiceProvider.GetRequiredService<IPrestamoService>();
        var hoyUtc = DateTime.UtcNow.Date;

        // Cada paso se guarda por separado: si uno falla (p. ej. un congelado con datos raros),
        // los demás igual se aplican y el error queda en el log con su causa real.

        // 1) Préstamos normales sin nada pendiente -> Pagado
        var normales = await context.Prestamos
            .Include(p => p.Cuotas)
            .Where(p => (p.EstadoPrestamo == "Activo" || p.EstadoPrestamo == "Vencido") && !p.EsCongelado)
            .ToListAsync();
        var marcadosPagados = 0;
        foreach (var p in normales.Where(p => p.Cuotas.Count > 0
                     && p.Cuotas.All(c => c.EstadoCuota == "Pagada" || c.SaldoPendiente <= 0.01m)))
        {
            // Todas cobradas (p. ej. con "Marcar cobrado") o con saldo 0 en otro estado
            foreach (var c in p.Cuotas.Where(c => c.EstadoCuota != "Pagada"))
            {
                c.EstadoCuota = "Pagada";
                c.SaldoPendiente = 0;
            }
            p.EstadoPrestamo = "Pagado";
            marcadosPagados++;
        }
        if (await GuardarAsync(context, "marcar Pagado los préstamos sin saldo") && marcadosPagados > 0)
            _logger.LogInformation("Préstamos sin saldo marcados Pagado: {Pagados}", marcadosPagados);

        // 2) Congelados: generar la cuota de interés que falte y alinear su vencimiento (uno por uno)
        var congelados = await context.Prestamos
            .Include(p => p.Cuotas)
            .Where(p => (p.EstadoPrestamo == "Activo" || p.EstadoPrestamo == "Vencido") && p.EsCongelado)
            .ToListAsync();
        var congeladosReparados = 0;
        foreach (var p in congelados)
        {
            CuotaPrestamo? nueva;
            try
            {
                nueva = prestamoService.GenerarCuotaCongeladaSiFalta(p);
                if (nueva != null)
                {
                    context.CuotasPrestamo.Add(nueva);
                }
                else
                {
                    // El vencimiento de un congelado es su cuota pendiente
                    var pendiente = p.Cuotas.Where(c => c.EstadoCuota != "Pagada").OrderBy(c => c.FechaCobro).FirstOrDefault();
                    if (pendiente != null && p.FechaVencimiento.Date != pendiente.FechaCobro.Date)
                        p.FechaVencimiento = pendiente.FechaCobro;
                }
            }
            catch (Exception ex)
            {
                // Un congelado con datos raros (fechas, frecuencia) no debe frenar a los demás
                _logger.LogError(ex, "⚠️ No se pudo calcular la cuota del congelado #{Id}: {Causa}", p.Id, ex.Message);
                Console.WriteLine($"⚠️ Estados de préstamos: congelado #{p.Id} omitido: {ex.Message}");
                continue;
            }
            if (!context.ChangeTracker.HasChanges()) continue;
            if (await GuardarAsync(context, $"reparar el préstamo congelado #{p.Id}") && nueva != null)
                congeladosReparados++;
        }
        if (congeladosReparados > 0)
            _logger.LogInformation("Congelados con nueva cuota de interés: {Congelados}", congeladosReparados);

        // 3) Vencido solo si todavía debe algo; reactivar los Vencido cuyo vencimiento aún no pasó
        var prestamosAVencer = await context.Prestamos
            .Where(p => p.EstadoPrestamo == "Activo" && p.FechaVencimiento < hoyUtc
                     && (p.EsCongelado || p.Cuotas.Any(c => c.SaldoPendiente > 0.01m)))
            .ToListAsync();
        foreach (var p in prestamosAVencer)
            p.EstadoPrestamo = "Vencido";

        var prestamosAReactivar = await context.Prestamos
            .Where(p => p.EstadoPrestamo == "Vencido" && p.FechaVencimiento >= hoyUtc)
            .ToListAsync();
        foreach (var p in prestamosAReactivar)
            p.EstadoPrestamo = "Activo";

        if ((prestamosAVencer.Any() || prestamosAReactivar.Any())
            && await GuardarAsync(context, "actualizar Vencido/Activo"))
            _logger.LogInformation(
                "Estados préstamos actualizados: {Vencidos} marcados Vencido, {Reactivados} reactivados a Activo",
                prestamosAVencer.Count, prestamosAReactivar.Count);
    }

    /// <summary>
    /// Guarda los cambios pendientes. Si falla, registra la causa real (el error interno de la base
    /// de datos, no solo "An error occurred while saving") y descarta esos cambios para no arrastrarlos
    /// al siguiente paso.
    /// </summary>
    private async Task<bool> GuardarAsync(PrestamosDbContext context, string paso)
    {
        if (!context.ChangeTracker.HasChanges()) return true;
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            var causa = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "⚠️ No se pudo {Paso}: {Causa}", paso, causa);
            Console.WriteLine($"⚠️ Estados de préstamos: no se pudo {paso}: {causa}");
            // Descartar solo los cambios que fallaron; el resto de entidades sigue rastreado
            foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList())
            {
                if (entry.State == EntityState.Added)
                    entry.State = EntityState.Detached;
                else
                {
                    entry.CurrentValues.SetValues(entry.OriginalValues);
                    entry.State = EntityState.Unchanged;
                }
            }
            return false;
        }
    }
}
