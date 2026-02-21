using System.ComponentModel.DataAnnotations.Schema;

namespace PrestamosApi.Models;

[Table("liquidacionescobrador")]
public class LiquidacionCobrador
{
    public int Id { get; set; }

    [Column("cobradorid")]
    public int CobradorId { get; set; }

    [Column("montoliquidado")]
    public decimal MontoLiquidado { get; set; }

    [Column("fechaliquidacion")]
    public DateTime FechaLiquidacion { get; set; } = DateTime.UtcNow;

    [Column("observaciones")]
    public string? Observaciones { get; set; }

    [Column("realizadopor")]
    public int? RealizadoPor { get; set; }

    // Navegación
    public Usuario? Cobrador { get; set; }
    public Usuario? RealizadoPorUsuario { get; set; }
}
