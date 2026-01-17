// API Types

// Usuario Types
export interface Usuario {
  id: number;
  nombre: string;
  email: string;
  telefono?: string;
  rol: string;
  porcentajeParticipacion: number;
  tasaInteresMensual: number;
  activo: boolean;
}

export interface LoginDto {
  email: string;
  password: string;
}

export interface RegisterDto {
  nombre: string;
  email: string;
  password: string;
  telefono?: string;
  rol: string;
}

export interface AuthResponse {
  token: string;
  usuario: Usuario;
}

// Aporte/Capital Types
export interface Aporte {
  id: number;
  montoInicial: number;
  montoActual: number;
  fechaAporte: string;
  descripcion?: string;
}

export interface MovimientoCapital {
  id: number;
  tipoMovimiento: string;
  monto: number;
  saldoAnterior: number;
  saldoNuevo: number;
  fechaMovimiento: string;
  descripcion?: string;
}

export interface BalanceSocio {
  id: number;
  nombre: string;
  email: string;
  rol: string;
  porcentajeParticipacion: number;
  tasaInteresMensual: number;
  capitalInicial: number;
  capitalActual: number;
  gananciasAcumuladas: number;
  gananciasPendientes: number;
}

export interface Cobrador {
  id: number;
  nombre: string;
  telefono?: string;
}

// Cobros Types
export interface CuotaCobro {
  id: number;
  prestamoId: number;
  numeroCuota: number;
  fechaCobro: string;
  montoCuota: number;
  montoPagado: number;
  saldoPendiente: number;
  estadoCuota: string;
  cobrado: boolean;
  clienteNombre: string;
  clienteTelefono?: string;
  cobradorNombre?: string;
  vencido: boolean;
}

export interface CobrosHoy {
  fecha: string;
  cuotasHoy: CuotaCobro[];
  cuotasVencidas: CuotaCobro[];
  resumen: {
    totalCuotasHoy: number;
    totalCuotasVencidas: number;
    montoTotalHoy: number;
    montoTotalVencido: number;
    montoPendienteTotal: number;
  };
}

// Original Types
export interface Cliente {
  id: number;
  nombre: string;
  cedula: string;
  telefono?: string;
  direccion?: string;
  email?: string;
  fechaRegistro: string;
  estado: string;
  prestamosActivos: number;
  totalPrestado: number;
}

export interface CreateClienteDto {
  nombre: string;
  cedula: string;
  telefono?: string;
  direccion?: string;
  email?: string;
}

export interface UpdateClienteDto {
  nombre: string;
  cedula?: string;
  telefono?: string;
  direccion?: string;
  email?: string;
  estado: string;
}

export interface CuotaProxima {
  fechaCobro: string;
  monto: number;
}

export interface Prestamo {
  id: number;
  clienteId: number;
  clienteNombre: string;
  clienteCedula: string;
  clienteTelefono?: string;
  montoPrestado: number;
  tasaInteres: number;
  tipoInteres: string;
  frecuenciaPago: string;
  numeroCuotas: number;
  fechaPrestamo: string;
  fechaVencimiento: string;
  montoTotal: number;
  montoIntereses: number;
  montoCuota: number;
  estadoPrestamo: string;
  descripcion?: string;
  totalPagado: number;
  saldoPendiente: number;
  cuotasPagadas: number;
  proximaCuota?: CuotaProxima;
  cobradorId?: number;
  cobradorNombre?: string;
  porcentajeCobrador: number;
  diaSemana?: string;
  esCongelado?: boolean; // Préstamo congelado: solo paga intereses
}

export interface CreatePrestamoDto {
  clienteId: number;
  montoPrestado: number;
  tasaInteres: number;
  tipoInteres: string;
  frecuenciaPago: string;
  duracion: number;
  unidadDuracion: string;
  fechaPrestamo: string;
  descripcion?: string;
  cobradorId?: number;
  porcentajeCobrador: number;
  diaSemana?: string; // Para frecuencia Semanal: Lunes, Martes, etc.
  esCongelado?: boolean; // Préstamo congelado: solo intereses
}

export interface Cuota {
  id: number;
  prestamoId: number;
  numeroCuota: number;
  fechaCobro: string;
  montoCuota: number;
  montoPagado: number;
  saldoPendiente: number;
  estadoCuota: string;
  fechaPago?: string;
  observaciones?: string;
  fechaEditada: boolean;
  cobrado: boolean;
}

export interface Pago {
  id: number;
  prestamoId: number;
  cuotaId?: number;
  numeroCuota?: number;
  montoPago: number;
  fechaPago: string;
  metodoPago?: string;
  comprobante?: string;
  observaciones?: string;
}

export interface CreatePagoDto {
  prestamoId: number;
  cuotaId?: number;
  montoPago: number;
  fechaPago: string;
  metodoPago?: string;
  comprobante?: string;
  observaciones?: string;
}

export interface EvolucionPrestamos {
  fecha: string;
  montoPrestadoAcumulado: number;
  montoCobradoAcumulado: number;
}

export interface TopCliente {
  nombre: string;
  totalPrestado: number;
}

export interface DistribucionEstados {
  activos: number;
  pagados: number;
  vencidos: number;
}

export interface IngresoMensual {
  mes: string;
  interesesGanados: number;
  capitalRecuperado: number;
}

export interface CuotaProximaDetalle {
  cuotaId: number;
  prestamoId: number;
  clienteNombre: string;
  fechaCobro: string;
  montoCuota: number;
  estadoCuota: string;
  diasParaVencer: number;
}

export interface DashboardMetricas {
  totalPrestado: number;
  totalACobrar: number;
  interesMes: number;         // Intereses de cuotas del mes
  gananciaTotalMes: number;   // Total cuotas a cobrar del mes
  prestamosActivos: number;
  montoPrestamosActivos: number;
  cuotasVencidasHoy: number;
  montoCuotasVencidasHoy: number;
  cuotasProximas7Dias: number;
  montoCuotasProximas7Dias: number;
  tasaPromedioInteres: number;
  porcentajeMorosidad: number;
  evolucionPrestamos: EvolucionPrestamos[];
  topClientes: TopCliente[];
  distribucionEstados: DistribucionEstados;
  ingresosMensuales: IngresoMensual[];
  cuotasProximasDetalle: CuotaProximaDetalle[];
  // Flujo de Capital
  totalCobrado: number;
  dineroCirculando: number;
  reservaDisponible: number;
  capitalInicial: number;  // Nuevo: suma préstamos - (pagos - intereses)
}

// Aportador Externo
export interface AportadorExterno {
  id: number;
  nombre: string;
  telefono?: string;
  email?: string;
  tasaInteres: number;
  diasParaPago: number;
  montoTotalAportado: number;
  montoPagado: number;
  saldoPendiente: number;
  estado: string;
  fechaCreacion: string;
  notas?: string;
}

export interface CreateAportadorExternoDto {
  nombre: string;
  telefono?: string;
  email?: string;
  tasaInteres: number;
  diasParaPago: number;
  notas?: string;
  montoTotalAportado?: number;
  estado?: string;  // Para updates
}

// Fuente de Capital para préstamos
export interface FuenteCapital {
  tipo: 'Reserva' | 'Interno' | 'Externo';
  usuarioId?: number;
  aportadorExternoId?: number;
  montoAportado: number;
}

// Socio interno
export interface Socio {
  id: number;
  nombre: string;
  telefono?: string;
  rol: string;
  tasaInteresMensual: number;
  porcentajeParticipacion: number;
  capitalActual: number;
  gananciasAcumuladas: number;
  saldoTotal: number;
  ultimoCalculoInteres?: string;
}

// Balance de capital para crear préstamos
export interface BalanceCapital {
  reservaDisponible: number;
  totalCobrado: number;
  capitalUsadoDeReserva: number;
  socios: Socio[];
  aportadoresExternos: AportadorExterno[];
}

// DTO para crear préstamo con fuentes
export interface CreatePrestamoConFuentesDto {
  clienteId: number;
  montoPrestado: number;
  tasaInteres: number;
  tipoInteres: string;
  frecuenciaPago: string;
  duracion: number;
  unidadDuracion: string;
  fechaPrestamo: string;
  descripcion?: string;
  cobradorId?: number;
  porcentajeCobrador: number;
  fuentesCapital: FuenteCapital[];
}

// SMS Campaign Types
export interface SmsCampaign {
  id: number;
  nombre: string;
  mensaje: string;
  activo: boolean;
  diasEnvio: string;
  horasEnvio: string;
  vecesPorDia: number;
  tipoDestinatario: string;
  fechaCreacion: string;
  fechaModificacion?: string;
  smsEnviados?: number;
}

export interface CreateSmsCampaignDto {
  nombre: string;
  mensaje: string;
  activo: boolean;
  diasEnvio: string;
  horasEnvio: string;
  vecesPorDia: number;
  tipoDestinatario: string;
}

export interface SmsHistory {
  id: number;
  smsCampaignId?: number;
  campaignNombre?: string;
  clienteId?: number;
  clienteNombre?: string;
  numeroTelefono: string;
  mensaje: string;
  fechaEnvio: string;
  estado: string;
  twilioSid?: string;
  errorMessage?: string;
}

export interface SmsHistoryResponse {
  data: SmsHistory[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// Extended Cuota for Cobros del Mes
export interface CuotaCobroMes {
  id: number;
  prestamoId: number;
  numeroCuota: number;
  fechaCobro: string;
  montoCuota: number;
  montoPagado: number;
  saldoPendiente: number;
  estadoCuota: string;
  cobrado: boolean;
  clienteNombre: string;
  clienteTelefono?: string;
  cobradorNombre?: string;
  diasParaVencer: number;
}

export interface CobrosDelMes {
  fecha: string;
  mesActual: string;
  cuotasHoy: CuotaCobroMes[];
  cuotasVencidas: CuotaCobroMes[];
  cuotasProximas: CuotaCobroMes[];
  resumen: {
    totalCuotasHoy: number;
    totalCuotasVencidas: number;
    totalCuotasProximas: number;
    montoTotalHoy: number;
    montoTotalVencido: number;
    montoTotalProximas: number;
  };
}

// Préstamos del Día
export interface PrestamoDelDia {
  id: number;
  clienteId: number;
  clienteNombre: string;
  clienteCedula: string;
  clienteTelefono?: string;
  montoPrestado: number;
  tasaInteres: number;
  tipoInteres: string;
  frecuenciaPago: string;
  numeroCuotas: number;
  fechaPrestamo: string;
  cobradorNombre?: string;
  porcentajeCobrador: number;
  estadoPrestamo: string;
}

export interface PrestamosDelDia {
  fecha: string;
  prestamosHoy: PrestamoDelDia[];
  resumen: {
    totalPrestamosHoy: number;
    montoTotalDesembolsado: number;
  };
}


// Mi Balance Types
export interface AporteDetalle {
  id: number;
  montoInicial: number;
  montoActual: number;
  fechaAporte: string;
  descripcion?: string;
  mesesTranscurridos: number;
  interesGenerado: number;
}

export interface MiBalance {
  usuarioId: number;
  nombreUsuario: string;
  tasaInteresMensual: number;
  capitalAportado: number;
  interesGanado: number;
  capitalConInteres: number;
  fechaInicio?: string;
  mesesTranscurridos: number;
  totalCapitalNegocio: number;
  restoTorta: number;
  aportes: AporteDetalle[];
}

// Costo Operativo Types
export interface Costo {
  id: number;
  nombre: string;
  monto: number;
  frecuencia: string;  // Mensual, Quincenal, Único
  descripcion?: string;
  activo: boolean;
  fechaCreacion: string;
  fechaFin?: string;
}

export interface CreateCostoDto {
  nombre: string;
  monto: number;
  frecuencia: string;
  descripcion?: string;
}

export interface UpdateCostoDto {
  nombre: string;
  monto: number;
  frecuencia: string;
  descripcion?: string;
  activo: boolean;
  fechaFin?: string;
}

// Ganancia de socio por préstamo
export interface GananciaSocioPrestamo {
  socioId: number;
  nombreSocio: string;
  gananciaAcumulada: number;
  gananciaProyectada: number;
}

// Préstamo con ganancias de socios
export interface PrestamoConGanancias {
  prestamoId: number;
  clienteNombre: string;
  montoPrestado: number;
  montoIntereses: number;
  tasaInteres: number;
  cobradorNombre?: string;
  porcentajeCobrador: number;
  cuotasTotales: number;
  cuotasPagadas: number;
  interesAcumulado: number;
  gananciaCobrador: number;
  interesNetoSocios: number;
  gananciasSocios: GananciaSocioPrestamo[];
}
