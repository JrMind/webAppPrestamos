using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
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
        var hoyUtc = DateTime.UtcNow.Date;

        var prestamosAVencer = await context.Prestamos
            .Where(p => p.EstadoPrestamo == "Activo" && p.FechaVencimiento < hoyUtc)
            .ToListAsync();

        foreach (var p in prestamosAVencer)
            p.EstadoPrestamo = "Vencido";

        // Revertir préstamos marcados "Vencido" cuya fecha de vencimiento aún no ha pasado
        var prestamosAReactivar = await context.Prestamos
            .Where(p => p.EstadoPrestamo == "Vencido" && p.FechaVencimiento >= hoyUtc)
            .ToListAsync();

        foreach (var p in prestamosAReactivar)
            p.EstadoPrestamo = "Activo";

        if (prestamosAVencer.Any() || prestamosAReactivar.Any())
        {
            await context.SaveChangesAsync();
            _logger.LogInformation(
                "Estados préstamos actualizados: {Vencidos} marcados Vencido, {Reactivados} reactivados a Activo",
                prestamosAVencer.Count, prestamosAReactivar.Count);
        }
    }
}
