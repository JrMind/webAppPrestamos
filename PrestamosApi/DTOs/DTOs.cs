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
    string? Cedula,
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
    decimal PorcentajeCobrador = 5,
    bool EsCongelado = false
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
    decimal PorcentajeCobrador = 5,
    string? DiaSemana = null, // Para Semanal: Lunes, Martes, etc.
    bool EsCongelado = false, // Préstamo congelado: solo intereses
    int? NumeroCuotasDirecto = null // Override: usar X cuotas en vez de calcular
);

public record UpdatePrestamoDto(
    decimal MontoPrestado,
    decimal TasaInteres,
    string TipoInteres,
    string FrecuenciaPago,
    int NumeroCuotas,
    DateTime FechaPrestamo,
    DateTime? FechaPrimerPago, // Nueva opción
    string EstadoPrestamo,
    string? Descripcion,
    int? CobradorId,
    decimal PorcentajeCobrador,
    string? DiaSemana,
    bool EsCongelado = false
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
    decimal InteresMes,         // Intereses de cuotas del mes
    decimal GananciaTotalMes,   // Total cuotas a cobrar del mes
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
    decimal ReservaDisponible = 0,
    decimal CapitalInicial = 0   // Nuevo: suma prestamos - (pagos - intereses)
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

// Aportador Externo DTOs
public record AportadorExternoDto(
    int Id,
    string Nombre,
    string? Telefono,
    string? Email,
    decimal TasaInteres,
    int DiasParaPago,
    decimal MontoTotalAportado,
    decimal MontoPagado,
    decimal SaldoPendiente,
    string Estado,
    DateTime FechaCreacion,
    string? Notas
);

public record CreateAportadorExternoDto(
    string Nombre,
    string? Telefono,
    string? Email,
    decimal TasaInteres,
    int DiasParaPago,
    string? Notas,
    decimal MontoTotalAportado = 0
);

public record UpdateAportadorExternoDto(
    string Nombre,
    string? Telefono,
    string? Email,
    decimal TasaInteres,
    int DiasParaPago,
    string Estado,
    string? Notas,
    decimal MontoTotalAportado = 0
);

// Fuente de Capital DTOs
public record FuenteCapitalDto(
    string Tipo, // "Reserva" | "Interno" | "Externo"
    int? UsuarioId, // Solo para Interno (socio)
    int? AportadorExternoId, // Solo para Externo
    decimal MontoAportado
);

// DTO para crear préstamo con fuentes de capital
public record CreatePrestamoConFuentesDto(
    int ClienteId,
    decimal MontoPrestado,
    decimal TasaInteres,
    string TipoInteres,
    string FrecuenciaPago,
    int Duracion,
    string UnidadDuracion,
    DateTime FechaPrestamo,
    string? Descripcion,
    int? CobradorId,
    decimal PorcentajeCobrador,
    List<FuenteCapitalDto> FuentesCapital, // Lista de fuentes
    bool EsCongelado = false,
    int? NumeroCuotasDirecto = null // Override: usar X cuotas
);

// Socio/Inversor interno DTOs
public record SocioDto(
    int Id,
    string Nombre,
    string? Telefono,
    string Rol,
    decimal TasaInteresMensual,
    decimal PorcentajeParticipacion,
    decimal CapitalActual,
    decimal GananciasAcumuladas,
    decimal SaldoTotal,
    DateTime? UltimoCalculoInteres
);

// Balance de capital
public record BalanceCapitalDto(
    decimal ReservaDisponible,
    decimal TotalCobrado,
    decimal CapitalUsadoDeReserva,
    List<SocioDto> Socios,
    List<AportadorExternoDto> AportadoresExternos
);

