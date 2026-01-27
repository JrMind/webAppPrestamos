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
public class CostosController : ControllerBase
{
    private readonly PrestamosDbContext _context;

    public CostosController(PrestamosDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtener todos los costos operativos
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CostoDto>>> GetAll()
    {
        var costos = await _context.Costos
            .OrderByDescending(c => c.Activo)
            .ThenByDescending(c => c.FechaCreacion)
            .Select(c => new CostoDto(
                c.Id,
                c.Nombre,
                c.Monto,
                c.Frecuencia,
                c.Descripcion,
                c.Activo,
                c.FechaCreacion,
                c.FechaFin,
                c.TotalPagado,
                c.Monto - c.TotalPagado // Restante
            ))
            .ToListAsync();

        return Ok(costos);
    }

    /// <summary>
    /// Obtener costo por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<CostoDto>> GetById(int id)
    {
        var costo = await _context.Costos.FindAsync(id);
        if (costo == null) return NotFound();

        return Ok(new CostoDto(
            costo.Id,
            costo.Nombre,
            costo.Monto,
            costo.Frecuencia,
            costo.Descripcion,
            costo.Activo,
            costo.FechaCreacion,
            costo.FechaFin,
            costo.TotalPagado,
            costo.Monto - costo.TotalPagado
        ));
    }

    /// <summary>
    /// Crear nuevo costo operativo
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CostoDto>> Create(CreateCostoDto dto)
    {
        var costo = new Costo
        {
            Nombre = dto.Nombre,
            Monto = dto.Monto,
            Frecuencia = dto.Frecuencia,
            Descripcion = dto.Descripcion,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        _context.Costos.Add(costo);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = costo.Id }, new CostoDto(
            costo.Id,
            costo.Nombre,
            costo.Monto,
            costo.Frecuencia,
            costo.Descripcion,
            costo.Activo,
            costo.FechaCreacion,
            costo.FechaFin,
            costo.TotalPagado,
            costo.Monto - costo.TotalPagado
        ));
    }

    /// <summary>
    /// Actualizar costo existente
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<CostoDto>> Update(int id, UpdateCostoDto dto)
    {
        var costo = await _context.Costos.FindAsync(id);
        if (costo == null) return NotFound();

        costo.Nombre = dto.Nombre;
        costo.Monto = dto.Monto;
        costo.Frecuencia = dto.Frecuencia;
        costo.Descripcion = dto.Descripcion;
        costo.Activo = dto.Activo;
        costo.FechaFin = dto.FechaFin;

        await _context.SaveChangesAsync();

        return Ok(new CostoDto(
            costo.Id,
            costo.Nombre,
            costo.Monto,
            costo.Frecuencia,
            costo.Descripcion,
            costo.Activo,
            costo.FechaCreacion,
            costo.FechaFin,
            costo.TotalPagado,
            costo.Monto - costo.TotalPagado
        ));
    }

    /// <summary>
    /// Eliminar costo
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var costo = await _context.Costos.FindAsync(id);
        if (costo == null) return NotFound();

        _context.Costos.Remove(costo);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Obtener resumen de costos mensuales
    /// </summary>
    [HttpGet("resumen-mensual")]
    public async Task<ActionResult<object>> GetResumenMensual()
    {
        var costosActivos = await _context.Costos
            .Where(c => c.Activo)
            .ToListAsync();

        var costosMensuales = costosActivos
            .Where(c => c.Frecuencia == "Mensual")
            .Sum(c => c.Monto);

        var costosQuincenales = costosActivos
            .Where(c => c.Frecuencia == "Quincenal")
            .Sum(c => c.Monto * 2); // x2 para mensualizar

        var totalMensual = costosMensuales + costosQuincenales;

        return Ok(new
        {
            CostosMensuales = costosMensuales,
            CostosQuincenales = costosQuincenales,
            TotalMensualizado = totalMensual
        });
    }

    /// <summary>
    /// Pagar (total o parcial) un gasto
    /// </summary>
    [HttpPost("{id}/pagar")]
    public async Task<ActionResult> PagarCosto(
        int id,
        [FromBody] PagarCostoDto dto,
        [FromHeader(Name = "X-User-Email")] string userEmail,
        [FromHeader(Name = "Authorization")] string authorization)
    {
        var costo = await _context.Costos
            .Include(c => c.Pagos)
            .FirstOrDefaultAsync(c => c.Id == id);
            
        if (costo == null)
            return NotFound("Costo no encontrado");
        
        // Calcular restante
        var restante = costo.Monto - costo.TotalPagado;
        
        // Validar que no se pague más del restante
        if (dto.Monto > restante)
            return BadRequest(new
            {
                Error = $"El monto a pagar ({dto.Monto:C}) excede el restante del gasto ({restante:C})"
            });
        
        // Crear servicio de ganancias para validar reserva
        var gananciasService = new Services.GananciasService(_context);
        var reservaActual = await gananciasService.CalcularReservaDisponibleAsync();
        
        // Validar que haya suficiente en reserva
        if (dto.Monto > reservaActual)
            return BadRequest(new
            {
                Error = $"Fondos insuficientes en reserva",
                ReservaDisponible = reservaActual,
                MontoSolicitado = dto.Monto,
                Faltante = dto.Monto - reservaActual
            });
        
        // Registrar el pago
        var pago = new PagoCosto
        {
            CostoId = id,
            MontoPagado = dto.Monto,
            FechaPago = DateTime.UtcNow,
            MetodoPago = dto.MetodoPago,
            Comprobante = dto.Comprobante,
            Observaciones = dto.Observaciones
        };
        
        _context.PagosCostos.Add(pago);
        
        // Actualizar total pagado
        costo.TotalPagado += dto.Monto;
        
        await _context.SaveChangesAsync();
        
        // ACTUALIZAR RESERVA: Descontar el monto pagado
        await gananciasService.ActualizarReservaAsync(-dto.Monto, $"Pago costo: {costo.Nombre} - ${dto.Monto:N0}");
        
        // Recalcular reserva después del pago
        var nuevaReserva = await gananciasService.CalcularReservaDisponibleAsync();
        
        // Responder con información completa
        return Ok(new
        {
            PagoId = pago.Id,
            CostoId = id,
            NombreCosto = costo.Nombre,
            MontoPagado = dto.Monto,
            MontoTotalCosto = costo.Monto,
            TotalPagado = costo.TotalPagado,
            Restante = costo.Monto - costo.TotalPagado,
            ReservaAnterior = reservaActual,
            ReservaActual = nuevaReserva,
            FechaPago = pago.FechaPago
        });
    }
}
