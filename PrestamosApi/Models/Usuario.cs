namespace PrestamosApi.Models;

public enum RolUsuario
{
    Socio,
    AportadorInterno,
    AportadorExterno,
    Cobrador
}

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public RolUsuario Rol { get; set; }
    public decimal PorcentajeParticipacion { get; set; } // % de ganancias
    public decimal TasaInteresMensual { get; set; } = 3; // 3% mensual por defecto
    public bool Activo { get; set; } = true;
    
    // Navegación
    public ICollection<Aporte> Aportes { get; set; } = new List<Aporte>();
    public ICollection<MovimientoCapital> Movimientos { get; set; } = new List<MovimientoCapital>();
    public ICollection<Prestamo> PrestamosComoCobrador { get; set; } = new List<Prestamo>();
    public ICollection<DistribucionGanancia> Ganancias { get; set; } = new List<DistribucionGanancia>();
}
