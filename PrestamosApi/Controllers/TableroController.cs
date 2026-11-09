using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Attributes;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

/// <summary>
/// Tablero: notas/compromisos asociados a préstamos y cuenta corriente entre cobradores y la oficina.
/// El rol Administrador solo ve los préstamos y cobradores de su alcance.
/// </summary>
[ApiController]
[Route("api/tablero")]
[Authorize]
[AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin, RolUsuario.Administrador)]
public class TableroController : BaseApiController
{
    private readonly PrestamosDbContext _context;

    public TableroController(PrestamosDbContext context)
    {
        _context = context;
    }

    // ───────────────────────── Notas de préstamos ─────────────────────────

    [HttpGet("notas-prestamos")]
    public async Task<ActionResult<IEnumerable<NotaTableroDto>>> GetNotasPrestamos([FromQuery] int? prestamoId)
    {
        var query = _context.NotasPrestamo.AsQueryable();
        if (prestamoId.HasValue) query = query.Where(n => n.PrestamoId == prestamoId.Value);

        var permitidos = IsAdministrador() ? GetCobradorIdsPermitidos() : null;
        if (permitidos != null)
            query = query.Where(n => n.Prestamo!.CobradorId != null && permitidos.Contains(n.Prestamo.CobradorId.Value));

        var notas = await query
            .OrderByDescending(n => n.FechaCreacion)
            .Select(n => new NotaTableroDto
            {
                Id = n.Id,
                PrestamoId = n.PrestamoId,
                ClienteNombre = n.Prestamo!.Cliente != null ? n.Prestamo.Cliente.Nombre : "",
                ClienteCedula = n.Prestamo.Cliente != null ? n.Prestamo.Cliente.Cedula : "",
                ClienteTelefono = n.Prestamo.Cliente != null ? n.Prestamo.Cliente.Telefono : null,
                CobradorId = n.Prestamo.CobradorId,
                CobradorNombre = n.Prestamo.Cobrador != null ? n.Prestamo.Cobrador.Nombre : null,
                EstadoPrestamo = n.Prestamo.EstadoPrestamo,
                Contenido = n.Contenido,
                FechaCompromiso = n.FechaCompromiso,
                MontoCompromiso = n.MontoCompromiso,
                Cumplida = n.Cumplida,
                FechaCumplida = n.FechaCumplida,
                FechaCreacion = n.FechaCreacion,
                UsuarioNombre = n.Usuario != null ? n.Usuario.Nombre : "Sistema"
            })
            .ToListAsync();

        return Ok(notas);
    }

    [HttpPost("notas-prestamos")]
    public async Task<ActionResult> CreateNotaPrestamo([FromBody] GuardarNotaPrestamoDto dto)
    {
        var error = ValidarNota(dto);
        if (error != null) return BadRequest(new { message = error });

        var prestamo = await _context.Prestamos.FindAsync(dto.PrestamoId);
        if (prestamo == null) return NotFound(new { message = "Préstamo no encontrado" });
        if (!PuedeVerCobrador(prestamo.CobradorId)) return Forbid();

        var nota = new NotaPrestamo
        {
            PrestamoId = dto.PrestamoId,
            Contenido = dto.Contenido.Trim(),
            FechaCompromiso = dto.FechaCompromiso,
            MontoCompromiso = dto.MontoCompromiso,
            UsuarioId = GetCurrentUserId(),
            FechaCreacion = DateTime.UtcNow
        };
        _context.NotasPrestamo.Add(nota);
        await _context.SaveChangesAsync();
        return Ok(new { nota.Id });
    }

    [HttpPut("notas-prestamos/{id}")]
    public async Task<ActionResult> UpdateNotaPrestamo(int id, [FromBody] GuardarNotaPrestamoDto dto)
    {
        var error = ValidarNota(dto);
        if (error != null) return BadRequest(new { message = error });

        var nota = await _context.NotasPrestamo.Include(n => n.Prestamo).FirstOrDefaultAsync(n => n.Id == id);
        if (nota == null) return NotFound(new { message = "Nota no encontrada" });
        if (!PuedeVerCobrador(nota.Prestamo?.CobradorId)) return Forbid();

        nota.Contenido = dto.Contenido.Trim();
        nota.FechaCompromiso = dto.FechaCompromiso;
        nota.MontoCompromiso = dto.MontoCompromiso;
        await _context.SaveChangesAsync();
        return Ok(new { nota.Id });
    }

    [HttpPatch("notas-prestamos/{id}/cumplida")]
    public async Task<ActionResult> MarcarNotaCumplida(int id, [FromBody] MarcarCumplidaDto dto)
    {
        var nota = await _context.NotasPrestamo.Include(n => n.Prestamo).FirstOrDefaultAsync(n => n.Id == id);
        if (nota == null) return NotFound(new { message = "Nota no encontrada" });
        if (!PuedeVerCobrador(nota.Prestamo?.CobradorId)) return Forbid();

        nota.Cumplida = dto.Cumplida;
        nota.FechaCumplida = dto.Cumplida ? DateTime.UtcNow : null;
        await _context.SaveChangesAsync();
        return Ok(new { nota.Id, nota.Cumplida });
    }

    [HttpDelete("notas-prestamos/{id}")]
    public async Task<ActionResult> DeleteNotaPrestamo(int id)
    {
        var nota = await _context.NotasPrestamo.Include(n => n.Prestamo).FirstOrDefaultAsync(n => n.Id == id);
        if (nota == null) return NotFound(new { message = "Nota no encontrada" });
        if (!PuedeVerCobrador(nota.Prestamo?.CobradorId)) return Forbid();

        _context.NotasPrestamo.Remove(nota);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ───────────────────── Cuenta corriente de cobradores ─────────────────────

    /// <summary>
    /// Saldo por cobrador. Saldo positivo = el cobrador le debe a la oficina;
    /// negativo = la oficina le debe al cobrador.
    /// </summary>
    [HttpGet("cuentas-cobradores")]
    public async Task<ActionResult<IEnumerable<CuentaCobradorDto>>> GetCuentasCobradores()
    {
        var cobradoresQuery = _context.Usuarios.Where(u => u.Rol == RolUsuario.Cobrador);
        var permitidos = IsAdministrador() ? GetCobradorIdsPermitidos() : null;
        if (permitidos != null) cobradoresQuery = cobradoresQuery.Where(u => permitidos.Contains(u.Id));

        var cobradores = await cobradoresQuery
            .Select(u => new { u.Id, u.Nombre, u.Activo })
            .ToListAsync();

        var ids = cobradores.Select(c => c.Id).ToList();
        var resumen = await _context.MovimientosCuentaCobrador
            .Where(m => ids.Contains(m.CobradorId))
            .GroupBy(m => m.CobradorId)
            .Select(g => new
            {
                CobradorId = g.Key,
                Saldo = g.Sum(m => m.Tipo == MovimientoCuentaCobrador.TipoCobradorDebe ? m.Monto : -m.Monto),
                Cantidad = g.Count(),
                Ultimo = g.Max(m => m.Fecha)
            })
            .ToDictionaryAsync(x => x.CobradorId);

        var cuentas = cobradores
            .Select(c =>
            {
                resumen.TryGetValue(c.Id, out var r);
                return new CuentaCobradorDto
                {
                    CobradorId = c.Id,
                    CobradorNombre = c.Nombre,
                    Activo = c.Activo,
                    Saldo = r?.Saldo ?? 0,
                    CantidadMovimientos = r?.Cantidad ?? 0,
                    UltimoMovimiento = r?.Ultimo
                };
            })
            // Se muestran los activos y los inactivos que aún tienen movimientos
            .Where(c => c.Activo || c.CantidadMovimientos > 0)
            .OrderByDescending(c => Math.Abs(c.Saldo))
            .ThenBy(c => c.CobradorNombre)
            .ToList();

        return Ok(cuentas);
    }

    [HttpGet("cuentas-cobradores/{cobradorId}/movimientos")]
    public async Task<ActionResult<IEnumerable<MovimientoCuentaDto>>> GetMovimientosCobrador(int cobradorId)
    {
        if (!PuedeVerCobrador(cobradorId)) return Forbid();

        var movimientos = await _context.MovimientosCuentaCobrador
            .Where(m => m.CobradorId == cobradorId)
            .OrderBy(m => m.Fecha).ThenBy(m => m.Id)
            .Select(m => new MovimientoCuentaDto
            {
                Id = m.Id,
                CobradorId = m.CobradorId,
                Tipo = m.Tipo,
                Monto = m.Monto,
                Descripcion = m.Descripcion,
                Fecha = m.Fecha,
                FechaCreacion = m.FechaCreacion,
                UsuarioNombre = m.Usuario != null ? m.Usuario.Nombre : "Sistema"
            })
            .ToListAsync();

        // Saldo acumulado después de cada movimiento (en orden cronológico)
        decimal acumulado = 0;
        foreach (var m in movimientos)
        {
            acumulado += m.Tipo == MovimientoCuentaCobrador.TipoCobradorDebe ? m.Monto : -m.Monto;
            m.SaldoAcumulado = acumulado;
        }

        movimientos.Reverse(); // Más recientes primero
        return Ok(movimientos);
    }

    [HttpPost("cuentas-cobradores/{cobradorId}/movimientos")]
    public async Task<ActionResult> CreateMovimientoCobrador(int cobradorId, [FromBody] CrearMovimientoCuentaDto dto)
    {
        if (dto.Tipo != MovimientoCuentaCobrador.TipoCobradorDebe && dto.Tipo != MovimientoCuentaCobrador.TipoOficinaDebe)
            return BadRequest(new { message = "Tipo de movimiento inválido" });
        if (dto.Monto <= 0)
            return BadRequest(new { message = "El monto debe ser mayor a cero" });
        if (string.IsNullOrWhiteSpace(dto.Descripcion))
            return BadRequest(new { message = "La descripción es requerida" });

        var cobrador = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == cobradorId && u.Rol == RolUsuario.Cobrador);
        if (cobrador == null) return NotFound(new { message = "Cobrador no encontrado" });
        if (!PuedeVerCobrador(cobradorId)) return Forbid();

        var mov = new MovimientoCuentaCobrador
        {
            CobradorId = cobradorId,
            Tipo = dto.Tipo,
            Monto = dto.Monto,
            Descripcion = dto.Descripcion.Trim(),
            Fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5)), // Hora Colombia
            FechaCreacion = DateTime.UtcNow,
            UsuarioId = GetCurrentUserId()
        };
        _context.MovimientosCuentaCobrador.Add(mov);
        await _context.SaveChangesAsync();
        return Ok(new { mov.Id });
    }

    [HttpDelete("movimientos-cobrador/{id}")]
    public async Task<ActionResult> DeleteMovimientoCobrador(int id)
    {
        var mov = await _context.MovimientosCuentaCobrador.FindAsync(id);
        if (mov == null) return NotFound(new { message = "Movimiento no encontrado" });
        if (!PuedeVerCobrador(mov.CobradorId)) return Forbid();

        _context.MovimientosCuentaCobrador.Remove(mov);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ───────────────────────── Helpers ─────────────────────────

    private bool PuedeVerCobrador(int? cobradorId)
    {
        var permitidos = IsAdministrador() ? GetCobradorIdsPermitidos() : null;
        return permitidos == null || (cobradorId.HasValue && permitidos.Contains(cobradorId.Value));
    }

    private static string? ValidarNota(GuardarNotaPrestamoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Contenido)) return "El contenido de la nota es requerido";
        if (dto.MontoCompromiso.HasValue && dto.MontoCompromiso.Value <= 0) return "El monto pactado debe ser mayor a cero";
        return null;
    }
}

public class GuardarNotaPrestamoDto
{
    public int PrestamoId { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public DateOnly? FechaCompromiso { get; set; }
    public decimal? MontoCompromiso { get; set; }
}

public class MarcarCumplidaDto
{
    public bool Cumplida { get; set; }
}

public class CrearMovimientoCuentaDto
{
    public string Tipo { get; set; } = MovimientoCuentaCobrador.TipoCobradorDebe;
    public decimal Monto { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public DateOnly? Fecha { get; set; }
}

public class NotaTableroDto
{
    public int Id { get; set; }
    public int PrestamoId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public string ClienteCedula { get; set; } = string.Empty;
    public string? ClienteTelefono { get; set; }
    public int? CobradorId { get; set; }
    public string? CobradorNombre { get; set; }
    public string EstadoPrestamo { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public DateOnly? FechaCompromiso { get; set; }
    public decimal? MontoCompromiso { get; set; }
    public bool Cumplida { get; set; }
    public DateTime? FechaCumplida { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
}

public class CuentaCobradorDto
{
    public int CobradorId { get; set; }
    public string CobradorNombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public decimal Saldo { get; set; }
    public int CantidadMovimientos { get; set; }
    public DateOnly? UltimoMovimiento { get; set; }
}

public class MovimientoCuentaDto
{
    public int Id { get; set; }
    public int CobradorId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public decimal SaldoAcumulado { get; set; }
}
