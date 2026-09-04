using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.DTOs;
using PrestamosApi.Models;

namespace PrestamosApi.Services;

public interface IMoraService
{
    Task<decimal> ObtenerTasaMoraMensualAsync();
    Task ActualizarTasaMoraMensualAsync(decimal tasaMensual);
    Task<MoraPrestamoDto?> CalcularMoraPrestamoAsync(int prestamoId);
    Task<decimal> CalcularMoraDeCuotasAsync(IQueryable<CuotaPrestamo> cuotas);
}

/// <summary>
/// Cuantifica la penalidad por atraso de las cuotas vencidas.
///
/// Fórmula (definida por el negocio):
///   tasaDiaria    = tasaMoraMensual / 100 / 30      (20% mensual => 0.6667% diario)
///   baseMora      = capital de la cuota que sigue impago
///   moraAcumulada = baseMora * tasaDiaria * díasVencidos
///
/// La mora de un préstamo es la suma de la mora de cada una de sus cuotas vencidas.
/// Es informativa: cobrarla o no es decisión del socio.
/// </summary>
public class MoraService : IMoraService
{
    public const string ClaveTasaMora = "TasaMoraMensual";
    public const decimal TasaMoraPorDefecto = 20m;

    private static readonly string[] EstadosConMora = { "Pendiente", "Parcial", "Vencida", "Mora" };
    private static readonly string[] EstadosPrestamoConMora = { "Activo", "Vencido" };

    private readonly PrestamosDbContext _context;

    public MoraService(PrestamosDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> ObtenerTasaMoraMensualAsync()
    {
        var config = await _context.ConfiguracionesSistema
            .FirstOrDefaultAsync(c => c.Clave == ClaveTasaMora);

        if (config != null && decimal.TryParse(config.Valor, out var tasa) && tasa >= 0)
            return tasa;

        return TasaMoraPorDefecto;
    }

    public async Task ActualizarTasaMoraMensualAsync(decimal tasaMensual)
    {
        var config = await _context.ConfiguracionesSistema
            .FirstOrDefaultAsync(c => c.Clave == ClaveTasaMora);

        if (config == null)
        {
            _context.ConfiguracionesSistema.Add(new ConfiguracionSistema
            {
                Clave = ClaveTasaMora,
                Valor = tasaMensual.ToString("F2"),
                FechaActualizacion = DateTime.UtcNow,
                Descripcion = "Tasa de mora mensual (%) aplicada a cuotas vencidas"
            });
        }
        else
        {
            config.Valor = tasaMensual.ToString("F2");
            config.FechaActualizacion = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<MoraPrestamoDto?> CalcularMoraPrestamoAsync(int prestamoId)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cuotas)
            .FirstOrDefaultAsync(p => p.Id == prestamoId);

        if (prestamo == null) return null;

        var tasaMensual = await ObtenerTasaMoraMensualAsync();
        var tasaDiaria = tasaMensual / 100m / 30m;
        var hoy = DateTime.UtcNow.Date;

        var detalle = new List<MoraCuotaDto>();

        // Un préstamo ya liquidado (Pagado/Terminado) deja de generar mora
        if (EstadosPrestamoConMora.Contains(prestamo.EstadoPrestamo))
        {
            foreach (var cuota in prestamo.Cuotas.OrderBy(c => c.NumeroCuota))
            {
                if (cuota.SaldoPendiente <= 0.01m) continue;
                if (!EstadosConMora.Contains(cuota.EstadoCuota)) continue;
                if (cuota.FechaCobro.Date >= hoy) continue;

                var diasVencidos = (hoy - cuota.FechaCobro.Date).Days;
                var baseMora = CalcularBaseMora(prestamo, cuota);
                var moraDiaria = baseMora * tasaDiaria;

                detalle.Add(new MoraCuotaDto(
                    cuota.Id,
                    cuota.NumeroCuota,
                    cuota.FechaCobro,
                    diasVencidos,
                    Math.Round(baseMora, 2),
                    Math.Round(moraDiaria, 2),
                    Math.Round(moraDiaria * diasVencidos, 2)
                ));
            }
        }

        var moraTotal = detalle.Sum(d => d.MoraAcumulada);

        var moraPagada = await _context.Pagos
            .Where(p => p.PrestamoId == prestamoId && p.TipoPago == "Mora")
            .SumAsync(p => (decimal?)p.MontoPago) ?? 0;

        return new MoraPrestamoDto(
            prestamoId,
            tasaMensual,
            Math.Round(tasaDiaria * 100m, 4),
            Math.Round(moraTotal, 2),
            Math.Round(moraPagada, 2),
            Math.Round(moraTotal - moraPagada, 2),
            detalle
        );
    }

    /// <summary>
    /// Mora acumulada de un conjunto de cuotas. Recibe el IQueryable ya filtrado
    /// para que el llamador pueda aplicar sus propios scopes (rol, cobrador, fecha).
    /// </summary>
    public async Task<decimal> CalcularMoraDeCuotasAsync(IQueryable<CuotaPrestamo> cuotasQuery)
    {
        var tasaDiaria = (await ObtenerTasaMoraMensualAsync()) / 100m / 30m;
        var hoy = DateTime.UtcNow.Date;

        var cuotas = await cuotasQuery
            .Include(c => c.Prestamo)
            .Where(c => c.SaldoPendiente > 0.01m
                     && c.FechaCobro.Date < hoy
                     && EstadosConMora.Contains(c.EstadoCuota)
                     && c.Prestamo != null
                     && EstadosPrestamoConMora.Contains(c.Prestamo.EstadoPrestamo))
            .ToListAsync();

        var total = cuotas.Sum(c =>
            CalcularBaseMora(c.Prestamo!, c) * tasaDiaria * (hoy - c.FechaCobro.Date).Days);

        return Math.Round(total, 2);
    }

    /// <summary>
    /// Capital de la cuota que sigue impago.
    /// En préstamos congelados la cuota es 100% interés (MontoCapital = 0), así que
    /// se usa el saldo pendiente completo para que la mora no quede siempre en cero.
    /// </summary>
    private static decimal CalcularBaseMora(Prestamo prestamo, CuotaPrestamo cuota)
    {
        if (prestamo.EsCongelado || cuota.MontoCuota <= 0 || cuota.MontoCapital <= 0)
            return cuota.SaldoPendiente;

        var ratioCapital = cuota.MontoCapital / cuota.MontoCuota;
        return cuota.SaldoPendiente * ratioCapital;
    }
}
