namespace PrestamosApi.Models;

public class Prestamo
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public decimal MontoPrestado { get; set; }
    public decimal TasaInteres { get; set; }
    public string TipoInteres { get; set; } = "Simple"; // Simple o Compuesto
    public string FrecuenciaPago { get; set; } = string.Empty; // Diario, Semanal, Quincenal, Mensual
    public int NumeroCuotas { get; set; }
    public DateTime FechaPrestamo { get; set; }
    public DateTime FechaVencimiento { get; set; }
    public decimal MontoTotal { get; set; }
    public decimal MontoIntereses { get; set; }
    public decimal MontoCuota { get; set; }
    public string EstadoPrestamo { get; set; } = "Activo"; // Activo, Pagado, Vencido
    public string? Descripcion { get; set; }
    public string? UsuarioCreacion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? UsuarioModificacion { get; set; }
    public DateTime? FechaModificacion { get; set; }

    // Navegación
    public Cliente? Cliente { get; set; }
    public ICollection<CuotaPrestamo> Cuotas { get; set; } = new List<CuotaPrestamo>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}
