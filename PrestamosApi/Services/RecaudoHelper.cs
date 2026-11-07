using Microsoft.EntityFrameworkCore;
using PrestamosApi.Models;

namespace PrestamosApi.Services;

/// <summary>Plata que entró por cuotas en una fecha concreta, con su parte de interés.</summary>
public record MovimientoCobro(int PrestamoId, Prestamo? Prestamo, DateTime Fecha, decimal Monto, decimal Interes);

/// <summary>
/// Lo cobrado por cuotas, contado en la fecha real de cada pago (no en la fecha del último abono de la cuota).
/// Así un abono de septiembre no aparece en noviembre cuando la cuota se termina de pagar en noviembre,
/// y un pago que cubre varias cuotas cuenta una sola vez, en su fecha.
///
/// Fuentes (misma regla que el saldo de Nequi/Efectivo):
///  - Pagos de tipo "Cuota" del historial, por su fecha y monto.
///  - Cuotas marcadas como cobradas en Cobros (✓) que no tienen ningún pago en el historial: por la fecha en que
///    se marcaron y su monto pagado.
/// </summary>
public static class RecaudoHelper
{
    public static async Task<List<MovimientoCobro>> CargarAsync(
        IQueryable<Pago> pagos, IQueryable<CuotaPrestamo> cuotas, IQueryable<Pago> todosLosPagos,
        DateTime desde, DateTime hasta)
    {
        var pagosCuota = await pagos
            .Include(p => p.Cuota)
            .Include(p => p.Prestamo).ThenInclude(p => p!.Cliente)
            .Include(p => p.Prestamo).ThenInclude(p => p!.Cobrador)
            .Where(p => p.TipoPago == TiposPago.Cuota && p.FechaPago >= desde && p.FechaPago < hasta)
            .ToListAsync();

        var cuotasMarcadas = await cuotas
            .Include(c => c.Prestamo).ThenInclude(p => p!.Cliente)
            .Include(c => c.Prestamo).ThenInclude(p => p!.Cobrador)
            .Where(c => c.Cobrado && c.MontoPagado > 0 && c.FechaPago.HasValue
                     && c.FechaPago >= desde && c.FechaPago < hasta
                     && !todosLosPagos.Any(pg => pg.CuotaId == c.Id))
            .ToListAsync();

        // Parte de interés de un pago: la proporción de la cuota a la que se registró
        // (en congelados la cuota es todo interés). Si la cuota ya no existe, la del préstamo.
        static decimal ProporcionInteres(Pago p)
        {
            if (p.Cuota is { MontoCuota: > 0 } c) return c.MontoInteres / c.MontoCuota;
            if (p.Prestamo is null) return 0m;
            if (p.Prestamo.EsCongelado) return 1m;
            return p.Prestamo.MontoTotal > 0 ? p.Prestamo.MontoIntereses / p.Prestamo.MontoTotal : 0m;
        }

        var movimientos = pagosCuota
            .Select(p => new MovimientoCobro(p.PrestamoId, p.Prestamo, p.FechaPago, p.MontoPago, p.MontoPago * ProporcionInteres(p)))
            .ToList();
        movimientos.AddRange(cuotasMarcadas.Select(c => new MovimientoCobro(
            c.PrestamoId, c.Prestamo, c.FechaPago!.Value, c.MontoPagado,
            c.MontoCuota > 0 ? c.MontoInteres * c.MontoPagado / c.MontoCuota : c.MontoInteres)));
        return movimientos;
    }
}
