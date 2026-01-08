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

        // 1. APORTADORES - Gasto financiero fijo (independiente de préstamos)
        var aportadores = await _context.AportadoresExternos
            .Where(a => a.Estado == "Activo")
            .ToListAsync();

        // Gasto Mensual Total por Aportadores (3% de su capital total)
        // Ojo: Esto es MENSUAL.
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

        // 2. DISTRIBUCIÓN DE INTERESES DE PRÉSTAMOS (Cobrador vs Socios)
        // Usamos MontoIntereses como la verdad absoluta del total a ganar
        
        var cobradoresAgrupados = prestamosActivos
            .Where(p => p.CobradorId.HasValue)
            .GroupBy(p => new { p.CobradorId, CobradorNombre = p.Cobrador?.Nombre ?? "Sin nombre" })
            .Select(g => {
                var gananciaProyectadaTotal = g.Sum(p => {
                    if (p.TasaInteres == 0) return 0;
                    // Proporción que le toca al cobrador del total de intereses
                    var factorCobrador = p.PorcentajeCobrador / p.TasaInteres;
                    return p.MontoIntereses * factorCobrador;
                });

                var gananciaRealizadaTotal = g.Sum(p => {
                    if (p.TasaInteres == 0) return 0;
                    var interesPagado = p.Cuotas.Where(c => c.EstadoCuota == "Pagada").Sum(c => c.MontoCuota); // Cuota tiene capital e interes
                    // Asumimos para cuota fija que la proporción es constante para simplificar, 
                    // o usamos MontoIntereses si es congelado. 
                    // MEJOR APROXIMACIÓN: (MontoCuotaPagada / MontoTotalPrestamo) * ParteCobradorTotal
                    // Pero para prestamos simples Cuota = Capital/N + Interes. 
                    // Simplificación válida para dashboard: Proporción de la tasa sobre lo cobrado.
                    var factorCobrador = p.PorcentajeCobrador / p.TasaInteres;
                    
                    // Si es simple o congelado, podemos estimar el interes pagado.
                    // Para dashboard usaremos: (TotalPagado - CapitalPagado) * Factor? No, muy complejo.
                    // Usaremos: Interés estimado de la cuota * Factor.
                    // Estimación simple: TotalPagado * (%Interes del Total) * FactorCobrador
                    // Pero el cobrador cobra sobre el interés generado.
                    
                    return p.Cuotas.Where(c => c.EstadoCuota == "Pagada")
                        .Sum(c => {
                             // Calcular componente de interés de esta cuota específica es difícil sin tabla de amortización.
                             // Usaremos la proporción global del préstamo: (MontoIntereses / MontoTotal)
                             if (p.MontoTotal <= 0) return 0;
                             var proporcionInteres = p.MontoIntereses / p.MontoTotal;
                             var interesDeCuota = c.MontoCuota * proporcionInteres;
                             return interesDeCuota * factorCobrador;
                        });
                });

                return new
                {
                    CobradorId = g.Key.CobradorId,
                    Nombre = g.Key.CobradorNombre,
                    PrestamosAsignados = g.Count(),
                    GananciaProyectada = gananciaProyectadaTotal,
                    GananciaRealizada = gananciaRealizadaTotal,
                    Detalle = g.Select(p => new { 
                        p.Id, 
                        p.MontoIntereses, 
                        p.PorcentajeCobrador,
                        Proyeccion = p.TasaInteres > 0 ? p.MontoIntereses * (p.PorcentajeCobrador / p.TasaInteres) : 0
                    }).ToList()
                };
            }).ToList();

        // 3. SOCIOS (El resto de los intereses)
        var totalInteresesGenerados = prestamosActivos.Sum(p => p.MontoIntereses);
        var totalGananciaCobradores = cobradoresAgrupados.Sum(c => c.GananciaProyectada);
        
        // Ganancia Bruta Socios = Total - Cobradores
        var gananciaBrutaSocios = totalInteresesGenerados - totalGananciaCobradores;
        
        // El usuario quiere ver que se reste el gasto de aportadores "sobre las ganancias".
        // Pero el gasto aportadores es mensual y perpetuo (mientras tengan capital), y la ganancia prestamos es proyectada total.
        // No se pueden restar peras (mensual) con manzanas (total proyectado a futuro).
        // Sin embargo, mostraremos la "Ganancia Neta Proyectada" asumiendo que el gasto de aportadores se proyecta X meses? No.
        // Mostraremos: Ganancia Bruta (de prestamos) y el Gasto Mensual Recurrente por separado como pide el usuario.
        // "Quiero ver que si pongo que hay que pagarle... reste esa cifra".
        // Lo restaremos en el frontend visualmente o crearemos un "Ganancia Neta Estimada Mensual".
        
        // Para socios:
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
            // Proyectada TOTAL (bruta)
            GananciaProyectadaTotal = gananciaBrutaSocios / numSocios,
            // Realizada (aproximada basada en lo cobrado)
            GananciaRealizada = 0 // Simplificado por ahora
        }).ToList();

            // 4. PROYECCIÓN INTERESES MES ACTUAL
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

            decimal proyeccionInteresesMesActual = 0;

            foreach(var p in prestamosActivos)
            {
                 // Cuotas que vencen este mes (pagadas o pendientes)
                 // Queremos saber cuánto interés se genera este mes, independientemente de si se paga o no.
                 var cuotasMes = p.Cuotas.Where(c => c.FechaCobro >= startOfMonth && c.FechaCobro <= endOfMonth).ToList();
                 
                 if (cuotasMes.Count > 0 && p.MontoTotal > 0)
                 {
                     // Proporción de interés en cada cuota
                     // Estimación: (TotalIntereses / TotalDeuda) * MontoCuota
                     decimal ratioInteres = p.MontoIntereses / p.MontoTotal;
                     
                     proyeccionInteresesMesActual += cuotasMes.Sum(c => c.MontoCuota * ratioInteres);
                 }
            }

            var resumen = new
            {
                TotalCapitalPrestado = prestamosActivos.Sum(p => p.MontoPrestado),
                TotalInteresesProyectados = Math.Round(totalInteresesGenerados, 0),
                
                // Nuevo Campo
                ProyeccionInteresesMesActual = Math.Round(proyeccionInteresesMesActual, 0),

                // Desglose
                TotalGananciaCobradores = Math.Round(totalGananciaCobradores, 0),
                TotalGananciaSociosBruta = Math.Round(gananciaBrutaSocios, 0),
                
                // Gasto Aportadores (MENSUAL)
                GastoMensualAportadores = Math.Round(gastoMensualAportadores, 0),
                
                // Verificación
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
