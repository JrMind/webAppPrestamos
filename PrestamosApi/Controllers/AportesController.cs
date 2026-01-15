using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Services;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AportesController : ControllerBase
{
    private readonly PrestamosDbContext _context;
    private readonly IGananciasService _gananciasService;

    public AportesController(PrestamosDbContext context, IGananciasService gananciasService)
    {
        _context = context;
        _gananciasService = gananciasService;
    }

    [HttpGet("balance")]
    public async Task<ActionResult<IEnumerable<object>>> GetBalanceSocios()
    {
        var balance = await _gananciasService.ObtenerBalanceSociosAsync();
        return Ok(balance);
    }

    [HttpGet("usuario/{usuarioId}")]
    public async Task<ActionResult<object>> GetAportesUsuario(int usuarioId)
    {
        var aportes = await _context.Aportes
            .Where(a => a.UsuarioId == usuarioId)
            .OrderByDescending(a => a.FechaAporte)
            .Select(a => new
            {
                a.Id,
                a.MontoInicial,
                a.MontoActual,
                a.FechaAporte,
                a.Descripcion
            })
            .ToListAsync();

        var capitalTotal = await _gananciasService.CalcularCapitalActualAsync(usuarioId);

        return Ok(new
        {
            aportes,
            capitalTotal
        });
    }

    [HttpGet("movimientos/{usuarioId}")]
    public async Task<ActionResult<IEnumerable<object>>> GetMovimientos(int usuarioId)
    {
        var movimientos = await _context.MovimientosCapital
            .Where(m => m.UsuarioId == usuarioId)
            .OrderByDescending(m => m.FechaMovimiento)
            .Select(m => new
            {
                m.Id,
                TipoMovimiento = m.TipoMovimiento.ToString(),
                m.Monto,
                m.SaldoAnterior,
                m.SaldoNuevo,
                m.FechaMovimiento,
                m.Descripcion
            })
            .ToListAsync();

        return Ok(movimientos);
    }

    [HttpPost("aporte")]
    public async Task<IActionResult> RegistrarAporte([FromBody] AporteDto dto)
    {
        await _gananciasService.RegistrarAporteAsync(dto.UsuarioId, dto.Monto, dto.Descripcion);
        return Ok(new { message = "Aporte registrado exitosamente" });
    }

    [HttpPost("retiro")]
    public async Task<IActionResult> RegistrarRetiro([FromBody] AporteDto dto)
    {
        var capitalActual = await _gananciasService.CalcularCapitalActualAsync(dto.UsuarioId);
        if (dto.Monto > capitalActual)
        {
            return BadRequest(new { message = "Monto de retiro excede el capital disponible" });
        }

        await _gananciasService.RegistrarRetiroAsync(dto.UsuarioId, dto.Monto, dto.Descripcion);
        return Ok(new { message = "Retiro registrado exitosamente" });
    }

        await _gananciasService.AplicarInteresMensualAsync();
        return Ok(new { message = "Interés mensual aplicado exitosamente" });
    }

    [HttpPost("ajustar-capital")]
    public async Task<IActionResult> AjustarCapital([FromBody] PrestamosApi.DTOs.AjustarCapitalDto dto)
    {
        var capitalActual = await _gananciasService.CalcularCapitalActualAsync(dto.UsuarioId);
        var diferencia = dto.NuevoCapital - capitalActual;

        if (diferencia == 0) return Ok(new { message = "El capital ya está actualizado" });

        if (diferencia > 0)
        {
            await _gananciasService.RegistrarAporteAsync(dto.UsuarioId, diferencia, "Ajuste manual de capital");
        }
        else
        {
            await _gananciasService.RegistrarRetiroAsync(dto.UsuarioId, Math.Abs(diferencia), "Ajuste manual de capital");
        }

        return Ok(new { message = "Capital ajustado exitosamente" });
    }

    [HttpGet("mi-balance")]
    public async Task<ActionResult<object>> GetMiBalance([FromQuery] int? usuarioId)
    {
        // Si no se especifica usuarioId, intentar obtener del token
        var targetUserId = usuarioId;
        if (!targetUserId.HasValue)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var parsedId))
                targetUserId = parsedId;
        }

        if (!targetUserId.HasValue)
            return BadRequest(new { message = "Se requiere especificar el usuario" });

        var usuario = await _context.Usuarios.FindAsync(targetUserId.Value);
        if (usuario == null)
            return NotFound(new { message = "Usuario no encontrado" });

        // Obtener aportes del usuario
        var aportes = await _context.Aportes
            .Where(a => a.UsuarioId == targetUserId.Value)
            .OrderBy(a => a.FechaAporte)
            .ToListAsync();

        // Calcular interés ganado basado en 3% mensual desde fecha de aporte
        decimal interesGanado = 0;
        decimal capitalAportado = 0;
        var today = DateTime.UtcNow;
        var tasaMensual = usuario.TasaInteresMensual / 100; // 3% = 0.03

        var aportesDetalle = new List<object>();
        DateTime? fechaInicioMasAntigua = null;

        foreach (var aporte in aportes)
        {
            capitalAportado += aporte.MontoInicial;
            
            // Calcular meses transcurridos desde aporte
            var mesesTranscurridos = (int)((today - aporte.FechaAporte).TotalDays / 30);
            if (mesesTranscurridos > 0)
            {
                // Interés simple: capital * tasa * meses
                var interesAporte = aporte.MontoInicial * tasaMensual * mesesTranscurridos;
                interesGanado += interesAporte;
            }

            if (fechaInicioMasAntigua == null || aporte.FechaAporte < fechaInicioMasAntigua)
                fechaInicioMasAntigua = aporte.FechaAporte;

            aportesDetalle.Add(new
            {
                aporte.Id,
                aporte.MontoInicial,
                aporte.MontoActual,
                aporte.FechaAporte,
                aporte.Descripcion,
                MesesTranscurridos = mesesTranscurridos,
                InteresGenerado = aporte.MontoInicial * tasaMensual * mesesTranscurridos
            });
        }

        // Calcular resto de la torta
        // Capital total de TODO el negocio (de todos los usuarios/aportadores)
        var totalCapitalNegocio = await _context.Aportes.SumAsync(a => a.MontoInicial);
        
        // Resto de la torta = (total - mi aporte) / 3 (según indicación del usuario)
        var capitalOtros = totalCapitalNegocio - capitalAportado;
        var restoTorta = capitalOtros > 0 ? capitalOtros / 3 : 0;

        var mesesTotales = fechaInicioMasAntigua.HasValue 
            ? (int)((today - fechaInicioMasAntigua.Value).TotalDays / 30) 
            : 0;

        return Ok(new
        {
            usuarioId = targetUserId.Value,
            nombreUsuario = usuario.Nombre,
            tasaInteresMensual = usuario.TasaInteresMensual,
            capitalAportado,
            interesGanado,
            capitalConInteres = capitalAportado + interesGanado,
            fechaInicio = fechaInicioMasAntigua,
            mesesTranscurridos = mesesTotales,
            totalCapitalNegocio,
            restoTorta,
            aportes = aportesDetalle
        });
    }
}

public class AporteDto
{
    public int UsuarioId { get; set; }
    public decimal Monto { get; set; }
    public string? Descripcion { get; set; }
}

