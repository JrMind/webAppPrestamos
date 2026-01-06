using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.Models;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SmsCampaignsController : ControllerBase
{
    private readonly PrestamosDbContext _context;

    public SmsCampaignsController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        var campaigns = await _context.SmsCampaigns
            .OrderByDescending(c => c.FechaCreacion)
            .Select(c => new
            {
                c.Id,
                c.Nombre,
                c.Mensaje,
                c.Activo,
                c.DiasEnvio,
                c.HorasEnvio,
                c.VecesPorDia,
                TipoDestinatario = c.TipoDestinatario.ToString(),
                c.FechaCreacion,
                c.FechaModificacion,
                SmsEnviados = c.HistorialSms.Count
            })
            .ToListAsync();

        return Ok(campaigns);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetById(int id)
    {
        var campaign = await _context.SmsCampaigns
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Nombre,
                c.Mensaje,
                c.Activo,
                c.DiasEnvio,
                c.HorasEnvio,
                c.VecesPorDia,
                TipoDestinatario = c.TipoDestinatario.ToString(),
                c.FechaCreacion,
                c.FechaModificacion
            })
            .FirstOrDefaultAsync();

        if (campaign == null)
            return NotFound();

        return Ok(campaign);
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateSmsCampaignDto dto)
    {
        var campaign = new SmsCampaign
        {
            Nombre = dto.Nombre,
            Mensaje = dto.Mensaje,
            Activo = dto.Activo,
            DiasEnvio = dto.DiasEnvio,
            HorasEnvio = dto.HorasEnvio,
            VecesPorDia = dto.VecesPorDia,
            TipoDestinatario = Enum.Parse<TipoDestinatarioSms>(dto.TipoDestinatario),
            FechaCreacion = DateTime.UtcNow
        };

        _context.SmsCampaigns.Add(campaign);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Campaña SMS creada exitosamente", id = campaign.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSmsCampaignDto dto)
    {
        var campaign = await _context.SmsCampaigns.FindAsync(id);
        if (campaign == null)
            return NotFound();

        campaign.Nombre = dto.Nombre ?? campaign.Nombre;
        campaign.Mensaje = dto.Mensaje ?? campaign.Mensaje;
        campaign.Activo = dto.Activo ?? campaign.Activo;
        campaign.DiasEnvio = dto.DiasEnvio ?? campaign.DiasEnvio;
        campaign.HorasEnvio = dto.HorasEnvio ?? campaign.HorasEnvio;
        campaign.VecesPorDia = dto.VecesPorDia ?? campaign.VecesPorDia;
        if (dto.TipoDestinatario != null)
            campaign.TipoDestinatario = Enum.Parse<TipoDestinatarioSms>(dto.TipoDestinatario);
        campaign.FechaModificacion = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Campaña actualizada exitosamente" });
    }

    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var campaign = await _context.SmsCampaigns.FindAsync(id);
        if (campaign == null)
            return NotFound();

        campaign.Activo = !campaign.Activo;
        campaign.FechaModificacion = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = campaign.Activo ? "Campaña activada" : "Campaña desactivada", activo = campaign.Activo });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var campaign = await _context.SmsCampaigns.FindAsync(id);
        if (campaign == null)
            return NotFound();

        _context.SmsCampaigns.Remove(campaign);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Campaña eliminada exitosamente" });
    }
}

public class CreateSmsCampaignDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public string DiasEnvio { get; set; } = "[]";
    public string HorasEnvio { get; set; } = "[]";
    public int VecesPorDia { get; set; } = 1;
    public string TipoDestinatario { get; set; } = "CuotasHoy";
}

public class UpdateSmsCampaignDto
{
    public string? Nombre { get; set; }
    public string? Mensaje { get; set; }
    public bool? Activo { get; set; }
    public string? DiasEnvio { get; set; }
    public string? HorasEnvio { get; set; }
    public int? VecesPorDia { get; set; }
    public string? TipoDestinatario { get; set; }
}
