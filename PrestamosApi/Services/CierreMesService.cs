using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Services;

public interface ICierreMesService
{
    Task<string> EjecutarCierreMes(int mes, int anio, bool force = false);
}

public class CierreMesService : ICierreMesService
{
    private readonly PrestamosDbContext _context;
    private readonly ILogger<CierreMesService> _logger;

    public CierreMesService(PrestamosDbContext context, ILogger<CierreMesService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> EjecutarCierreMes(int mes, int anio, bool force = false)
    {
        try 
        {
            // 1. Verificar si ya se corrió (si no es forzado)
            if (!force)
            {
                var yaEjecutado = await _context.CierreMesLogs
                    .AnyAsync(l => l.Mes == mes && l.Anio == anio);
                    
                if (yaEjecutado)
                {
                    _logger.LogInformation("Cierre de mes {Mes}/{Anio} omitido: Ya fue procesado anteriormente.", mes, anio);
                    return "El cierre para este mes ya fue ejecutado previamente.";
                }
            }

            // 2. Obtener Aportadores Activos
            var aportadores = await _context.AportadoresExternos
                .Where(a => a.Estado == "Activo")
                .ToListAsync();

            if (!aportadores.Any())
            {
                return "No hay aportadores activos para procesar.";
            }

            // 3. Capitalizar Intereses
            int procesados = 0;
            foreach (var aportador in aportadores)
            {
                // Cálculo: Interés Mensual = Capital * (Tasa / 100)
                // Se asume que TasaInteres es mensual (ej. 3.0 para 3%)
                decimal interesGanado = Math.Round(aportador.MontoTotalAportado * (aportador.TasaInteres / 100m), 2);
                
                if (interesGanado > 0)
                {
                    aportador.MontoTotalAportado += interesGanado;
                    
                    // Agregar nota de auditoría simple
                    var nota = $"\n[Auto {DateTime.UtcNow:yyyy-MM-dd}] Capitalización Mes {mes}/{anio}: +{interesGanado:C} (Nuevo Capital: {aportador.MontoTotalAportado:C})";
                    aportador.Notas = (aportador.Notas ?? "") + nota;
                    
                    procesados++;
                }
            }

            // 4. Registrar Log
            var log = new CierreMesLog
            {
                Mes = mes,
                Anio = anio,
                FechaEjecucion = DateTime.UtcNow,
                Resultado = $"Exitoso. {procesados} aportadores capitalizados."
            };
            _context.CierreMesLogs.Add(log);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Cierre de mes {Mes}/{Anio} completado. {Procesados} aportadores actualizados.", mes, anio, procesados);
            return $"Cierre completado exitosamente. {procesados} aportadores capitalizados.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando cierre de mes {Mes}/{Anio}", mes, anio);
            throw;
        }
    }
}
