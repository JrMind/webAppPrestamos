using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PrestamosApi.Models;

public class NotaPrestamo
{
    public int Id { get; set; }
    
    public int PrestamoId { get; set; }
    
    [Required]
    public string Contenido { get; set; } = string.Empty;
    
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    
    public int? UsuarioId { get; set; } // El usuario que creó la nota
    
    // Navegación
    [JsonIgnore]
    public Prestamo? Prestamo { get; set; }
    
    public Usuario? Usuario { get; set; }
}
