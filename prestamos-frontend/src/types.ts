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
  totalGanadoIntereses: number;
  interesesProyectados: number; // NUEVO
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

