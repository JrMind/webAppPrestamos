namespace PrestamosApi.Models;

public class PagoAportadorExterno
{
    public int Id { get; set; }
    public int AportadorExternoId { get; set; }
    public decimal Monto { get; set; }
    public decimal MontoCapital { get; set; } = 0; // Porción que reduce la deuda
    public decimal MontoIntereses { get; set; } = 0; // Porción de intereses
    public DateTime FechaPago { get; set; }
    public string? MetodoPago { get; set; }
    public string? Comprobante { get; set; }
    public string? Notas { get; set; }
    
    // Navegación
    public AportadorExterno? AportadorExterno { get; set; }
}
