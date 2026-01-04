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
public class ClientesController : BaseApiController
{
    private readonly PrestamosDbContext _context;

    public ClientesController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet("buscar")]
    public async Task<ActionResult<IEnumerable<ClienteDto>>> BuscarClientes(
        [FromQuery] string q = "",
        [FromQuery] int limite = 10)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Ok(new List<ClienteDto>());
        }

        var termino = q.ToLower();
        var userId = GetCurrentUserId();
        var isCobrador = IsCobrador();

        var query = _context.Clientes.AsQueryable();

        // Si es cobrador, solo mostrar clientes con préstamos asignados a él
        if (isCobrador && userId.HasValue)
        {
            query = query.Where(c => c.Prestamos.Any(p => p.CobradorId == userId.Value));
        }

        var clientes = await query
            .Where(c => c.Nombre.ToLower().Contains(termino) || 
                        c.Cedula.ToLower().Contains(termino))
            .OrderBy(c => c.Nombre)
            .Take(limite)
            .Select(c => new ClienteDto(
                c.Id,
                c.Nombre,
                c.Cedula,
                c.Telefono,
                c.Direccion,
                c.Email,
                c.FechaRegistro,
                c.Estado,
                c.Prestamos.Count(p => p.EstadoPrestamo == "Activo"),
                c.Prestamos.Sum(p => p.MontoPrestado)
            ))
            .ToListAsync();

        return Ok(clientes);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ClienteDto>>> GetClientes()
    {
        var userId = GetCurrentUserId();
        var isCobrador = IsCobrador();

        var query = _context.Clientes
            .Include(c => c.Prestamos)
            .AsQueryable();

        // Si es cobrador, solo mostrar clientes con préstamos asignados a él
        if (isCobrador && userId.HasValue)
        {
            query = query.Where(c => c.Prestamos.Any(p => p.CobradorId == userId.Value));
        }

        var clientes = await query
            .Select(c => new ClienteDto(
                c.Id,
                c.Nombre,
                c.Cedula,
                c.Telefono,
                c.Direccion,
                c.Email,
                c.FechaRegistro,
                c.Estado,
                c.Prestamos.Count(p => p.EstadoPrestamo == "Activo"),
                c.Prestamos.Sum(p => p.MontoPrestado)
            ))
            .ToListAsync();

        return Ok(clientes);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ClienteDto>> GetCliente(int id)
    {
        var cliente = await _context.Clientes
            .Include(c => c.Prestamos)
            .Where(c => c.Id == id)
            .Select(c => new ClienteDto(
                c.Id,
                c.Nombre,
                c.Cedula,
                c.Telefono,
                c.Direccion,
                c.Email,
                c.FechaRegistro,
                c.Estado,
                c.Prestamos.Count(p => p.EstadoPrestamo == "Activo"),
                c.Prestamos.Sum(p => p.MontoPrestado)
            ))
            .FirstOrDefaultAsync();

        if (cliente == null)
            return NotFound(new { message = "Cliente no encontrado" });

        return Ok(cliente);
    }

    [HttpPost]
    public async Task<ActionResult<ClienteDto>> CreateCliente(CreateClienteDto dto)
    {
        // Validar cédula única
        if (await _context.Clientes.AnyAsync(c => c.Cedula == dto.Cedula))
            return BadRequest(new { message = "Ya existe un cliente con esta cédula" });

        var cliente = new Cliente
        {
            Nombre = dto.Nombre,
            Cedula = dto.Cedula,
            Telefono = dto.Telefono,
            Direccion = dto.Direccion,
            Email = dto.Email,
            FechaRegistro = DateTime.UtcNow
        };

        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCliente), new { id = cliente.Id },
            new ClienteDto(cliente.Id, cliente.Nombre, cliente.Cedula, cliente.Telefono,
                cliente.Direccion, cliente.Email, cliente.FechaRegistro, cliente.Estado, 0, 0));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCliente(int id, UpdateClienteDto dto)
    {
        var cliente = await _context.Clientes.FindAsync(id);
        if (cliente == null)
            return NotFound(new { message = "Cliente no encontrado" });

        cliente.Nombre = dto.Nombre;
        cliente.Telefono = dto.Telefono;
        cliente.Direccion = dto.Direccion;
        cliente.Email = dto.Email;
        cliente.Estado = dto.Estado;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCliente(int id)
    {
        var cliente = await _context.Clientes
            .Include(c => c.Prestamos)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cliente == null)
            return NotFound(new { message = "Cliente no encontrado" });

        if (cliente.Prestamos.Any(p => p.EstadoPrestamo == "Activo"))
            return BadRequest(new { message = "No se puede eliminar un cliente con préstamos activos" });

        _context.Clientes.Remove(cliente);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
