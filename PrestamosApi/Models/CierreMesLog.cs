using System;

namespace PrestamosApi.Models;

public class CierreMesLog
{
    public int Id { get; set; }
    public int Mes { get; set; }
    public int Anio { get; set; }
    public DateTime FechaEjecucion { get; set; } = DateTime.UtcNow;
    public string Resultado { get; set; } = string.Empty;
}
