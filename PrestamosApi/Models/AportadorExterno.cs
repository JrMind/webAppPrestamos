namespace PrestamosApi.Models;

public class AportadorExterno
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public decimal TasaInteres { get; set; } = 3; // % que cobra el aportador
    public int DiasParaPago { get; set; } = 30; // cada cuántos días pagar
    public decimal MontoTotalAportado { get; set; } = 0;
    public decimal MontoPagado { get; set; } = 0;
    public decimal SaldoPendiente { get; set; } = 0;
    public string Estado { get; set; } = "Activo"; // Activo, Pagado
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? Notas { get; set; }
    
    // Navegación
    public ICollection<FuenteCapitalPrestamo> FuentesCapital { get; set; } = new List<FuenteCapitalPrestamo>();
    public ICollection<PagoAportadorExterno> Pagos { get; set; } = new List<PagoAportadorExterno>();
}
