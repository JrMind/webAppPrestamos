import { Cliente, CreateClienteDto, CreatePagoDto, CreatePrestamoDto, Cuota, DashboardMetricas, Pago, Prestamo } from './types';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';

async function handleResponse<T>(response: Response): Promise<T> {
    if (!response.ok) {
        const error = await response.json().catch(() => ({ message: 'Error desconocido' }));
        throw new Error(error.message || 'Error en la petición');
    }
    return response.json();
}

// Clientes
export const clientesApi = {
    getAll: async (): Promise<Cliente[]> => {
        const response = await fetch(`${API_URL}/clientes`);
        return handleResponse<Cliente[]>(response);
    },

    getById: async (id: number): Promise<Cliente> => {
        const response = await fetch(`${API_URL}/clientes/${id}`);
        return handleResponse<Cliente>(response);
    },

    create: async (data: CreateClienteDto): Promise<Cliente> => {
        const response = await fetch(`${API_URL}/clientes`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data),
        });
        return handleResponse<Cliente>(response);
    },

    update: async (id: number, data: Partial<Cliente>): Promise<void> => {
        const response = await fetch(`${API_URL}/clientes/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data),
        });
        if (!response.ok) throw new Error('Error al actualizar cliente');
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${API_URL}/clientes/${id}`, {
            method: 'DELETE',
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
        const response = await fetch(url);
        return handleResponse<Prestamo[]>(response);
    },

    getById: async (id: number): Promise<Prestamo> => {
        const response = await fetch(`${API_URL}/prestamos/${id}`);
        return handleResponse<Prestamo>(response);
    },

    getByCliente: async (clienteId: number): Promise<Prestamo[]> => {
        const response = await fetch(`${API_URL}/prestamos/cliente/${clienteId}`);
        return handleResponse<Prestamo[]>(response);
    },

    create: async (data: CreatePrestamoDto): Promise<Prestamo> => {
        const response = await fetch(`${API_URL}/prestamos`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data),
        });
        return handleResponse<Prestamo>(response);
    },

    update: async (id: number, data: { estadoPrestamo: string; descripcion?: string }): Promise<void> => {
        const response = await fetch(`${API_URL}/prestamos/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data),
        });
        if (!response.ok) throw new Error('Error al actualizar préstamo');
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${API_URL}/prestamos/${id}`, {
            method: 'DELETE',
        });
        if (!response.ok) throw new Error('Error al eliminar préstamo');
    },
};

// Cuotas
export const cuotasApi = {
    getByPrestamo: async (prestamoId: number): Promise<Cuota[]> => {
        const response = await fetch(`${API_URL}/cuotas/prestamo/${prestamoId}`);
        return handleResponse<Cuota[]>(response);
    },

    updateFecha: async (id: number, fechaCobro: string): Promise<void> => {
        const response = await fetch(`${API_URL}/cuotas/${id}/fecha`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ fechaCobro }),
        });
        if (!response.ok) throw new Error('Error al actualizar fecha de cuota');
    },

    getProximasVencer: async (dias: number = 7): Promise<Cuota[]> => {
        const response = await fetch(`${API_URL}/cuotas/proximas-vencer?dias=${dias}`);
        return handleResponse<Cuota[]>(response);
    },
};

// Pagos
export const pagosApi = {
    getByPrestamo: async (prestamoId: number): Promise<Pago[]> => {
        const response = await fetch(`${API_URL}/pagos/prestamo/${prestamoId}`);
        return handleResponse<Pago[]>(response);
    },

    create: async (data: CreatePagoDto): Promise<Pago> => {
        const response = await fetch(`${API_URL}/pagos`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data),
        });
        return handleResponse<Pago>(response);
    },

    delete: async (id: number): Promise<void> => {
        const response = await fetch(`${API_URL}/pagos/${id}`, {
            method: 'DELETE',
        });
        if (!response.ok) throw new Error('Error al eliminar pago');
    },
};

// Dashboard
export const dashboardApi = {
    getMetricas: async (): Promise<DashboardMetricas> => {
        const response = await fetch(`${API_URL}/dashboard/metricas`);
        return handleResponse<DashboardMetricas>(response);
    },
};
