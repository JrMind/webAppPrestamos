using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SmsHistoryController : ControllerBase
{
    private readonly PrestamosDbContext _context;

    public SmsHistoryController(PrestamosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<object>> GetAll(
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] int? campaignId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _context.SmsHistories
            .Include(h => h.SmsCampaign)
            .Include(h => h.Cliente)
            .AsQueryable();

        if (fechaDesde.HasValue)
            query = query.Where(h => h.FechaEnvio >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(h => h.FechaEnvio <= fechaHasta.Value.AddDays(1));

        if (campaignId.HasValue)
            query = query.Where(h => h.SmsCampaignId == campaignId.Value);

        var total = await query.CountAsync();

        var history = await query
            .OrderByDescending(h => h.FechaEnvio)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => new
            {
                h.Id,
                h.SmsCampaignId,
                CampaignNombre = h.SmsCampaign != null ? h.SmsCampaign.Nombre : null,
                h.ClienteId,
                ClienteNombre = h.Cliente != null ? h.Cliente.Nombre : null,
                h.NumeroTelefono,
                h.Mensaje,
                h.FechaEnvio,
                Estado = h.Estado.ToString(),
                h.TwilioSid,
                h.ErrorMessage
            })
            .ToListAsync();

        return Ok(new
        {
            data = history,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetStats([FromQuery] int? days = 30)
    {
        var desde = DateTime.UtcNow.AddDays(-days.Value);

        var stats = await _context.SmsHistories
            .Where(h => h.FechaEnvio >= desde)
            .GroupBy(h => h.Estado)
            .Select(g => new { Estado = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        var totalEnviados = await _context.SmsHistories.CountAsync(h => h.FechaEnvio >= desde);

        return Ok(new
        {
            totalEnviados,
            porEstado = stats
        });
    }
}
