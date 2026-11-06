using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Attributes;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

/// <summary>
/// Balance económico del mes: qué costos cubre lo que recoge cada cobrador (interés + mora),
/// cuánto salario le corresponde, y si el negocio en total fue rentable o se comió capital.
///
/// Orden de prioridad de los costos de cada cobrador:
///   1. Costo del capital a cargo (tasa promedio ponderada de los aportadores externos)
///   2. Costo de ruta (valor único editable, uno por cobrador)
///   3. Moto (valor mensual por cobrador, editable y borrable)
///   4. Cuota mensual del préstamo asociado al cobrador (préstamo del sistema)
/// Solo cuando esos costos están cubiertos se va llenando el salario, con tope en su salario mensual.
///
/// La configuración se guarda en ConfiguracionesSistema (sin cambios de esquema).
/// </summary>
[ApiController]
[Route("api/balance-economico")]
[AuthorizeRoles(RolUsuario.Socio, RolUsuario.Admin)]
public class BalanceEconomicoController : BaseApiController
{
    private const string ClaveCostoRuta = "CostoRutaMensual";
    private const decimal CostoRutaPorDefecto = 300_000m;
    private const string PrefijoMoto = "MotoCobrador:";
    private const string PrefijoPrestamo = "PrestamoCobrador:";
    private const string PrefijoSalario = "SalarioCobrador:";
    private const string PrefijoCostoCobradores = "CostoOperativoCobradores:"; // costoId -> "1,5" (ids de cobradores)
    private const string PrefijoOculto = "BalanceOcultarCobrador:"; // cobrador que no se muestra ni paga ruta

    private readonly PrestamosDbContext _context;

    public BalanceEconomicoController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<BalanceEconomicoDto>> Get([FromQuery] int? anio = null, [FromQuery] int? mes = null)
    {
        var hoy = DateTime.UtcNow;
        var inicio = DateTime.SpecifyKind(new DateTime(anio ?? hoy.Year, mes is >= 1 and <= 12 ? mes.Value : hoy.Month, 1), DateTimeKind.Utc);
        var fin = inicio.AddMonths(1);

        // ── Configuración ──────────────────────────────────────────
        var config = await _context.ConfiguracionesSistema
            .Where(c => c.Clave == ClaveCostoRuta || c.Clave.StartsWith(PrefijoMoto)
                     || c.Clave.StartsWith(PrefijoPrestamo) || c.Clave.StartsWith(PrefijoSalario)
                     || c.Clave.StartsWith(PrefijoCostoCobradores) || c.Clave.StartsWith(PrefijoOculto))
            .ToDictionaryAsync(c => c.Clave, c => c.Valor);

        decimal LeerDecimal(string clave, decimal porDefecto = 0m) =>
            config.TryGetValue(clave, out var v) && decimal.TryParse(v, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : porDefecto;
        int? LeerEntero(string clave) =>
            config.TryGetValue(clave, out var v) && int.TryParse(v, out var i) ? i : null;

        var costoRuta = LeerDecimal(ClaveCostoRuta, CostoRutaPorDefecto);

        // ── Costo del capital: tasa promedio ponderada de los aportadores externos ──
        var aportadores = await _context.AportadoresExternos.Where(a => a.Estado == "Activo").ToListAsync();
        var capitalAportadores = aportadores.Sum(a => a.MontoTotalAportado);
        var costoAportadoresMes = aportadores.Sum(a => a.MontoTotalAportado * a.TasaInteres / 100m);
        var tasaCapital = capitalAportadores > 0 ? costoAportadoresMes / capitalAportadores * 100m : 0m;

        // ── Recaudo del mes (interés + mora) por préstamo ──────────────
        var cuotasPagadasMes = await _context.CuotasPrestamo
            .Include(c => c.Prestamo)
            .Where(c => c.FechaPago.HasValue && c.FechaPago >= inicio && c.FechaPago < fin && c.MontoPagado > 0)
            .ToListAsync();
        var pagosSinCuotaMes = await _context.Pagos
            .Include(p => p.Prestamo)
            .Where(p => (p.TipoPago == TiposPago.SoloInteres || p.TipoPago == TiposPago.Mora)
                     && p.FechaPago >= inicio && p.FechaPago < fin)
            .ToListAsync();

        static decimal InteresDeCuota(CuotaPrestamo c) =>
            c.MontoInteres * (c.MontoCuota > 0 ? c.MontoPagado / c.MontoCuota : 1m);

        // Interés programado del mes (meta): interés de las cuotas con fecha de cobro en el mes
        var interesProgramado = await _context.CuotasPrestamo
            .Where(c => c.FechaCobro >= inicio && c.FechaCobro < fin && c.Prestamo!.EstadoPrestamo != "Terminado")
            .GroupBy(c => c.Prestamo!.CobradorId)
            .Select(g => new { CobradorId = g.Key, Interes = g.Sum(c => c.MontoInteres) })
            .ToListAsync();

        // ── Cobradores y su capital a cargo ────────────────────────
        var todosCobradores = await _context.Usuarios
            .Where(u => u.Rol == RolUsuario.Cobrador && u.Activo)
            .OrderBy(u => u.Nombre)
            .Select(u => new { u.Id, u.Nombre })
            .ToListAsync();
        var cobradores = todosCobradores.Where(c => !config.ContainsKey(PrefijoOculto + c.Id)).ToList();
        var cobradoresOcultos = todosCobradores.Where(c => config.ContainsKey(PrefijoOculto + c.Id))
            .Select(c => new CobradorOcultoDto { CobradorId = c.Id, CobradorNombre = c.Nombre }).ToList();

        // Capital a cargo: préstamos activos y vencidos (un vencido sigue en la calle y lo sigue cobrando él)
        var prestamosActivos = await _context.Prestamos
            .Where(p => p.EstadoPrestamo == "Activo" || p.EstadoPrestamo == "Vencido")
            .Select(p => new { p.CobradorId, p.MontoPrestado })
            .ToListAsync();

        // Préstamos asociados a cobradores: cuotas que vencen en el mes
        var idsPrestamosAsociados = cobradores
            .Select(c => LeerEntero(PrefijoPrestamo + c.Id))
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var prestamosAsociados = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .Where(p => idsPrestamosAsociados.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // Costos operativos activos, mensualizados, y a qué cobradores están asociados (se reparten en partes iguales)
        var costosOperativos = await _context.Costos.Where(c => c.Activo).OrderBy(c => c.Nombre).ToListAsync();
        static decimal Mensual(Costo c) => c.Frecuencia switch
        {
            "Mensual" => c.Monto,
            "Quincenal" => c.Monto * 2,
            _ => 0m // "Único": no es un costo mensual
        };
        List<int> CobradoresDeCosto(int costoId) =>
            config.TryGetValue(PrefijoCostoCobradores + costoId, out var v)
                ? v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x, out var i) ? i : 0).Where(i => i > 0).Distinct().ToList()
                : new List<int>();
        var costosConCobradores = costosOperativos
            .Select(c => (Costo: c, Mensual: Mensual(c), Cobradores: CobradoresDeCosto(c.Id)))
            .ToList();

        var filas = new List<BalanceCobradorDto>();
        foreach (var cob in cobradores)
        {
            var capitalACargo = prestamosActivos.Where(p => p.CobradorId == cob.Id).Sum(p => p.MontoPrestado);
            var interesCobrado = cuotasPagadasMes.Where(c => c.Prestamo?.CobradorId == cob.Id).Sum(InteresDeCuota)
                               + pagosSinCuotaMes.Where(p => p.TipoPago == TiposPago.SoloInteres && p.Prestamo?.CobradorId == cob.Id).Sum(p => p.MontoPago);
            var moraCobrada = pagosSinCuotaMes.Where(p => p.TipoPago == TiposPago.Mora && p.Prestamo?.CobradorId == cob.Id).Sum(p => p.MontoPago);
            var recaudo = interesCobrado + moraCobrada;

            var moto = LeerDecimal(PrefijoMoto + cob.Id);
            var prestamoId = LeerEntero(PrefijoPrestamo + cob.Id);
            Prestamo? prestamo = prestamoId.HasValue && prestamosAsociados.TryGetValue(prestamoId.Value, out var pr) ? pr : null;
            var cuotaPrestamo = prestamo != null ? CuotaMensual(prestamo, inicio, fin) : 0m;

            var costos = new List<CostoCobradorDto>
            {
                new() { Clave = "capital", Nombre = $"Costo del capital a cargo ({tasaCapital:0.##}%)", Monto = Math.Round(capitalACargo * tasaCapital / 100m, 0) },
                new() { Clave = "ruta", Nombre = "Costo de ruta", Monto = costoRuta },
            };
            if (moto > 0) costos.Add(new() { Clave = "moto", Nombre = "Moto", Monto = moto });
            if (prestamoId.HasValue)
                costos.Add(new()
                {
                    Clave = "prestamo",
                    Nombre = prestamo != null
                        ? $"Cuota préstamo #{prestamo.Id} ({prestamo.Cliente?.Nombre}{(prestamo.EsCongelado ? ", congelado" : "")})"
                        : $"Préstamo #{prestamoId} (no existe)",
                    Monto = Math.Round(cuotaPrestamo, 0)
                });

            // Costos operativos asociados a este cobrador (si un costo es de varios, se reparte en partes iguales)
            foreach (var (costo, mensual, ids) in costosConCobradores.Where(x => x.Cobradores.Contains(cob.Id)))
                costos.Add(new()
                {
                    Clave = $"costo-{costo.Id}",
                    Nombre = ids.Count > 1 ? $"{costo.Nombre} (1/{ids.Count})" : costo.Nombre,
                    Monto = Math.Round(mensual / ids.Count, 0)
                });

            // Llenar costos en orden de prioridad con lo recaudado
            var disponible = recaudo;
            foreach (var c in costos)
            {
                c.Cubierto = Math.Round(Math.Min(Math.Max(disponible, 0), c.Monto), 0);
                disponible -= c.Monto;
            }
            var totalCostos = costos.Sum(c => c.Monto);
            var salario = LeerDecimal(PrefijoSalario + cob.Id);
            var sobranteTrasCostos = recaudo - totalCostos;
            var salarioCorresponde = salario > 0 ? Math.Min(Math.Max(sobranteTrasCostos, 0), salario) : Math.Max(sobranteTrasCostos, 0);

            filas.Add(new BalanceCobradorDto
            {
                CobradorId = cob.Id,
                CobradorNombre = cob.Nombre,
                CapitalACargo = Math.Round(capitalACargo, 0),
                InteresProgramado = Math.Round(interesProgramado.FirstOrDefault(i => i.CobradorId == cob.Id)?.Interes ?? 0, 0),
                InteresCobrado = Math.Round(interesCobrado, 0),
                MoraCobrada = Math.Round(moraCobrada, 0),
                Recaudo = Math.Round(recaudo, 0),
                Costos = costos,
                TotalCostos = Math.Round(totalCostos, 0),
                CostosCubiertos = Math.Round(Math.Min(Math.Max(recaudo, 0), totalCostos), 0),
                Moto = moto,
                PrestamoAsociadoId = prestamoId,
                PrestamoAsociadoCliente = prestamo?.Cliente?.Nombre,
                PrestamoAsociadoCongelado = prestamo?.EsCongelado ?? false,
                Salario = salario,
                SalarioCorresponde = Math.Round(salarioCorresponde, 0),
                Excedente = Math.Round(sobranteTrasCostos - (salario > 0 ? salario : 0), 0),
                Estado = sobranteTrasCostos < 0 ? "NoCubreCostos"
                       : salario > 0 && sobranteTrasCostos < salario ? "SalarioParcial"
                       : "Rentable"
            });
        }

        // ── Barra general del negocio ──────────────────────────────
        var interesTotal = cuotasPagadasMes.Sum(InteresDeCuota)
                         + pagosSinCuotaMes.Where(p => p.TipoPago == TiposPago.SoloInteres).Sum(p => p.MontoPago);
        var moraTotal = pagosSinCuotaMes.Where(p => p.TipoPago == TiposPago.Mora).Sum(p => p.MontoPago);
        var ingresos = interesTotal + moraTotal;

        var costosOperativosMes = costosConCobradores.Sum(x => x.Mensual);

        var costosNegocio = new List<CostoCobradorDto>
        {
            new()
            {
                Clave = "operativos",
                Nombre = $"Costos operativos (incluye {costoAportadoresMes:N0} de aportadores externos)",
                Monto = Math.Round(costosOperativosMes + costoAportadoresMes, 0)
            },
            new() { Clave = "ruta", Nombre = $"Rutas ({cobradores.Count} × {costoRuta:N0})", Monto = costoRuta * cobradores.Count },
        };
        var motos = filas.Sum(f => f.Moto);
        if (motos > 0) costosNegocio.Add(new() { Clave = "motos", Nombre = "Motos", Monto = motos });
        var disponibleNegocio = ingresos;
        foreach (var c in costosNegocio)
        {
            c.Cubierto = Math.Round(Math.Min(Math.Max(disponibleNegocio, 0), c.Monto), 0);
            disponibleNegocio -= c.Monto;
        }
        var totalCostosNegocio = costosNegocio.Sum(c => c.Monto);
        var salariosPagables = filas.Sum(f => f.SalarioCorresponde);
        var resultado = ingresos - totalCostosNegocio - salariosPagables;

        return Ok(new BalanceEconomicoDto
        {
            Anio = inicio.Year,
            Mes = inicio.Month,
            CostoRuta = costoRuta,
            TasaCostoCapital = Math.Round(tasaCapital, 2),
            Cobradores = filas,
            CobradoresOcultos = cobradoresOcultos,
            CostosOperativos = costosConCobradores.Select(x => new CostoOperativoAsociableDto
            {
                Id = x.Costo.Id,
                Nombre = x.Costo.Nombre,
                MontoMensual = Math.Round(x.Mensual, 0),
                CobradorIds = x.Cobradores
            }).ToList(),
            Negocio = new BalanceNegocioDto
            {
                InteresCobrado = Math.Round(interesTotal, 0),
                MoraCobrada = Math.Round(moraTotal, 0),
                Ingresos = Math.Round(ingresos, 0),
                Costos = costosNegocio,
                TotalCostos = Math.Round(totalCostosNegocio, 0),
                SalariosCobradores = Math.Round(salariosPagables, 0),
                Resultado = Math.Round(resultado, 0)
            }
        });
    }

    [HttpPut("costo-ruta")]
    public async Task<IActionResult> ActualizarCostoRuta([FromBody] ValorDto dto)
    {
        if (dto.Valor < 0) return BadRequest(new { message = "El costo de ruta no puede ser negativo" });
        await GuardarAsync(ClaveCostoRuta, dto.Valor.ToString(System.Globalization.CultureInfo.InvariantCulture), "Costo de ruta mensual por cobrador");
        return Ok(new { costoRuta = dto.Valor });
    }

    /// <summary>Moto mensual del cobrador. Valor null o 0 la elimina.</summary>
    [HttpPut("cobradores/{cobradorId:int}/moto")]
    public async Task<IActionResult> ActualizarMoto(int cobradorId, [FromBody] ValorOpcionalDto dto)
    {
        if (!await EsCobradorAsync(cobradorId)) return NotFound(new { message = "Cobrador no encontrado" });
        if (dto.Valor is < 0) return BadRequest(new { message = "El valor de la moto no puede ser negativo" });
        if (dto.Valor is null or 0) await BorrarAsync(PrefijoMoto + cobradorId);
        else await GuardarAsync(PrefijoMoto + cobradorId, dto.Valor.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Moto mensual del cobrador {cobradorId}");
        return Ok(new { moto = dto.Valor ?? 0 });
    }

    /// <summary>Préstamo del sistema asociado al cobrador. PrestamoId null lo desasocia.</summary>
    [HttpPut("cobradores/{cobradorId:int}/prestamo")]
    public async Task<IActionResult> ActualizarPrestamo(int cobradorId, [FromBody] PrestamoAsociadoDto dto)
    {
        if (!await EsCobradorAsync(cobradorId)) return NotFound(new { message = "Cobrador no encontrado" });
        if (dto.PrestamoId is null)
        {
            await BorrarAsync(PrefijoPrestamo + cobradorId);
            return Ok(new { prestamoId = (int?)null });
        }
        var prestamo = await _context.Prestamos.Include(p => p.Cliente).FirstOrDefaultAsync(p => p.Id == dto.PrestamoId);
        if (prestamo == null) return BadRequest(new { message = $"No existe el préstamo #{dto.PrestamoId}" });
        await GuardarAsync(PrefijoPrestamo + cobradorId, prestamo.Id.ToString(), $"Préstamo asociado al cobrador {cobradorId}");
        return Ok(new { prestamoId = prestamo.Id, clienteNombre = prestamo.Cliente?.Nombre });
    }

    /// <summary>Oculta o vuelve a mostrar un cobrador en el balance (oculto: no paga ruta ni cuenta en costos).</summary>
    [HttpPut("cobradores/{cobradorId:int}/visible")]
    public async Task<IActionResult> CambiarVisible(int cobradorId, [FromBody] VisibleDto dto)
    {
        if (!await EsCobradorAsync(cobradorId)) return NotFound(new { message = "Cobrador no encontrado" });
        if (dto.Visible) await BorrarAsync(PrefijoOculto + cobradorId);
        else await GuardarAsync(PrefijoOculto + cobradorId, "1", $"Cobrador {cobradorId} oculto en el balance económico");
        return Ok(new { cobradorId, visible = dto.Visible });
    }

    /// <summary>Asocia un costo operativo a uno o varios cobradores (lista vacía lo desasocia).</summary>
    [HttpPut("costos-operativos/{costoId:int}/cobradores")]
    public async Task<IActionResult> AsociarCostoOperativo(int costoId, [FromBody] CostoCobradoresDto dto)
    {
        if (!await _context.Costos.AnyAsync(c => c.Id == costoId))
            return NotFound(new { message = "Costo operativo no encontrado" });
        var ids = (dto.CobradorIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count > 0)
        {
            var validos = await _context.Usuarios.CountAsync(u => ids.Contains(u.Id) && u.Rol == RolUsuario.Cobrador);
            if (validos != ids.Count) return BadRequest(new { message = "Alguno de los cobradores no existe" });
            await GuardarAsync(PrefijoCostoCobradores + costoId, string.Join(",", ids), $"Cobradores que cubren el costo operativo {costoId}");
        }
        else await BorrarAsync(PrefijoCostoCobradores + costoId);
        return Ok(new { costoId, cobradorIds = ids });
    }

    /// <summary>
    /// Lo que el préstamo asociado cuesta en el mes. Congelado: su cuota de interés por los períodos del mes
    /// (la siguiente cuota solo se genera al pagar la anterior). Normal: las cuotas que vencen en el mes.
    /// </summary>
    private static decimal CuotaMensual(Prestamo p, DateTime inicio, DateTime fin)
    {
        if (p.EsCongelado)
        {
            var periodosMes = p.FrecuenciaPago switch
            {
                "Diario" => 30m,
                "Semanal" => 52m / 12m,
                "Quincenal" => 2m,
                _ => 1m
            };
            return p.MontoCuota * periodosMes;
        }
        return p.Cuotas.Where(c => c.FechaCobro >= inicio && c.FechaCobro < fin).Sum(c => c.MontoCuota);
    }

    private Task<bool> EsCobradorAsync(int id) =>
        _context.Usuarios.AnyAsync(u => u.Id == id && u.Rol == RolUsuario.Cobrador);

    private async Task GuardarAsync(string clave, string valor, string descripcion)
    {
        var existente = await _context.ConfiguracionesSistema.FirstOrDefaultAsync(c => c.Clave == clave);
        if (existente == null)
            _context.ConfiguracionesSistema.Add(new ConfiguracionSistema
            {
                Clave = clave, Valor = valor, Descripcion = descripcion, FechaActualizacion = DateTime.UtcNow
            });
        else
        {
            existente.Valor = valor;
            existente.FechaActualizacion = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
    }

    private async Task BorrarAsync(string clave)
    {
        var existente = await _context.ConfiguracionesSistema.FirstOrDefaultAsync(c => c.Clave == clave);
        if (existente == null) return;
        _context.ConfiguracionesSistema.Remove(existente);
        await _context.SaveChangesAsync();
    }

    public record ValorDto(decimal Valor);
    public record ValorOpcionalDto(decimal? Valor);
    public record PrestamoAsociadoDto(int? PrestamoId);
    public record CostoCobradoresDto(List<int>? CobradorIds);
    public record VisibleDto(bool Visible);
}

public class CostoCobradorDto
{
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public decimal Cubierto { get; set; }
}

public class BalanceCobradorDto
{
    public int CobradorId { get; set; }
    public string CobradorNombre { get; set; } = string.Empty;
    public decimal CapitalACargo { get; set; }
    public decimal InteresProgramado { get; set; }   // Meta: interés de las cuotas del mes
    public decimal InteresCobrado { get; set; }
    public decimal MoraCobrada { get; set; }
    public decimal Recaudo { get; set; }             // Interés + mora cobrados en el mes
    public List<CostoCobradorDto> Costos { get; set; } = new();
    public decimal TotalCostos { get; set; }
    public decimal CostosCubiertos { get; set; }
    public decimal Moto { get; set; }
    public int? PrestamoAsociadoId { get; set; }
    public string? PrestamoAsociadoCliente { get; set; }
    public bool PrestamoAsociadoCongelado { get; set; }
    public decimal Salario { get; set; }
    public decimal SalarioCorresponde { get; set; }
    public decimal Excedente { get; set; }           // Lo que queda después de costos y salario completo
    public string Estado { get; set; } = string.Empty; // NoCubreCostos | SalarioParcial | Rentable
}

public class BalanceNegocioDto
{
    public decimal InteresCobrado { get; set; }
    public decimal MoraCobrada { get; set; }
    public decimal Ingresos { get; set; }
    public List<CostoCobradorDto> Costos { get; set; } = new();
    public decimal TotalCostos { get; set; }          // Sin salarios de cobradores
    public decimal SalariosCobradores { get; set; }   // Lo que les corresponde según su recaudo
    public decimal Resultado { get; set; }            // > 0 ganancia; < 0 nos comimos capital
}

public class BalanceEconomicoDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public decimal CostoRuta { get; set; }
    public decimal TasaCostoCapital { get; set; }
    public List<BalanceCobradorDto> Cobradores { get; set; } = new();
    public List<CostoOperativoAsociableDto> CostosOperativos { get; set; } = new();
    public List<CobradorOcultoDto> CobradoresOcultos { get; set; } = new();
    public BalanceNegocioDto Negocio { get; set; } = new();
}

public class CobradorOcultoDto
{
    public int CobradorId { get; set; }
    public string CobradorNombre { get; set; } = string.Empty;
}

public class CostoOperativoAsociableDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal MontoMensual { get; set; }
    public List<int> CobradorIds { get; set; } = new();
}
