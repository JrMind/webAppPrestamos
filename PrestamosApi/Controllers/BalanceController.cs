using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BalanceController : BaseApiController
{
    private readonly PrestamosDbContext _context;

    public BalanceController(PrestamosDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene el balance personal del usuario autenticado
    /// </summary>
    [HttpGet("mi-balance")]
    public async Task<ActionResult<object>> GetMiBalance()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(new { message = "Usuario no autenticado" });
        }

        var usuario = await _context.Usuarios
            .Include(u => u.Aportes)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (usuario == null)
        {
            return NotFound(new { message = "Usuario no encontrado" });
        }

        if (usuario.Rol == RolUsuario.Cobrador)
        {
            return Ok(await CalcularBalanceCobrador(userId.Value));
        }
        else // Socio, AportadorInterno, AportadorExterno
        {
            return Ok(await CalcularBalanceSocio(userId.Value, usuario));
        }
    }

    /// <summary>
    /// Calcula el balance de un cobrador basado en sus comisiones
    /// Comisión = MontoTotal del préstamo × %Cobrador
    /// </summary>
    private async Task<object> CalcularBalanceCobrador(int cobradorId)
    {
        // Préstamos asignados al cobrador
        var prestamos = await _context.Prestamos
            .Include(p => p.Cuotas)
            .Include(p => p.Cliente)
            .Where(p => p.CobradorId == cobradorId)
            .ToListAsync();

        // Calcular comisiones totales (% del MontoTotal del préstamo)
        decimal comisionesTotales = prestamos.Sum(p => p.MontoTotal * (p.PorcentajeCobrador / 100m));

        // Calcular comisiones por cobrar (de préstamos activos)
        decimal comisionesPendientes = prestamos
            .Where(p => p.EstadoPrestamo == "Activo")
            .Sum(p => p.MontoTotal * (p.PorcentajeCobrador / 100m));

        // Calcular comisiones cobradas (de préstamos pagados)
        decimal comisionesCobradas = prestamos
            .Where(p => p.EstadoPrestamo == "Pagado")
            .Sum(p => p.MontoTotal * (p.PorcentajeCobrador / 100m));

        // Detalle por préstamo activo
        var detalleActivos = prestamos
            .Where(p => p.EstadoPrestamo == "Activo")
            .Select(p => new
            {
                p.Id,
                ClienteNombre = p.Cliente?.Nombre,
                p.MontoTotal,
                p.PorcentajeCobrador,
                Comision = p.MontoTotal * (p.PorcentajeCobrador / 100m),
                TotalPagado = p.Cuotas.Sum(c => c.MontoPagado),
                SaldoPendiente = p.Cuotas.Sum(c => c.SaldoPendiente)
            })
            .ToList();

        return new
        {
            Rol = "Cobrador",
            TotalPrestamosAsignados = prestamos.Count,
            PrestamosActivos = prestamos.Count(p => p.EstadoPrestamo == "Activo"),
            ComisionesTotales = Math.Round(comisionesTotales, 2),
            ComisionesPendientes = Math.Round(comisionesPendientes, 2),
            ComisionesCobradas = Math.Round(comisionesCobradas, 2),
            MontoTotalReferido = prestamos.Sum(p => p.MontoTotal),
            DetalleActivos = detalleActivos
        };
    }

    /// <summary>
    /// Calcula el balance de un socio/aportador basado en sus aportes y ganancias
    /// </summary>
    private async Task<object> CalcularBalanceSocio(int usuarioId, Usuario usuario)
    {
        // Capital aportado
        var capitalInicial = usuario.Aportes?.Sum(a => a.MontoInicial) ?? 0;
        var capitalActual = usuario.Aportes?.Sum(a => a.MontoActual) ?? 0;

        // Ganancias según distribución
        var ganancias = await _context.Set<DistribucionGanancia>()
            .Where(d => d.UsuarioId == usuarioId)
            .ToListAsync();

        var gananciasAcumuladas = ganancias.Sum(g => g.MontoGanancia);
        var gananciasPendientes = ganancias.Where(g => !g.Liquidado).Sum(g => g.MontoGanancia);
        var gananciasPagadas = ganancias.Where(g => g.Liquidado).Sum(g => g.MontoGanancia);

        // Métricas generales del portafolio (solo para socios)
        var totalPrestadoGlobal = await _context.Prestamos.SumAsync(p => p.MontoPrestado);
        var totalCobradoGlobal = await _context.CuotasPrestamo.SumAsync(c => c.MontoPagado);
        var prestamosActivosGlobal = await _context.Prestamos.CountAsync(p => p.EstadoPrestamo == "Activo");

        return new
        {
            Rol = usuario.Rol.ToString(),
            PorcentajeParticipacion = usuario.PorcentajeParticipacion,
            TasaInteresMensual = usuario.TasaInteresMensual,
            CapitalInicial = Math.Round(capitalInicial, 2),
            CapitalActual = Math.Round(capitalActual, 2),
            GananciasAcumuladas = Math.Round(gananciasAcumuladas, 2),
            GananciasPendientes = Math.Round(gananciasPendientes, 2),
            GananciasPagadas = Math.Round(gananciasPagadas, 2),
            // Métricas globales del portafolio
            PortafolioGlobal = new
            {
                TotalPrestado = totalPrestadoGlobal,
                TotalCobrado = totalCobradoGlobal,
                PrestamosActivos = prestamosActivosGlobal,
                // Participación proporcional del socio
                MiParticipacionCapital = Math.Round(totalPrestadoGlobal * (usuario.PorcentajeParticipacion / 100m), 2)
            }
        };
    }
}
