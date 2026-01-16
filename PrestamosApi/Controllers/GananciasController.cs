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
    private const decimal PORCENTAJE_APORTADOR = 3m; // 3% fijo mensual
    private const int NUM_SOCIOS = 3; // Número fijo de socios

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

        // --- 1.5 COSTOS OPERATIVOS ---
        var costosActivos = await _context.Costos
            .Where(c => c.Activo)
            .ToListAsync();

        var costosMensuales = costosActivos
            .Where(c => c.Frecuencia == "Mensual")
            .Sum(c => c.Monto);

        var costosQuincenales = costosActivos
            .Where(c => c.Frecuencia == "Quincenal")
            .Sum(c => c.Monto * 2); // x2 para mensualizar

        var costosTotalesMes = costosMensuales + costosQuincenales;


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


        // --- 3. SOCIOS (Fórmula: (Interés - 3% Aportador) / 3 Socios) ---
        var totalInteresesGenerados = prestamosActivos.Sum(p => p.MontoIntereses);
        var totalCapitalPrestado = prestamosActivos.Sum(p => p.MontoPrestado);
        
        // Descuento mensual aportadores
        var descuentoAportador = gastoMensualAportadores;
        
        // Ganancia proyectada de cobradores
        var gananciaCobradoresTotal = cobradoresAgrupados.Sum(c => c.GananciaProyectada);
        
        // Ganancia de interés por socio = (InterésTotal - Aportadores - Cobradores) / 3
        var gananciaInteresPorSocio = (totalInteresesGenerados - gananciaCobradoresTotal - descuentoAportador) / NUM_SOCIOS;
        
        // Calcular cantidad de quincenas promedio de los préstamos activos
        var quincenasPromedio = prestamosActivos.Count > 0 
            ? prestamosActivos.Average(p => p.NumeroCuotas * (p.FrecuenciaPago == "Quincenal" ? 1 : 
                                                              p.FrecuenciaPago == "Mensual" ? 2 : 
                                                              p.FrecuenciaPago == "Semanal" ? 0.5m : 1))
            : 1;
        
        // Ganancia Total = Ganancia Interés × Quincenas + Capital/3
        var gananciaTotal = gananciaInteresPorSocio + (totalCapitalPrestado / NUM_SOCIOS);
        
        // Mensuales Socios (basado en cuotas del mes actual)
        // Interés neto = Interés Total - Aportadores - Cobradores
        var interesNetoSociosMes = (globalInteresMes - gastoMensualAportadores - globalGananciaCobradoresMes) / NUM_SOCIOS;

        var socios = await _context.Usuarios
            .Where(u => u.Activo && u.Rol == RolUsuario.Socio)
            .Include(u => u.Aportes)
            .ToListAsync();
        
        // Ganancia neta mensual por socio = (Flujo Total - Aportadores - Cobradores) / 3
        var gananciaTotalMesPorSocio = (globalFlujoMes - gastoMensualAportadores - globalGananciaCobradoresMes) / NUM_SOCIOS;
        
        var sociosResumen = socios.Select(s => new
        {
            s.Id,
            s.Nombre,
            CapitalAportado = s.Aportes.Sum(a => a.MontoInicial),
            CapitalActual = s.Aportes.Sum(a => a.MontoActual) + s.CapitalActual,
            Porcentaje = 100m / NUM_SOCIOS, // 33.33% cada uno
            
            // Total del MES = Solo Interés neto (Ganancia real)
            GananciaProyectadaTotal = Math.Round(interesNetoSociosMes, 0),
            GananciaRealizada = 0,
            
            // Solo intereses del mes (neto)
            GananciaInteresMes = Math.Round(interesNetoSociosMes, 0),
            // Flujo bruto del mes (sin descontar cobradores)
            FlujoNetoMes = Math.Round((globalFlujoMes - gastoMensualAportadores) / NUM_SOCIOS, 0)
        }).ToList();

        var totalCapitalAportadoSocios = socios.Sum(s => s.Aportes.Sum(a => a.MontoInicial));
        var totalCapitalAportadoExternos = aportadores.Sum(a => a.MontoTotalAportado);
        var totalCapitalBase = totalCapitalAportadoSocios + totalCapitalAportadoExternos;
        var capitalReinvertido = totalCapitalPrestado - totalCapitalBase;

        // Capital en la calle (Saldo de capital pendiente por cobrar)
        var capitalEnCalle = prestamosActivos.Sum(p => 
            p.EsCongelado 
                ? p.MontoPrestado // En congelados, el MontoPrestado es el saldo actual
                : (p.MontoPrestado - p.Cuotas
                    .Where(c => c.EstadoCuota == "Pagada")
                    .Sum(c => c.MontoCapital))
        );

        var resumen = new
        {
            TotalCapitalPrestado = Math.Round(totalCapitalPrestado, 0),
            TotalCapitalBase = Math.Round(totalCapitalBase, 0),
            CapitalReinvertido = Math.Round(capitalReinvertido, 0),
            CapitalEnCalle = Math.Round(capitalEnCalle, 0), // Nuevo: Riesgo actual
            
            TotalInteresesProyectados = Math.Round(totalInteresesGenerados, 0),
            DescuentoAportador3Porciento = Math.Round(descuentoAportador, 0),
            GananciaInteresPorSocio = Math.Round(gananciaInteresPorSocio, 0),
            ProyeccionInteresesMesActual = Math.Round(globalInteresMes, 0),
            FlujoTotalMes = Math.Round(globalFlujoMes, 0), // Capital + Intereses del mes

            TotalGananciaCobradores = Math.Round(cobradoresAgrupados.Sum(c => c.GananciaProyectada), 0),
            GastoMensualAportadores = Math.Round(gastoMensualAportadores, 0),
            CostosTotalesMes = Math.Round(costosTotalesMes, 0),
            // Ganancia Interés Neta = Intereses - Cobradores - Aportadores - Costos
            GananciaInteresNeta = Math.Round(globalInteresMes - globalGananciaCobradoresMes - gastoMensualAportadores - costosTotalesMes, 0),
            NumeroSociosFijo = NUM_SOCIOS
        };

        return Ok(new
        {
            aportadores = aportadoresResumen,
            cobradores = cobradoresAgrupados,
            socios = sociosResumen,
            resumen
        });
    }

    /// <summary>
    /// Obtiene el balance detallado de capital por fuente
    /// </summary>
    [HttpGet("balance-capital")]
    public async Task<ActionResult<object>> GetBalanceCapital()
    {
        // 1. Capital de SOCIOS (Aportes iniciales registrados)
        var aportesSocios = await _context.Aportes.SumAsync(a => a.MontoInicial);
        
        // 2. Capital de APORTADORES EXTERNOS
        var aportadoresExternos = await _context.AportadoresExternos
            .Where(a => a.Estado == "Activo")
            .Select(a => new {
                a.Id,
                a.Nombre,
                CapitalAportado = a.MontoTotalAportado,
                a.TasaInteres
            })
            .ToListAsync();
        var capitalExternos = aportadoresExternos.Sum(a => a.CapitalAportado);
        
        // 3. Capital REINVERTIDO (ganancias de socios)
        var capitalReinvertido = await _context.Usuarios
            .Where(u => u.Activo && u.Rol == RolUsuario.Socio)
            .SumAsync(u => u.CapitalActual);
        
        // 4. CAPITAL PRESTADO por fuente (desde FuentesCapitalPrestamo en préstamos ACTIVOS)
        var fuentesPrestamos = await _context.FuentesCapitalPrestamo
            .Include(f => f.Prestamo)
            .Include(f => f.AportadorExterno)
            .Include(f => f.Usuario)
            .Where(f => f.Prestamo != null && f.Prestamo.EstadoPrestamo == "Activo")
            .ToListAsync();
        
        var prestadoPorAportador = fuentesPrestamos
            .Where(f => f.Tipo == "Externo" && f.AportadorExternoId.HasValue)
            .GroupBy(f => f.AportadorExternoId)
            .ToDictionary(g => g.Key!.Value, g => g.Sum(f => f.MontoAportado));
        
        var prestadoDeReserva = fuentesPrestamos
            .Where(f => f.Tipo == "Reserva")
            .Sum(f => f.MontoAportado);
            
        var prestadoDeInternos = fuentesPrestamos
            .Where(f => f.Tipo == "Interno")
            .Sum(f => f.MontoAportado);
        
        // 5. Capital EN LA CALLE (solo capital, no intereses, de cuotas pendientes)
        var prestamosActivos = await _context.Prestamos
            .Include(p => p.Cuotas)
            .Where(p => p.EstadoPrestamo == "Activo")
            .ToListAsync();
            
        decimal capitalEnCalle = 0;
        foreach (var prestamo in prestamosActivos)
        {
            if (prestamo.Cuotas.Any())
            {
                foreach (var cuota in prestamo.Cuotas)
                {
                    if (cuota.MontoCuota > 0)
                    {
                        var ratioCapital = cuota.MontoCapital / cuota.MontoCuota;
                        capitalEnCalle += cuota.SaldoPendiente * ratioCapital;
                    }
                }
            }
            else
            {
                capitalEnCalle += prestamo.MontoPrestado;
            }
        }
        
        // 6. CUOTAS pagadas y pendientes
        var todasCuotas = await _context.CuotasPrestamo
            .Include(c => c.Prestamo)
            .Where(c => c.Prestamo != null && c.Prestamo.EstadoPrestamo == "Activo")
            .ToListAsync();
        
        var cuotasPagadas = todasCuotas.Where(c => c.EstadoCuota == "Pagada").ToList();
        var cuotasPendientes = todasCuotas.Where(c => c.EstadoCuota != "Pagada").ToList();
        
        // 7. Total COBRADO históricamente
        var totalCobrado = await _context.Pagos.SumAsync(p => p.MontoPago);
        
        // 8. Préstamos SIN fuente de capital asignada
        var prestamosSinFuente = await _context.Prestamos
            .Include(p => p.FuentesCapital)
            .Include(p => p.Cliente)
            .Where(p => p.EstadoPrestamo == "Activo" && !p.FuentesCapital.Any())
            .Select(p => new {
                p.Id,
                ClienteNombre = p.Cliente != null ? p.Cliente.Nombre : "Sin cliente",
                p.MontoPrestado,
                p.FechaPrestamo
            })
            .ToListAsync();
        
        // CÁLCULO DE RESERVA DISPONIBLE
        var capitalTotal = aportesSocios + capitalExternos + capitalReinvertido;
        var reservaDisponible = capitalTotal - capitalEnCalle;
        
        // Balance por aportador externo
        var balanceAportadores = aportadoresExternos.Select(a => new {
            a.Id,
            a.Nombre,
            a.CapitalAportado,
            CapitalPrestado = prestadoPorAportador.GetValueOrDefault(a.Id, 0m),
            Disponible = a.CapitalAportado - prestadoPorAportador.GetValueOrDefault(a.Id, 0m)
        }).ToList();
        
        return Ok(new {
            capitalPorFuente = new {
                socios = new {
                    capitalAportado = Math.Round(aportesSocios, 0),
                    capitalReinvertido = Math.Round(capitalReinvertido, 0),
                    total = Math.Round(aportesSocios + capitalReinvertido, 0)
                },
                aportadoresExternos = balanceAportadores,
                totalCapitalExternos = Math.Round(capitalExternos, 0)
            },
            capitalPrestado = new {
                deReserva = Math.Round(prestadoDeReserva, 0),
                deInternos = Math.Round(prestadoDeInternos, 0),
                deExternos = Math.Round(prestadoPorAportador.Values.Sum(), 0),
                total = Math.Round(prestadoDeReserva + prestadoDeInternos + prestadoPorAportador.Values.Sum(), 0)
            },
            resumen = new {
                capitalTotal = Math.Round(capitalTotal, 0),
                capitalEnCalle = Math.Round(capitalEnCalle, 0),
                reservaDisponible = Math.Round(reservaDisponible, 0),
                totalCobrado = Math.Round(totalCobrado, 0),
                cuotasPagadas = new {
                    cantidad = cuotasPagadas.Count,
                    monto = Math.Round(cuotasPagadas.Sum(c => c.MontoPagado), 0)
                },
                cuotasPendientes = new {
                    cantidad = cuotasPendientes.Count,
                    monto = Math.Round(cuotasPendientes.Sum(c => c.SaldoPendiente), 0)
                }
            },
            alertas = new {
                prestamosSinFuente = prestamosSinFuente.Count > 0 
                    ? new { cantidad = prestamosSinFuente.Count, prestamos = prestamosSinFuente }
                    : null
            }
        });
    }
}
