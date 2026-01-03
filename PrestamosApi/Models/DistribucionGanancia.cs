namespace PrestamosApi.Models;

public class DistribucionGanancia
{
    public int Id { get; set; }
    public int PrestamoId { get; set; }
    public int UsuarioId { get; set; }
    public decimal PorcentajeAsignado { get; set; }
    public decimal MontoGanancia { get; set; }
    public DateTime FechaDistribucion { get; set; }
    public bool Liquidado { get; set; } = false;
    
    // Navegación
    public Prestamo? Prestamo { get; set; }
    public Usuario? Usuario { get; set; }
}
