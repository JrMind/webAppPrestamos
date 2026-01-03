namespace PrestamosApi.Models;

public enum TipoMovimiento
{
    Aporte,
    Retiro,
    InteresGenerado
}

public class MovimientoCapital
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public TipoMovimiento TipoMovimiento { get; set; }
    public decimal Monto { get; set; }
    public decimal SaldoAnterior { get; set; }
    public decimal SaldoNuevo { get; set; }
    public DateTime FechaMovimiento { get; set; }
    public string? Descripcion { get; set; }
    
    // Navegación
    public Usuario? Usuario { get; set; }
}
