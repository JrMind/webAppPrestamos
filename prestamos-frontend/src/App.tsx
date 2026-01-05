import { useState, useEffect, useCallback, useRef } from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, Legend } from 'recharts';
import { clientesApi, prestamosApi, cuotasApi, pagosApi, dashboardApi, authApi, usuariosApi, cobrosApi, aportesApi, getAuthToken } from './api';
import { Cliente, CreateClienteDto, CreatePrestamoDto, CreatePagoDto, Cuota, DashboardMetricas, Pago, Prestamo, Usuario, Cobrador, CobrosHoy, BalanceSocio } from './types';
import './App.css';

const formatMoney = (amount: number): string => new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(amount);
const formatDate = (dateStr: string): string => new Date(dateStr).toLocaleDateString('es-CO');
const formatDateInput = (date: Date): string => date.toISOString().split('T')[0];

interface Toast { id: number; message: string; type: 'success' | 'error' | 'warning'; }

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(!!getAuthToken());
  const [currentUser, setCurrentUser] = useState<Usuario | null>(null);
  const [loading, setLoading] = useState(true);
  const [toasts, setToasts] = useState<Toast[]>([]);
  const [activeTab, setActiveTab] = useState<'prestamos' | 'clientes' | 'cuotas' | 'cobros' | 'socios' | 'usuarios'>('prestamos');

  // Data states
  const [metricas, setMetricas] = useState<DashboardMetricas | null>(null);
  const [prestamos, setPrestamos] = useState<Prestamo[]>([]);
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [cobradores, setCobradores] = useState<Cobrador[]>([]);
  const [cobrosHoy, setCobrosHoy] = useState<CobrosHoy | null>(null);
  const [balanceSocios, setBalanceSocios] = useState<BalanceSocio[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);

  // Filters
  const [filtroEstado, setFiltroEstado] = useState('Todos');
  const [filtroFrecuencia, setFiltroFrecuencia] = useState('Todos');
  const [filtroBusqueda, setFiltroBusqueda] = useState('');
  const [filtroClienteId] = useState<number | undefined>();

  // Modals
  const [showClienteModal, setShowClienteModal] = useState(false);
  const [showPrestamoModal, setShowPrestamoModal] = useState(false);
  const [showDetalleModal, setShowDetalleModal] = useState(false);
  const [showPagoModal, setShowPagoModal] = useState(false);
  const [showUsuarioModal, setShowUsuarioModal] = useState(false);
  const [showAporteModal, setShowAporteModal] = useState(false);
  const [selectedPrestamo, setSelectedPrestamo] = useState<Prestamo | null>(null);
  const [cuotasDetalle, setCuotasDetalle] = useState<Cuota[]>([]);
  const [pagosDetalle, setPagosDetalle] = useState<Pago[]>([]);
  const [selectedCuota, setSelectedCuota] = useState<Cuota | null>(null);

  // Client search state
  const [clienteSearch, setClienteSearch] = useState('');
  const [clienteSearchResults, setClienteSearchResults] = useState<Cliente[]>([]);
  const [selectedCliente, setSelectedCliente] = useState<Cliente | null>(null);
  const [showClienteDropdown, setShowClienteDropdown] = useState(false);
  const searchTimeoutRef = useRef<number | null>(null);

  // Login form
  const [loginForm, setLoginForm] = useState({ email: '', password: '' });
  const [loginError, setLoginError] = useState('');

  const showToast = (message: string, type: Toast['type']) => {
    const id = Date.now();
    setToasts(prev => [...prev, { id, message, type }]);
    setTimeout(() => setToasts(prev => prev.filter(t => t.id !== id)), 3000);
  };

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoginError('');
    try {
      const response = await authApi.login(loginForm);
      setCurrentUser(response.usuario);
      setIsAuthenticated(true);
      showToast('Bienvenido ' + response.usuario.nombre, 'success');
    } catch (error) {
      setLoginError('Email o contraseña incorrectos');
    }
  };

  const handleLogout = () => {
    authApi.logout();
    setIsAuthenticated(false);
    setCurrentUser(null);
  };

  const loadData = useCallback(async () => {
    if (!isAuthenticated) { setLoading(false); return; }
    try {
      const [metricasData, prestamosData, clientesData, cobradoresData] = await Promise.all([
        dashboardApi.getMetricas(),
        prestamosApi.getAll({ estado: filtroEstado !== 'Todos' ? filtroEstado : undefined, frecuencia: filtroFrecuencia !== 'Todos' ? filtroFrecuencia : undefined, busqueda: filtroBusqueda || undefined, clienteId: filtroClienteId }),
        clientesApi.getAll(),
        usuariosApi.getCobradores()
      ]);
      setMetricas(metricasData);
      setPrestamos(prestamosData);
      setClientes(clientesData);
      setCobradores(cobradoresData);
    } catch (error) {
      console.error('Error loading data:', error);
    } finally { setLoading(false); }
  }, [isAuthenticated, filtroEstado, filtroFrecuencia, filtroBusqueda, filtroClienteId]);

  const loadCobros = async () => {
    try {
      const data = await cobrosApi.getCobrosHoy();
      setCobrosHoy(data);
    } catch (error) { console.error('Error loading cobros:', error); }
  };

  const loadBalanceSocios = async () => {
    try {
      const data = await aportesApi.getBalance();
      setBalanceSocios(data);
    } catch (error) { console.error('Error loading balance:', error); }
  };

  const loadUsuarios = async () => {
    try {
      const data = await usuariosApi.getAll();
      setUsuarios(data);
    } catch (error) { console.error('Error loading usuarios:', error); }
  };

  const loadClientes = async () => {
    try {
      const data = await clientesApi.getAll();
      setClientes(data);
    } catch (error) { console.error('Error loading clientes:', error); }
  };

  useEffect(() => { loadData(); }, [loadData]);
  useEffect(() => { if (activeTab === 'cobros') loadCobros(); }, [activeTab]);
  useEffect(() => { if (activeTab === 'socios') loadBalanceSocios(); }, [activeTab]);
  useEffect(() => { if (activeTab === 'usuarios') loadUsuarios(); }, [activeTab]);
  useEffect(() => { if (activeTab === 'clientes') loadClientes(); }, [activeTab]);

  // Client search handler with debounce
  const handleClienteSearch = (value: string) => {
    setClienteSearch(value);
    setShowClienteDropdown(true);

    if (searchTimeoutRef.current) {
      clearTimeout(searchTimeoutRef.current);
    }

    if (value.length >= 2) {
      searchTimeoutRef.current = setTimeout(async () => {
        try {
          const results = await clientesApi.buscar(value);
          setClienteSearchResults(results);
        } catch (error) {
          console.error('Error searching clients:', error);
          setClienteSearchResults([]);
        }
      }, 300);
    } else {
      setClienteSearchResults([]);
    }
  };

  const selectCliente = (cliente: Cliente) => {
    setSelectedCliente(cliente);
    setClienteSearch(`${cliente.nombre} - ${cliente.cedula}`);
    setPrestamoForm({ ...prestamoForm, clienteId: cliente.id });
    setShowClienteDropdown(false);
    setClienteSearchResults([]);
  };

  const clearClienteSelection = () => {
    setSelectedCliente(null);
    setClienteSearch('');
    setPrestamoForm({ ...prestamoForm, clienteId: 0 });
    setClienteSearchResults([]);
  };

  // Forms
  const [clienteForm, setClienteForm] = useState<CreateClienteDto>({ nombre: '', cedula: '', telefono: '', direccion: '', email: '' });
  const [prestamoForm, setPrestamoForm] = useState<CreatePrestamoDto>({
    clienteId: 0, montoPrestado: 0, tasaInteres: 15, tipoInteres: 'Simple',
    frecuenciaPago: 'Quincenal', duracion: 3, unidadDuracion: 'Meses',
    fechaPrestamo: formatDateInput(new Date()), descripcion: '',
    cobradorId: undefined, porcentajeCobrador: 5
  });
  const [pagoForm, setPagoForm] = useState<CreatePagoDto>({
    prestamoId: 0, cuotaId: undefined, montoPago: 0,
    fechaPago: formatDateInput(new Date()), metodoPago: 'Efectivo', observaciones: ''
  });
  const [usuarioForm, setUsuarioForm] = useState({ nombre: '', email: '', password: '', telefono: '', rol: 'Socio', porcentajeParticipacion: 0, tasaInteresMensual: 3 });
  const [aporteForm, setAporteForm] = useState({ usuarioId: 0, monto: 0, descripcion: '', tipo: 'aporte' });

  const handleCreateCliente = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await clientesApi.create(clienteForm);
      showToast('Cliente creado exitosamente', 'success');
      setShowClienteModal(false);
      setClienteForm({ nombre: '', cedula: '', telefono: '', direccion: '', email: '' });
      loadData();
    } catch (error: unknown) { showToast(error instanceof Error ? error.message : 'Error', 'error'); }
  };

  const calcularPreview = () => {
    if (!prestamoForm.montoPrestado || prestamoForm.montoPrestado < 50) return null;
    const diasMap: Record<string, number> = { Dias: 1, Semanas: 7, Quincenas: 15, Meses: 30 };
    const diasTotales = prestamoForm.duracion * (diasMap[prestamoForm.unidadDuracion] || 30);
    const frecuenciaDias: Record<string, number> = { Diario: 1, Semanal: 7, Quincenal: 15, Mensual: 30 };
    const diasEntreCuotas = frecuenciaDias[prestamoForm.frecuenciaPago] || 15;
    let numeroCuotas = Math.ceil(diasTotales / diasEntreCuotas);
    let montoIntereses: number, montoTotal: number;
    if (prestamoForm.tipoInteres === 'Simple') {
      // Convertir días a meses (tasa es mensual)
      const meses = diasTotales / 30;
      // Interés Simple: I = P * (r/100) * meses
      montoIntereses = prestamoForm.montoPrestado * (prestamoForm.tasaInteres / 100) * meses;
      montoTotal = prestamoForm.montoPrestado + montoIntereses;
    } else {
      const tasaPorPeriodo = (prestamoForm.tasaInteres / 100) / (365 / diasEntreCuotas);
      montoTotal = prestamoForm.montoPrestado * Math.pow(1 + tasaPorPeriodo, numeroCuotas);
      montoIntereses = montoTotal - prestamoForm.montoPrestado;
    }
    const montoCuota = montoTotal / numeroCuotas;
    const fechaVencimiento = new Date(prestamoForm.fechaPrestamo);
    fechaVencimiento.setDate(fechaVencimiento.getDate() + diasTotales);
    return { numeroCuotas, montoIntereses, montoTotal, montoCuota, fechaVencimiento: fechaVencimiento.toISOString() };
  };
  const preview = calcularPreview();

  const handleCreatePrestamo = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!prestamoForm.clienteId) { showToast('Seleccione un cliente', 'warning'); return; }
    try {
      await prestamosApi.create(prestamoForm);
      showToast('Préstamo creado exitosamente', 'success');
      setShowPrestamoModal(false);
      setPrestamoForm({ clienteId: 0, montoPrestado: 0, tasaInteres: 15, tipoInteres: 'Simple', frecuenciaPago: 'Quincenal', duracion: 3, unidadDuracion: 'Meses', fechaPrestamo: formatDateInput(new Date()), descripcion: '', cobradorId: undefined, porcentajeCobrador: 5 });
      loadData();
    } catch (error: unknown) { showToast(error instanceof Error ? error.message : 'Error', 'error'); }
  };

  const openDetalle = async (prestamo: Prestamo) => {
    setSelectedPrestamo(prestamo);
    try {
      const [cuotas, pagos] = await Promise.all([cuotasApi.getByPrestamo(prestamo.id), pagosApi.getByPrestamo(prestamo.id)]);
      setCuotasDetalle(cuotas);
      setPagosDetalle(pagos);
      setShowDetalleModal(true);
    } catch { showToast('Error al cargar detalles', 'error'); }
  };

  const openPagoModal = (cuota: Cuota) => {
    setSelectedCuota(cuota);
    setPagoForm({ prestamoId: cuota.prestamoId, cuotaId: cuota.id, montoPago: cuota.saldoPendiente, fechaPago: formatDateInput(new Date()), metodoPago: 'Efectivo', observaciones: '' });
    setShowPagoModal(true);
  };

  const handleCreatePago = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await pagosApi.create(pagoForm);
      showToast('Pago registrado', 'success');
      setShowPagoModal(false);
      if (selectedPrestamo) {
        const [cuotas, pagos] = await Promise.all([cuotasApi.getByPrestamo(selectedPrestamo.id), pagosApi.getByPrestamo(selectedPrestamo.id)]);
        setCuotasDetalle(cuotas);
        setPagosDetalle(pagos);
      }
      loadData();
    } catch (error: unknown) { showToast(error instanceof Error ? error.message : 'Error', 'error'); }
  };

  const handleMarcarCobrado = async (cuotaId: number, cobrado: boolean) => {
    try {
      await cobrosApi.marcarCobrado(cuotaId, cobrado);
      showToast(cobrado ? 'Cuota marcada como cobrada' : 'Marca removida', 'success');
      loadCobros();
    } catch { showToast('Error al marcar cuota', 'error'); }
  };

  const handleCreateUsuario = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await usuariosApi.create(usuarioForm);
      showToast('Usuario creado', 'success');
      setShowUsuarioModal(false);
      setUsuarioForm({ nombre: '', email: '', password: '', telefono: '', rol: 'Socio', porcentajeParticipacion: 0, tasaInteresMensual: 3 });
      loadUsuarios();
      loadData();
    } catch (error: unknown) { showToast(error instanceof Error ? error.message : 'Error', 'error'); }
  };

  const handleAporteRetiro = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (aporteForm.tipo === 'aporte') {
        await aportesApi.registrarAporte({ usuarioId: aporteForm.usuarioId, monto: aporteForm.monto, descripcion: aporteForm.descripcion });
      } else {
        await aportesApi.registrarRetiro({ usuarioId: aporteForm.usuarioId, monto: aporteForm.monto, descripcion: aporteForm.descripcion });
      }
      showToast(aporteForm.tipo === 'aporte' ? 'Aporte registrado' : 'Retiro registrado', 'success');
      setShowAporteModal(false);
      loadBalanceSocios();
    } catch (error: unknown) { showToast(error instanceof Error ? error.message : 'Error', 'error'); }
  };

  const handleDeletePrestamo = async (id: number) => {
    if (!confirm('¿Eliminar este préstamo?')) return;
    try { await prestamosApi.delete(id); showToast('Préstamo eliminado', 'success'); loadData(); }
    catch (error: unknown) { showToast(error instanceof Error ? error.message : 'Error', 'error'); }
  };

  const COLORS = ['#10b981', '#3b82f6', '#ef4444'];

  // Login Screen
  if (!isAuthenticated) {
    return (
      <div className="login-container">
        <div className="login-card">
          <h1>🏦 PrestamosApp</h1>
          <p>Sistema de Gestión de Préstamos</p>
          <form onSubmit={handleLogin}>
            <div className="form-group">
              <label>Email</label>
              <input type="email" required value={loginForm.email} onChange={e => setLoginForm({ ...loginForm, email: e.target.value })} placeholder="correo@ejemplo.com" />
            </div>
            <div className="form-group">
              <label>Contraseña</label>
              <input type="password" required value={loginForm.password} onChange={e => setLoginForm({ ...loginForm, password: e.target.value })} placeholder="••••••••" />
            </div>
            {loginError && <div className="error-message">{loginError}</div>}
            <button type="submit" className="btn btn-primary btn-full">Iniciar Sesión</button>
          </form>
        </div>
      </div>
    );
  }

  if (loading) return <div className="dashboard"><div className="loading"><div className="spinner"></div></div></div>;

  return (
    <div className="dashboard">
      {/* Toasts */}
      <div className="toasts">{toasts.map(t => <div key={t.id} className={`toast toast-${t.type}`}>{t.message}</div>)}</div>

      {/* Header */}
      <header className="header">
        <div className="header-left">
          <h1>Sistema de Gestión de Préstamos</h1>
          <p>Bienvenido, {currentUser?.nombre} ({currentUser?.rol})</p>
        </div>
        <div className="header-right">
          <div className="header-stat"><span>Activos</span><strong>{metricas?.prestamosActivos || 0}</strong></div>
          <button className="btn btn-secondary" onClick={() => setShowClienteModal(true)}>+ Cliente</button>
          <button className="btn btn-primary" onClick={() => setShowPrestamoModal(true)}>+ Préstamo</button>
          <button className="btn btn-danger" onClick={handleLogout}>Salir</button>
        </div>
      </header>

      <main className="main-content">
        {/* Filters */}
        <div className="filters-bar">
          <div className="filter-group"><label>Estado</label><select value={filtroEstado} onChange={e => setFiltroEstado(e.target.value)}><option>Todos</option><option>Activo</option><option>Pagado</option><option>Vencido</option></select></div>
          <div className="filter-group"><label>Frecuencia</label><select value={filtroFrecuencia} onChange={e => setFiltroFrecuencia(e.target.value)}><option>Todos</option><option>Diario</option><option>Semanal</option><option>Quincenal</option><option>Mensual</option></select></div>
          <div className="filter-group" style={{ flex: 1 }}><label>Buscar</label><input type="text" placeholder="Nombre o cédula..." value={filtroBusqueda} onChange={e => setFiltroBusqueda(e.target.value)} /></div>
          <button className="btn btn-secondary btn-sm" onClick={() => { setFiltroEstado('Todos'); setFiltroFrecuencia('Todos'); setFiltroBusqueda(''); }}>Limpiar</button>
        </div>

        {/* KPIs */}
        <div className="kpi-grid">
          <div className="kpi-card"><div className="kpi-header"><span className="kpi-title">Total Prestado</span></div><span className="kpi-value">{formatMoney(metricas?.totalPrestado || 0)}</span></div>
          <div className="kpi-card"><div className="kpi-header"><span className="kpi-title">Total Cobrado</span></div><span className="kpi-value">{formatMoney(metricas?.totalCobrado || 0)}</span></div>
          <div className="kpi-card"><div className="kpi-header"><span className="kpi-title">Intereses Ganados</span></div><span className="kpi-value">{formatMoney(metricas?.totalGanadoIntereses || 0)}</span></div>
          <div className="kpi-card"><div className="kpi-header"><span className="kpi-title">Cuotas Vencidas</span></div><span className="kpi-value">{metricas?.cuotasVencidasHoy || 0}</span></div>
        </div>

        {/* Flujo de Capital */}
        <div className="kpi-grid" style={{ marginTop: '1rem' }}>
          <div className="kpi-card" style={{ borderLeft: '4px solid #f59e0b', background: 'linear-gradient(135deg, rgba(245,158,11,0.1) 0%, transparent 100%)' }}>
            <div className="kpi-header"><span className="kpi-title">💰 Dinero Circulando</span></div>
            <span className="kpi-value" style={{ color: '#f59e0b' }}>{formatMoney(metricas?.dineroCirculando || 0)}</span>
            <span className="kpi-sub" style={{ marginTop: '0.5rem', color: '#999' }}>Capital prestado aún no recuperado</span>
          </div>
          <div className="kpi-card" style={{ borderLeft: '4px solid #10b981', background: 'linear-gradient(135deg, rgba(16,185,129,0.1) 0%, transparent 100%)' }}>
            <div className="kpi-header"><span className="kpi-title">🏦 Reserva Disponible</span></div>
            <span className="kpi-value" style={{ color: '#10b981' }}>{formatMoney(metricas?.reservaDisponible || 0)}</span>
            <span className="kpi-sub" style={{ marginTop: '0.5rem', color: '#999' }}>Dinero listo para prestar</span>
          </div>
          <div className="kpi-card" style={{ borderLeft: '4px solid #3b82f6' }}>
            <div className="kpi-header"><span className="kpi-title">📊 Total a Cobrar</span></div>
            <span className="kpi-value">{formatMoney(metricas?.totalACobrar || 0)}</span>
            <span className="kpi-sub" style={{ marginTop: '0.5rem', color: '#999' }}>Préstamos activos pendientes</span>
          </div>
        </div>

        {/* Charts */}
        <div className="charts-grid">
          <div className="chart-container">
            <h3 className="chart-title">Evolución</h3>
            <ResponsiveContainer width="100%" height={200}>
              <LineChart data={metricas?.evolucionPrestamos || []}>
                <CartesianGrid strokeDasharray="3 3" stroke="#2a2a2a" /><XAxis dataKey="fecha" tickFormatter={v => new Date(v).toLocaleDateString('es-CO', { month: 'short' })} stroke="#666" /><YAxis stroke="#666" tickFormatter={v => `${(v / 1000000).toFixed(0)}M`} />
                <Tooltip formatter={v => formatMoney(Number(v || 0))} contentStyle={{ background: '#1a1a1a', border: '1px solid #2a2a2a' }} />
                <Line type="monotone" dataKey="montoPrestadoAcumulado" stroke="#3b82f6" strokeWidth={2} /><Line type="monotone" dataKey="montoCobradoAcumulado" stroke="#10b981" strokeWidth={2} />
              </LineChart>
            </ResponsiveContainer>
          </div>
          <div className="chart-container">
            <h3 className="chart-title">Estados</h3>
            <ResponsiveContainer width="100%" height={200}>
              <PieChart><Pie data={[{ name: 'Activos', value: metricas?.distribucionEstados?.activos || 0 }, { name: 'Pagados', value: metricas?.distribucionEstados?.pagados || 0 }, { name: 'Vencidos', value: metricas?.distribucionEstados?.vencidos || 0 }]} cx="50%" cy="50%" outerRadius={60} dataKey="value" label>{COLORS.map((c, i) => <Cell key={i} fill={c} />)}</Pie><Legend /><Tooltip contentStyle={{ background: '#1a1a1a', border: '1px solid #2a2a2a' }} /></PieChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Tabs */}
        <div className="section">
          <div className="tabs">
            <button className={`tab ${activeTab === 'prestamos' ? 'active' : ''}`} onClick={() => setActiveTab('prestamos')}>Préstamos</button>
            <button className={`tab ${activeTab === 'clientes' ? 'active' : ''}`} onClick={() => setActiveTab('clientes')}>Clientes</button>
            <button className={`tab ${activeTab === 'cobros' ? 'active' : ''}`} onClick={() => setActiveTab('cobros')}>Cobros del Día</button>
            <button className={`tab ${activeTab === 'socios' ? 'active' : ''}`} onClick={() => setActiveTab('socios')}>Socios/Aportadores</button>
            <button className={`tab ${activeTab === 'usuarios' ? 'active' : ''}`} onClick={() => setActiveTab('usuarios')}>Usuarios</button>
          </div>

          {/* Prestamos Tab */}
          {activeTab === 'prestamos' && (
            <div className="table-container">
              <table><thead><tr><th>ID</th><th>Cliente</th><th>Monto</th><th>Interés</th><th>Cobrador</th><th>Cuotas</th><th>Estado</th><th>Acciones</th></tr></thead>
                <tbody>{prestamos.map(p => (
                  <tr key={p.id}>
                    <td>#{p.id}</td>
                    <td><strong>{p.clienteNombre}</strong><div style={{ color: '#666', fontSize: '0.75rem' }}>{p.clienteCedula}</div></td>
                    <td className="money">{formatMoney(p.montoPrestado)}</td>
                    <td>{p.tasaInteres}%</td>
                    <td>{p.cobradorNombre || '-'}</td>
                    <td>{p.cuotasPagadas}/{p.numeroCuotas}</td>
                    <td><span className={`badge ${p.estadoPrestamo === 'Activo' ? 'badge-green' : p.estadoPrestamo === 'Pagado' ? 'badge-blue' : 'badge-red'}`}>{p.estadoPrestamo}</span></td>
                    <td><div className="actions"><button className="btn btn-secondary btn-sm" onClick={() => openDetalle(p)}>Ver</button><button className="btn btn-danger btn-sm" onClick={() => handleDeletePrestamo(p.id)}>✕</button></div></td>
                  </tr>
                ))}{prestamos.length === 0 && <tr><td colSpan={8} className="empty-state">No hay préstamos</td></tr>}</tbody>
              </table>
            </div>
          )}

          {/* Clientes Tab */}
          {activeTab === 'clientes' && (
            <div className="table-container">
              <table>
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Nombre</th>
                    <th>Cédula</th>
                    <th>Teléfono</th>
                    <th>Préstamos</th>
                    <th>Total</th>
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
                      <td>{c.prestamosActivos}</td>
                      <td className="money">{formatMoney(c.totalPrestado)}</td>
                      <td>
                        <span className={`badge ${c.estado === 'Activo' ? 'badge-green' : 'badge-gray'}`}>
                          {c.estado}
                        </span>
                      </td>
                    </tr>
                  ))}
                  {clientes.length === 0 && (
                    <tr>
                      <td colSpan={7} className="empty-state">No hay clientes</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          )}

          {/* Cobros Tab */}
          {activeTab === 'cobros' && cobrosHoy && (
            <div>
              <div className="kpi-grid" style={{ marginBottom: '1rem' }}>
                <div className="kpi-card"><span className="kpi-title">Por Cobrar Hoy</span><span className="kpi-value">{formatMoney(cobrosHoy.resumen.montoTotalHoy)}</span></div>
                <div className="kpi-card" style={{ borderColor: '#ef4444' }}><span className="kpi-title">Vencido</span><span className="kpi-value" style={{ color: '#ef4444' }}>{formatMoney(cobrosHoy.resumen.montoTotalVencido)}</span></div>
                <div className="kpi-card"><span className="kpi-title">Total Pendiente</span><span className="kpi-value">{formatMoney(cobrosHoy.resumen.montoPendienteTotal)}</span></div>
              </div>
              {cobrosHoy.cuotasVencidas.length > 0 && (
                <><h4 style={{ color: '#ef4444', margin: '1rem 0 0.5rem' }}>⚠️ Cuotas Vencidas ({cobrosHoy.cuotasVencidas.length})</h4>
                  <div className="table-container">
                    <table><thead><tr><th>✓</th><th>Cliente</th><th>Fecha</th><th>Monto</th><th>Cobrador</th></tr></thead>
                      <tbody>{cobrosHoy.cuotasVencidas.map(c => (
                        <tr key={c.id} style={{ background: 'rgba(239,68,68,0.1)' }}>
                          <td><input type="checkbox" checked={c.cobrado} onChange={e => handleMarcarCobrado(c.id, e.target.checked)} /></td>
                          <td><strong>{c.clienteNombre}</strong><div style={{ fontSize: '0.75rem' }}>{c.clienteTelefono}</div></td>
                          <td style={{ color: '#ef4444' }}>{formatDate(c.fechaCobro)}</td>
                          <td className="money">{formatMoney(c.saldoPendiente)}</td>
                          <td>{c.cobradorNombre || '-'}</td>
                        </tr>
                      ))}</tbody>
                    </table>
                  </div></>
              )}
              <h4 style={{ margin: '1rem 0 0.5rem' }}>📅 Cuotas del Día ({cobrosHoy.cuotasHoy.length})</h4>
              <div className="table-container">
                <table><thead><tr><th>✓</th><th>Cliente</th><th>Cuota</th><th>Monto</th><th>Cobrador</th></tr></thead>
                  <tbody>{cobrosHoy.cuotasHoy.map(c => (
                    <tr key={c.id} style={{ opacity: c.cobrado ? 0.6 : 1 }}>
                      <td><input type="checkbox" checked={c.cobrado} onChange={e => handleMarcarCobrado(c.id, e.target.checked)} /></td>
                      <td><strong>{c.clienteNombre}</strong></td>
                      <td>#{c.numeroCuota}</td>
                      <td className="money">{formatMoney(c.saldoPendiente)}</td>
                      <td>{c.cobradorNombre || '-'}</td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
            </div>
          )}

          {/* Socios Tab */}
          {activeTab === 'socios' && (
            <div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '1rem' }}>
                <button className="btn btn-primary" onClick={() => { setShowAporteModal(true); setAporteForm({ ...aporteForm, tipo: 'aporte' }) }}>+ Aporte/Retiro</button>
              </div>
              <div className="table-container">
                <table><thead><tr><th>Nombre</th><th>Rol</th><th>% Participación</th><th>Tasa Mensual</th><th>Capital Inicial</th><th>Capital Actual</th><th>Ganancias</th></tr></thead>
                  <tbody>{balanceSocios.map(s => (
                    <tr key={s.id}>
                      <td><strong>{s.nombre}</strong><div style={{ fontSize: '0.75rem', color: '#666' }}>{s.email}</div></td>
                      <td><span className="badge badge-blue">{s.rol}</span></td>
                      <td>{s.porcentajeParticipacion}%</td>
                      <td>{s.tasaInteresMensual}%</td>
                      <td className="money">{formatMoney(s.capitalInicial)}</td>
                      <td className="money" style={{ color: '#10b981' }}>{formatMoney(s.capitalActual)}</td>
                      <td className="money">{formatMoney(s.gananciasAcumuladas + s.gananciasPendientes)}</td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
            </div>
          )}

          {/* Usuarios Tab */}
          {activeTab === 'usuarios' && (
            <div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '1rem' }}>
                <button className="btn btn-primary" onClick={() => setShowUsuarioModal(true)}>+ Nuevo Usuario</button>
              </div>
              <div className="table-container">
                <table><thead><tr><th>Nombre</th><th>Email</th><th>Teléfono</th><th>Rol</th><th>% Participación</th><th>Estado</th></tr></thead>
                  <tbody>{usuarios.map(u => (
                    <tr key={u.id}>
                      <td><strong>{u.nombre}</strong></td>
                      <td>{u.email}</td>
                      <td>{u.telefono || '-'}</td>
                      <td><span className="badge badge-blue">{u.rol}</span></td>
                      <td>{u.porcentajeParticipacion}%</td>
                      <td><span className={`badge ${u.activo ? 'badge-green' : 'badge-gray'}`}>{u.activo ? 'Activo' : 'Inactivo'}</span></td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      </main>

      {/* Modals */}
      {showClienteModal && (
        <div className="modal-overlay" onClick={() => setShowClienteModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <div className="modal-header"><h2>Nuevo Cliente</h2><button className="modal-close" onClick={() => setShowClienteModal(false)}>×</button></div>
            <form onSubmit={handleCreateCliente}>
              <div className="modal-body">
                <div className="form-grid">
                  <div className="form-group"><label>Nombre *</label><input type="text" required value={clienteForm.nombre} onChange={e => setClienteForm({ ...clienteForm, nombre: e.target.value })} /></div>
                  <div className="form-group"><label>Cédula *</label><input type="text" required value={clienteForm.cedula} onChange={e => setClienteForm({ ...clienteForm, cedula: e.target.value })} /></div>
                  <div className="form-group"><label>Teléfono</label><input type="tel" value={clienteForm.telefono || ''} onChange={e => setClienteForm({ ...clienteForm, telefono: e.target.value })} /></div>
                  <div className="form-group"><label>Email</label><input type="email" value={clienteForm.email || ''} onChange={e => setClienteForm({ ...clienteForm, email: e.target.value })} /></div>
                </div>
              </div>
              <div className="modal-footer"><button type="button" className="btn btn-secondary" onClick={() => setShowClienteModal(false)}>Cancelar</button><button type="submit" className="btn btn-primary">Guardar</button></div>
            </form>
          </div>
        </div>
      )}

      {showPrestamoModal && (
        <div className="modal-overlay" onClick={() => setShowPrestamoModal(false)}>
          <div className="modal modal-lg" onClick={e => e.stopPropagation()}>
            <div className="modal-header"><h2>Nuevo Préstamo</h2><button className="modal-close" onClick={() => setShowPrestamoModal(false)}>×</button></div>
            <form onSubmit={handleCreatePrestamo}>
              <div className="modal-body">
                <div className="form-grid">
                  <div className="form-group full-width" style={{ position: 'relative' }}>
                    <label>Cliente *</label>
                    <div style={{ position: 'relative' }}>
                      <input
                        type="text"
                        placeholder="Buscar cliente por nombre o cédula..."
                        value={clienteSearch}
                        onChange={e => handleClienteSearch(e.target.value)}
                        onFocus={() => clienteSearchResults.length > 0 && setShowClienteDropdown(true)}
                        required={!selectedCliente}
                        style={{ paddingRight: selectedCliente ? '40px' : undefined }}
                      />
                      {selectedCliente && (
                        <button
                          type="button"
                          onClick={clearClienteSelection}
                          style={{ position: 'absolute', right: '10px', top: '50%', transform: 'translateY(-50%)', background: 'none', border: 'none', cursor: 'pointer', fontSize: '1.2rem', color: '#888' }}
                        >×</button>
                      )}
                      {showClienteDropdown && clienteSearchResults.length > 0 && (
                        <div style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '8px', maxHeight: '200px', overflow: 'auto', zIndex: 1000, boxShadow: '0 4px 12px rgba(0,0,0,0.15)' }}>
                          {clienteSearchResults.map(c => (
                            <div
                              key={c.id}
                              onClick={() => selectCliente(c)}
                              style={{ padding: '0.75rem 1rem', cursor: 'pointer', borderBottom: '1px solid var(--border)' }}
                              onMouseEnter={e => (e.currentTarget.style.background = 'var(--primary)', e.currentTarget.style.color = 'white')}
                              onMouseLeave={e => (e.currentTarget.style.background = 'transparent', e.currentTarget.style.color = 'inherit')}
                            >
                              <strong>{c.nombre}</strong> - {c.cedula}
                              {c.telefono && <span style={{ marginLeft: '0.5rem', opacity: 0.7 }}>({c.telefono})</span>}
                            </div>
                          ))}
                        </div>
                      )}
                      {clienteSearch.length >= 2 && clienteSearchResults.length === 0 && showClienteDropdown && (
                        <div style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '8px', padding: '0.75rem 1rem', color: '#888' }}>
                          No se encontraron clientes
                        </div>
                      )}
                    </div>
                    <input type="hidden" value={prestamoForm.clienteId || ''} required />
                  </div>
                  <div className="form-group"><label>Monto ($) *</label><input type="number" min="50" required value={prestamoForm.montoPrestado || ''} onChange={e => setPrestamoForm({ ...prestamoForm, montoPrestado: Number(e.target.value) })} /></div>
                  <div className="form-group"><label>Tasa Interés (%) *</label><input type="number" min="0" step="0.1" required value={prestamoForm.tasaInteres} onChange={e => setPrestamoForm({ ...prestamoForm, tasaInteres: Number(e.target.value) })} /></div>
                  <div className="form-group"><label>Frecuencia *</label><select value={prestamoForm.frecuenciaPago} onChange={e => setPrestamoForm({ ...prestamoForm, frecuenciaPago: e.target.value })}><option>Diario</option><option>Semanal</option><option>Quincenal</option><option>Mensual</option></select></div>
                  <div className="form-group"><label>Duración *</label><div style={{ display: 'flex', gap: '0.5rem' }}><input type="number" min="1" required value={prestamoForm.duracion} onChange={e => setPrestamoForm({ ...prestamoForm, duracion: Number(e.target.value) })} style={{ width: '80px' }} /><select value={prestamoForm.unidadDuracion} onChange={e => setPrestamoForm({ ...prestamoForm, unidadDuracion: e.target.value })}><option>Dias</option><option>Semanas</option><option>Quincenas</option><option>Meses</option></select></div></div>
                  <div className="form-group"><label>Cobrador (Referido)</label><select value={prestamoForm.cobradorId || ''} onChange={e => setPrestamoForm({ ...prestamoForm, cobradorId: e.target.value ? Number(e.target.value) : undefined })}><option value="">Sin asignar</option>{cobradores.map(c => <option key={c.id} value={c.id}>{c.nombre}</option>)}</select></div>
                  {prestamoForm.tasaInteres >= 15 && <div className="form-group"><label>% Cobrador</label><input type="number" min="0" max="15" step="0.5" value={prestamoForm.porcentajeCobrador} onChange={e => setPrestamoForm({ ...prestamoForm, porcentajeCobrador: Number(e.target.value) })} /></div>}
                  <div className="form-group"><label>Fecha *</label><input type="date" required value={prestamoForm.fechaPrestamo} onChange={e => setPrestamoForm({ ...prestamoForm, fechaPrestamo: e.target.value })} /></div>
                </div>
                {preview && <div className="preview-card"><h4>Vista Previa</h4><div className="preview-grid"><div className="preview-item"><span>Cuotas</span><strong>{preview.numeroCuotas}</strong></div><div className="preview-item"><span>Intereses</span><strong style={{ color: '#10b981' }}>{formatMoney(preview.montoIntereses)}</strong></div><div className="preview-item"><span>Total</span><strong>{formatMoney(preview.montoTotal)}</strong></div><div className="preview-item"><span>Por Cuota</span><strong style={{ color: '#3b82f6' }}>{formatMoney(preview.montoCuota)}</strong></div></div></div>}
              </div>
              <div className="modal-footer"><button type="button" className="btn btn-secondary" onClick={() => setShowPrestamoModal(false)}>Cancelar</button><button type="submit" className="btn btn-primary">Crear</button></div>
            </form>
          </div>
        </div>
      )}

      {showDetalleModal && selectedPrestamo && (
        <div className="modal-overlay" onClick={() => setShowDetalleModal(false)}>
          <div className="modal modal-lg" onClick={e => e.stopPropagation()}>
            <div className="modal-header"><h2>Préstamo #{selectedPrestamo.id}</h2><button className="modal-close" onClick={() => setShowDetalleModal(false)}>×</button></div>
            <div className="modal-body">
              <div className="detail-grid">
                <div className="detail-item"><label>Cliente</label><span>{selectedPrestamo.clienteNombre}</span></div>
                <div className="detail-item"><label>Monto</label><span>{formatMoney(selectedPrestamo.montoPrestado)}</span></div>
                <div className="detail-item"><label>Total</label><span>{formatMoney(selectedPrestamo.montoTotal)}</span></div>
                <div className="detail-item"><label>Pagado</label><span style={{ color: '#10b981' }}>{formatMoney(selectedPrestamo.totalPagado)}</span></div>
                <div className="detail-item"><label>Pendiente</label><span style={{ color: '#ef4444' }}>{formatMoney(selectedPrestamo.saldoPendiente)}</span></div>
                <div className="detail-item"><label>Cobrador</label><span>{selectedPrestamo.cobradorNombre || 'No asignado'}</span></div>
              </div>
              <div className="progress-bar" style={{ margin: '1rem 0' }}><div className="progress-fill" style={{ width: `${(selectedPrestamo.totalPagado / selectedPrestamo.montoTotal) * 100}%` }}></div></div>
              <h4>Cuotas</h4>
              <div className="table-container" style={{ maxHeight: '200px', overflow: 'auto' }}>
                <table><thead><tr><th>#</th><th>Fecha</th><th>Monto</th><th>Pagado</th><th>Estado</th><th></th></tr></thead>
                  <tbody>{cuotasDetalle.map(c => (
                    <tr key={c.id}><td>{c.numeroCuota}</td><td>{formatDate(c.fechaCobro)}</td><td>{formatMoney(c.montoCuota)}</td><td>{formatMoney(c.montoPagado)}</td><td><span className={`badge ${c.estadoCuota === 'Pagada' ? 'badge-green' : c.estadoCuota === 'Vencida' ? 'badge-red' : 'badge-gray'}`}>{c.estadoCuota}</span></td><td>{c.estadoCuota !== 'Pagada' && <button className="btn btn-primary btn-sm" onClick={() => openPagoModal(c)}>Pagar</button>}</td></tr>
                  ))}</tbody>
                </table>
              </div>
              {pagosDetalle.length > 0 && (
                <>
                  <h4 style={{ marginTop: '1rem' }}>Historial de Pagos</h4>
                  <div className="table-container" style={{ maxHeight: '150px', overflow: 'auto' }}>
                    <table>
                      <thead><tr><th>Fecha</th><th>Monto</th><th>Método</th></tr></thead>
                      <tbody>{pagosDetalle.map(p => (
                        <tr key={p.id}>
                          <td>{formatDate(p.fechaPago)}</td>
                          <td className="money" style={{ color: '#10b981' }}>{formatMoney(p.montoPago)}</td>
                          <td>{p.metodoPago || 'Efectivo'}</td>
                        </tr>
                      ))}</tbody>
                    </table>
                  </div>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {showPagoModal && selectedCuota && (
        <div className="modal-overlay" onClick={() => setShowPagoModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <div className="modal-header"><h2>Registrar Pago - Cuota #{selectedCuota.numeroCuota}</h2><button className="modal-close" onClick={() => setShowPagoModal(false)}>×</button></div>
            <form onSubmit={handleCreatePago}>
              <div className="modal-body">
                <div className="form-group"><label>Saldo Pendiente</label><div className="money" style={{ fontSize: '1.5rem', color: '#ef4444' }}>{formatMoney(selectedCuota.saldoPendiente)}</div></div>
                <div className="form-grid">
                  <div className="form-group"><label>Monto a Pagar *</label><input type="number" min="0.01" step="0.01" required value={pagoForm.montoPago} onChange={e => setPagoForm({ ...pagoForm, montoPago: Number(e.target.value) })} /></div>
                  <div className="form-group"><label>Fecha *</label><input type="date" required value={pagoForm.fechaPago} onChange={e => setPagoForm({ ...pagoForm, fechaPago: e.target.value })} /></div>
                  <div className="form-group"><label>Método</label><select value={pagoForm.metodoPago || ''} onChange={e => setPagoForm({ ...pagoForm, metodoPago: e.target.value })}><option>Efectivo</option><option>Transferencia</option><option>Nequi</option><option>Daviplata</option></select></div>
                </div>
              </div>
              <div className="modal-footer"><button type="button" className="btn btn-secondary" onClick={() => setShowPagoModal(false)}>Cancelar</button><button type="submit" className="btn btn-primary">Registrar</button></div>
            </form>
          </div>
        </div>
      )}

      {showUsuarioModal && (
        <div className="modal-overlay" onClick={() => setShowUsuarioModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <div className="modal-header"><h2>Nuevo Usuario</h2><button className="modal-close" onClick={() => setShowUsuarioModal(false)}>×</button></div>
            <form onSubmit={handleCreateUsuario}>
              <div className="modal-body">
                <div className="form-grid">
                  <div className="form-group"><label>Nombre *</label><input type="text" required value={usuarioForm.nombre} onChange={e => setUsuarioForm({ ...usuarioForm, nombre: e.target.value })} /></div>
                  <div className="form-group"><label>Email *</label><input type="email" required value={usuarioForm.email} onChange={e => setUsuarioForm({ ...usuarioForm, email: e.target.value })} /></div>
                  <div className="form-group"><label>Contraseña *</label><input type="password" required value={usuarioForm.password} onChange={e => setUsuarioForm({ ...usuarioForm, password: e.target.value })} /></div>
                  <div className="form-group"><label>Teléfono</label><input type="tel" value={usuarioForm.telefono} onChange={e => setUsuarioForm({ ...usuarioForm, telefono: e.target.value })} placeholder="+57..." /></div>
                  <div className="form-group"><label>Rol *</label><select value={usuarioForm.rol} onChange={e => setUsuarioForm({ ...usuarioForm, rol: e.target.value })}><option>Socio</option><option>AportadorInterno</option><option>AportadorExterno</option><option>Cobrador</option></select></div>
                  <div className="form-group"><label>% Participación</label><input type="number" min="0" max="100" step="0.1" value={usuarioForm.porcentajeParticipacion} onChange={e => setUsuarioForm({ ...usuarioForm, porcentajeParticipacion: Number(e.target.value) })} /></div>
                  <div className="form-group"><label>Tasa Mensual (%)</label><input type="number" min="0" max="100" step="0.1" value={usuarioForm.tasaInteresMensual} onChange={e => setUsuarioForm({ ...usuarioForm, tasaInteresMensual: Number(e.target.value) })} /></div>
                </div>
              </div>
              <div className="modal-footer"><button type="button" className="btn btn-secondary" onClick={() => setShowUsuarioModal(false)}>Cancelar</button><button type="submit" className="btn btn-primary">Crear</button></div>
            </form>
          </div>
        </div>
      )}

      {showAporteModal && (
        <div className="modal-overlay" onClick={() => setShowAporteModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <div className="modal-header"><h2>Registrar Aporte/Retiro</h2><button className="modal-close" onClick={() => setShowAporteModal(false)}>×</button></div>
            <form onSubmit={handleAporteRetiro}>
              <div className="modal-body">
                <div className="form-grid">
                  <div className="form-group full-width"><label>Socio/Aportador *</label><select required value={aporteForm.usuarioId || ''} onChange={e => setAporteForm({ ...aporteForm, usuarioId: Number(e.target.value) })}><option value="">Seleccione...</option>{balanceSocios.map(s => <option key={s.id} value={s.id}>{s.nombre} - {s.rol}</option>)}</select></div>
                  <div className="form-group"><label>Tipo *</label><select value={aporteForm.tipo} onChange={e => setAporteForm({ ...aporteForm, tipo: e.target.value })}><option value="aporte">Aporte</option><option value="retiro">Retiro</option></select></div>
                  <div className="form-group"><label>Monto *</label><input type="number" min="1" required value={aporteForm.monto || ''} onChange={e => setAporteForm({ ...aporteForm, monto: Number(e.target.value) })} /></div>
                  <div className="form-group full-width"><label>Descripción</label><input type="text" value={aporteForm.descripcion} onChange={e => setAporteForm({ ...aporteForm, descripcion: e.target.value })} /></div>
                </div>
              </div>
              <div className="modal-footer"><button type="button" className="btn btn-secondary" onClick={() => setShowAporteModal(false)}>Cancelar</button><button type="submit" className="btn btn-primary">Registrar</button></div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

export default App;
