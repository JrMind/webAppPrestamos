namespace PrestamosApi.Models;

public class Pago
{
    public int Id { get; set; }
    public int PrestamoId { get; set; }
    public int? CuotaId { get; set; }
    public decimal MontoPago { get; set; }
    public DateTime FechaPago { get; set; }
    public string? MetodoPago { get; set; } // Efectivo, Transferencia, etc.
    public string? Comprobante { get; set; }
    public string? Observaciones { get; set; }

    // Navegación
    public Prestamo? Prestamo { get; set; }
    public CuotaPrestamo? Cuota { get; set; }
}
