namespace PrestamosApi.Models;

public class FuenteCapitalPrestamo
{
    public int Id { get; set; }
    public int PrestamoId { get; set; }
    public string Tipo { get; set; } = "Reserva"; // "Reserva", "Interno", "Externo"
    public int? UsuarioId { get; set; } // Solo para tipo "Interno" (socio)
    public int? AportadorExternoId { get; set; } // Solo para tipo "Externo"
    public decimal MontoAportado { get; set; }
    public decimal PorcentajeParticipacion { get; set; } = 0; // % de las ganancias de este préstamo
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    
    // Navegación
    public Prestamo? Prestamo { get; set; }
    public Usuario? Usuario { get; set; }
    public AportadorExterno? AportadorExterno { get; set; }
}
