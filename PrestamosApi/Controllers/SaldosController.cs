using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SaldosController : ControllerBase
{
    private readonly PrestamosDbContext _context;

    public SaldosController(PrestamosDbContext context)
    {
        _context = context;
    }

    // GET api/saldos
    [HttpGet]
    public async Task<IActionResult> GetAjustes()
    {
        var nequi = await GetAjuste("nequi");
        var efectivo = await GetAjuste("efectivo");
        return Ok(new { nequi, efectivo });
    }

    // GET api/saldos/nequi  o  api/saldos/efectivo
    [HttpGet("{medio}")]
    public async Task<IActionResult> GetAjuste(string medio)
    {
        var clave = $"ajuste_saldo_{medio.ToLower()}";
        var config = await _context.ConfiguracionesSistema.FirstOrDefaultAsync(c => c.Clave == clave);
        var monto = config != null && decimal.TryParse(config.Valor, out var v) ? v : 0m;
        return Ok(new { medio = medio.ToLower(), ajuste = monto });
    }

    // PUT api/saldos/nequi    body: { "monto": 500000 }
    // PUT api/saldos/efectivo  body: { "monto": 200000 }
    [HttpPut("{medio}")]
    public async Task<IActionResult> SetAjuste(string medio, [FromBody] SetAjusteDto dto)
    {
        var medioLower = medio.ToLower();
        if (medioLower != "nequi" && medioLower != "efectivo")
            return BadRequest(new { error = "medio debe ser 'nequi' o 'efectivo'" });

        var clave = $"ajuste_saldo_{medioLower}";
        var config = await _context.ConfiguracionesSistema.FirstOrDefaultAsync(c => c.Clave == clave);

        if (config == null)
        {
            _context.ConfiguracionesSistema.Add(new ConfiguracionSistema
            {
                Clave = clave,
                Valor = dto.Monto.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                Descripcion = $"Ajuste manual de saldo {medio}",
                FechaActualizacion = DateTime.UtcNow
            });
        }
        else
        {
            config.Valor = dto.Monto.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            config.FechaActualizacion = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(new { medio = medioLower, ajuste = dto.Monto });
    }
}

public class SetAjusteDto
{
    public decimal Monto { get; set; }
}
