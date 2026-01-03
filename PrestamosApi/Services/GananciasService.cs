using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Services;

public interface IGananciasService
{
    Task<decimal> CalcularCapitalActualAsync(int usuarioId);
    Task AplicarInteresMensualAsync();
    Task<IEnumerable<object>> ObtenerBalanceSociosAsync();
    Task RegistrarAporteAsync(int usuarioId, decimal monto, string? descripcion);
    Task RegistrarRetiroAsync(int usuarioId, decimal monto, string? descripcion);
    Task DistribuirGananciasPrestamoAsync(int prestamoId);
}

public class GananciasService : IGananciasService
{
    private readonly PrestamosDbContext _context;

    public GananciasService(PrestamosDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> CalcularCapitalActualAsync(int usuarioId)
    {
        var aportes = await _context.Aportes
            .Where(a => a.UsuarioId == usuarioId)
            .SumAsync(a => a.MontoActual);
        return aportes;
    }

    public async Task AplicarInteresMensualAsync()
    {
        var usuarios = await _context.Usuarios
            .Where(u => u.Activo && (u.Rol == RolUsuario.Socio || u.Rol == RolUsuario.AportadorInterno || u.Rol == RolUsuario.AportadorExterno))
            .ToListAsync();

        foreach (var usuario in usuarios)
        {
            var aportes = await _context.Aportes
                .Where(a => a.UsuarioId == usuario.Id)
                .ToListAsync();

            foreach (var aporte in aportes)
            {
                var interes = aporte.MontoActual * (usuario.TasaInteresMensual / 100);
                var saldoAnterior = aporte.MontoActual;
                aporte.MontoActual += interes;

                _context.MovimientosCapital.Add(new MovimientoCapital
                {
                    UsuarioId = usuario.Id,
                    TipoMovimiento = TipoMovimiento.InteresGenerado,
                    Monto = interes,
                    SaldoAnterior = saldoAnterior,
                    SaldoNuevo = aporte.MontoActual,
                    FechaMovimiento = DateTime.UtcNow,
                    Descripcion = $"Interés mensual {usuario.TasaInteresMensual}%"
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<object>> ObtenerBalanceSociosAsync()
    {
        var usuarios = await _context.Usuarios
            .Where(u => u.Activo && (u.Rol == RolUsuario.Socio || u.Rol == RolUsuario.AportadorInterno || u.Rol == RolUsuario.AportadorExterno))
            .Include(u => u.Aportes)
            .Include(u => u.Ganancias)
            .Select(u => new
            {
                u.Id,
                u.Nombre,
                u.Email,
                Rol = u.Rol.ToString(),
                u.PorcentajeParticipacion,
                u.TasaInteresMensual,
                CapitalInicial = u.Aportes.Sum(a => a.MontoInicial),
                CapitalActual = u.Aportes.Sum(a => a.MontoActual),
                GananciasAcumuladas = u.Ganancias.Where(g => g.Liquidado).Sum(g => g.MontoGanancia),
                GananciasPendientes = u.Ganancias.Where(g => !g.Liquidado).Sum(g => g.MontoGanancia)
            })
            .ToListAsync();

        return usuarios;
    }

    public async Task RegistrarAporteAsync(int usuarioId, decimal monto, string? descripcion)
    {
        var usuario = await _context.Usuarios.FindAsync(usuarioId);
        if (usuario == null) return;

        var capitalActual = await CalcularCapitalActualAsync(usuarioId);

        var aporte = new Aporte
        {
            UsuarioId = usuarioId,
            MontoInicial = monto,
            MontoActual = monto,
            FechaAporte = DateTime.UtcNow,
            Descripcion = descripcion
        };

        _context.Aportes.Add(aporte);

        _context.MovimientosCapital.Add(new MovimientoCapital
        {
            UsuarioId = usuarioId,
            TipoMovimiento = TipoMovimiento.Aporte,
            Monto = monto,
            SaldoAnterior = capitalActual,
            SaldoNuevo = capitalActual + monto,
            FechaMovimiento = DateTime.UtcNow,
            Descripcion = descripcion
        });

        await _context.SaveChangesAsync();
    }

    public async Task RegistrarRetiroAsync(int usuarioId, decimal monto, string? descripcion)
    {
        var capitalActual = await CalcularCapitalActualAsync(usuarioId);
        if (monto > capitalActual) return;

        // Reducir del aporte más antiguo primero
        var aportes = await _context.Aportes
            .Where(a => a.UsuarioId == usuarioId && a.MontoActual > 0)
            .OrderBy(a => a.FechaAporte)
            .ToListAsync();

        var montoRestante = monto;
        foreach (var aporte in aportes)
        {
            if (montoRestante <= 0) break;

            if (aporte.MontoActual >= montoRestante)
            {
                aporte.MontoActual -= montoRestante;
                montoRestante = 0;
            }
            else
            {
                montoRestante -= aporte.MontoActual;
                aporte.MontoActual = 0;
            }
        }

        _context.MovimientosCapital.Add(new MovimientoCapital
        {
            UsuarioId = usuarioId,
            TipoMovimiento = TipoMovimiento.Retiro,
            Monto = monto,
            SaldoAnterior = capitalActual,
            SaldoNuevo = capitalActual - monto,
            FechaMovimiento = DateTime.UtcNow,
            Descripcion = descripcion
        });

        await _context.SaveChangesAsync();
    }

    public async Task DistribuirGananciasPrestamoAsync(int prestamoId)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cobrador)
            .FirstOrDefaultAsync(p => p.Id == prestamoId);

        if (prestamo == null) return;

        var interesesTotales = prestamo.MontoIntereses;
        var distribuciones = new List<DistribucionGanancia>();

        // 1. Asignar al cobrador su porcentaje
        if (prestamo.CobradorId.HasValue && prestamo.PorcentajeCobrador > 0)
        {
            var montoCobrador = interesesTotales * (prestamo.PorcentajeCobrador / 100);
            distribuciones.Add(new DistribucionGanancia
            {
                PrestamoId = prestamoId,
                UsuarioId = prestamo.CobradorId.Value,
                PorcentajeAsignado = prestamo.PorcentajeCobrador,
                MontoGanancia = montoCobrador,
                FechaDistribucion = DateTime.UtcNow
            });
            interesesTotales -= montoCobrador;
        }

        // 2. Distribuir el resto entre socios según su porcentaje de participación
        var socios = await _context.Usuarios
            .Where(u => u.Activo && (u.Rol == RolUsuario.Socio || u.Rol == RolUsuario.AportadorInterno))
            .Where(u => u.PorcentajeParticipacion > 0)
            .ToListAsync();

        var totalParticipacion = socios.Sum(s => s.PorcentajeParticipacion);
        if (totalParticipacion > 0)
        {
            foreach (var socio in socios)
            {
                var porcentajeReal = (socio.PorcentajeParticipacion / totalParticipacion) * 100;
                var montoSocio = interesesTotales * (porcentajeReal / 100);
                distribuciones.Add(new DistribucionGanancia
                {
                    PrestamoId = prestamoId,
                    UsuarioId = socio.Id,
                    PorcentajeAsignado = porcentajeReal,
                    MontoGanancia = montoSocio,
                    FechaDistribucion = DateTime.UtcNow
                });
            }
        }

        _context.DistribucionesGanancia.AddRange(distribuciones);
        await _context.SaveChangesAsync();
    }
}
