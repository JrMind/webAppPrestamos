namespace PrestamosApi.Models;

public class PagoCosto
{
    public int Id { get; set; }
    public int CostoId { get; set; }
    public decimal MontoPagado { get; set; }
    public DateTime FechaPago { get; set; } = DateTime.UtcNow;
    public string? MetodoPago { get; set; } // Efectivo, Transferencia, etc.
    public string? Comprobante { get; set; }
    public string? Observaciones { get; set; }
    
    // Navegación
    public Costo? Costo { get; set; }
}
