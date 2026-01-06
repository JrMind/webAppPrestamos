namespace PrestamosApi.Models;

public class SmsHistory
{
    public int Id { get; set; }
    public int? SmsCampaignId { get; set; }
    public int? ClienteId { get; set; }
    public string NumeroTelefono { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;
    public EstadoSms Estado { get; set; } = EstadoSms.Pendiente;
    public string? TwilioSid { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Navegación
    public SmsCampaign? SmsCampaign { get; set; }
    public Cliente? Cliente { get; set; }
}

public enum EstadoSms
{
    Pendiente,
    Enviado,
    Fallido,
    Entregado
}
