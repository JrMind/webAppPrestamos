using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;
using PrestamosApi.Services;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SaldosController : ControllerBase
{
    private readonly PrestamosDbContext _context;
    private readonly IGananciasService _gananciasService;

    public SaldosController(PrestamosDbContext context, IGananciasService gananciasService)
    {
        _context = context;
        _gananciasService = gananciasService;
    }

    private async Task<decimal> ObtenerAjuste(string medioLower)
    {
        var clave = $"ajuste_saldo_{medioLower}";
        var config = await _context.ConfiguracionesSistema.FirstOrDefaultAsync(c => c.Clave == clave);
        return config != null && decimal.TryParse(config.Valor, out var v) ? v : 0m;
    }

    // GET api/saldos
    [HttpGet]
    public async Task<IActionResult> GetAjustes()
    {
        var nequiAjuste = await ObtenerAjuste("nequi");
        var efectivoAjuste = await ObtenerAjuste("efectivo");
        return Ok(new
        {
            nequi = new { medio = "nequi", ajuste = nequiAjuste },
            efectivo = new { medio = "efectivo", ajuste = efectivoAjuste }
        });
    }

    // GET api/saldos/nequi  o  api/saldos/efectivo
    [HttpGet("{medio}")]
    public async Task<IActionResult> GetAjuste(string medio)
    {
        var monto = await ObtenerAjuste(medio.ToLower());
        return Ok(new { medio = medio.ToLower(), ajuste = monto });
    }

    // PUT api/saldos/nequi    body: { "monto": 500000 }
    // PUT api/saldos/efectivo  body: { "monto": 200000 }
    // "monto" es el total deseado visible, no el ajuste crudo.
    [HttpPut("{medio}")]
    public async Task<IActionResult> SetAjuste(string medio, [FromBody] SetAjusteDto dto)
    {
        var medioLower = medio.ToLower();
        if (medioLower != "nequi" && medioLower != "efectivo")
            return BadRequest(new { error = "medio debe ser 'nequi' o 'efectivo'" });

        var medioPago = medioLower == "nequi" ? "Nequi" : "Efectivo";

        // Calcular la base sin el ajuste actual para que el total quede exacto
        var ajusteActual = await ObtenerAjuste(medioLower);
        var totalActual = await _gananciasService.CalcularBalanceMedioAsync(medioPago);
        var baseBalance = totalActual - ajusteActual;
        var newAjuste = dto.Monto - baseBalance;

        var clave = $"ajuste_saldo_{medioLower}";
        var config = await _context.ConfiguracionesSistema.FirstOrDefaultAsync(c => c.Clave == clave);

        if (config == null)
        {
            _context.ConfiguracionesSistema.Add(new ConfiguracionSistema
            {
                Clave = clave,
                Valor = newAjuste.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                Descripcion = $"Ajuste manual de saldo {medio}",
                FechaActualizacion = DateTime.UtcNow
            });
        }
        else
        {
            config.Valor = newAjuste.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            config.FechaActualizacion = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(new { medio = medioLower, ajuste = newAjuste, total = dto.Monto });
    }
}

public class SetAjusteDto
{
    public decimal Monto { get; set; }
}
