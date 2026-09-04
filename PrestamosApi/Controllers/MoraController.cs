using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Attributes;
using PrestamosApi.Data;
using PrestamosApi.DTOs;
using PrestamosApi.Models;
using PrestamosApi.Services;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MoraController : BaseApiController
{
    private readonly PrestamosDbContext _context;
    private readonly IMoraService _moraService;
    private readonly IGananciasService _gananciasService;
    private readonly ILogger<MoraController> _logger;

    public MoraController(
        PrestamosDbContext context,
        IMoraService moraService,
        IGananciasService gananciasService,
        ILogger<MoraController> logger)
    {
        _context = context;
        _moraService = moraService;
        _gananciasService = gananciasService;
        _logger = logger;
    }

    /// <summary>
    /// Mora acumulada de un préstamo, con el desglose por cuota vencida.
    /// </summary>
    [HttpGet("prestamo/{prestamoId}")]
    public async Task<ActionResult<MoraPrestamoDto>> GetMoraPrestamo(int prestamoId)
    {
        var mora = await _moraService.CalcularMoraPrestamoAsync(prestamoId);
        if (mora == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        return Ok(mora);
    }

    /// <summary>
    /// Registra el cobro (total o parcial) de la mora de un préstamo.
    /// No amortiza capital ni cuotas: entra a caja y al balance del medio de pago.
    /// </summary>
    [HttpPost("pago")]
    public async Task<IActionResult> RegistrarPagoMora([FromBody] RegistrarPagoMoraDto dto)
    {
        if (dto.Monto <= 0)
            return BadRequest(new { message = "El monto debe ser mayor a 0" });

        var prestamo = await _context.Prestamos.FirstOrDefaultAsync(p => p.Id == dto.PrestamoId);
        if (prestamo == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        var metodoPago = string.IsNullOrWhiteSpace(dto.MetodoPago) ? "Efectivo" : dto.MetodoPago;

        var pago = new Pago
        {
            PrestamoId = dto.PrestamoId,
            CuotaId = null,
            MontoPago = dto.Monto,
            FechaPago = dto.FechaPago.HasValue
                ? DateTime.SpecifyKind(dto.FechaPago.Value, DateTimeKind.Utc)
                : DateTime.UtcNow,
            MetodoPago = metodoPago,
            TipoPago = "Mora",
            Observaciones = dto.Observaciones ?? "Cobro de mora por cuotas vencidas"
        };

        _context.Pagos.Add(pago);
        await _context.SaveChangesAsync();

        // El dinero cobrado entra a caja. El balance de Nequi/Efectivo se recalcula
        // solo, porque suma todos los Pagos con ese MetodoPago.
        await _gananciasService.ActualizarReservaAsync(
            dto.Monto, $"Cobro de mora préstamo #{dto.PrestamoId}");

        _logger.LogInformation("Mora cobrada en préstamo #{PrestamoId}: ${Monto} por {Medio}",
            dto.PrestamoId, dto.Monto, metodoPago);

        var mora = await _moraService.CalcularMoraPrestamoAsync(dto.PrestamoId);

        return Ok(new
        {
            message = $"Cobro de mora de ${dto.Monto:N0} registrado",
            pagoId = pago.Id,
            mora
        });
    }

    /// <summary>
    /// Tasa de mora mensual global (%).
    /// </summary>
    [HttpGet("tasa")]
    public async Task<ActionResult<TasaMoraDto>> GetTasaMora()
    {
        var tasa = await _moraService.ObtenerTasaMoraMensualAsync();
        return Ok(new TasaMoraDto(tasa));
    }

    [HttpPut("tasa")]
    [AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin, RolUsuario.Administrador)]
    public async Task<IActionResult> UpdateTasaMora([FromBody] TasaMoraDto dto)
    {
        if (dto.TasaMoraMensual < 0 || dto.TasaMoraMensual > 100)
            return BadRequest(new { message = "La tasa de mora debe estar entre 0 y 100" });

        await _moraService.ActualizarTasaMoraMensualAsync(dto.TasaMoraMensual);
        return Ok(new TasaMoraDto(dto.TasaMoraMensual));
    }
}
