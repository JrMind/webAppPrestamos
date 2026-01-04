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

    [HttpPost("aplicar-interes")]
    public async Task<IActionResult> AplicarInteresMensual()
    {
        await _gananciasService.AplicarInteresMensualAsync();
        return Ok(new { message = "Interés mensual aplicado exitosamente" });
    }
}

public class AporteDto
{
    public int UsuarioId { get; set; }
    public decimal Monto { get; set; }
    public string? Descripcion { get; set; }
}
