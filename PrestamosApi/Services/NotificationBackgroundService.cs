using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Services;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public NotificationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotificationBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servicio de notificaciones iniciado");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var colombiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
                var colombiaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, colombiaTimeZone);
                
                var sendHour = int.Parse(_configuration["Notifications:SendHour"] ?? "8");
                var sendMinute = int.Parse(_configuration["Notifications:SendMinute"] ?? "0");

                // Calcular próxima ejecución a las 8:00 AM Colombia
                var nextRun = new DateTime(colombiaTime.Year, colombiaTime.Month, colombiaTime.Day, sendHour, sendMinute, 0);
                if (colombiaTime >= nextRun)
                {
                    nextRun = nextRun.AddDays(1);
                }

                var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(nextRun, colombiaTimeZone);
                var delay = nextRunUtc - DateTime.UtcNow;

                _logger.LogInformation("Próxima notificación programada para: {NextRun} Colombia ({Delay})", nextRun, delay);

                await Task.Delay(delay, stoppingToken);

                await SendDailyNotificationsAsync();
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el servicio de notificaciones");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task SendDailyNotificationsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PrestamosDbContext>();
        var twilioService = scope.ServiceProvider.GetRequiredService<ITwilioService>();

        var colombiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, colombiaTimeZone).Date;
        var yesterday = today.AddDays(-1);

        // Cuotas cobradas ayer
        var cuotasCobradasAyer = await context.CuotasPrestamo
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cliente)
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cobrador)
            .Where(c => c.FechaPago.HasValue && c.FechaPago.Value.Date == yesterday && c.Cobrado)
            .ToListAsync();

        // Cuotas a cobrar hoy
        var cuotasHoy = await context.CuotasPrestamo
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cliente)
            .Include(c => c.Prestamo)
                .ThenInclude(p => p!.Cobrador)
            .Where(c => c.FechaCobro.Date == today && c.EstadoCuota != "Pagada")
            .ToListAsync();

        // Obtener socios y cobradores activos
        var destinatarios = await context.Usuarios
            .Where(u => u.Activo && u.Telefono != null && (u.Rol == RolUsuario.Socio || u.Rol == RolUsuario.Cobrador))
            .ToListAsync();

        var montoRecaudadoAyer = cuotasCobradasAyer.Sum(c => c.MontoPagado);
        var montoACobrarHoy = cuotasHoy.Sum(c => c.SaldoPendiente);

        // Mensaje para socios
        var mensajeSocios = $"📊 Resumen Diario PrestamosApp\n" +
            $"💰 Recaudado ayer: ${montoRecaudadoAyer:N0}\n" +
            $"📅 Por cobrar hoy: ${montoACobrarHoy:N0}\n" +
            $"📝 Cuotas pendientes: {cuotasHoy.Count}";

        foreach (var socio in destinatarios.Where(d => d.Rol == RolUsuario.Socio))
        {
            if (!string.IsNullOrEmpty(socio.Telefono))
            {
                await twilioService.SendSmsAsync(socio.Telefono, mensajeSocios);
            }
        }

        // Mensaje personalizado para cobradores
        foreach (var cobrador in destinatarios.Where(d => d.Rol == RolUsuario.Cobrador))
        {
            var cuotasCobrador = cuotasHoy.Where(c => c.Prestamo?.CobradorId == cobrador.Id).ToList();
            if (cuotasCobrador.Any() && !string.IsNullOrEmpty(cobrador.Telefono))
            {
                var montoCobrador = cuotasCobrador.Sum(c => c.SaldoPendiente);
                var mensaje = $"📋 Cobros del día - {cobrador.Nombre}\n" +
                    $"💵 Total a cobrar: ${montoCobrador:N0}\n" +
                    $"📝 Cuotas: {cuotasCobrador.Count}\n";

                foreach (var cuota in cuotasCobrador.Take(5))
                {
                    mensaje += $"\n• {cuota.Prestamo?.Cliente?.Nombre}: ${cuota.SaldoPendiente:N0}";
                }

                if (cuotasCobrador.Count > 5)
                {
                    mensaje += $"\n... y {cuotasCobrador.Count - 5} más";
                }

                await twilioService.SendSmsAsync(cobrador.Telefono, mensaje);
            }
        }

        // ... (Existing admin notification logic remains)
        
        // ---------------------------------------------------------
        // PROCESAR CAMPAÑAS DE SMS PARA CLIENTES
        // ---------------------------------------------------------
        try 
        {
            var activeCampaigns = await context.SmsCampaigns
                .Where(c => c.Activo && c.TipoDestinatario != TipoDestinatarioSms.ConfirmacionPago)
                .ToListAsync();

            var dayName = today.ToString("dddd", new System.Globalization.CultureInfo("es-ES"));
            // Capitalize first letter: lunes -> Lunes
            if (!string.IsNullOrEmpty(dayName)) dayName = char.ToUpper(dayName[0]) + dayName.Substring(1);

            foreach (var campaign in activeCampaigns)
            {
                // Verificar Días de Envio
                if (!string.IsNullOrEmpty(campaign.DiasEnvio) && campaign.DiasEnvio != "[]")
                {
                    if (!campaign.DiasEnvio.Contains(dayName)) continue;
                }

                List<CuotaPrestamo> targetCuotas = new List<CuotaPrestamo>();

                if (campaign.TipoDestinatario == TipoDestinatarioSms.CuotasHoy)
                {
                    targetCuotas = await context.CuotasPrestamo
                        .Include(c => c.Prestamo).ThenInclude(p => p!.Cliente)
                        .Where(c => c.FechaCobro.Date == today && c.EstadoCuota != "Pagada")
                        .ToListAsync();
                }
                else if (campaign.TipoDestinatario == TipoDestinatarioSms.CuotasVencidas)
                {
                    targetCuotas = await context.CuotasPrestamo
                        .Include(c => c.Prestamo).ThenInclude(p => p!.Cliente)
                        .Where(c => c.FechaCobro.Date < today && c.EstadoCuota != "Pagada")
                        .ToListAsync();
                }
                else if (campaign.TipoDestinatario == TipoDestinatarioSms.ProximasVencer)
                {
                    var targetDate = today.AddDays(3); // Default 3 días
                    targetCuotas = await context.CuotasPrestamo
                        .Include(c => c.Prestamo).ThenInclude(p => p!.Cliente)
                        .Where(c => c.FechaCobro.Date == targetDate && c.EstadoCuota != "Pagada")
                        .ToListAsync();
                }
                else if (campaign.TipoDestinatario == TipoDestinatarioSms.TodosClientesActivos)
                {
                   // Logic for all active clients (might need distinct loans)
                   var activeLoans = await context.Prestamos
                       .Include(p => p.Cliente)
                       .Include(p => p.Cuotas)
                       .Where(p => p.EstadoPrestamo == "Activo")
                       .ToListAsync();

                   foreach(var loan in activeLoans)
                   {
                       if (string.IsNullOrEmpty(loan.Cliente?.Telefono)) continue;
                       
                       // Create dummy cuota for template or handle differently
                       var proxima = loan.Cuotas.Where(c => !c.Cobrado).OrderBy(c => c.FechaCobro).FirstOrDefault();
                       if (proxima != null) targetCuotas.Add(proxima);
                   }
                }

                // Enviar SMS
                foreach (var cuota in targetCuotas)
                {
                    if (cuota.Prestamo?.Cliente?.Telefono == null) continue;

                    var prestamo = cuota.Prestamo;
                    // Calcular variables para template
                    var cuotasPagadas = prestamo.Cuotas.Count(c => c.Cobrado);
                    var saldoPendiente = prestamo.Cuotas.Where(c => !c.Cobrado).Sum(c => c.SaldoPendiente);
                    
                    var mensaje = campaign.Mensaje
                        .Replace("{cliente}", prestamo.Cliente.Nombre)
                        .Replace("{monto}", cuota.MontoCuota.ToString("N0")) // Monto de la cuota actual/próxima
                        .Replace("{fecha}", cuota.FechaCobro.ToString("dd/MM/yyyy"))
                        .Replace("{cuotasPagadas}", cuotasPagadas.ToString())
                        .Replace("{saldoPendiente}", saldoPendiente.ToString("N0"));

                    var sent = await twilioService.SendSmsAsync(prestamo.Cliente.Telefono, mensaje);

                    context.SmsHistories.Add(new SmsHistory
                    {
                        SmsCampaignId = campaign.Id,
                        ClienteId = prestamo.ClienteId,
                        NumeroTelefono = prestamo.Cliente.Telefono,
                        Mensaje = mensaje,
                        FechaEnvio = DateTime.UtcNow,
                        Estado = sent ? EstadoSms.Enviado : EstadoSms.Fallido,
                        TwilioSid = sent ? "SentBackground" : null
                    });
                }
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SMS campaigns");
        }

        _logger.LogInformation("Notificaciones enviadas: {Socios} socios, {Cobradores} cobradores", 
            destinatarios.Count(d => d.Rol == RolUsuario.Socio),
            destinatarios.Count(d => d.Rol == RolUsuario.Cobrador));
    }
}
