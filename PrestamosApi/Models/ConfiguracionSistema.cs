using Microsoft.EntityFrameworkCore;

namespace PrestamosApi.Models;

public class ConfiguracionSistema
{
    public int Id { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public DateTime FechaActualizacion { get; set; }
    public string? Descripcion { get; set; }
}
