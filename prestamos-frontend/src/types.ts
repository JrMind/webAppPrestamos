// API Types
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
  fechaCreacion: string;
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
}
