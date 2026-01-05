using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.DTOs;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CapitalController : ControllerBase
{
    private readonly PrestamosDbContext _context;

    public CapitalController(PrestamosDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene el balance de capital disponible para crear préstamos
    /// </summary>
    [HttpGet("balance")]
    public async Task<ActionResult<BalanceCapitalDto>> GetBalance()
    {
        // Total cobrado
        var totalCobrado = await _context.Pagos.SumAsync(p => p.MontoPago);
        
        // Capital usado de la reserva para préstamos activos
        var capitalUsadoDeReserva = await _context.FuentesCapitalPrestamo
            .Include(f => f.Prestamo)
            .Where(f => f.Tipo == "Reserva" && f.Prestamo!.EstadoPrestamo == "Activo")
            .SumAsync(f => f.MontoAportado);
        
        // Reserva disponible
        var reservaDisponible = totalCobrado - capitalUsadoDeReserva;
        if (reservaDisponible < 0) reservaDisponible = 0;

        // Socios internos (Admin, Socio, AportadorInterno)
        var socios = await _context.Usuarios
            .Where(u => u.Activo && (u.Rol == RolUsuario.Admin || u.Rol == RolUsuario.Socio || u.Rol == RolUsuario.AportadorInterno))
            .Select(u => new SocioDto(
                u.Id,
                u.Nombre,
                u.Telefono,
                u.Rol.ToString() ?? "Socio",
                u.TasaInteresMensual,
                u.PorcentajeParticipacion,
                u.CapitalActual,
                u.GananciasAcumuladas,
                u.CapitalActual + u.GananciasAcumuladas,
                u.UltimoCalculoInteres
            ))
            .ToListAsync();

        // Aportadores externos activos
        var aportadores = await _context.AportadoresExternos
            .Where(a => a.Estado == "Activo")
            .Select(a => new AportadorExternoDto(
                a.Id, a.Nombre, a.Telefono, a.Email,
                a.TasaInteres, a.DiasParaPago,
                a.MontoTotalAportado, a.MontoPagado, a.SaldoPendiente,
                a.Estado, a.FechaCreacion, a.Notas
            ))
            .ToListAsync();

        return Ok(new BalanceCapitalDto(
            reservaDisponible,
            totalCobrado,
            capitalUsadoDeReserva,
            socios,
            aportadores
        ));
    }

    /// <summary>
    /// Obtiene solo los socios internos para selección
    /// </summary>
    [HttpGet("socios")]
    public async Task<ActionResult<IEnumerable<SocioDto>>> GetSocios()
    {
        var socios = await _context.Usuarios
            .Where(u => u.Activo && (u.Rol == RolUsuario.Admin || u.Rol == RolUsuario.Socio || u.Rol == RolUsuario.AportadorInterno))
            .Select(u => new SocioDto(
                u.Id,
                u.Nombre,
                u.Telefono,
                u.Rol.ToString() ?? "Socio",
                u.TasaInteresMensual,
                u.PorcentajeParticipacion,
                u.CapitalActual,
                u.GananciasAcumuladas,
                u.CapitalActual + u.GananciasAcumuladas,
                u.UltimoCalculoInteres
            ))
            .ToListAsync();

        return Ok(socios);
    }
}
