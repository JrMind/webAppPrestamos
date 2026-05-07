namespace PrestamosApi.Models;

public class Gasto
{
    public int Id { get; set; }
    public string Descripcion { get; set; } = "";
    public decimal Monto { get; set; }
    public string MedioPago { get; set; } = "Efectivo"; // Nequi | Efectivo
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Categoria { get; set; }
}
