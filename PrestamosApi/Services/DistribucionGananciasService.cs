using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Services;

public interface IDistribucionGananciasService
{
    Task DistribuirGananciasPago(int prestamoId, decimal montoPago);
}

public class DistribucionGananciasService : IDistribucionGananciasService
{
    private readonly PrestamosDbContext _context;

    public DistribucionGananciasService(PrestamosDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Distribuye las ganancias de un pago según las fuentes de capital del préstamo
    /// </summary>
    public async Task DistribuirGananciasPago(int prestamoId, decimal montoPago)
    {
        // Obtener el préstamo con sus fuentes de capital
        var prestamo = await _context.Prestamos
            .Include(p => p.FuentesCapital)
            .FirstOrDefaultAsync(p => p.Id == prestamoId);

        if (prestamo == null || !prestamo.FuentesCapital.Any()) return;

        // Calcular el porcentaje de interés respecto al total
        // Si el préstamo es por $100,000 con $20,000 de intereses
        // Cada cuota contiene un % de capital y un % de intereses
        var totalPrestamo = prestamo.MontoTotal;
        var montoIntereses = prestamo.MontoIntereses;
        var montoPrestado = prestamo.MontoPrestado;

        if (totalPrestamo <= 0) return;

        // Proporción del pago que corresponde a intereses
        var proporcionIntereses = montoIntereses / totalPrestamo;
        var gananciaDelPago = montoPago * proporcionIntereses;

        // Proporción del pago que corresponde a capital
        var proporcionCapital = montoPrestado / totalPrestamo;
        var capitalDelPago = montoPago * proporcionCapital;

        // Calcular la porción para el cobrador (si aplica)
        var gananciaNetaDistribuir = gananciaDelPago;
        if (prestamo.CobradorId.HasValue && prestamo.PorcentajeCobrador > 0)
        {
            var gananciaCobrador = gananciaDelPago * (prestamo.PorcentajeCobrador / 100);
            gananciaNetaDistribuir = gananciaDelPago - gananciaCobrador;

            // Registrar ganancia del cobrador
            var distribucionCobrador = new DistribucionGanancia
            {
                PrestamoId = prestamoId,
                UsuarioId = prestamo.CobradorId.Value,
                PorcentajeAsignado = prestamo.PorcentajeCobrador,
                MontoGanancia = gananciaCobrador,
                FechaDistribucion = DateTime.UtcNow,
                Liquidado = false
            };
            _context.DistribucionesGanancia.Add(distribucionCobrador);

            // Actualizar ganancias acumuladas del cobrador
            var cobrador = await _context.Usuarios.FindAsync(prestamo.CobradorId.Value);
            if (cobrador != null)
            {
                cobrador.GananciasAcumuladas += gananciaCobrador;
            }
        }

        // Calcular participación total de las fuentes para normalizar porcentajes
        var totalCapitalFuentes = prestamo.FuentesCapital.Sum(f => f.MontoAportado);
        if (totalCapitalFuentes <= 0) return;

        foreach (var fuente in prestamo.FuentesCapital)
        {
            // Porcentaje de participación de esta fuente
            var porcentajeParticipacion = (fuente.MontoAportado / totalCapitalFuentes) * 100;
            var gananciaFuente = gananciaNetaDistribuir * (fuente.MontoAportado / totalCapitalFuentes);
            var capitalFuente = capitalDelPago * (fuente.MontoAportado / totalCapitalFuentes);

            if (fuente.Tipo == "Interno" && fuente.UsuarioId.HasValue)
            {
                // Socio interno - registrar distribución y actualizar ganancias
                var distribucion = new DistribucionGanancia
                {
                    PrestamoId = prestamoId,
                    UsuarioId = fuente.UsuarioId.Value,
                    PorcentajeAsignado = porcentajeParticipacion,
                    MontoGanancia = gananciaFuente,
                    FechaDistribucion = DateTime.UtcNow,
                    Liquidado = false
                };
                _context.DistribucionesGanancia.Add(distribucion);

                // Actualizar ganancias del socio
                var socio = await _context.Usuarios.FindAsync(fuente.UsuarioId.Value);
                if (socio != null)
                {
                    socio.GananciasAcumuladas += gananciaFuente;
                }
            }
            else if (fuente.Tipo == "Externo" && fuente.AportadorExternoId.HasValue)
            {
                // Aportador externo - el capital recuperado reduce su saldo pendiente
                var aportador = await _context.AportadoresExternos.FindAsync(fuente.AportadorExternoId.Value);
                if (aportador != null)
                {
                    aportador.MontoPagado += capitalFuente;
                    aportador.SaldoPendiente -= capitalFuente;
                    if (aportador.SaldoPendiente < 0) aportador.SaldoPendiente = 0;
                    
                    // Si se pagó todo, marcar como pagado
                    if (aportador.SaldoPendiente <= 0)
                    {
                        aportador.Estado = "Pagado";
                    }
                }
            }
            // Tipo "Reserva" - las ganancias se quedan en el pool (no requiere acción específica)
        }

        await _context.SaveChangesAsync();
    }
}
