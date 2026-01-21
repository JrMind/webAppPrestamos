namespace PrestamosApi.Models;

public class Costo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;  // Ej: "Salario Cobrador X"
    public decimal Monto { get; set; }
    public string Frecuencia { get; set; } = "Mensual"; // Mensual, Quincenal, Único
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFin { get; set; }  // Para costos temporales
    public decimal TotalPagado { get; set; } = 0;  // Total pagado hasta ahora
    
    // Navegación
    public ICollection<PagoCosto> Pagos { get; set; } = new List<PagoCosto>();
}

