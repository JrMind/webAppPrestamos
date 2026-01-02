namespace PrestamosApi.Models;

public class CuotaPrestamo
{
    public int Id { get; set; }
    public int PrestamoId { get; set; }
    public int NumeroCuota { get; set; }
    public DateTime FechaCobro { get; set; }
    public decimal MontoCuota { get; set; }
    public decimal MontoPagado { get; set; } = 0;
    public decimal SaldoPendiente { get; set; }
    public string EstadoCuota { get; set; } = "Pendiente"; // Pendiente, Pagada, Vencida, Parcial
    public DateTime? FechaPago { get; set; }
    public string? Observaciones { get; set; }
    public string? UsuarioCreacion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? UsuarioModificacion { get; set; }
    public DateTime? FechaModificacion { get; set; }

    // Navegación
    public Prestamo? Prestamo { get; set; }
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}
