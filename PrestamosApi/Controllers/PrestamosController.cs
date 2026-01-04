using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.DTOs;
using PrestamosApi.Models;
using PrestamosApi.Services;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PrestamosController : BaseApiController
{
    private readonly PrestamosDbContext _context;
    private readonly IPrestamoService _prestamoService;

    public PrestamosController(PrestamosDbContext context, IPrestamoService prestamoService)
    {
        _context = context;
        _prestamoService = prestamoService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PrestamoDto>>> GetPrestamos(
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] string? estado,
        [FromQuery] string? frecuencia,
        [FromQuery] int? clienteId,
        [FromQuery] string? busqueda)
    {
        var userId = GetCurrentUserId();
        var isCobrador = IsCobrador();

        var query = _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cobrador)
            .Include(p => p.Cuotas)
            .Include(p => p.Pagos)
            .AsQueryable();

        // Si es cobrador, solo mostrar préstamos asignados a él
        if (isCobrador && userId.HasValue)
        {
            query = query.Where(p => p.CobradorId == userId.Value);
        }

        if (fechaDesde.HasValue)
            query = query.Where(p => p.FechaPrestamo >= fechaDesde.Value);
        
        if (fechaHasta.HasValue)
            query = query.Where(p => p.FechaPrestamo <= fechaHasta.Value);

        if (!string.IsNullOrEmpty(estado) && estado != "Todos")
            query = query.Where(p => p.EstadoPrestamo == estado);

        if (!string.IsNullOrEmpty(frecuencia) && frecuencia != "Todos")
            query = query.Where(p => p.FrecuenciaPago == frecuencia);

        if (clienteId.HasValue)
            query = query.Where(p => p.ClienteId == clienteId.Value);

        if (!string.IsNullOrEmpty(busqueda))
            query = query.Where(p => 
                p.Cliente!.Nombre.ToLower().Contains(busqueda.ToLower()) ||
                p.Cliente!.Cedula.Contains(busqueda));

        var prestamos = await query
            .OrderByDescending(p => p.FechaPrestamo)
            .Select(p => new PrestamoDto(
                p.Id,
                p.ClienteId,
                p.Cliente!.Nombre,
                p.Cliente.Cedula,
                p.Cliente.Telefono,
                p.MontoPrestado,
                p.TasaInteres,
                p.TipoInteres,
                p.FrecuenciaPago,
                p.NumeroCuotas,
                p.FechaPrestamo,
                p.FechaVencimiento,
                p.MontoTotal,
                p.MontoIntereses,
                p.MontoCuota,
                p.EstadoPrestamo,
                p.Descripcion,
                p.Cuotas.Sum(c => c.MontoPagado),
                p.Cuotas.Sum(c => c.SaldoPendiente),
                p.Cuotas.Count(c => c.EstadoCuota == "Pagada"),
                p.Cuotas
                    .Where(c => c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida")
                    .OrderBy(c => c.FechaCobro)
                    .Select(c => new CuotaProximaDto(c.FechaCobro, c.SaldoPendiente))
                    .FirstOrDefault(),
                p.CobradorId,
                p.Cobrador != null ? p.Cobrador.Nombre : null,
                p.PorcentajeCobrador
            ))
            .ToListAsync();

        return Ok(prestamos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PrestamoDto>> GetPrestamo(int id)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .Include(p => p.Pagos)
            .Where(p => p.Id == id)
            .Select(p => new PrestamoDto(
                p.Id,
                p.ClienteId,
                p.Cliente!.Nombre,
                p.Cliente.Cedula,
                p.Cliente.Telefono,
                p.MontoPrestado,
                p.TasaInteres,
                p.TipoInteres,
                p.FrecuenciaPago,
                p.NumeroCuotas,
                p.FechaPrestamo,
                p.FechaVencimiento,
                p.MontoTotal,
                p.MontoIntereses,
                p.MontoCuota,
                p.EstadoPrestamo,
                p.Descripcion,
                p.Cuotas.Sum(c => c.MontoPagado),
                p.Cuotas.Sum(c => c.SaldoPendiente),
                p.Cuotas.Count(c => c.EstadoCuota == "Pagada"),
                p.Cuotas
                    .Where(c => c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida")
                    .OrderBy(c => c.FechaCobro)
                    .Select(c => new CuotaProximaDto(c.FechaCobro, c.SaldoPendiente))
                    .FirstOrDefault(),
                p.CobradorId,
                p.Cobrador != null ? p.Cobrador.Nombre : null,
                p.PorcentajeCobrador
            ))
            .FirstOrDefaultAsync();

        if (prestamo == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        return Ok(prestamo);
    }

    [HttpGet("cliente/{clienteId}")]
    public async Task<ActionResult<IEnumerable<PrestamoDto>>> GetPrestamosByCliente(int clienteId)
    {
        var prestamos = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .Where(p => p.ClienteId == clienteId)
            .OrderByDescending(p => p.FechaPrestamo)
            .Select(p => new PrestamoDto(
                p.Id,
                p.ClienteId,
                p.Cliente!.Nombre,
                p.Cliente.Cedula,
                p.Cliente.Telefono,
                p.MontoPrestado,
                p.TasaInteres,
                p.TipoInteres,
                p.FrecuenciaPago,
                p.NumeroCuotas,
                p.FechaPrestamo,
                p.FechaVencimiento,
                p.MontoTotal,
                p.MontoIntereses,
                p.MontoCuota,
                p.EstadoPrestamo,
                p.Descripcion,
                p.Cuotas.Sum(c => c.MontoPagado),
                p.Cuotas.Sum(c => c.SaldoPendiente),
                p.Cuotas.Count(c => c.EstadoCuota == "Pagada"),
                p.Cuotas
                    .Where(c => c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida")
                    .OrderBy(c => c.FechaCobro)
                    .Select(c => new CuotaProximaDto(c.FechaCobro, c.SaldoPendiente))
                    .FirstOrDefault(),
                p.CobradorId,
                p.Cobrador != null ? p.Cobrador.Nombre : null,
                p.PorcentajeCobrador
            ))
            .ToListAsync();

        return Ok(prestamos);
    }

    [HttpPost]
    public async Task<ActionResult<PrestamoDto>> CreatePrestamo(CreatePrestamoDto dto)
    {
        // Validar cliente existe
        var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
        if (cliente == null)
            return BadRequest(new { message = "Cliente no encontrado" });

        // Validar monto mínimo
        if (dto.MontoPrestado < 50)
            return BadRequest(new { message = "El monto mínimo del préstamo es 50$" });

        // Calcular préstamo
        var (montoTotal, montoIntereses, montoCuota, numeroCuotas, fechaVencimiento) = 
            _prestamoService.CalcularPrestamo(
                dto.MontoPrestado, dto.TasaInteres, dto.TipoInteres,
                dto.FrecuenciaPago, dto.Duracion, dto.UnidadDuracion, dto.FechaPrestamo);

        var prestamo = new Prestamo
        {
            ClienteId = dto.ClienteId,
            CobradorId = dto.CobradorId,
            MontoPrestado = dto.MontoPrestado,
            TasaInteres = dto.TasaInteres,
            TipoInteres = dto.TipoInteres,
            FrecuenciaPago = dto.FrecuenciaPago,
            NumeroCuotas = numeroCuotas,
            FechaPrestamo = dto.FechaPrestamo,
            FechaVencimiento = fechaVencimiento,
            MontoTotal = montoTotal,
            MontoIntereses = montoIntereses,
            MontoCuota = montoCuota,
            EstadoPrestamo = "Activo",
            Descripcion = dto.Descripcion,
            PorcentajeCobrador = dto.PorcentajeCobrador
        };

        _context.Prestamos.Add(prestamo);
        await _context.SaveChangesAsync();

        // Generar cuotas
        var cuotas = _prestamoService.GenerarCuotas(prestamo);
        _context.CuotasPrestamo.AddRange(cuotas);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPrestamo), new { id = prestamo.Id },
            new PrestamoDto(
                prestamo.Id, prestamo.ClienteId, cliente.Nombre, cliente.Cedula, cliente.Telefono,
                prestamo.MontoPrestado, prestamo.TasaInteres, prestamo.TipoInteres, prestamo.FrecuenciaPago,
                prestamo.NumeroCuotas, prestamo.FechaPrestamo, prestamo.FechaVencimiento,
                prestamo.MontoTotal, prestamo.MontoIntereses, prestamo.MontoCuota,
                prestamo.EstadoPrestamo, prestamo.Descripcion, 0, prestamo.MontoTotal, 0,
                new CuotaProximaDto(cuotas.First().FechaCobro, cuotas.First().SaldoPendiente)
            ));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePrestamo(int id, UpdatePrestamoDto dto)
    {
        var prestamo = await _context.Prestamos.FindAsync(id);
        if (prestamo == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        prestamo.EstadoPrestamo = dto.EstadoPrestamo;
        prestamo.Descripcion = dto.Descripcion;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePrestamo(int id)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Pagos)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prestamo == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        if (prestamo.Pagos.Any())
            return BadRequest(new { message = "No se puede eliminar un préstamo con pagos registrados" });

        _context.Prestamos.Remove(prestamo);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
