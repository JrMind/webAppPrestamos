namespace PrestamosApi.Models;

public class Cliente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public string? Email { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public string Estado { get; set; } = "Activo";
    public string? UsuarioCreacion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? UsuarioModificacion { get; set; }
    public DateTime? FechaModificacion { get; set; }

    // Navegación
    public ICollection<Prestamo> Prestamos { get; set; } = new List<Prestamo>();
}
