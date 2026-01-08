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
    private const decimal PORCENTAJE_APORTADOR = 3m; // 3% fijo mensual sobre SU capital

    public GananciasController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet("resumen-participacion")]
    public async Task<ActionResult<object>> GetResumenParticipacion()
    {
        // Obtener préstamos activos
        var prestamosActivos = await _context.Prestamos
            .Include(p => p.Cobrador)
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .Where(p => p.EstadoPrestamo == "Activo")
            .ToListAsync();

        // Fechas para cálculo mensual
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

        // --- 0. PRE-CÁLCULO GLOBALES MENSUALES ---
        decimal globalInteresMes = 0;
        decimal globalFlujoMes = 0; // Cobros esperados (Capital + Interés)
        decimal globalGananciaCobradoresMes = 0;

        foreach (var p in prestamosActivos)
        {
            var cuotasMes = p.Cuotas.Where(c => c.FechaCobro >= startOfMonth && c.FechaCobro <= endOfMonth).ToList();
            if (cuotasMes.Count == 0 || p.MontoTotal <= 0) continue;

            // Ratio Interés
            decimal ratioInteres = p.MontoIntereses / p.MontoTotal;

            // Totales Préstamo este mes
            decimal flujoPrestamoMes = cuotasMes.Sum(c => c.MontoCuota);
            decimal interesPrestamoMes = cuotasMes.Sum(c => c.MontoCuota * ratioInteres);

            globalFlujoMes += flujoPrestamoMes;
            globalInteresMes += interesPrestamoMes;

            // Parte Cobrador
            if (p.CobradorId.HasValue && p.TasaInteres > 0)
            {
                var factor = p.PorcentajeCobrador / p.TasaInteres;
                globalGananciaCobradoresMes += interesPrestamoMes * factor;
            }
        }


        // --- 1. APORTADORES ---
        var aportadores = await _context.AportadoresExternos
            .Where(a => a.Estado == "Activo")
            .ToListAsync();

        // Gasto Mensual Total por Aportadores
        var gastoMensualAportadores = aportadores.Count > 0 
            ? aportadores.Sum(a => a.MontoTotalAportado * (a.TasaInteres / 100m)) 
            : 0;

        var aportadoresResumen = aportadores.Select(a => new
        {
            a.Id,
            a.Nombre,
            CapitalAportado = a.MontoTotalAportado,
            TasaInteres = a.TasaInteres,
            GananciaMensual = a.MontoTotalAportado * (a.TasaInteres / 100m),
            a.Estado
        }).ToList();


        // --- 2. COBRADORES (Agrupados) ---
        var cobradoresAgrupados = prestamosActivos
            .Where(p => p.CobradorId.HasValue)
            .GroupBy(p => new { p.CobradorId, CobradorNombre = p.Cobrador?.Nombre ?? "Sin nombre" })
            .Select(g => {
                // Ganancia Total Proyectada
                var gananciaProyectadaTotal = g.Sum(p => {
                    if (p.TasaInteres == 0) return 0;
                    var factor = p.PorcentajeCobrador / p.TasaInteres;
                    return p.MontoIntereses * factor;
                });

                // Ganancia Mes Proyectada
                var gananciaInteresMes = g.Sum(p => {
                    var cuotasMes = p.Cuotas.Where(c => c.FechaCobro >= startOfMonth && c.FechaCobro <= endOfMonth).ToList();
                    if (cuotasMes.Count == 0 || p.MontoTotal <= 0 || p.TasaInteres == 0) return 0;
                    
                    decimal ratioInteres = p.MontoIntereses / p.MontoTotal;
                    decimal interesMes = cuotasMes.Sum(c => c.MontoCuota * ratioInteres);
                    decimal factor = p.PorcentajeCobrador / p.TasaInteres;
                    return interesMes * factor;
                });

                var gananciaRealizadaTotal = 0m; 

                return new
                {
                    CobradorId = g.Key.CobradorId,
                    Nombre = g.Key.CobradorNombre,
                    PrestamosAsignados = g.Count(),
                    GananciaProyectada = gananciaProyectadaTotal,
                    GananciaInteresMes = gananciaInteresMes, // Nuevo
                    GananciaRealizada = gananciaRealizadaTotal,
                    Detalle = g.Select(p => new { 
                        p.Id, 
                        p.MontoIntereses, 
                        p.PorcentajeCobrador
                    }).ToList()
                };
            }).ToList();


        // --- 3. SOCIOS ---
        var totalInteresesGenerados = prestamosActivos.Sum(p => p.MontoIntereses);
        var totalGananciaCobradores = cobradoresAgrupados.Sum(c => c.GananciaProyectada);
        var gananciaBrutaSocios = totalInteresesGenerados - totalGananciaCobradores;
        
        // Mensuales Socios
        var interesNetoSociosMes = globalInteresMes - globalGananciaCobradoresMes;
        var flujoNetoSociosMes = globalFlujoMes - gastoMensualAportadores; // Capital + Interes - Gastos

        var socios = await _context.Usuarios
            .Where(u => u.Activo && u.Rol == RolUsuario.Socio)
            .Include(u => u.Aportes)
            .ToListAsync();
            
        int numSocios = socios.Count > 0 ? socios.Count : 1;
        
        var sociosResumen = socios.Select(s => new
        {
            s.Id,
            s.Nombre,
            CapitalAportado = s.Aportes.Sum(a => a.MontoInicial),
            CapitalActual = s.Aportes.Sum(a => a.MontoActual),
            Porcentaje = 100m / numSocios,
            
            // Totales
            GananciaProyectadaTotal = gananciaBrutaSocios / numSocios,
            GananciaRealizada = 0,
            
            // Mensuales
            GananciaInteresMes = interesNetoSociosMes / numSocios,
            FlujoNetoMes = flujoNetoSociosMes / numSocios
        }).ToList();

        var resumen = new
        {
            TotalCapitalPrestado = prestamosActivos.Sum(p => p.MontoPrestado),
            TotalInteresesProyectados = Math.Round(totalInteresesGenerados, 0),
            ProyeccionInteresesMesActual = Math.Round(globalInteresMes, 0),

            TotalGananciaCobradores = Math.Round(totalGananciaCobradores, 0),
            TotalGananciaSociosBruta = Math.Round(gananciaBrutaSocios, 0),
            GastoMensualAportadores = Math.Round(gastoMensualAportadores, 0),
            
            SumaPartes = Math.Round(totalGananciaCobradores + gananciaBrutaSocios, 0),
            Diferencia = Math.Round(totalInteresesGenerados - (totalGananciaCobradores + gananciaBrutaSocios), 0)
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
