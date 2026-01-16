using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;
using PrestamosApi.Services;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly PrestamosDbContext _context;
    private readonly IDistribucionGananciasService _distribucionService;
    private readonly IGananciasService _gananciasService;

    public AdminController(
        PrestamosDbContext context,
        IDistribucionGananciasService distribucionService,
        IGananciasService gananciasService)
    {
        _context = context;
        _distribucionService = distribucionService;
        _gananciasService = gananciasService;
    }

    /// <summary>
    /// Diagnóstico detallado del flujo de capital en el sistema
    /// </summary>
    [HttpGet("diagnostico-capital")]
    public async Task<IActionResult> DiagnosticoCapital()
    {
        // 1. Aportes iniciales de socios
        var aportesSocios = await _context.Aportes.SumAsync(a => a.MontoInicial);
        
        // 2. Capital de aportadores externos
        var capitalAportadoresExternos = await _context.AportadoresExternos
            .Where(a => a.Estado == "Activo")
            .SumAsync(a => a.MontoTotalAportado);
        
        // 3. Capital reinvertido (ganancias acumuladas en CapitalActual)
        var socios = await _context.Usuarios
            .Where(u => u.Activo && u.Rol == RolUsuario.Socio)
            .ToListAsync();
        
        var capitalReinvertido = socios.Sum(s => s.CapitalActual);
        
        // 4. Capital en préstamos activos (pendiente de cobrar)
        var prestamosActivos = await _context.Prestamos
            .Include(p => p.Cuotas)
            .Where(p => p.EstadoPrestamo == "Activo")
            .ToListAsync();
        
        decimal capitalEnCalle = 0;
        foreach (var prestamo in prestamosActivos)
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
        
        // 5. Calcular reserva disponible
        var reservaDisponible = aportesSocios + capitalAportadoresExternos + capitalReinvertido - capitalEnCalle;
        
        // 6. Detalles por socio
        var detallesSocios = socios.Select(s => new
        {
            s.Id,
            s.Nombre,
            s.CapitalActual,
            s.GananciasAcumuladas,
            AportesIniciales = _context.Aportes
                .Where(a => a.UsuarioId == s.Id)
                .Sum(a => a.MontoInicial)
        }).ToList();
        
        return Ok(new
        {
            resumen = new
            {
                aportesSocios,
                capitalAportadoresExternos,
                capitalReinvertido,
                capitalTotal = aportesSocios + capitalAportadoresExternos + capitalReinvertido,
                capitalEnCalle,
                reservaDisponible,
                formula = "Reserva = Aportes Socios + Capital Externos + Capital Reinvertido - Capital En Calle"
            },
            socios = detallesSocios,
            prestamosActivos = new
            {
                cantidad = prestamosActivos.Count,
                capitalPendiente = capitalEnCalle
            }
        });
    }

    /// <summary>
    /// Recalcula completamente el sistema financiero desde cero
    /// Resetea CapitalActual y GananciasAcumuladas, luego reproduce todos los pagos
    /// </summary>
    [HttpPost("recalcular-sistema")]
    public async Task<IActionResult> RecalcularSistemaCompleto()
    {
        try
        {
            // 1. Resetear CapitalActual y GananciasAcumuladas de todos los socios
            var socios = await _context.Usuarios
                .Where(u => u.Rol == RolUsuario.Socio)
                .ToListAsync();
            
            foreach (var socio in socios)
            {
                socio.CapitalActual = 0;
                socio.GananciasAcumuladas = 0;
            }
            
            // 2. Resetear cobradores
            var cobradores = await _context.Usuarios
                .Where(u => u.Rol == RolUsuario.Cobrador)
                .ToListAsync();
            
            foreach (var cobrador in cobradores)
            {
                cobrador.GananciasAcumuladas = 0;
            }
            
            // 3. Borrar todas las distribuciones existentes
            var distribuciones = await _context.DistribucionesGanancia.ToListAsync();
            _context.DistribucionesGanancia.RemoveRange(distribuciones);
            await _context.SaveChangesAsync();
            
            // 4. Reproducir todos los pagos en orden cronológico
            var pagos = await _context.Pagos
                .Include(p => p.Prestamo)
                    .ThenInclude(pr => pr!.FuentesCapital)
                .OrderBy(p => p.FechaPago)
                .ToListAsync();
            
            int pagosReprocesados = 0;
            foreach (var pago in pagos)
            {
                if (pago.Prestamo != null && pago.Prestamo.FuentesCapital != null)
                {
                    await _distribucionService.DistribuirGananciasPago(
                        pago.PrestamoId, 
                        pago.MontoPago
                    );
                    pagosReprocesados++;
                }
            }
            
            // 5. Guardar cambios finales
            await _context.SaveChangesAsync();
            
            // 6. Calcular nueva reserva disponible
            var reservaDisponible = await _gananciasService.CalcularReservaDisponibleAsync();
            
            return Ok(new
            {
                message = "Sistema recalculado exitosamente",
                sociosActualizados = socios.Count,
                cobradoresActualizados = cobradores.Count,
                pagosReprocesados,
                nuevaReservaDisponible = reservaDisponible
            });
        }
    }

    /// <summary>
    /// Establece manualmente la reserva disponible (lo que realmente hay en caja)
    /// </summary>
    [HttpPost("reserva-disponible")]
    public async Task<IActionResult> SetReservaDisponible([FromBody] decimal montoDisponible)
    {
        try
        {
            var config = await _context.ConfiguracionesSistema
                .FirstOrDefaultAsync(c => c.Clave == "ReservaDisponibleManual");

            if (config == null)
            {
                config = new ConfiguracionSistema
                {
                    Clave = "ReservaDisponibleManual",
                    Valor = montoDisponible.ToString("F2"),
                    FechaActualizacion = DateTime.UtcNow,
                    Descripcion = "Monto disponible en caja/banco configurado manualmente"
                };
                _context.ConfiguracionesSistema.Add(config);
            }
            else
            {
                config.Valor = montoDisponible.ToString("F2");
                config.FechaActualizacion = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Reserva disponible actualizada exitosamente",
                montoDisponible,
                fechaActualizacion = config.FechaActualizacion
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = "Error al actualizar reserva disponible",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Obtiene la reserva disponible manual (si existe) o calculada automáticamente
    /// </summary>
    [HttpGet("reserva-disponible")]
    public async Task<IActionResult> GetReservaDisponible()
    {
        try
        {
            // Intentar obtener valor manual primero
            var configManual = await _context.ConfiguracionesSistema
                .FirstOrDefaultAsync(c => c.Clave == "ReservaDisponibleManual");

            if (configManual != null && decimal.TryParse(configManual.Valor, out var montoManual))
            {
                return Ok(new
                {
                    montoDisponible = montoManual,
                    esManual = true,
                    fechaActualizacion = configManual.FechaActualizacion,
                    descripcion = "Valor configurado manualmente"
                });
            }

            // Si no hay valor manual, calcular automáticamente
            var montoCalculado = await _gananciasService.CalcularReservaDisponibleAsync();

            return Ok(new
            {
                montoDisponible = montoCalculado,
                esManual = false,
                descripcion = "Valor calculado automáticamente"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = "Error al obtener reserva disponible",
                error = ex.Message
            });
        }
    }
}
