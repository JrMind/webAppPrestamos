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
    private const decimal PORCENTAJE_APORTADOR = 3m; // 3% fijo para el aportador

    public GananciasController(PrestamosDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene el resumen de ganancias por participación
    /// La tasa del préstamo se distribuye:
    /// - Cobrador: PorcentajeCobrador del préstamo
    /// - Aportador: 3% fijo
    /// - Socios: Resto (TasaInteres - Cobrador% - 3%) dividido entre 3
    /// </summary>
    [HttpGet("resumen-participacion")]
    public async Task<ActionResult<object>> GetResumenParticipacion()
    {
        // Obtener préstamos activos con todos los datos necesarios
        var prestamosActivos = await _context.Prestamos
            .Include(p => p.Cobrador)
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .Where(p => p.EstadoPrestamo == "Activo")
            .ToListAsync();

        // 1. APORTADORES - Reciben 3% fijo del capital de cada préstamo
        var aportadoresExternos = await _context.AportadoresExternos
            .Where(a => a.Estado == "Activo")
            .ToListAsync();

        // Ganancia del aportador: 3% del capital de TODOS los préstamos activos
        var totalCapitalPrestado = prestamosActivos.Sum(p => p.MontoPrestado);
        var gananciaAportadorProyectada = totalCapitalPrestado * (PORCENTAJE_APORTADOR / 100m);
        
        // Cuotas pagadas para calcular ganancia realizada
        var totalCuotasPagadas = prestamosActivos.Sum(p => 
            p.Cuotas.Where(c => c.EstadoCuota == "Pagada").Sum(c => c.MontoCuota));
        var gananciaAportadorRealizada = totalCuotasPagadas * (PORCENTAJE_APORTADOR / 100m);

        var aportadoresResumen = aportadoresExternos.Select(a => new
        {
            a.Id,
            a.Nombre,
            CapitalAportado = a.MontoTotalAportado,
            TasaInteres = PORCENTAJE_APORTADOR, // Siempre 3%
            GananciaProyectadaMensual = gananciaAportadorProyectada, // Su parte del 3%
            GananciaRealizada = gananciaAportadorRealizada,
            a.Estado
        }).ToList();

        // 2. COBRADORES - Reciben su % configurado del capital de sus préstamos asignados
        var cobradoresAgrupados = prestamosActivos
            .Where(p => p.CobradorId.HasValue)
            .GroupBy(p => new { p.CobradorId, CobradorNombre = p.Cobrador?.Nombre ?? "Sin nombre" })
            .Select(g => new
            {
                CobradorId = g.Key.CobradorId,
                Nombre = g.Key.CobradorNombre,
                PrestamosAsignados = g.Count(),
                // Ganancia = Capital × PorcentajeCobrador%
                GananciaProyectada = g.Sum(p => p.MontoPrestado * (p.PorcentajeCobrador / 100m)),
                // Ganancia realizada basada en cuotas pagadas
                GananciaRealizada = g.Sum(p => 
                    p.Cuotas.Where(c => c.EstadoCuota == "Pagada")
                            .Sum(c => c.MontoCuota) * (p.PorcentajeCobrador / p.TasaInteres)
                ),
                Detalle = g.Select(p => new
                {
                    PrestamoId = p.Id,
                    ClienteNombre = p.Cliente?.Nombre,
                    MontoPrestado = p.MontoPrestado,
                    TasaInteres = p.TasaInteres,
                    PorcentajeCobrador = p.PorcentajeCobrador,
                    // Cobrador recibe: Capital × PorcentajeCobrador%
                    ComisionProyectada = p.MontoPrestado * (p.PorcentajeCobrador / 100m),
                    CuotasPagadas = p.Cuotas.Count(c => c.EstadoCuota == "Pagada"),
                    TotalCuotas = p.NumeroCuotas
                }).ToList()
            })
            .ToList();

        // 3. SOCIOS - Reciben el resto (TasaInteres - Cobrador% - 3%) dividido entre 3
        var socios = await _context.Usuarios
            .Where(u => u.Activo && u.Rol == RolUsuario.Socio)
            .Include(u => u.Aportes)
            .ToListAsync();

        var cantidadSocios = socios.Count > 0 ? socios.Count : 1;

        // Calcular porcentaje para socios: TasaInteres - Cobrador% - 3%
        // Para cada préstamo, calcular cuánto va a los socios
        var gananciaSociosProyectada = prestamosActivos.Sum(p => {
            var porcentajeCobrador = p.CobradorId.HasValue ? p.PorcentajeCobrador : 0;
            var porcentajeSocios = p.TasaInteres - porcentajeCobrador - PORCENTAJE_APORTADOR;
            if (porcentajeSocios < 0) porcentajeSocios = 0;
            return p.MontoPrestado * (porcentajeSocios / 100m);
        });

        var gananciaSociosRealizada = prestamosActivos.Sum(p => {
            var cuotasPagadas = p.Cuotas.Where(c => c.EstadoCuota == "Pagada").Sum(c => c.MontoCuota);
            var porcentajeCobrador = p.CobradorId.HasValue ? p.PorcentajeCobrador : 0;
            var porcentajeSocios = p.TasaInteres - porcentajeCobrador - PORCENTAJE_APORTADOR;
            if (porcentajeSocios < 0) porcentajeSocios = 0;
            // Proporción de cuotas que va a socios
            return cuotasPagadas * (porcentajeSocios / p.TasaInteres);
        });

        var sociosResumen = socios.Select(s => new
        {
            s.Id,
            s.Nombre,
            CapitalAportado = s.Aportes.Sum(a => a.MontoInicial),
            CapitalActual = s.Aportes.Sum(a => a.MontoActual),
            Porcentaje = Math.Round(100m / cantidadSocios, 2),
            GananciaProyectada = Math.Round(gananciaSociosProyectada / cantidadSocios, 0),
            GananciaRealizada = Math.Round(gananciaSociosRealizada / cantidadSocios, 0)
        }).ToList();

        // Totales comisiones cobradores
        var totalComisionesCobradores = prestamosActivos
            .Where(p => p.CobradorId.HasValue)
            .Sum(p => p.MontoPrestado * (p.PorcentajeCobrador / 100m));

        // 4. RESUMEN GENERAL
        var totalInteresesProyectados = prestamosActivos.Sum(p => p.MontoIntereses);
        
        var resumen = new
        {
            TotalCapitalPrestado = totalCapitalPrestado,
            TotalInteresesProyectados = Math.Round(totalInteresesProyectados, 0),
            TotalComisionesCobradores = Math.Round(totalComisionesCobradores, 0),
            TotalGananciasAportadores = Math.Round(gananciaAportadorProyectada, 0),
            TotalGananciasSocios = Math.Round(gananciaSociosProyectada, 0),
            PrestamosActivos = prestamosActivos.Count,
            // Verificación: Cobrador + Aportador + Socios = Intereses Totales
            VerificacionTotal = Math.Round(totalComisionesCobradores + gananciaAportadorProyectada + gananciaSociosProyectada, 0)
        };

        return Ok(new
        {
            aportadores = aportadoresResumen,
            cobradores = cobradoresAgrupados,
            socios = sociosResumen,
            resumen
        });
    }
}
