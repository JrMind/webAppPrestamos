namespace PrestamosApi.DTOs;

// Cliente DTOs
public record ClienteDto(
    int Id,
    string Nombre,
    string Cedula,
    string? Telefono,
    string? Direccion,
    string? Email,
    DateTime FechaRegistro,
    string Estado,
    int PrestamosActivos,
    decimal TotalPrestado
);

public record CreateClienteDto(
    string Nombre,
    string Cedula,
    string? Telefono,
    string? Direccion,
    string? Email
);

public record UpdateClienteDto(
    string Nombre,
    string? Telefono,
    string? Direccion,
    string? Email,
    string Estado
);

// Prestamo DTOs
public record PrestamoDto(
    int Id,
    int ClienteId,
    string ClienteNombre,
    string ClienteCedula,
    string? ClienteTelefono,
    decimal MontoPrestado,
    decimal TasaInteres,
    string TipoInteres,
    string FrecuenciaPago,
    int NumeroCuotas,
    DateTime FechaPrestamo,
    DateTime FechaVencimiento,
    decimal MontoTotal,
    decimal MontoIntereses,
    decimal MontoCuota,
    string EstadoPrestamo,
    string? Descripcion,
    decimal TotalPagado,
    decimal SaldoPendiente,
    int CuotasPagadas,
    CuotaProximaDto? ProximaCuota,
    int? CobradorId = null,
    string? CobradorNombre = null,
    decimal PorcentajeCobrador = 5
);

public record CuotaProximaDto(
    DateTime FechaCobro,
    decimal Monto
);

public record CreatePrestamoDto(
    int ClienteId,
    decimal MontoPrestado,
    decimal TasaInteres,
    string TipoInteres,
    string FrecuenciaPago,
    int Duracion,
    string UnidadDuracion, // Dias, Semanas, Quincenas, Meses
    DateTime FechaPrestamo,
    string? Descripcion,
    int? CobradorId = null,
    decimal PorcentajeCobrador = 5
);

public record UpdatePrestamoDto(
    string EstadoPrestamo,
    string? Descripcion
);

// Cuota DTOs
public record CuotaDto(
    int Id,
    int PrestamoId,
    int NumeroCuota,
    DateTime FechaCobro,
    decimal MontoCuota,
    decimal MontoPagado,
    decimal SaldoPendiente,
    string EstadoCuota,
    DateTime? FechaPago,
    string? Observaciones,
    bool FechaEditada
);

public record UpdateCuotaFechaDto(
    DateTime FechaCobro
);

// Pago DTOs
public record PagoDto(
    int Id,
    int PrestamoId,
    int? CuotaId,
    int? NumeroCuota,
    decimal MontoPago,
    DateTime FechaPago,
    string? MetodoPago,
    string? Comprobante,
    string? Observaciones
);

public record CreatePagoDto(
    int PrestamoId,
    int? CuotaId,
    decimal MontoPago,
    DateTime FechaPago,
    string? MetodoPago,
    string? Comprobante,
    string? Observaciones
);

// Dashboard DTOs
public record DashboardMetricasDto(
    decimal TotalPrestado,
    decimal TotalACobrar,
    decimal TotalGanadoIntereses,
    int PrestamosActivos,
    decimal MontoPrestamosActivos,
    int CuotasVencidasHoy,
    decimal MontoCuotasVencidasHoy,
    int CuotasProximas7Dias,
    decimal MontoCuotasProximas7Dias,
    decimal TasaPromedioInteres,
    decimal PorcentajeMorosidad,
    List<EvolucionPrestamosDto> EvolucionPrestamos,
    List<TopClienteDto> TopClientes,
    DistribucionEstadosDto DistribucionEstados,
    List<IngresoMensualDto> IngresosMensuales,
    List<CuotaProximaDetalleDto> CuotasProximasDetalle,
    // Flujo de Capital
    decimal TotalCobrado = 0,
    decimal DineroCirculando = 0,
    decimal ReservaDisponible = 0
);

public record EvolucionPrestamosDto(
    DateTime Fecha,
    decimal MontoPrestadoAcumulado,
    decimal MontoCobradoAcumulado
);

public record TopClienteDto(
    string Nombre,
    decimal TotalPrestado
);

public record DistribucionEstadosDto(
    int Activos,
    int Pagados,
    int Vencidos
);

public record IngresoMensualDto(
    string Mes,
    decimal InteresesGanados,
    decimal CapitalRecuperado
);

public record CuotaProximaDetalleDto(
    int CuotaId,
    int PrestamoId,
    string ClienteNombre,
    DateTime FechaCobro,
    decimal MontoCuota,
    string EstadoCuota,
    int DiasParaVencer
);
