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
    bool EsCongelado = false,
    // Cargos adicionales
    decimal ValorSistema = 0,
    bool SistemaCobrado = false,
    DateTime? FechaSistemaCobrado = null,
    decimal ValorRenovacion = 0,
    bool RenovacionCobrada = false,
    DateTime? FechaRenovacionCobrada = null
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
    int? NumeroCuotasDirecto = null, // Override: usar X cuotas en vez de calcular
    // Cargos adicionales
    decimal ValorSistema = 0,
    decimal ValorRenovacion = 0
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
    bool EsCongelado = false,
    // Cargos adicionales
    decimal ValorSistema = 0,
    bool SistemaCobrado = false,
    decimal ValorRenovacion = 0,
    bool RenovacionCobrada = false
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
    int? NumeroCuotasDirecto = null, // Override: usar X cuotas
    // Cargos adicionales
    decimal ValorSistema = 0,
    decimal ValorRenovacion = 0
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

// Ganancia de socio por préstamo (para mostrar en detalle de préstamo)
public record GananciaSocioPrestamoDto(
    int SocioId,
    string NombreSocio,
    decimal GananciaAcumulada,    // Cuotas ya pagadas
    decimal GananciaProyectada    // Total al finalizar préstamo
);

// Costos operativos DTOs
public record CostoDto(
    int Id,
    string Nombre,
    decimal Monto,
    string Frecuencia,
    string? Descripcion,
    bool Activo,
    DateTime FechaCreacion,
    DateTime? FechaFin,
    decimal TotalPagado,
    decimal Restante
);

public record CreateCostoDto(
    string Nombre,
    decimal Monto,
    string Frecuencia,
    string? Descripcion
);

public record UpdateCostoDto(
    string Nombre,
    decimal Monto,
    string Frecuencia,
    string? Descripcion,
    bool Activo,
    DateTime? FechaFin
);

public record PagarCostoDto(
    decimal Monto,
    string? MetodoPago,
    string? Comprobante,
    string? Observaciones
);

// ──────────────────────────────────────────────────
// Liquidación / Comisiones de Cobradores
// ──────────────────────────────────────────────────

/// <summary>Detalle de una cuota pagada con su comisión</summary>
public record ComisionCuotaDto(
    int CuotaId,
    int NumeroCuota,
    DateTime? FechaPago,
    decimal MontoCuota,
    decimal MontoPagado,
    decimal MontoCapital,
    decimal MontoInteres,
    decimal PorcentajeCobrador,
    decimal ComisionCuota       // MontoPagado * PorcentajeCobrador / 100
);

/// <summary>Resumen de comisiones por préstamo</summary>
public record ComisionPrestamoDto(
    int PrestamoId,
    string ClienteNombre,
    string ClienteCedula,
    decimal MontoPrestado,
    string EstadoPrestamo,
    decimal PorcentajeCobrador,
    int CuotasTotales,
    int CuotasPagadas,
    int CuotasParciales,
    decimal TotalRecaudado,         // Suma de MontoPagado en cuotas pagadas/abonadas
    decimal TotalRecaudadoParcial,  // Suma de MontoPagado en cuotas parciales
    decimal ComisionPrestamo,       // Comisión sobre cuotas pagadas/abonadas
    decimal ComisionParcial,        // Comisión proporcional sobre cuotas parciales
    List<ComisionCuotaDto> CuotasPagadasDetalle
);

/// <summary>Liquidación completa de un cobrador: todo lo que ha generado</summary>
public record LiquidacionCobradorDto(
    int CobradorId,
    string CobradorNombre,
    string? CobradorTelefono,
    int TotalPrestamos,
    int TotalCuotasPagadas,
    int TotalCuotasParciales,
    decimal TotalRecaudado,         // Base de comisión (solo pagadas/abonadas)
    decimal TotalRecaudadoParcial,  // Cuotas parciales
    decimal TotalComision,          // Comisión devengada sobre pagadas/abonadas
    decimal TotalComisionParcial,   // Comisión proporcional sobre parciales
    decimal TotalComisionGeneral,   // TotalComision + TotalComisionParcial
    decimal TotalLiquidado,         // Suma de pagos ya realizados al cobrador
    decimal SaldoPendiente,         // TotalComisionGeneral - TotalLiquidado
    DateTime FechaConsulta,
    List<ComisionPrestamoDto> Prestamos,
    List<LiquidacionRegistroDto>? HistorialLiquidaciones = null
);

/// <summary>Registro de un pago individual al cobrador</summary>
public record LiquidacionRegistroDto(
    int Id,
    decimal MontoLiquidado,
    DateTime FechaLiquidacion,
    string? Observaciones,
    string? RealizadoPorNombre
);

/// <summary>DTO de entrada para registrar un pago al cobrador</summary>
public record RegistrarLiquidacionDto(
    int CobradorId,
    decimal Monto,
    string? Observaciones
);

// ──────────────────────────────────────────────────
// Cargos adicionales: Sistema y Renovación
// ──────────────────────────────────────────────────

/// <summary>Línea de detalle de un préstamo con sus cargos de sistema/renovación</summary>
public record CargoAdicionalPrestamoDto(
    int PrestamoId,
    string ClienteNombre,
    string ClienteCedula,
    DateTime FechaPrestamo,
    string EstadoPrestamo,
    // Sistema
    decimal ValorSistema,
    bool SistemaCobrado,
    DateTime? FechaSistemaCobrado,
    // Renovación
    decimal ValorRenovacion,
    bool RenovacionCobrada,
    DateTime? FechaRenovacionCobrada
);

/// <summary>Resumen acumulado de cargos adicionales (Sistema + Renovación)</summary>
public record ResumenCargosAdicionalesDto(
    // SISTEMA
    decimal TotalSistemaFacturado,   // Suma de todos los ValorSistema
    decimal TotalSistemaCobrado,     // Solo los que tienen SistemaCobrado = true
    decimal TotalSistemaXCobrar,     // Pendientes
    int PrestamosConSistema,
    int SistemasCobrados,
    int SistemasPendientes,
    // RENOVACIÓN
    decimal TotalRenovacionFacturada,
    decimal TotalRenovacionCobrada,
    decimal TotalRenovacionXCobrar,
    int PrestamosConRenovacion,
    int RenovacionesCobradas,
    int RenovacionesPendientes,
    // GRAN TOTAL
    decimal TotalCargosFacturados,   // Sistema + Renovación facturados
    decimal TotalCargosCobrados,     // Sistema + Renovación cobrados
    decimal TotalCargosXCobrar,      // Pendientes totales
    DateTime FechaConsulta,
    List<CargoAdicionalPrestamoDto> Detalle
);

/// <summary>DTO para marcar sistema o renovación como cobrado</summary>
public record MarcarCargoDto(
    bool SistemaCobrado,
    bool RenovacionCobrada
);

