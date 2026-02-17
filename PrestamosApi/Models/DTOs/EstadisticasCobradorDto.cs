namespace PrestamosApi.Models.DTOs;

public class EstadisticasCobradorDto
{
    public int CobradorId { get; set; }
    public string CobradorNombre { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty; // "Cobrador 1", "Cobrador 2", etc.
    public decimal PromedioTasaInteres { get; set; }
    public decimal PromedioTasaInteresNeto { get; set; } // Restando 8%
    public decimal CapitalTotalPrestado { get; set; }
    public int TotalCreditosActivos { get; set; }
}

public class MetricasGeneralesDto
{
    public decimal PromedioTasasActivos { get; set; }
    public decimal CapitalFantasma { get; set; } // Capital total en préstamos activos
    public int TotalPrestamosActivos { get; set; }
    public List<EstadisticasCobradorDto> EstadisticasCobradores { get; set; } = new();
}
