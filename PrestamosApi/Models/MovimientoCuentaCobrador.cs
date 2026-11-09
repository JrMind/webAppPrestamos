namespace PrestamosApi.Models;

/// <summary>
/// Movimiento de la cuenta corriente entre un cobrador y la oficina.
/// El saldo de la cuenta es la suma de los movimientos:
///   CobradorDebe  → el cobrador le debe ese monto a la oficina (suma)
///   OficinaDebe   → la oficina le debe ese monto al cobrador (resta)
/// Saldo positivo = el cobrador le debe a la oficina; negativo = la oficina le debe al cobrador.
/// Un abono/pago se registra como movimiento del tipo contrario.
/// </summary>
public class MovimientoCuentaCobrador
{
    public const string TipoCobradorDebe = "CobradorDebe";
    public const string TipoOficinaDebe = "OficinaDebe";

    public int Id { get; set; }
    public int CobradorId { get; set; }
    public string Tipo { get; set; } = TipoCobradorDebe;
    public decimal Monto { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow); // Fecha del movimiento
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public int? UsuarioId { get; set; }                       // Quién lo registró

    public Usuario? Cobrador { get; set; }
    public Usuario? Usuario { get; set; }
}
