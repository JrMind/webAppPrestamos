namespace PrestamosApi.Models;

public class Aporte
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public decimal MontoInicial { get; set; }
    public decimal MontoActual { get; set; }
    public DateTime FechaAporte { get; set; }
    public string? Descripcion { get; set; }
    
    // Navegación
    public Usuario? Usuario { get; set; }
}
