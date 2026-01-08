using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GananciasController : ControllerBase
{
    private readonly PrestamosDbContext _context;

    public GananciasController(PrestamosDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene el resumen de ganancias por participación
    /// - Aportadores: 3% (o tasa configurada) sobre su capital aportado
    /// - Cobradores: % del interés de préstamos asignados
    /// - Socios: Resto de ganancias divididas equitativamente
    /// </summary>
    [HttpGet("resumen-participacion")]
    public async Task<ActionResult<object>> GetResumenParticipacion()
    {
        // 1. APORTADORES EXTERNOS - Ganan interés sobre su capital aportado
        var aportadoresExternos = await _context.AportadoresExternos
            .Where(a => a.Estado == "Activo")
            .Select(a => new
            {
                a.Id,
                a.Nombre,
                CapitalAportado = a.MontoTotalAportado,
                TasaInteres = a.TasaInteres,
                GananciaProyectadaMensual = a.MontoTotalAportado * (a.TasaInteres / 100m),
                a.Estado
            })
            .ToListAsync();

        // 2. COBRADORES - Ganan % del interés de préstamos asignados
        var prestamosConCobrador = await _context.Prestamos
            .Include(p => p.Cobrador)
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .Where(p => p.CobradorId.HasValue && p.EstadoPrestamo == "Activo")
            .ToListAsync();

        var cobradoresAgrupados = prestamosConCobrador
            .GroupBy(p => new { p.CobradorId, CobradorNombre = p.Cobrador?.Nombre ?? "Sin nombre" })
            .Select(g => new
            {
                CobradorId = g.Key.CobradorId,
                Nombre = g.Key.CobradorNombre,
                PrestamosAsignados = g.Count(),
                // Ganancia proyectada total (% de intereses totales de sus préstamos)
                GananciaProyectada = g.Sum(p => p.MontoIntereses * (p.PorcentajeCobrador / 100m)),
                // Ganancia realizada (% de intereses de cuotas pagadas)
                GananciaRealizada = g.Sum(p => 
                    p.Cuotas.Where(c => c.EstadoCuota == "Pagada")
                            .Sum(c => c.MontoCuota * (p.PorcentajeCobrador / 100m))
                ),
                // Detalle por préstamo
                Detalle = g.Select(p => new
                {
                    PrestamoId = p.Id,
                    ClienteNombre = p.Cliente?.Nombre,
                    MontoIntereses = p.MontoIntereses,
                    PorcentajeCobrador = p.PorcentajeCobrador,
                    ComisionProyectada = p.MontoIntereses * (p.PorcentajeCobrador / 100m),
                    CuotasPagadas = p.Cuotas.Count(c => c.EstadoCuota == "Pagada"),
                    TotalCuotas = p.NumeroCuotas
                }).ToList()
            })
            .ToList();

        // 3. SOCIOS - Se reparten el resto de las ganancias
        var socios = await _context.Usuarios
            .Where(u => u.Activo && u.Rol == RolUsuario.Socio)
            .Include(u => u.Aportes)
            .ToListAsync();

        // Calcular ganancias para socios (intereses menos comisión del cobrador)
        var prestamosActivos = await _context.Prestamos
            .Include(p => p.Cuotas)
            .Where(p => p.EstadoPrestamo == "Activo")
            .ToListAsync();

        // Total intereses proyectados
        var totalInteresesProyectados = prestamosActivos.Sum(p => p.MontoIntereses);
        
        // Total comisiones cobradores
        var totalComisionesCobradores = prestamosActivos
            .Where(p => p.CobradorId.HasValue)
            .Sum(p => p.MontoIntereses * (p.PorcentajeCobrador / 100m));
        
        // Ganancias netas para socios (intereses - comisiones cobradores)
        var gananciasNetasSocios = totalInteresesProyectados - totalComisionesCobradores;
        
        // Dividir entre los 3 socios equitativamente
        var cantidadSocios = socios.Count > 0 ? socios.Count : 1;
        var gananciaPorSocio = gananciasNetasSocios / cantidadSocios;

        var sociosResumen = socios.Select(s => new
        {
            s.Id,
            s.Nombre,
            CapitalAportado = s.Aportes.Sum(a => a.MontoInicial),
            CapitalActual = s.Aportes.Sum(a => a.MontoActual),
            Porcentaje = Math.Round(100m / cantidadSocios, 2),
            GananciaProyectada = Math.Round(gananciaPorSocio, 0),
            // Ganancia realizada basada en cuotas pagadas
            GananciaRealizada = Math.Round(
                (prestamosActivos.Sum(p => 
                    p.Cuotas.Where(c => c.EstadoCuota == "Pagada").Sum(c => c.MontoCuota)
                ) - prestamosActivos.Where(p => p.CobradorId.HasValue).Sum(p => 
                    p.Cuotas.Where(c => c.EstadoCuota == "Pagada").Sum(c => c.MontoCuota) * (p.PorcentajeCobrador / 100m)
                )) / cantidadSocios, 0)
        }).ToList();

        // 4. RESUMEN GENERAL
        var resumen = new
        {
            TotalCapitalPrestado = prestamosActivos.Sum(p => p.MontoPrestado),
            TotalInteresesProyectados = Math.Round(totalInteresesProyectados, 0),
            TotalComisionesCobradores = Math.Round(totalComisionesCobradores, 0),
            TotalGananciasAportadores = Math.Round(aportadoresExternos.Sum(a => a.GananciaProyectadaMensual), 0),
            TotalGananciasSocios = Math.Round(gananciasNetasSocios, 0),
            PrestamosActivos = prestamosActivos.Count
        };

        return Ok(new
        {
            aportadores = aportadoresExternos,
            cobradores = cobradoresAgrupados,
            socios = sociosResumen,
            resumen
        });
    }
}
