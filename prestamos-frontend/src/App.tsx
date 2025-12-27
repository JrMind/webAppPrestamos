import { useState, useEffect, useCallback } from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar, PieChart, Pie, Cell, Legend } from 'recharts';
import { clientesApi, prestamosApi, cuotasApi, pagosApi, dashboardApi } from './api';
import { Cliente, CreateClienteDto, CreatePrestamoDto, CreatePagoDto, Cuota, DashboardMetricas, Pago, Prestamo } from './types';
import './App.css';

// Utility functions
const formatMoney = (amount: number): string => {
  return new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'COP' }).format(amount);
};

const formatDate = (dateStr: string): string => {
  return new Date(dateStr).toLocaleDateString('es-ES');
};

const formatDateInput = (date: Date): string => {
  return date.toISOString().split('T')[0];
};

// Toast component
interface Toast {
  id: number;
  message: string;
  type: 'success' | 'error' | 'warning';
}

function App() {
  // State
  const [loading, setLoading] = useState(true);
  const [toasts, setToasts] = useState<Toast[]>([]);
  const [activeTab, setActiveTab] = useState<'prestamos' | 'clientes' | 'cuotas'>('prestamos');

  // Data
  const [metricas, setMetricas] = useState<DashboardMetricas | null>(null);
  const [prestamos, setPrestamos] = useState<Prestamo[]>([]);
  const [clientes, setClientes] = useState<Cliente[]>([]);

  // Filters
  const [filtroEstado, setFiltroEstado] = useState('Todos');
  const [filtroFrecuencia, setFiltroFrecuencia] = useState('Todos');
  const [filtroBusqueda, setFiltroBusqueda] = useState('');
  const [filtroClienteId, setFiltroClienteId] = useState<number | undefined>();

  // Modals
  const [showClienteModal, setShowClienteModal] = useState(false);
  const [showPrestamoModal, setShowPrestamoModal] = useState(false);
  const [showDetalleModal, setShowDetalleModal] = useState(false);
  const [showPagoModal, setShowPagoModal] = useState(false);
  const [selectedPrestamo, setSelectedPrestamo] = useState<Prestamo | null>(null);
  const [cuotasDetalle, setCuotasDetalle] = useState<Cuota[]>([]);
  const [pagosDetalle, setPagosDetalle] = useState<Pago[]>([]);
  const [selectedCuota, setSelectedCuota] = useState<Cuota | null>(null);

  // Toast helper
  const showToast = (message: string, type: Toast['type']) => {
    const id = Date.now();
    setToasts(prev => [...prev, { id, message, type }]);
    setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 3000);
  };

  // Load data
  const loadData = useCallback(async () => {
    try {
      const [metricasData, prestamosData, clientesData] = await Promise.all([
        dashboardApi.getMetricas(),
        prestamosApi.getAll({
          estado: filtroEstado !== 'Todos' ? filtroEstado : undefined,
          frecuencia: filtroFrecuencia !== 'Todos' ? filtroFrecuencia : undefined,
          busqueda: filtroBusqueda || undefined,
          clienteId: filtroClienteId,
        }),
        clientesApi.getAll(),
      ]);
      setMetricas(metricasData);
      setPrestamos(prestamosData);
      setClientes(clientesData);
    } catch (error) {
      console.error('Error loading data:', error);
    } finally {
      setLoading(false);
    }
  }, [filtroEstado, filtroFrecuencia, filtroBusqueda, filtroClienteId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Cliente Form
  const [clienteForm, setClienteForm] = useState<CreateClienteDto>({
    nombre: '', cedula: '', telefono: '', direccion: '', email: ''
  });

  const handleCreateCliente = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await clientesApi.create(clienteForm);
      showToast('Cliente creado exitosamente', 'success');
      setShowClienteModal(false);
      setClienteForm({ nombre: '', cedula: '', telefono: '', direccion: '', email: '' });
      loadData();
    } catch (error: unknown) {
      showToast(error instanceof Error ? error.message : 'Error al crear cliente', 'error');
    }
  };

  // Prestamo Form
  const [prestamoForm, setPrestamoForm] = useState<CreatePrestamoDto>({
    clienteId: 0, montoPrestado: 0, tasaInteres: 15, tipoInteres: 'Simple',
    frecuenciaPago: 'Quincenal', duracion: 3, unidadDuracion: 'Meses',
    fechaPrestamo: formatDateInput(new Date()), descripcion: ''
  });

  // Calculate preview
  const calcularPreview = () => {
    if (!prestamoForm.montoPrestado || prestamoForm.montoPrestado < 50) return null;

    const diasMap: Record<string, number> = { Dias: 1, Semanas: 7, Quincenas: 15, Meses: 30 };
    const diasTotales = prestamoForm.duracion * (diasMap[prestamoForm.unidadDuracion] || 30);

    const frecuenciaDias: Record<string, number> = { Diario: 1, Semanal: 7, Quincenal: 15, Mensual: 30 };
    const diasEntreCuotas = frecuenciaDias[prestamoForm.frecuenciaPago] || 15;

    let numeroCuotas = Math.ceil(diasTotales / diasEntreCuotas);
    if (prestamoForm.unidadDuracion === 'Quincenas' && prestamoForm.frecuenciaPago === 'Quincenal') {
      numeroCuotas = prestamoForm.duracion;
    } else if (prestamoForm.unidadDuracion === 'Meses' && prestamoForm.frecuenciaPago === 'Mensual') {
      numeroCuotas = prestamoForm.duracion;
    }

    let montoIntereses: number;
    let montoTotal: number;

    if (prestamoForm.tipoInteres === 'Simple') {
      montoIntereses = (prestamoForm.montoPrestado * prestamoForm.tasaInteres * diasTotales) / (100 * 365);
      montoTotal = prestamoForm.montoPrestado + montoIntereses;
    } else {
      const tasaPorPeriodo = (prestamoForm.tasaInteres / 100) / (365 / diasEntreCuotas);
      montoTotal = prestamoForm.montoPrestado * Math.pow(1 + tasaPorPeriodo, numeroCuotas);
      montoIntereses = montoTotal - prestamoForm.montoPrestado;
    }

    const montoCuota = montoTotal / numeroCuotas;
    const fechaVencimiento = new Date(prestamoForm.fechaPrestamo);
    fechaVencimiento.setDate(fechaVencimiento.getDate() + diasTotales);

    return {
      numeroCuotas, montoIntereses, montoTotal, montoCuota,
      fechaVencimiento: fechaVencimiento.toISOString(), diasTotales
    };
  };

  const preview = calcularPreview();

  const handleCreatePrestamo = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!prestamoForm.clienteId) {
      showToast('Seleccione un cliente', 'warning');
      return;
    }
    try {
      await prestamosApi.create(prestamoForm);
      showToast('Préstamo creado exitosamente', 'success');
      setShowPrestamoModal(false);
      setPrestamoForm({
        clienteId: 0, montoPrestado: 0, tasaInteres: 15, tipoInteres: 'Simple',
        frecuenciaPago: 'Quincenal', duracion: 3, unidadDuracion: 'Meses',
        fechaPrestamo: formatDateInput(new Date()), descripcion: ''
      });
      loadData();
    } catch (error: unknown) {
      showToast(error instanceof Error ? error.message : 'Error al crear préstamo', 'error');
    }
  };

  // Detalle prestamo
  const openDetalle = async (prestamo: Prestamo) => {
    setSelectedPrestamo(prestamo);
    try {
      const [cuotas, pagos] = await Promise.all([
        cuotasApi.getByPrestamo(prestamo.id),
        pagosApi.getByPrestamo(prestamo.id),
      ]);
      setCuotasDetalle(cuotas);
      setPagosDetalle(pagos);
      setShowDetalleModal(true);
    } catch (error) {
      showToast('Error al cargar detalles', 'error');
    }
  };

  // Pago Form
  const [pagoForm, setPagoForm] = useState<CreatePagoDto>({
    prestamoId: 0, cuotaId: undefined, montoPago: 0,
    fechaPago: formatDateInput(new Date()), metodoPago: 'Efectivo', observaciones: ''
  });

  const openPagoModal = (cuota: Cuota) => {
    setSelectedCuota(cuota);
    setPagoForm({
      prestamoId: cuota.prestamoId,
      cuotaId: cuota.id,
      montoPago: cuota.saldoPendiente,
      fechaPago: formatDateInput(new Date()),
      metodoPago: 'Efectivo',
      observaciones: ''
    });
    setShowPagoModal(true);
  };

  const handleCreatePago = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await pagosApi.create(pagoForm);
      showToast('Pago registrado exitosamente', 'success');
      setShowPagoModal(false);
      if (selectedPrestamo) {
        const [cuotas, pagos] = await Promise.all([
          cuotasApi.getByPrestamo(selectedPrestamo.id),
          pagosApi.getByPrestamo(selectedPrestamo.id),
        ]);
        setCuotasDetalle(cuotas);
        setPagosDetalle(pagos);
      }
      loadData();
    } catch (error: unknown) {
      showToast(error instanceof Error ? error.message : 'Error al registrar pago', 'error');
    }
  };

  // Update fecha cuota
  const handleUpdateFechaCuota = async (cuotaId: number, nuevaFecha: string) => {
    try {
      await cuotasApi.updateFecha(cuotaId, nuevaFecha);
      showToast('Fecha actualizada', 'success');
      if (selectedPrestamo) {
        const cuotas = await cuotasApi.getByPrestamo(selectedPrestamo.id);
        setCuotasDetalle(cuotas);
      }
    } catch (error) {
      showToast('Error al actualizar fecha', 'error');
    }
  };

  // Delete prestamo
  const handleDeletePrestamo = async (id: number) => {
    if (!confirm('¿Está seguro de eliminar este préstamo?')) return;
    try {
      await prestamosApi.delete(id);
      showToast('Préstamo eliminado', 'success');
      loadData();
    } catch (error: unknown) {
      showToast(error instanceof Error ? error.message : 'Error al eliminar', 'error');
    }
  };

  // Colors for charts
  const COLORS = ['#10b981', '#3b82f6', '#ef4444'];

  if (loading) {
    return (
      <div className="dashboard">
        <div className="loading"><div className="spinner"></div></div>
      </div>
    );
  }

  return (
    <div className="dashboard">
      {/* Header */}
      <header className="header">
        <div className="header-left">
          <h1>Sistema de Gestión de Préstamos</h1>
          <p>Control total de préstamos, clientes y rentabilidad</p>
        </div>
        <div className="header-right">
          <div className="header-stat">
            <span>Préstamos Activos</span>
            <strong>{metricas?.prestamosActivos || 0}</strong>
          </div>
          <button className="btn btn-secondary" onClick={() => setShowClienteModal(true)}>
            + Nuevo Cliente
          </button>
          <button className="btn btn-primary" onClick={() => setShowPrestamoModal(true)}>
            + Nuevo Préstamo
          </button>
        </div>
      </header>

      {/* Main Content */}
      <main className="main-content">
        {/* Filters */}
        <div className="filters-bar">
          <div className="filter-group">
            <label>Estado</label>
            <select value={filtroEstado} onChange={e => setFiltroEstado(e.target.value)}>
              <option>Todos</option>
              <option>Activo</option>
              <option>Pagado</option>
              <option>Vencido</option>
            </select>
          </div>
          <div className="filter-group">
            <label>Frecuencia</label>
            <select value={filtroFrecuencia} onChange={e => setFiltroFrecuencia(e.target.value)}>
              <option>Todos</option>
              <option>Diario</option>
              <option>Semanal</option>
              <option>Quincenal</option>
              <option>Mensual</option>
            </select>
          </div>
          <div className="filter-group">
            <label>Cliente</label>
            <select value={filtroClienteId || ''} onChange={e => setFiltroClienteId(e.target.value ? Number(e.target.value) : undefined)}>
              <option value="">Todos</option>
              {clientes.map(c => <option key={c.id} value={c.id}>{c.nombre}</option>)}
            </select>
          </div>
          <div className="filter-group" style={{ flex: 1 }}>
            <label>Buscar</label>
            <input type="text" placeholder="Nombre o cédula..." value={filtroBusqueda} onChange={e => setFiltroBusqueda(e.target.value)} />
          </div>
          <button className="btn btn-secondary btn-sm" onClick={() => { setFiltroEstado('Todos'); setFiltroFrecuencia('Todos'); setFiltroBusqueda(''); setFiltroClienteId(undefined); }}>
            Limpiar
          </button>
        </div>

        {/* KPIs */}
        <div className="kpi-grid">
          <div className="kpi-card" style={{ '--kpi-color': '#3b82f6' } as React.CSSProperties}>
            <div className="kpi-header">
              <span className="kpi-title">Total Prestado</span>
              <span className="kpi-icon" style={{ background: 'rgba(59, 130, 246, 0.15)', color: '#3b82f6' }}>$</span>
            </div>
            <span className="kpi-value">{formatMoney(metricas?.totalPrestado || 0)}</span>
          </div>
          <div className="kpi-card" style={{ '--kpi-color': '#10b981' } as React.CSSProperties}>
            <div className="kpi-header">
              <span className="kpi-title">Total a Cobrar</span>
              <span className="kpi-icon" style={{ background: 'rgba(16, 185, 129, 0.15)', color: '#10b981' }}>↗</span>
            </div>
            <span className="kpi-value">{formatMoney(metricas?.totalACobrar || 0)}</span>
          </div>
          <div className="kpi-card" style={{ '--kpi-color': '#059669' } as React.CSSProperties}>
            <div className="kpi-header">
              <span className="kpi-title">Intereses Ganados</span>
              <span className="kpi-icon" style={{ background: 'rgba(5, 150, 105, 0.15)', color: '#059669' }}>★</span>
            </div>
            <span className="kpi-value">{formatMoney(metricas?.totalGanadoIntereses || 0)}</span>
          </div>
          <div className="kpi-card" style={{ '--kpi-color': '#f59e0b' } as React.CSSProperties}>
            <div className="kpi-header">
              <span className="kpi-title">Préstamos Activos</span>
              <span className="kpi-icon" style={{ background: 'rgba(245, 158, 11, 0.15)', color: '#f59e0b' }}>📋</span>
            </div>
            <span className="kpi-value">{metricas?.prestamosActivos || 0}</span>
            <span className="kpi-sub">{formatMoney(metricas?.montoPrestamosActivos || 0)}</span>
          </div>
          <div className={`kpi-card ${(metricas?.cuotasVencidasHoy || 0) > 0 ? 'alert' : ''}`} style={{ '--kpi-color': '#ef4444' } as React.CSSProperties}>
            <div className="kpi-header">
              <span className="kpi-title">Cuotas Vencidas Hoy</span>
              <span className="kpi-icon" style={{ background: 'rgba(239, 68, 68, 0.15)', color: '#ef4444' }}>⚠</span>
            </div>
            <span className="kpi-value">{metricas?.cuotasVencidasHoy || 0}</span>
            <span className="kpi-sub">{formatMoney(metricas?.montoCuotasVencidasHoy || 0)}</span>
          </div>
          <div className="kpi-card" style={{ '--kpi-color': '#f97316' } as React.CSSProperties}>
            <div className="kpi-header">
              <span className="kpi-title">Cuotas Próximas 7 días</span>
              <span className="kpi-icon" style={{ background: 'rgba(249, 115, 22, 0.15)', color: '#f97316' }}>📅</span>
            </div>
            <span className="kpi-value">{metricas?.cuotasProximas7Dias || 0}</span>
            <span className="kpi-sub">{formatMoney(metricas?.montoCuotasProximas7Dias || 0)}</span>
          </div>
          <div className="kpi-card" style={{ '--kpi-color': '#8b5cf6' } as React.CSSProperties}>
            <div className="kpi-header">
              <span className="kpi-title">Tasa Promedio</span>
              <span className="kpi-icon" style={{ background: 'rgba(139, 92, 246, 0.15)', color: '#8b5cf6' }}>%</span>
            </div>
            <span className="kpi-value">{(metricas?.tasaPromedioInteres || 0).toFixed(2)}%</span>
          </div>
          <div className="kpi-card" style={{ '--kpi-color': (metricas?.porcentajeMorosidad || 0) > 10 ? '#ef4444' : (metricas?.porcentajeMorosidad || 0) > 5 ? '#f59e0b' : '#10b981' } as React.CSSProperties}>
            <div className="kpi-header">
              <span className="kpi-title">Morosidad</span>
              <span className="kpi-icon" style={{ background: 'rgba(239, 68, 68, 0.15)', color: '#ef4444' }}>📉</span>
            </div>
            <span className="kpi-value">{(metricas?.porcentajeMorosidad || 0).toFixed(2)}%</span>
          </div>
        </div>

        {/* Charts */}
        <div className="charts-grid">
          <div className="chart-container">
            <h3 className="chart-title">Evolución de Préstamos</h3>
            <ResponsiveContainer width="100%" height={250}>
              <LineChart data={metricas?.evolucionPrestamos || []}>
                <CartesianGrid strokeDasharray="3 3" stroke="#2a2a2a" />
                <XAxis dataKey="fecha" tickFormatter={(v) => new Date(v).toLocaleDateString('es-ES', { month: 'short' })} stroke="#666" />
                <YAxis stroke="#666" tickFormatter={(v) => `${(v / 1000).toFixed(0)}k`} />
                <Tooltip formatter={(value) => formatMoney(Number(value || 0))} contentStyle={{ background: '#1a1a1a', border: '1px solid #2a2a2a' }} />
                <Line type="monotone" dataKey="montoPrestadoAcumulado" stroke="#3b82f6" name="Prestado" strokeWidth={2} />
                <Line type="monotone" dataKey="montoCobradoAcumulado" stroke="#10b981" name="Cobrado" strokeWidth={2} />
              </LineChart>
            </ResponsiveContainer>
          </div>
          <div className="chart-container">
            <h3 className="chart-title">Distribución por Estado</h3>
            <ResponsiveContainer width="100%" height={250}>
              <PieChart>
                <Pie
                  data={[
                    { name: 'Activos', value: metricas?.distribucionEstados?.activos || 0 },
                    { name: 'Pagados', value: metricas?.distribucionEstados?.pagados || 0 },
                    { name: 'Vencidos', value: metricas?.distribucionEstados?.vencidos || 0 },
                  ]}
                  cx="50%" cy="50%" outerRadius={80} dataKey="value" label
                >
                  {COLORS.map((color, index) => <Cell key={`cell-${index}`} fill={color} />)}
                </Pie>
                <Legend />
                <Tooltip contentStyle={{ background: '#1a1a1a', border: '1px solid #2a2a2a' }} />
              </PieChart>
            </ResponsiveContainer>
          </div>
          <div className="chart-container">
            <h3 className="chart-title">Top 10 Clientes</h3>
            <ResponsiveContainer width="100%" height={250}>
              <BarChart data={(metricas?.topClientes || []).slice(0, 10)} layout="vertical">
                <CartesianGrid strokeDasharray="3 3" stroke="#2a2a2a" />
                <XAxis type="number" stroke="#666" tickFormatter={(v) => `${(v / 1000).toFixed(0)}k`} />
                <YAxis dataKey="nombre" type="category" stroke="#666" width={100} tick={{ fontSize: 11 }} />
                <Tooltip formatter={(value) => formatMoney(Number(value || 0))} contentStyle={{ background: '#1a1a1a', border: '1px solid #2a2a2a' }} />
                <Bar dataKey="totalPrestado" fill="#3b82f6" radius={[0, 4, 4, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
          <div className="chart-container">
            <h3 className="chart-title">Ingresos Mensuales</h3>
            <ResponsiveContainer width="100%" height={250}>
              <BarChart data={metricas?.ingresosMensuales || []}>
                <CartesianGrid strokeDasharray="3 3" stroke="#2a2a2a" />
                <XAxis dataKey="mes" stroke="#666" />
                <YAxis stroke="#666" tickFormatter={(v) => `${(v / 1000).toFixed(0)}k`} />
                <Tooltip formatter={(value) => formatMoney(Number(value || 0))} contentStyle={{ background: '#1a1a1a', border: '1px solid #2a2a2a' }} />
                <Bar dataKey="capitalRecuperado" stackId="a" fill="#3b82f6" name="Capital" />
                <Bar dataKey="interesesGanados" stackId="a" fill="#10b981" name="Intereses" />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Tabs */}
        <div className="section">
          <div className="tabs">
            <button className={`tab ${activeTab === 'prestamos' ? 'active' : ''}`} onClick={() => setActiveTab('prestamos')}>
              Préstamos ({prestamos.length})
            </button>
            <button className={`tab ${activeTab === 'clientes' ? 'active' : ''}`} onClick={() => setActiveTab('clientes')}>
              Clientes ({clientes.length})
            </button>
            <button className={`tab ${activeTab === 'cuotas' ? 'active' : ''}`} onClick={() => setActiveTab('cuotas')}>
              Cuotas Próximas ({metricas?.cuotasProximasDetalle?.length || 0})
            </button>
          </div>

          {/* Prestamos Table */}
          {activeTab === 'prestamos' && (
            <div className="table-container">
              <table>
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Cliente</th>
                    <th>Monto</th>
                    <th>Interés</th>
                    <th>Frecuencia</th>
                    <th>Cuotas</th>
                    <th>Próxima Cuota</th>
                    <th>Estado</th>
                    <th>Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {prestamos.map(p => (
                    <tr key={p.id}>
                      <td>#{p.id}</td>
                      <td>
                        <div><strong>{p.clienteNombre}</strong></div>
                        <div style={{ color: '#666', fontSize: '0.75rem' }}>{p.clienteCedula}</div>
                      </td>
                      <td className="money">{formatMoney(p.montoPrestado)}</td>
                      <td>{p.tasaInteres}%</td>
                      <td>{p.frecuenciaPago}</td>
                      <td>{p.cuotasPagadas} / {p.numeroCuotas}</td>
                      <td>
                        {p.proximaCuota ? (
                          <div>
                            <div>{formatDate(p.proximaCuota.fechaCobro)}</div>
                            <div style={{ color: '#10b981', fontSize: '0.75rem' }}>{formatMoney(p.proximaCuota.monto)}</div>
                          </div>
                        ) : '-'}
                      </td>
                      <td>
                        <span className={`badge ${p.estadoPrestamo === 'Activo' ? 'badge-green' : p.estadoPrestamo === 'Pagado' ? 'badge-blue' : 'badge-red'}`}>
                          {p.estadoPrestamo}
                        </span>
                      </td>
                      <td>
                        <div className="actions">
                          <button className="btn btn-secondary btn-sm" onClick={() => openDetalle(p)}>Ver</button>
                          <button className="btn btn-danger btn-sm" onClick={() => handleDeletePrestamo(p.id)}>✕</button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {prestamos.length === 0 && (
                    <tr><td colSpan={9} className="empty-state">No hay préstamos</td></tr>
                  )}
                </tbody>
              </table>
            </div>
          )}

          {/* Clientes Table */}
          {activeTab === 'clientes' && (
            <div className="table-container">
              <table>
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Nombre</th>
                    <th>Cédula</th>
                    <th>Teléfono</th>
                    <th>Email</th>
                    <th>Préstamos Activos</th>
                    <th>Total Prestado</th>
                    <th>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  {clientes.map(c => (
                    <tr key={c.id}>
                      <td>#{c.id}</td>
                      <td><strong>{c.nombre}</strong></td>
                      <td>{c.cedula}</td>
                      <td>{c.telefono || '-'}</td>
                      <td>{c.email || '-'}</td>
                      <td>{c.prestamosActivos}</td>
                      <td className="money">{formatMoney(c.totalPrestado)}</td>
                      <td><span className={`badge ${c.estado === 'Activo' ? 'badge-green' : 'badge-gray'}`}>{c.estado}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Cuotas Proximas */}
          {activeTab === 'cuotas' && (
            <div className="table-container">
              <table>
                <thead>
                  <tr>
                    <th>Préstamo</th>
                    <th>Cliente</th>
                    <th>Fecha Cobro</th>
                    <th>Monto</th>
                    <th>Días</th>
                    <th>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  {(metricas?.cuotasProximasDetalle || []).map(c => (
                    <tr key={c.cuotaId}>
                      <td>#{c.prestamoId}</td>
                      <td><strong>{c.clienteNombre}</strong></td>
                      <td>{formatDate(c.fechaCobro)}</td>
                      <td className="money">{formatMoney(c.montoCuota)}</td>
                      <td style={{ color: c.diasParaVencer <= 0 ? '#ef4444' : c.diasParaVencer <= 3 ? '#f59e0b' : '#10b981' }}>
                        {c.diasParaVencer <= 0 ? 'Hoy/Vencida' : `${c.diasParaVencer} días`}
                      </td>
                      <td>
                        <span className={`badge ${c.estadoCuota === 'Pendiente' ? 'badge-gray' : c.estadoCuota === 'Vencida' ? 'badge-red' : 'badge-yellow'}`}>
                          {c.estadoCuota}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </main>

      {/* Modals */}
      {/* Cliente Modal */}
      {showClienteModal && (
        <div className="modal-overlay" onClick={() => setShowClienteModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Nuevo Cliente</h2>
              <button className="modal-close" onClick={() => setShowClienteModal(false)}>&times;</button>
            </div>
            <form onSubmit={handleCreateCliente}>
              <div className="modal-body">
                <div className="form-grid">
                  <div className="form-group">
                    <label>Nombre <span>*</span></label>
                    <input type="text" required value={clienteForm.nombre} onChange={e => setClienteForm({ ...clienteForm, nombre: e.target.value })} />
                  </div>
                  <div className="form-group">
                    <label>Cédula/DNI <span>*</span></label>
                    <input type="text" required value={clienteForm.cedula} onChange={e => setClienteForm({ ...clienteForm, cedula: e.target.value })} />
                  </div>
                  <div className="form-group">
                    <label>Teléfono</label>
                    <input type="tel" value={clienteForm.telefono || ''} onChange={e => setClienteForm({ ...clienteForm, telefono: e.target.value })} />
                  </div>
                  <div className="form-group">
                    <label>Email</label>
                    <input type="email" value={clienteForm.email || ''} onChange={e => setClienteForm({ ...clienteForm, email: e.target.value })} />
                  </div>
                  <div className="form-group full-width">
                    <label>Dirección</label>
                    <input type="text" value={clienteForm.direccion || ''} onChange={e => setClienteForm({ ...clienteForm, direccion: e.target.value })} />
                  </div>
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setShowClienteModal(false)}>Cancelar</button>
                <button type="submit" className="btn btn-primary">Guardar Cliente</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Prestamo Modal */}
      {showPrestamoModal && (
        <div className="modal-overlay" onClick={() => setShowPrestamoModal(false)}>
          <div className="modal modal-lg" onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Nuevo Préstamo</h2>
              <button className="modal-close" onClick={() => setShowPrestamoModal(false)}>&times;</button>
            </div>
            <form onSubmit={handleCreatePrestamo}>
              <div className="modal-body">
                <div className="form-grid">
                  <div className="form-group full-width">
                    <label>Cliente <span>*</span></label>
                    <select required value={prestamoForm.clienteId || ''} onChange={e => setPrestamoForm({ ...prestamoForm, clienteId: Number(e.target.value) })}>
                      <option value="">Seleccione un cliente...</option>
                      {clientes.map(c => <option key={c.id} value={c.id}>{c.nombre} - {c.cedula}</option>)}
                    </select>
                  </div>
                  <div className="form-group">
                    <label>Monto del Préstamo ($) <span>*</span></label>
                    <input type="number" min="50" step="0.01" required value={prestamoForm.montoPrestado || ''} onChange={e => setPrestamoForm({ ...prestamoForm, montoPrestado: Number(e.target.value) })} />
                  </div>
                  <div className="form-group">
                    <label>Tasa de Interés (%) <span>*</span></label>
                    <input type="number" min="0" step="0.01" required value={prestamoForm.tasaInteres} onChange={e => setPrestamoForm({ ...prestamoForm, tasaInteres: Number(e.target.value) })} />
                  </div>
                  <div className="form-group">
                    <label>Tipo de Interés</label>
                    <div className="radio-group">
                      <label className="radio-option">
                        <input type="radio" name="tipoInteres" checked={prestamoForm.tipoInteres === 'Simple'} onChange={() => setPrestamoForm({ ...prestamoForm, tipoInteres: 'Simple' })} /> Simple
                      </label>
                      <label className="radio-option">
                        <input type="radio" name="tipoInteres" checked={prestamoForm.tipoInteres === 'Compuesto'} onChange={() => setPrestamoForm({ ...prestamoForm, tipoInteres: 'Compuesto' })} /> Compuesto
                      </label>
                    </div>
                  </div>
                  <div className="form-group">
                    <label>Frecuencia de Pago <span>*</span></label>
                    <select value={prestamoForm.frecuenciaPago} onChange={e => setPrestamoForm({ ...prestamoForm, frecuenciaPago: e.target.value })}>
                      <option>Diario</option>
                      <option>Semanal</option>
                      <option>Quincenal</option>
                      <option>Mensual</option>
                    </select>
                  </div>
                  <div className="form-group">
                    <label>Duración <span>*</span></label>
                    <div style={{ display: 'flex', gap: '0.5rem' }}>
                      <input type="number" min="1" required value={prestamoForm.duracion} onChange={e => setPrestamoForm({ ...prestamoForm, duracion: Number(e.target.value) })} style={{ width: '80px' }} />
                      <select value={prestamoForm.unidadDuracion} onChange={e => setPrestamoForm({ ...prestamoForm, unidadDuracion: e.target.value })}>
                        <option>Dias</option>
                        <option>Semanas</option>
                        <option>Quincenas</option>
                        <option>Meses</option>
                      </select>
                    </div>
                  </div>
                  <div className="form-group">
                    <label>Fecha del Préstamo <span>*</span></label>
                    <input type="date" required value={prestamoForm.fechaPrestamo} onChange={e => setPrestamoForm({ ...prestamoForm, fechaPrestamo: e.target.value })} />
                  </div>
                  <div className="form-group full-width">
                    <label>Descripción</label>
                    <textarea rows={2} value={prestamoForm.descripcion || ''} onChange={e => setPrestamoForm({ ...prestamoForm, descripcion: e.target.value })} />
                  </div>
                </div>

                {preview && (
                  <div className="preview-card">
                    <h4>Vista Previa del Préstamo</h4>
                    <div className="preview-grid">
                      <div className="preview-item">
                        <span>Monto Prestado</span>
                        <strong>{formatMoney(prestamoForm.montoPrestado)}</strong>
                      </div>
                      <div className="preview-item">
                        <span>Número de Cuotas</span>
                        <strong>{preview.numeroCuotas}</strong>
                      </div>
                      <div className="preview-item">
                        <span>Intereses</span>
                        <strong style={{ color: '#10b981' }}>{formatMoney(preview.montoIntereses)}</strong>
                      </div>
                      <div className="preview-item">
                        <span>Total a Pagar</span>
                        <strong>{formatMoney(preview.montoTotal)}</strong>
                      </div>
                      <div className="preview-item">
                        <span>Monto por Cuota</span>
                        <strong style={{ color: '#3b82f6' }}>{formatMoney(preview.montoCuota)}</strong>
                      </div>
                      <div className="preview-item">
                        <span>Fecha Vencimiento</span>
                        <strong>{formatDate(preview.fechaVencimiento)}</strong>
                      </div>
                    </div>
                  </div>
                )}
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setShowPrestamoModal(false)}>Cancelar</button>
                <button type="submit" className="btn btn-primary">Crear Préstamo</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Detalle Modal */}
      {showDetalleModal && selectedPrestamo && (
        <div className="modal-overlay" onClick={() => setShowDetalleModal(false)}>
          <div className="modal modal-lg" onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Préstamo #{selectedPrestamo.id}</h2>
              <button className="modal-close" onClick={() => setShowDetalleModal(false)}>&times;</button>
            </div>
            <div className="modal-body">
              <div className="detail-grid">
                <div className="detail-item">
                  <label>Cliente</label>
                  <span>{selectedPrestamo.clienteNombre}</span>
                </div>
                <div className="detail-item">
                  <label>Cédula</label>
                  <span>{selectedPrestamo.clienteCedula}</span>
                </div>
                <div className="detail-item">
                  <label>Monto Prestado</label>
                  <span>{formatMoney(selectedPrestamo.montoPrestado)}</span>
                </div>
                <div className="detail-item">
                  <label>Tasa de Interés</label>
                  <span>{selectedPrestamo.tasaInteres}% ({selectedPrestamo.tipoInteres})</span>
                </div>
                <div className="detail-item">
                  <label>Frecuencia</label>
                  <span>{selectedPrestamo.frecuenciaPago}</span>
                </div>
                <div className="detail-item">
                  <label>Total a Pagar</label>
                  <span>{formatMoney(selectedPrestamo.montoTotal)}</span>
                </div>
                <div className="detail-item">
                  <label>Pagado</label>
                  <span style={{ color: '#10b981' }}>{formatMoney(selectedPrestamo.totalPagado)}</span>
                </div>
                <div className="detail-item">
                  <label>Pendiente</label>
                  <span style={{ color: '#ef4444' }}>{formatMoney(selectedPrestamo.saldoPendiente)}</span>
                </div>
              </div>

              <div className="progress-bar" style={{ marginBottom: '1.5rem' }}>
                <div className="progress-fill" style={{ width: `${(selectedPrestamo.totalPagado / selectedPrestamo.montoTotal) * 100}%` }}></div>
              </div>

              <h4 style={{ marginBottom: '0.75rem' }}>Calendario de Cuotas</h4>
              <div className="table-container" style={{ maxHeight: '300px', overflow: 'auto' }}>
                <table>
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>Fecha Cobro</th>
                      <th>Monto</th>
                      <th>Pagado</th>
                      <th>Pendiente</th>
                      <th>Estado</th>
                      <th>Acciones</th>
                    </tr>
                  </thead>
                  <tbody>
                    {cuotasDetalle.map(c => (
                      <tr key={c.id}>
                        <td>{c.numeroCuota}</td>
                        <td>
                          <div className="date-input-cell">
                            <input type="date" value={c.fechaCobro.split('T')[0]} onChange={e => handleUpdateFechaCuota(c.id, e.target.value)} />
                            {c.fechaEditada && <span style={{ color: '#f59e0b', fontSize: '0.7rem' }}>editada</span>}
                          </div>
                        </td>
                        <td>{formatMoney(c.montoCuota)}</td>
                        <td style={{ color: '#10b981' }}>{formatMoney(c.montoPagado)}</td>
                        <td style={{ color: c.saldoPendiente > 0 ? '#ef4444' : '#10b981' }}>{formatMoney(c.saldoPendiente)}</td>
                        <td>
                          <span className={`badge ${c.estadoCuota === 'Pagada' ? 'badge-green' : c.estadoCuota === 'Vencida' ? 'badge-red' : c.estadoCuota === 'Parcial' ? 'badge-yellow' : 'badge-gray'}`}>
                            {c.estadoCuota}
                          </span>
                        </td>
                        <td>
                          {c.saldoPendiente > 0 && (
                            <button className="btn btn-primary btn-sm" onClick={() => openPagoModal(c)}>Pagar</button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {pagosDetalle.length > 0 && (
                <>
                  <h4 style={{ margin: '1.5rem 0 0.75rem' }}>Historial de Pagos</h4>
                  <div className="table-container">
                    <table>
                      <thead>
                        <tr>
                          <th>Fecha</th>
                          <th>Cuota</th>
                          <th>Monto</th>
                          <th>Método</th>
                          <th>Observaciones</th>
                        </tr>
                      </thead>
                      <tbody>
                        {pagosDetalle.map(p => (
                          <tr key={p.id}>
                            <td>{formatDate(p.fechaPago)}</td>
                            <td>{p.numeroCuota || '-'}</td>
                            <td style={{ color: '#10b981' }}>{formatMoney(p.montoPago)}</td>
                            <td>{p.metodoPago || '-'}</td>
                            <td>{p.observaciones || '-'}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Pago Modal */}
      {showPagoModal && selectedCuota && (
        <div className="modal-overlay" onClick={() => setShowPagoModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Registrar Pago - Cuota #{selectedCuota.numeroCuota}</h2>
              <button className="modal-close" onClick={() => setShowPagoModal(false)}>&times;</button>
            </div>
            <form onSubmit={handleCreatePago}>
              <div className="modal-body">
                <div className="form-grid">
                  <div className="form-group">
                    <label>Monto a Pagar <span>*</span></label>
                    <input type="number" min="0.01" step="0.01" required value={pagoForm.montoPago} onChange={e => setPagoForm({ ...pagoForm, montoPago: Number(e.target.value) })} />
                  </div>
                  <div className="form-group">
                    <label>Fecha de Pago <span>*</span></label>
                    <input type="date" required value={pagoForm.fechaPago} onChange={e => setPagoForm({ ...pagoForm, fechaPago: e.target.value })} />
                  </div>
                  <div className="form-group">
                    <label>Método de Pago</label>
                    <select value={pagoForm.metodoPago || ''} onChange={e => setPagoForm({ ...pagoForm, metodoPago: e.target.value })}>
                      <option>Efectivo</option>
                      <option>Transferencia</option>
                      <option>Tarjeta</option>
                      <option>Otro</option>
                    </select>
                  </div>
                  <div className="form-group full-width">
                    <label>Observaciones</label>
                    <textarea rows={2} value={pagoForm.observaciones || ''} onChange={e => setPagoForm({ ...pagoForm, observaciones: e.target.value })} />
                  </div>
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-secondary" onClick={() => setShowPagoModal(false)}>Cancelar</button>
                <button type="submit" className="btn btn-primary">Registrar Pago</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Toasts */}
      <div className="toast-container">
        {toasts.map(toast => (
          <div key={toast.id} className={`toast toast-${toast.type}`}>{toast.message}</div>
        ))}
      </div>
    </div>
  );
}

export default App;
