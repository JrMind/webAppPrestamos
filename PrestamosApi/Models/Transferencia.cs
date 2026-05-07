namespace PrestamosApi.Models;

public class Transferencia
{
    public int Id { get; set; }
    public string Origen { get; set; } = "Efectivo";  // Nequi | Efectivo
    public string Destino { get; set; } = "Nequi";    // Nequi | Efectivo
    public decimal Monto { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Descripcion { get; set; }
}
