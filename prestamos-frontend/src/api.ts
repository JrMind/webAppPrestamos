import {
    Cliente, CreateClienteDto, CreatePagoDto, CreatePrestamoDto,
    Cuota, DashboardMetricas, Pago, Prestamo, LoginDto, AuthResponse,
    Usuario, Cobrador, BalanceSocio, CobrosHoy, MovimientoCapital, Aporte
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
    }
};

// Usuarios
export const usuariosApi = {
    getAll: async (): Promise<Usuario[]> => {
        const response = await fetch(`${API_URL}/usuarios`, { headers: getHeaders() });
        return handleResponse<Usuario[]>(response);
    },

    getCobradores: async (): Promise<Cobrador[]> => {
        const response = await fetch(`${API_URL}/usuarios/cobradores`);
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
    }
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

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${API_URL}/prestamos/${id}`, {
            method: 'DELETE',
            headers: getHeaders(),
        });
        if (!response.ok) throw new Error('Error al eliminar préstamo');
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
};

// Dashboard
export const dashboardApi = {
    getMetricas: async (): Promise<DashboardMetricas> => {
        const response = await fetch(`${API_URL}/dashboard/metricas`, { headers: getHeaders() });
        return handleResponse<DashboardMetricas>(response);
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
