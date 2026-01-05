namespace PrestamosApi.Models;

public class Prestamo
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public int? CobradorId { get; set; }
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
    public decimal PorcentajeCobrador { get; set; } = 5; // % para el cobrador (configurable para préstamos al 15%)

    // Navegación
    public Cliente? Cliente { get; set; }
    public Usuario? Cobrador { get; set; }
    public ICollection<CuotaPrestamo> Cuotas { get; set; } = new List<CuotaPrestamo>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    public ICollection<DistribucionGanancia> Distribuciones { get; set; } = new List<DistribucionGanancia>();
    public ICollection<FuenteCapitalPrestamo> FuentesCapital { get; set; } = new List<FuenteCapitalPrestamo>();
}
