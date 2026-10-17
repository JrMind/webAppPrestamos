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

public class InteresMensualDto
{
    public string Mes { get; set; } = string.Empty;
    public decimal InteresCobrado { get; set; }
    public List<InteresMensualPrestamoDto> Prestamos { get; set; } = new();
}

public class InteresMensualPrestamoDto
{
    public int PrestamoId { get; set; }
    public string ClienteNombre { get; set; } = string.Empty;
    public int? CobradorId { get; set; }
    public string? CobradorNombre { get; set; }
    public bool EsCongelado { get; set; }
    public int CuotasPagadas { get; set; }
    public decimal MontoPagado { get; set; }
    public decimal InteresCobrado { get; set; }
}

public class MetricasGeneralesDto
{
    public decimal PromedioTasasActivos { get; set; }
    public decimal CapitalFantasma { get; set; } // Capital total en préstamos activos
    public int TotalPrestamosActivos { get; set; }
    public List<EstadisticasCobradorDto> EstadisticasCobradores { get; set; } = new();
    public List<InteresMensualDto> InteresesMensuales { get; set; } = new();
    public List<SalarioCobradorDto> SalariosCobradores { get; set; } = new();
}

public class SalarioCobradorDto
{
    public int CobradorId { get; set; }
    public string CobradorNombre { get; set; } = string.Empty;
    public decimal SalarioMensual { get; set; }
}

public class ActualizarSalarioCobradorDto
{
    public decimal SalarioMensual { get; set; }
}
