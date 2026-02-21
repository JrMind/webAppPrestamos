import {
    Cliente, CreateClienteDto, CreatePagoDto, CreatePrestamoDto,
    Cuota, DashboardMetricas, Pago, Prestamo, LoginDto, AuthResponse,
    Usuario, Cobrador, BalanceSocio, CobrosHoy, MovimientoCapital, Aporte,
    MetricasGenerales
} from './types';

const API_URL = import.meta.env.DEV
    ? 'http://localhost:5000/api'
    : 'https://plankton-app-eucni.ondigitalocean.app/api';

// Token management
let authToken: string | null = localStorage.getItem('token');

export const setAuthToken = (token: string | null) => {
    authToken = token;
    if (token) {
        localStorage.setItem('token', token);
    } else {
        localStorage.removeItem('token');
    }
};

export const getAuthToken = () => authToken;

const getHeaders = (): HeadersInit => {
    const headers: HeadersInit = { 'Content-Type': 'application/json' };
    if (authToken) {
        headers['Authorization'] = `Bearer ${authToken}`;
    }
    return headers;
};

async function handleResponse<T>(response: Response): Promise<T> {
    if (response.status === 401) {
        setAuthToken(null);
        window.location.href = '/';
        throw new Error('Sesión expirada');
    }
    if (!response.ok) {
        const error = await response.json().catch(() => ({ message: 'Error desconocido' }));
        throw new Error(error.message || 'Error en la petición');
    }
    return response.json();
}

// Auth
export const authApi = {
    login: async (data: LoginDto): Promise<AuthResponse> => {
        const response = await fetch(`${API_URL}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data),
        });
        const result = await handleResponse<AuthResponse>(response);
        setAuthToken(result.token);
        // Persistir usuario en localStorage para recuperarlo al refrescar
        localStorage.setItem('currentUser', JSON.stringify(result.usuario));
        return result;
    },

    register: async (data: { nombre: string; email: string; password: string; telefono?: string; rol: string }): Promise<{ message: string; usuario: Usuario }> => {
        const response = await fetch(`${API_URL}/auth/register`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data),
        });
        return handleResponse(response);
    },

    logout: () => {
        setAuthToken(null);
        localStorage.removeItem('currentUser');
    },

    // Recuperar usuario actual desde el token (para restaurar sesión al refrescar)
    me: async (): Promise<Usuario> => {
        const response = await fetch(`${API_URL}/auth/me`, { headers: getHeaders() });
        return handleResponse<Usuario>(response);
    }
};

// Usuarios
export const usuariosApi = {
    getAll: async (): Promise<Usuario[]> => {
        const response = await fetch(`${API_URL}/usuarios`, { headers: getHeaders() });
        return handleResponse<Usuario[]>(response);
    },

    getCobradores: async (): Promise<Cobrador[]> => {
        const response = await fetch(`${API_URL}/usuarios/cobradores`, { headers: getHeaders() });
        return handleResponse<Cobrador[]>(response);
    },

    create: async (data: { nombre: string; email: string; password: string; telefono?: string; rol: string; porcentajeParticipacion: number; tasaInteresMensual: number }): Promise<Usuario> => {
        const response = await fetch(`${API_URL}/usuarios`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        return handleResponse<Usuario>(response);
    },

    update: async (id: number, data: Partial<Usuario>): Promise<void> => {
        const response = await fetch(`${API_URL}/usuarios/${id}`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        if (!response.ok) throw new Error('Error al actualizar usuario');
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${API_URL}/usuarios/${id}`, {
            method: 'DELETE',
            headers: getHeaders(),
        });
        if (!response.ok) throw new Error('Error al eliminar usuario');
    },

    cambiarPassword: async (id: number, nuevaPassword: string): Promise<{ message: string }> => {
        const response = await fetch(`${API_URL}/usuarios/${id}/cambiar-password`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify({ nuevaPassword }),
        });
        if (!response.ok) {
            const error = await response.json().catch(() => ({ message: 'Error al cambiar contraseña' }));
            throw new Error(error.message);
        }
        return response.json();
    }
};

// Aportes
export const aportesApi = {
    getBalance: async (): Promise<BalanceSocio[]> => {
        const response = await fetch(`${API_URL}/aportes/balance`, { headers: getHeaders() });
        return handleResponse<BalanceSocio[]>(response);
    },

    getAportesUsuario: async (usuarioId: number): Promise<{ aportes: Aporte[]; capitalTotal: number }> => {
        const response = await fetch(`${API_URL}/aportes/usuario/${usuarioId}`, { headers: getHeaders() });
        return handleResponse(response);
    },

    getMovimientos: async (usuarioId: number): Promise<MovimientoCapital[]> => {
        const response = await fetch(`${API_URL}/aportes/movimientos/${usuarioId}`, { headers: getHeaders() });
        return handleResponse<MovimientoCapital[]>(response);
    },

    registrarAporte: async (data: { usuarioId: number; monto: number; descripcion?: string }): Promise<void> => {
        const response = await fetch(`${API_URL}/aportes/aporte`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        if (!response.ok) throw new Error('Error al registrar aporte');
    },

    registrarRetiro: async (data: { usuarioId: number; monto: number; descripcion?: string }): Promise<void> => {
        const response = await fetch(`${API_URL}/aportes/retiro`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        if (!response.ok) throw new Error('Error al registrar retiro');
    },

    ajustarCapital: async (data: { usuarioId: number; nuevoCapital: number }): Promise<{ message: string }> => {
        const response = await fetch(`${API_URL}/aportes/ajustar-capital`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        return handleResponse(response);
    }
};

// Cobros
export const cobrosApi = {
    getCobrosHoy: async (): Promise<CobrosHoy> => {
        const response = await fetch(`${API_URL}/cobros/hoy`, { headers: getHeaders() });
        return handleResponse<CobrosHoy>(response);
    },

    marcarCobrado: async (cuotaId: number, cobrado: boolean): Promise<void> => {
        const response = await fetch(`${API_URL}/cobros/${cuotaId}/marcar`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify({ cobrado }),
        });
        if (!response.ok) throw new Error('Error al marcar cuota');
    },

    enviarRecordatorio: async (cuotaId: number): Promise<{ message: string }> => {
        const response = await fetch(`${API_URL}/cobros/${cuotaId}/enviar-recordatorio`, {
            method: 'POST',
            headers: getHeaders(),
        });
        return handleResponse(response);
    },

    enviarBalanceSms: async (prestamoId: number): Promise<{ message: string }> => {
        const response = await fetch(`${API_URL}/cobros/${prestamoId}/enviar-balance`, {
            method: 'POST',
            headers: getHeaders(),
        });
        return handleResponse(response);
    },

    getComisiones: async (incluirParciales = false): Promise<import('./types').LiquidacionCobrador[]> => {
        const response = await fetch(`${API_URL}/cobros/comisiones?incluirParciales=${incluirParciales}`, { headers: getHeaders() });
        return handleResponse(response);
    },

    getComisionCobrador: async (cobradorId: number, incluirParciales = false): Promise<import('./types').LiquidacionCobrador> => {
        const response = await fetch(`${API_URL}/cobros/comisiones/${cobradorId}?incluirParciales=${incluirParciales}`, { headers: getHeaders() });
        return handleResponse(response);
    },

    liquidarCobrador: async (data: { cobradorId: number; monto: number; observaciones?: string }): Promise<{ message: string; liquidacionId: number }> => {
        const response = await fetch(`${API_URL}/cobros/liquidar`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        return handleResponse(response);
    },
};

// Clientes
export const clientesApi = {
    getAll: async (): Promise<Cliente[]> => {
        const response = await fetch(`${API_URL}/clientes`, { headers: getHeaders() });
        return handleResponse<Cliente[]>(response);
    },

    buscar: async (q: string, limite = 10): Promise<Cliente[]> => {
        const response = await fetch(`${API_URL}/clientes/buscar?q=${encodeURIComponent(q)}&limite=${limite}`, { headers: getHeaders() });
        return handleResponse<Cliente[]>(response);
    },

    getById: async (id: number): Promise<Cliente> => {
        const response = await fetch(`${API_URL}/clientes/${id}`, { headers: getHeaders() });
        return handleResponse<Cliente>(response);
    },

    create: async (data: CreateClienteDto): Promise<Cliente> => {
        const response = await fetch(`${API_URL}/clientes`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        return handleResponse<Cliente>(response);
    },

    update: async (id: number, data: Partial<Cliente>): Promise<void> => {
        const response = await fetch(`${API_URL}/clientes/${id}`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        if (!response.ok) throw new Error('Error al actualizar cliente');
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${API_URL}/clientes/${id}`, {
            method: 'DELETE',
            headers: getHeaders(),
        });
        if (!response.ok) throw new Error('Error al eliminar cliente');
    },
};

// Préstamos
export const prestamosApi = {
    getAll: async (params?: {
        fechaDesde?: string;
        fechaHasta?: string;
        estado?: string;
        frecuencia?: string;
        clienteId?: number;
        busqueda?: string;
    }): Promise<Prestamo[]> => {
        const searchParams = new URLSearchParams();
        if (params) {
            Object.entries(params).forEach(([key, value]) => {
                if (value) searchParams.append(key, String(value));
            });
        }
        const url = `${API_URL}/prestamos${searchParams.toString() ? '?' + searchParams.toString() : ''}`;
        const response = await fetch(url, { headers: getHeaders() });
        return handleResponse<Prestamo[]>(response);
    },

    getById: async (id: number): Promise<Prestamo> => {
        const response = await fetch(`${API_URL}/prestamos/${id}`, { headers: getHeaders() });
        return handleResponse<Prestamo>(response);
    },

    getByCliente: async (clienteId: number): Promise<Prestamo[]> => {
        const response = await fetch(`${API_URL}/prestamos/cliente/${clienteId}`, { headers: getHeaders() });
        return handleResponse<Prestamo[]>(response);
    },

    create: async (data: CreatePrestamoDto): Promise<Prestamo> => {
        const response = await fetch(`${API_URL}/prestamos`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        return handleResponse<Prestamo>(response);
    },

    update: async (id: number, data: { estadoPrestamo: string; descripcion?: string }): Promise<void> => {
        const response = await fetch(`${API_URL}/prestamos/${id}`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        if (!response.ok) throw new Error('Error al actualizar préstamo');
    },

    updateCompleto: async (id: number, data: any): Promise<void> => {
        const response = await fetch(`${API_URL}/prestamos/${id}`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        if (!response.ok) {
            const error = await response.json().catch(() => ({ message: 'Error al actualizar préstamo' }));
            throw new Error(error.message);
        }
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${API_URL}/prestamos/${id}`, {
            method: 'DELETE',
            headers: getHeaders(),
        });
        if (!response.ok) {
            const error = await response.json().catch(() => ({ message: 'Error' }));
            throw new Error(error.message);
        }
    },
};

// Cuotas
export const cuotasApi = {
    getByPrestamo: async (prestamoId: number): Promise<Cuota[]> => {
        const response = await fetch(`${API_URL}/cuotas/prestamo/${prestamoId}`, { headers: getHeaders() });
        return handleResponse<Cuota[]>(response);
    },

    updateFecha: async (id: number, fechaCobro: string): Promise<void> => {
        const response = await fetch(`${API_URL}/cuotas/${id}/fecha`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify({ fechaCobro }),
        });
        if (!response.ok) throw new Error('Error al actualizar fecha de cuota');
    },

    getProximasVencer: async (dias: number = 7): Promise<Cuota[]> => {
        const response = await fetch(`${API_URL}/cuotas/proximas-vencer?dias=${dias}`, { headers: getHeaders() });
        return handleResponse<Cuota[]>(response);
    },
};

// Pagos
export const pagosApi = {
    getByPrestamo: async (prestamoId: number): Promise<Pago[]> => {
        const response = await fetch(`${API_URL}/pagos/prestamo/${prestamoId}`, { headers: getHeaders() });
        return handleResponse<Pago[]>(response);
    },

    create: async (data: CreatePagoDto): Promise<Pago> => {
        const response = await fetch(`${API_URL}/pagos`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        return handleResponse<Pago>(response);
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${API_URL}/pagos/${id}`, {
            method: 'DELETE',
            headers: getHeaders(),
        });
        if (!response.ok) throw new Error('Error al eliminar pago');
    },

    abonoCapital: async (prestamoId: number, monto: number, metodoPago?: string): Promise<{
        message: string;
        capitalAnterior: number;
        nuevoCapital: number;
        nuevaCuota: number;
        estadoPrestamo: string;
    }> => {
        const response = await fetch(`${API_URL}/pagos/abono-capital/${prestamoId}`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify({ monto, metodoPago }),
        });
        return handleResponse(response);
    },

    getPorDia: async (fechaInicio?: string, fechaFin?: string): Promise<{
        fechaInicio: string;
        fechaFin: string;
        totalGeneral: number;
        totalPagos: number;
        diasConPagos: number;
        porDia: Array<{
            fecha: string;
            totalDia: number;
            cantidadPagos: number;
            pagos: Array<{
                id: number;
                prestamoId: number;
                clienteNombre: string;
                montoPago: number;
                fechaPago: string;
                metodoPago: string;
                observaciones: string;
            }>;
        }>;
    }> => {
        const params = new URLSearchParams();
        if (fechaInicio) params.append('fechaInicio', fechaInicio);
        if (fechaFin) params.append('fechaFin', fechaFin);
        const url = `${API_URL}/pagos/por-dia${params.toString() ? '?' + params.toString() : ''}`;
        const response = await fetch(url, { headers: getHeaders() });
        return handleResponse(response);
    }
};

// Dashboard
export const dashboardApi = {
    getMetricas: async (): Promise<DashboardMetricas> => {
        const response = await fetch(`${API_URL}/dashboard/metricas`, { headers: getHeaders() });
        return handleResponse<DashboardMetricas>(response);
    },
    getMetricasCobradores: async (): Promise<MetricasGenerales> => {
        const response = await fetch(`${API_URL}/dashboard/metricas-cobradores`, { headers: getHeaders() });
        return handleResponse<MetricasGenerales>(response);
    },
};

// Balance Personal
export interface MiBalanceCobrador {
    rol: string;
    totalPrestamosAsignados: number;
    prestamosActivos: number;
    comisionesTotales: number;
    comisionesPendientes: number;
    comisionesCobradas: number;
    montoTotalReferido: number;
    detalleActivos: Array<{
        id: number;
        clienteNombre: string;
        montoTotal: number;
        porcentajeCobrador: number;
        comision: number;
        totalPagado: number;
        saldoPendiente: number;
    }>;
}

export interface MiBalanceSocio {
    rol: string;
    porcentajeParticipacion: number;
    tasaInteresMensual: number;
    capitalInicial: number;
    capitalActual: number;
    gananciasAcumuladas: number;
    gananciasPendientes: number;
    gananciasPagadas: number;
    portafolioGlobal: {
        totalPrestado: number;
        totalCobrado: number;
        prestamosActivos: number;
        miParticipacionCapital: number;
    };
}

export type MiBalance = MiBalanceCobrador | MiBalanceSocio;

export const balanceApi = {
    getMiBalance: async (): Promise<MiBalance> => {
        const response = await fetch(`${API_URL}/balance/mi-balance`, { headers: getHeaders() });
        return handleResponse<MiBalance>(response);
    },
};

// Capital y Fuentes
import type { BalanceCapital, AportadorExterno, CreateAportadorExternoDto, CreatePrestamoConFuentesDto, Socio } from './types';

export const capitalApi = {
    getBalance: async (): Promise<BalanceCapital> => {
        const response = await fetch(`${API_URL}/capital/balance`, { headers: getHeaders() });
        return handleResponse<BalanceCapital>(response);
    },

    getSocios: async (): Promise<Socio[]> => {
        const response = await fetch(`${API_URL}/capital/socios`, { headers: getHeaders() });
        return handleResponse<Socio[]>(response);
    },
};

export const aportadoresExternosApi = {
    getAll: async (): Promise<AportadorExterno[]> => {
        const response = await fetch(`${API_URL}/aportadoresexternos`, { headers: getHeaders() });
        return handleResponse<AportadorExterno[]>(response);
    },

    getById: async (id: number): Promise<AportadorExterno> => {
        const response = await fetch(`${API_URL}/aportadoresexternos/${id}`, { headers: getHeaders() });
        return handleResponse<AportadorExterno>(response);
    },

    create: async (data: CreateAportadorExternoDto): Promise<AportadorExterno> => {
        const response = await fetch(`${API_URL}/aportadoresexternos`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        return handleResponse<AportadorExterno>(response);
    },

    update: async (id: number, data: Partial<AportadorExterno>): Promise<void> => {
        const response = await fetch(`${API_URL}/aportadoresexternos/${id}`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        if (!response.ok) {
            const error = await response.json().catch(() => ({ message: 'Error' }));
            throw new Error(error.message);
        }
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${API_URL}/aportadoresexternos/${id}`, {
            method: 'DELETE',
            headers: getHeaders(),
        });
        if (!response.ok) {
            const error = await response.json().catch(() => ({ message: 'Error' }));
            throw new Error(error.message);
        }
    },
};

// Extender prestamosApi para crear con fuentes
export const prestamosConFuentesApi = {
    create: async (data: CreatePrestamoConFuentesDto): Promise<Prestamo> => {
        const response = await fetch(`${API_URL}/prestamos/con-fuentes`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        return handleResponse<Prestamo>(response);
    },
};

// SMS Campaigns
import type { SmsCampaign, CreateSmsCampaignDto, SmsHistoryResponse, CobrosDelMes, MiBalance as MiBalanceType } from './types';

export const smsCampaignsApi = {
    getAll: async (): Promise<SmsCampaign[]> => {
        const response = await fetch(`${API_URL}/smscampaigns`, { headers: getHeaders() });
        return handleResponse<SmsCampaign[]>(response);
    },

    getById: async (id: number): Promise<SmsCampaign> => {
        const response = await fetch(`${API_URL}/smscampaigns/${id}`, { headers: getHeaders() });
        return handleResponse<SmsCampaign>(response);
    },

    create: async (data: CreateSmsCampaignDto): Promise<{ message: string; id: number }> => {
        const response = await fetch(`${API_URL}/smscampaigns`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        return handleResponse(response);
    },

    update: async (id: number, data: Partial<CreateSmsCampaignDto>): Promise<void> => {
        const response = await fetch(`${API_URL}/smscampaigns/${id}`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        if (!response.ok) throw new Error('Error al actualizar campaña');
    },

    toggle: async (id: number): Promise<{ activo: boolean }> => {
        const response = await fetch(`${API_URL}/smscampaigns/${id}/toggle`, {
            method: 'PUT',
            headers: getHeaders(),
        });
        return handleResponse(response);
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${API_URL}/smscampaigns/${id}`, {
            method: 'DELETE',
            headers: getHeaders(),
        });
        if (!response.ok) throw new Error('Error al eliminar campaña');
    },
};

export const smsHistoryApi = {
    getAll: async (params?: { fechaDesde?: string; fechaHasta?: string; campaignId?: number; page?: number; pageSize?: number }): Promise<SmsHistoryResponse> => {
        const searchParams = new URLSearchParams();
        if (params) {
            Object.entries(params).forEach(([key, value]) => {
                if (value !== undefined) searchParams.append(key, String(value));
            });
        }
        const url = `${API_URL}/smshistory${searchParams.toString() ? '?' + searchParams.toString() : ''}`;
        const response = await fetch(url, { headers: getHeaders() });
        return handleResponse<SmsHistoryResponse>(response);
    },

    getStats: async (days?: number): Promise<{ totalEnviados: number; porEstado: Array<{ Estado: string; Count: number }> }> => {
        const url = `${API_URL}/smshistory/stats${days ? '?days=' + days : ''}`;
        const response = await fetch(url, { headers: getHeaders() });
        return handleResponse(response);
    },
};

// Cobros del Mes (Tareas Diarias)
export const cobrosDelMesApi = {
    getCobrosDelMes: async (cobradorId?: number): Promise<CobrosDelMes> => {
        const url = cobradorId
            ? `${API_URL}/cobros/mes?cobradorId=${cobradorId}`
            : `${API_URL}/cobros/mes`;
        const response = await fetch(url, { headers: getHeaders() });
        return handleResponse<CobrosDelMes>(response);
    },
};

// Préstamos del Día
import type { PrestamosDelDia } from './types';

export const prestamosDelDiaApi = {
    getPrestamosDelDia: async (fecha?: string): Promise<PrestamosDelDia> => {
        const url = fecha
            ? `${API_URL}/prestamos/dia?fecha=${fecha}`
            : `${API_URL}/prestamos/dia`;
        const response = await fetch(url, { headers: getHeaders() });
        return handleResponse<PrestamosDelDia>(response);
    },
};



// Mi Balance (nuevo endpoint para balance personal con cálculo de interés)
export const miBalanceApi = {
    getMiBalance: async (usuarioId?: number): Promise<MiBalanceType> => {
        const url = usuarioId ? `${API_URL}/aportes/mi-balance?usuarioId=${usuarioId}` : `${API_URL}/aportes/mi-balance`;
        const response = await fetch(url, { headers: getHeaders() });
        return handleResponse<MiBalanceType>(response);
    },
};

// Ganancias por Participación
export interface ResumenParticipacion {
    aportadores: Array<{
        id: number;
        nombre: string;
        capitalAportado: number;
        tasaInteres: number;
        gananciaMensual: number;
        estado: string;
    }>;
    cobradores: Array<{
        cobradorId: number;
        nombre: string;
        prestamosAsignados: number;
        gananciaProyectada: number;
        gananciaInteresMes: number; // Nuevo
        gananciaRealizada: number;
        detalle: Array<{
            id: number;
            montoIntereses: number;
            porcentajeCobrador: number;
            proyeccion: number;
        }>;
    }>;
    socios: Array<{
        id: number;
        nombre: string;
        capitalAportado: number;
        capitalActual: number;
        porcentaje: number;
        gananciaProyectadaTotal: number;
        gananciaInteresMes: number; // Nuevo
        flujoNetoMes: number; // Nuevo
        gananciaRealizada: number;
    }>;
    resumen: {
        totalCapitalPrestado: number;
        totalCapitalBase: number;
        capitalReinvertido: number;
        capitalEnCalle: number; // Nuevo
        totalInteresesProyectados: number;
        proyeccionInteresesMesActual: number;
        flujoTotalMes: number;
        totalGananciaCobradores: number;
        totalGananciaSociosBruta: number;
        gastoMensualAportadores: number;
        costosTotalesMes: number;           // Nuevo: Costos operativos
        gananciaInteresNeta: number;        // Nuevo: Intereses - Cobradores - Aportadores - Costos
        sumaPartes: number;
        diferencia: number;
    };
}

export const gananciasApi = {
    getResumenParticipacion: async (): Promise<ResumenParticipacion> => {
        const response = await fetch(`${API_URL}/ganancias/resumen-participacion`, { headers: getHeaders() });
        return handleResponse<ResumenParticipacion>(response);
    },
};

// Costos Operativos
import type { Costo, CreateCostoDto, UpdateCostoDto, PrestamoConGanancias } from './types';

export const costosApi = {
    getAll: async (): Promise<Costo[]> => {
        const response = await fetch(`${API_URL}/costos`, { headers: getHeaders() });
        return handleResponse<Costo[]>(response);
    },

    getById: async (id: number): Promise<Costo> => {
        const response = await fetch(`${API_URL}/costos/${id}`, { headers: getHeaders() });
        return handleResponse<Costo>(response);
    },

    create: async (data: CreateCostoDto): Promise<Costo> => {
        const response = await fetch(`${API_URL}/costos`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        return handleResponse<Costo>(response);
    },

    update: async (id: number, data: UpdateCostoDto): Promise<Costo> => {
        const response = await fetch(`${API_URL}/costos/${id}`, {
            method: 'PUT',
            headers: getHeaders(),
            body: JSON.stringify(data),
        });
        return handleResponse<Costo>(response);
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${API_URL}/costos/${id}`, {
            method: 'DELETE',
            headers: getHeaders(),
        });
        if (!response.ok) throw new Error('Error al eliminar costo');
    },

    getResumenMensual: async (): Promise<{ costosMensuales: number; costosQuincenales: number; totalMensualizado: number }> => {
        const response = await fetch(`${API_URL}/costos/resumen-mensual`, { headers: getHeaders() });
        return handleResponse(response);
    },
};

// Ganancias de Socios por Préstamo
export const prestamoGananciasApi = {
    getGananciasSocios: async (prestamoId: number): Promise<PrestamoConGanancias> => {
        const response = await fetch(`${API_URL}/prestamos/${prestamoId}/ganancias-socios`, { headers: getHeaders() });
        return handleResponse<PrestamoConGanancias>(response);
    },
};
