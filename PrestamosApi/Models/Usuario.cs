namespace PrestamosApi.Models;

public enum RolUsuario
{
    Admin,
    Socio,
    AportadorInterno,
    AportadorExterno,
    Cobrador,
    Administrador  // Rol de vista acotada: accede solo a datos desde una fecha y cobradores específicos
}

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public RolUsuario? Rol { get; set; } = null; // null = pendiente de asignación por admin
    public decimal PorcentajeParticipacion { get; set; } // % de ganancias
    public decimal TasaInteresMensual { get; set; } = 3; // 3% mensual por defecto
    public bool Activo { get; set; } = true;
    
    // Campos para scope de datos (rol Administrador)
    public DateTime? FechaInicioAcceso { get; set; } = null; // Solo ve datos desde esta fecha
    public string? CobradorIdsPermitidos { get; set; } = null; // IDs de cobradores visibles, ej: "3,8"

    // Campos para tracking de capital (socios)
    public decimal CapitalActual { get; set; } = 0; // Capital con interés compuesto acumulado
    public decimal GananciasAcumuladas { get; set; } = 0; // Ganancias por participación en préstamos
    public DateTime? UltimoCalculoInteres { get; set; } // Fecha del último cálculo de interés compuesto
    
    // Navegación
    public ICollection<Aporte> Aportes { get; set; } = new List<Aporte>();
    public ICollection<MovimientoCapital> Movimientos { get; set; } = new List<MovimientoCapital>();
    public ICollection<Prestamo> PrestamosComoCobrador { get; set; } = new List<Prestamo>();
    public ICollection<DistribucionGanancia> Ganancias { get; set; } = new List<DistribucionGanancia>();
    public ICollection<FuenteCapitalPrestamo> FuentesCapital { get; set; } = new List<FuenteCapitalPrestamo>();
}
