namespace PrestamosApi.Models;

/// <summary>
/// Valores de <see cref="Pago.TipoPago"/>. Los cargos (Sistema, Papelería, Renovación) se descuentan
/// del desembolso al crear el préstamo: quedan como Pago para que entren a caja y a su fondo,
/// pero no son pagos del préstamo y no deben sumar en lo pagado por el cliente.
/// </summary>
public static class TiposPago
{
    public const string Cuota = "Cuota";
    public const string AbonoCapital = "AbonoCapital";
    public const string Mora = "Mora";
    public const string SoloInteres = "SoloInteres";
    public const string Sistema = "Sistema";
    public const string Papeleria = "Papeleria";
    public const string Renovacion = "Renovacion";

    /// <summary>Cargos descontados al desembolsar. EF traduce Contains sobre este arreglo a SQL.</summary>
    public static readonly string[] Cargos = { Sistema, Papeleria, Renovacion };
}
