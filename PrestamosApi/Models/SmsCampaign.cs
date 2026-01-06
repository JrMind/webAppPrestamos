namespace PrestamosApi.Models;

public class SmsCampaign
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty; // Template con placeholders: {cliente}, {monto}, {fecha}
    public bool Activo { get; set; } = true;
    public string DiasEnvio { get; set; } = "[]"; // JSON array: ["Lunes", "Martes", ...]
    public string HorasEnvio { get; set; } = "[]"; // JSON array: ["09:00", "14:00", ...]
    public int VecesPorDia { get; set; } = 1;
    public TipoDestinatarioSms TipoDestinatario { get; set; } = TipoDestinatarioSms.CuotasHoy;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaModificacion { get; set; }
    
    // Navegación
    public ICollection<SmsHistory> HistorialSms { get; set; } = new List<SmsHistory>();
}

public enum TipoDestinatarioSms
{
    CuotasHoy,
    CuotasVencidas,
    TodosClientesActivos,
    ProximasVencer // 3 días antes
}
