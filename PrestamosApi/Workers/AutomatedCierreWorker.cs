using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
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
                        // Ejecutar sin 'force', el servicio chequeará si ya existe en el log
                        await cierreService.EjecutarCierreMes(targetMes, targetAnio, force: false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico en AutomatedCierreWorker");
            }

            // Esperar 1 hora antes de la siguiente verificación
            await Task.Delay(TimeSpan.FromHours(CHECK_INTERVAL_HOURS), stoppingToken);
        }
    }
}
