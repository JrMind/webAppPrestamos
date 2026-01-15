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
                c.FechaFin
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
            costo.FechaFin
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
            costo.FechaFin
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
            costo.FechaFin
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
}
