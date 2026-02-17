import { useState, useEffect, useCallback, useRef } from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, Legend } from 'recharts';
import { clientesApi, prestamosApi, cuotasApi, pagosApi, dashboardApi, authApi, usuariosApi, cobrosApi, aportesApi, getAuthToken, capitalApi, prestamosConFuentesApi, aportadoresExternosApi, smsCampaignsApi, smsHistoryApi, cobrosDelMesApi, prestamosDelDiaApi, miBalanceApi, gananciasApi, ResumenParticipacion, costosApi } from './api';
import { Cliente, CreateClienteDto, CreatePrestamoDto, CreatePagoDto, Cuota, DashboardMetricas, Pago, Prestamo, Usuario, Cobrador, BalanceSocio, FuenteCapital, BalanceCapital, AportadorExterno, CreateAportadorExternoDto, SmsCampaign, CreateSmsCampaignDto, SmsHistory, CobrosDelMes, PrestamosDelDia, MiBalance, Costo, CreateCostoDto } from './types';
import { MetricasCobradores } from './components/MetricasCobradores';
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
  const [activeTab, setActiveTab] = useState<'prestamos' | 'clientes' | 'cuotas' | 'cobros' | 'prestamosdia' | 'pagosdia' | 'sms' | 'smshistory' | 'socios' | 'balance' | 'usuarios' | 'aportadores' | 'ganancias' | 'metricas'>('prestamos');

  // Data states
  const [metricas, setMetricas] = useState<DashboardMetricas | null>(null);
  const [prestamos, setPrestamos] = useState<Prestamo[]>([]);
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [balanceSocios, setBalanceSocios] = useState<BalanceSocio[]>([]);
  const [aportadoresExternos, setAportadoresExternos] = useState<AportadorExterno[]>([]);
  const [showAportadorModal, setShowAportadorModal] = useState(false);
  const [aportadorForm, setAportadorForm] = useState<CreateAportadorExternoDto>({ nombre: '', telefono: '', email: '', tasaInteres: 3, diasParaPago: 30, notas: '', montoTotalAportado: 0 });
  const [editingAportadorId, setEditingAportadorId] = useState<number | null>(null);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);

  // New feature states
  const [smsCampaigns, setSmsCampaigns] = useState<SmsCampaign[]>([]);
  const [smsHistoryData, setSmsHistoryData] = useState<SmsHistory[]>([]);
  const [cobrosDelMes, setCobrosDelMes] = useState<CobrosDelMes | null>(null);
  const [prestamosDelDia, setPrestamosDelDia] = useState<PrestamosDelDia | null>(null);
  const [miBalance, setMiBalance] = useState<MiBalance | null>(null);
  const [resumenParticipacion, setResumenParticipacion] = useState<ResumenParticipacion | null>(null);
  const [showSmsCampaignModal, setShowSmsCampaignModal] = useState(false);
  const [smsCampaignForm, setSmsCampaignForm] = useState<CreateSmsCampaignDto>({
    nombre: '', mensaje: '', activo: true, diasEnvio: '[]', horasEnvio: '[]', vecesPorDia: 1, tipoDestinatario: 'CuotasHoy'
  });
  const [showPasswordModal, setShowPasswordModal] = useState(false);
  const [passwordChangeUserId, setPasswordChangeUserId] = useState<number | null>(null);
  const [newPassword, setNewPassword] = useState('');

  // Estados para edición inline en Ganancias
  const [editingGananciaAportadorId, setEditingGananciaAportadorId] = useState<number | null>(null);
  const [editMontoAportado, setEditMontoAportado] = useState<string>('');

  // Estados para Costos Operativos
  const [costos, setCostos] = useState<Costo[]>([]);
  const [showCostoModal, setShowCostoModal] = useState(false);
  const [editingCostoId, setEditingCostoId] = useState<number | null>(null);
  const [costoForm, setCostoForm] = useState<CreateCostoDto>({
    nombre: '',
    monto: 0,
    frecuencia: 'Mensual',
    descripcion: ''
  });

  // Estado para Pagos por Día
  const [pagosPorDiaData, setPagosPorDiaData] = useState<{
    fechaInicio: string;
    fechaFin: string;
    totalGeneral: number;
    totalPagos: number;
    diasConPagos: number;
    porDia: Array<{
      fecha: string;
      totalDia: number;
      cantidadPagos: number;
      pagos: Array<{ id: number; prestamoId: number; clienteNombre: string; montoPago: number; fechaPago: string; metodoPago: string; observaciones: string; }>;
    }>;
  } | null>(null);
  const [pagosDiaFechaInicio, setPagosDiaFechaInicio] = useState<string>(formatDateInput(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)));
  const [pagosDiaFechaFin, setPagosDiaFechaFin] = useState<string>(formatDateInput(new Date()));
  const [expandedDays, setExpandedDays] = useState<Set<string>>(new Set());
  const [prestamosDelDiaFecha, setPrestamosDelDiaFecha] = useState<string>(formatDateInput(new Date()));


  const handleStartEditGananciaAportador = (id: number, currentMonto: number) => {
    setEditingGananciaAportadorId(id);
    setEditMontoAportado(currentMonto.toString());
  };

  const handleSaveGananciaAportador = async (id: number) => {
    try {
      if (!editMontoAportado) return;
      // Primero obtener el aportador actual para no perder otros datos
      const aportadorActual = await aportadoresExternosApi.getById(id);
      // Enviar objeto con las propiedades correctas incluyendo estado
      await aportadoresExternosApi.update(id, {
        nombre: aportadorActual.nombre,
        telefono: aportadorActual.telefono,
        email: aportadorActual.email,
        tasaInteres: aportadorActual.tasaInteres,
        diasParaPago: aportadorActual.diasParaPago,
        estado: aportadorActual.estado,
        notas: aportadorActual.notas,
        montoTotalAportado: Number(editMontoAportado)
      });
      showToast('Capital actualizado', 'success');
      setEditingGananciaAportadorId(null);
      loadResumenParticipacion(); // Recargar datos
    } catch (error) {
      showToast('Error al actualizar', 'error');
      console.error(error);
    }
  };

  // Estados para edición inline en Ganancias Socios
  const [editingGananciaSocioId, setEditingGananciaSocioId] = useState<number | null>(null);
  const [editMontoSocio, setEditMontoSocio] = useState<string>('');

  const handleStartEditGananciaSocio = (id: number, currentMonto: number) => {
    setEditingGananciaSocioId(id);
    setEditMontoSocio(currentMonto.toString());
  };

  const handleSaveGananciaSocio = async (id: number) => {
    try {
      if (!editMontoSocio) return;
      await aportesApi.ajustarCapital({
        usuarioId: id,
        nuevoCapital: Number(editMontoSocio)
      });
      showToast('Capital de socio actualizado', 'success');
      setEditingGananciaSocioId(null);
      loadResumenParticipacion(); // Recargar datos
    } catch (error) {
      showToast('Error al actualizar capital', 'error');
      console.error(error);
    }
  };

  // Handlers para Costos Operativos
  const loadCostos = async () => {
    try {
      const data = await costosApi.getAll();
      setCostos(data);
    } catch (error) {
      console.error('Error cargando costos:', error);
    }
  };

  const handleOpenCostoModal = (costo?: Costo) => {
    if (costo) {
      setEditingCostoId(costo.id);
      setCostoForm({
        nombre: costo.nombre,
        monto: costo.monto,
        frecuencia: costo.frecuencia,
        descripcion: costo.descripcion || ''
      });
    } else {
      setEditingCostoId(null);
      setCostoForm({ nombre: '', monto: 0, frecuencia: 'Mensual', descripcion: '' });
    }
    setShowCostoModal(true);
  };

  const handleSaveCosto = async () => {
    try {
      if (!costoForm.nombre || costoForm.monto <= 0) {
        showToast('Complete nombre y monto', 'error');
        return;
      }
      if (editingCostoId) {
        await costosApi.update(editingCostoId, {
          ...costoForm,
          activo: true,
          fechaFin: undefined
        });
        showToast('Costo actualizado', 'success');
      } else {
        await costosApi.create(costoForm);
        showToast('Costo creado', 'success');
      }
      setShowCostoModal(false);
      loadCostos();
      loadResumenParticipacion();
    } catch (error) {
      showToast('Error al guardar costo', 'error');
      console.error(error);
    }
  };

  const handleDeleteCosto = async (id: number) => {
    if (!window.confirm('¿Eliminar este costo?')) return;
    try {
      await costosApi.delete(id);
      showToast('Costo eliminado', 'success');
      loadCostos();
      loadResumenParticipacion();
    } catch (error) {
      showToast('Error al eliminar', 'error');
    }
  };

  const handleToggleCostoActivo = async (costo: Costo) => {
    try {
      await costosApi.update(costo.id, {
        nombre: costo.nombre,
        monto: costo.monto,
        frecuencia: costo.frecuencia,
        descripcion: costo.descripcion,
        activo: !costo.activo,
        fechaFin: !costo.activo ? undefined : new Date().toISOString()
      });
      showToast(costo.activo ? 'Costo desactivado' : 'Costo activado', 'success');
      loadCostos();
      loadResumenParticipacion();
    } catch (error) {
      showToast('Error al cambiar estado', 'error');
    }
  };

  // Filters
  const [filtroEstado, setFiltroEstado] = useState('Todos');
  const [filtroFrecuencia, setFiltroFrecuencia] = useState('Todos');
  const [filtroBusqueda, setFiltroBusqueda] = useState('');
  const [filtroClienteId] = useState<number | undefined>();
  const [filtroClienteBusqueda, setFiltroClienteBusqueda] = useState('');
  const [filtroCobradorId, setFiltroCobradorId] = useState<number | undefined>();
  const [cobradoresList, setCobradoresList] = useState<Cobrador[]>([]);

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

  // Cobrador search state
  const [cobradorSearch, setCobradorSearch] = useState('');
  const [cobradorSearchResults, setCobradorSearchResults] = useState<Cobrador[]>([]);
  const [selectedCobrador, setSelectedCobrador] = useState<Cobrador | null>(null);
  const [showCobradorDropdown, setShowCobradorDropdown] = useState(false);
  const cobradorSearchTimeoutRef = useRef<number | null>(null);

  // Fuentes de capital state
  const [balanceCapital, setBalanceCapital] = useState<BalanceCapital | null>(null);
  const [fuentesCapital, setFuentesCapital] = useState<FuenteCapital[]>([]);
  const [showFuentesSection, setShowFuentesSection] = useState(false);

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

  // Edición de Préstamo
  const [editMode, setEditMode] = useState(false);
  const [editingPrestamoId, setEditingPrestamoId] = useState<number | null>(null);
  const [editingClienteId, setEditingClienteId] = useState<number | null>(null);
  const [loadingCobros, setLoadingCobros] = useState(false);
  const [loadingPrestamosDelDia, setLoadingPrestamosDelDia] = useState(false);

  const loadData = useCallback(async () => {
    if (!isAuthenticated) { setLoading(false); return; }
    try {
      const [metricasData, prestamosData] = await Promise.all([
        dashboardApi.getMetricas(),
        prestamosApi.getAll({ estado: filtroEstado !== 'Todos' ? filtroEstado : undefined, frecuencia: filtroFrecuencia !== 'Todos' ? filtroFrecuencia : undefined, busqueda: filtroBusqueda || undefined, clienteId: filtroClienteId })
      ]);
      setMetricas(metricasData);
      setPrestamos(prestamosData);
    } catch (error) {
      console.error('Error loading data:', error);
    } finally { setLoading(false); }
  }, [isAuthenticated, filtroEstado, filtroFrecuencia, filtroBusqueda, filtroClienteId]);



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
  useEffect(() => { loadData(); }, [loadData]);
  useEffect(() => { if (activeTab === 'cobros') { loadCobrosDelMes(); loadCobradoresList(); } }, [activeTab, filtroCobradorId]);
  useEffect(() => { if (activeTab === 'prestamosdia') loadPrestamosDelDia(); }, [activeTab]);

  useEffect(() => { if (activeTab === 'socios') loadBalanceSocios(); }, [activeTab]);
  useEffect(() => { if (activeTab === 'usuarios') loadUsuarios(); }, [activeTab]);
  useEffect(() => { if (activeTab === 'clientes') loadClientes(); }, [activeTab]);
  useEffect(() => { if (activeTab === 'aportadores') loadAportadoresExternos(); }, [activeTab]);
  useEffect(() => { if (activeTab === 'sms') loadSmsCampaigns(); }, [activeTab]);
  useEffect(() => { if (activeTab === 'smshistory') loadSmsHistory(); }, [activeTab]);
  useEffect(() => { if (activeTab === 'balance') loadMiBalance(); }, [activeTab]);

  // New feature loaders
  const loadSmsCampaigns = async () => {
    try {
      const data = await smsCampaignsApi.getAll();
      setSmsCampaigns(data);
    } catch (error) { console.error('Error loading SMS campaigns:', error); }
  };

  const loadSmsHistory = async () => {
    try {
      const data = await smsHistoryApi.getAll();
      setSmsHistoryData(data.data);
    } catch (error) { console.error('Error loading SMS history:', error); }
  };

  const loadCobradoresList = async () => {
    try {
      const data = await usuariosApi.getCobradores();
      setCobradoresList(data);
    } catch (error) { console.error('Error loading cobradores list:', error); }
  };

  const loadCobrosDelMes = async () => {
    setLoadingCobros(true);
    setLoadingCobros(true);
    try {
      const data = await cobrosDelMesApi.getCobrosDelMes(filtroCobradorId);
      setCobrosDelMes(data);
    } catch (error) {
      console.error('Error loading cobros del mes:', error);
      showToast('Error al cargar cobros', 'error');
    } finally {
      setLoadingCobros(false);
    }
  };

  const loadPrestamosDelDia = async (fecha?: string) => {
    setLoadingPrestamosDelDia(true);
    try {
      const data = await prestamosDelDiaApi.getPrestamosDelDia(fecha || prestamosDelDiaFecha);
      setPrestamosDelDia(data);
    } catch (error) {
      console.error('Error loading préstamos del día:', error);
      showToast('Error al cargar préstamos del día', 'error');
    } finally {
      setLoadingPrestamosDelDia(false);
    }
  };

  const loadMiBalance = async () => {
    try {
      const data = await miBalanceApi.getMiBalance(currentUser?.id);
      setMiBalance(data);
    } catch (error) { console.error('Error loading balance:', error); }
  };

  const loadResumenParticipacion = async () => {
    try {
      const data = await gananciasApi.getResumenParticipacion();
      setResumenParticipacion(data);
    } catch (error) { console.error('Error loading resumen participacion:', error); }
  };

  const handleCreateSmsCampaign = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      await smsCampaignsApi.create(smsCampaignForm);
      showToast('Campaña SMS creada exitosamente', 'success');
      setShowSmsCampaignModal(false);
      setSmsCampaignForm({ nombre: '', mensaje: '', activo: true, diasEnvio: '[]', horasEnvio: '[]', vecesPorDia: 1, tipoDestinatario: 'CuotasHoy' });
      loadSmsCampaigns();
    } catch (error: unknown) { showToast(error instanceof Error ? error.message : 'Error', 'error'); }
  };

  const handleToggleSmsCampaign = async (id: number) => {
    try {
      await smsCampaignsApi.toggle(id);
      loadSmsCampaigns();
    } catch (error) { showToast('Error al cambiar estado', 'error'); }
  };

  const handleDeleteSmsCampaign = async (id: number) => {
    if (!confirm('¿Eliminar esta campaña SMS?')) return;
    try {
      await smsCampaignsApi.delete(id);
      showToast('Campaña eliminada', 'success');
      loadSmsCampaigns();
    } catch (error) { showToast('Error al eliminar', 'error'); }
  };

  const openPasswordModal = (userId: number) => {
    setPasswordChangeUserId(userId);
    setNewPassword('');
    setShowPasswordModal(true);
  };

  const handleChangePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!passwordChangeUserId) return;
    try {
      await usuariosApi.cambiarPassword(passwordChangeUserId, newPassword);
      showToast('Contraseña actualizada exitosamente', 'success');
      setShowPasswordModal(false);
      setPasswordChangeUserId(null);
      setNewPassword('');
    } catch (error: unknown) { showToast(error instanceof Error ? error.message : 'Error', 'error'); }
  };

  const loadAportadoresExternos = async () => {
    try {
      const data = await aportadoresExternosApi.getAll();
      setAportadoresExternos(data);
    } catch (error) {
      console.error('Error loading aportadores:', error);
    }
  };

  const handleCreateAportador = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingAportadorId) {
        await aportadoresExternosApi.update(editingAportadorId, aportadorForm);
        showToast('Aportador actualizado exitosamente', 'success');
      } else {
        await aportadoresExternosApi.create(aportadorForm);
        showToast('Aportador creado exitosamente', 'success');
      }
      setShowAportadorModal(false);
      setAportadorForm({ nombre: '', telefono: '', email: '', tasaInteres: 3, diasParaPago: 30, notas: '', montoTotalAportado: 0, estado: 'Activo' });
      setEditingAportadorId(null);
      loadAportadoresExternos();
    } catch (error: unknown) {
      showToast(error instanceof Error ? error.message : 'Error', 'error');
    }
  };

  const handleEditAportadorButton = (aportador: AportadorExterno) => {
    setAportadorForm({
      nombre: aportador.nombre,
      telefono: aportador.telefono || '',
      email: aportador.email || '',
      tasaInteres: aportador.tasaInteres,
      diasParaPago: aportador.diasParaPago,
      notas: aportador.notas || '',
      montoTotalAportado: aportador.montoTotalAportado,
      estado: aportador.estado
    });
    setEditingAportadorId(aportador.id);
    setShowAportadorModal(true);
  };

  const handleDeleteAportador = async (id: number) => {
    if (!confirm('¿Está seguro de eliminar este aportador?')) return;
    try {
      await aportadoresExternosApi.delete(id);
      showToast('Aportador eliminado', 'success');
      loadAportadoresExternos();
      loadResumenParticipacion(); // Refrescar también Ganancias si estamos ahí
    } catch (error: unknown) {
      showToast(error instanceof Error ? error.message : 'Error al eliminar', 'error');
    }
  };

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

  // Cobrador search handler with debounce
  const handleCobradorSearch = (value: string) => {
    setCobradorSearch(value);
    setShowCobradorDropdown(true);

    if (cobradorSearchTimeoutRef.current) {
      clearTimeout(cobradorSearchTimeoutRef.current);
    }

    if (value.length >= 2) {
      cobradorSearchTimeoutRef.current = setTimeout(async () => {
        try {
          const allCobradores = await usuariosApi.getCobradores();
          // Filtrar cobradores por nombre
          const filtered = allCobradores.filter(c =>
            c.nombre.toLowerCase().includes(value.toLowerCase())
          );
          setCobradorSearchResults(filtered);
        } catch (error) {
          console.error('Error searching cobradores:', error);
          setCobradorSearchResults([]);
        }
      }, 300);
    } else if (value.length === 0) {
      // Si está vacío, mostrar todos los cobradores
      cobradorSearchTimeoutRef.current = setTimeout(async () => {
        try {
          const allCobradores = await usuariosApi.getCobradores();
          setCobradorSearchResults(allCobradores);
        } catch (error) {
          console.error('Error loading cobradores:', error);
          setCobradorSearchResults([]);
        }
      }, 300);
    } else {
      setCobradorSearchResults([]);
    }
  };

  const selectCobrador = (cobrador: Cobrador) => {
    setSelectedCobrador(cobrador);
    setCobradorSearch(cobrador.nombre);
    setPrestamoForm({ ...prestamoForm, cobradorId: cobrador.id });
    setShowCobradorDropdown(false);
    setCobradorSearchResults([]);
  };

  const clearCobradorSelection = () => {
    setSelectedCobrador(null);
    setCobradorSearch('');
    setPrestamoForm({ ...prestamoForm, cobradorId: undefined });
    setCobradorSearchResults([]);
  };

  // Load cobradores when focusing the input
  const handleCobradorFocus = async () => {
    setShowCobradorDropdown(true);
    if (cobradorSearchResults.length === 0) {
      try {
        const allCobradores = await usuariosApi.getCobradores();
        setCobradorSearchResults(allCobradores);
      } catch (error) {
        console.error('Error loading cobradores:', error);
      }
    }
  };

  // Fuentes de Capital functions
  const loadBalanceCapital = async () => {
    try {
      const balance = await capitalApi.getBalance();
      setBalanceCapital(balance);
    } catch (error) {
      console.error('Error loading balance:', error);
    }
  };

  const openPrestamoModalWithBalance = async () => {
    await loadBalanceCapital();
    setEditMode(false);
    setEditingPrestamoId(null);
    setPrestamoForm({
      clienteId: 0,
      montoPrestado: 0,
      tasaInteres: 0,
      tipoInteres: 'Simple',
      frecuenciaPago: 'Mensual',
      duracion: 1,
      unidadDuracion: 'Meses',
      fechaPrestamo: new Date().toISOString().split('T')[0],
      descripcion: '',
      porcentajeCobrador: 0,
      diaSemana: undefined,
      esCongelado: false
    });
    setFuentesCapital([]);
    clearClienteSelection();
    clearCobradorSelection();
    setClienteSearch('');
    setShowFuentesSection(false);
    setShowPrestamoModal(true);
  };

  const addFuenteCapital = (tipo: 'Reserva' | 'Interno' | 'Externo', usuarioId?: number, aportadorExternoId?: number) => {
    const newFuente: FuenteCapital = {
      tipo,
      usuarioId: tipo === 'Interno' ? usuarioId : undefined,
      aportadorExternoId: tipo === 'Externo' ? aportadorExternoId : undefined,
      montoAportado: 0
    };
    setFuentesCapital([...fuentesCapital, newFuente]);
  };

  const updateFuenteMonto = (index: number, monto: number) => {
    const updated = [...fuentesCapital];
    updated[index].montoAportado = monto;
    setFuentesCapital(updated);
  };

  const removeFuente = (index: number) => {
    setFuentesCapital(fuentesCapital.filter((_, i) => i !== index));
  };

  const totalFuentesAsignado = fuentesCapital.reduce((sum, f) => sum + f.montoAportado, 0);

  // Forms
  const [clienteForm, setClienteForm] = useState<CreateClienteDto>({ nombre: '', cedula: '', telefono: '', direccion: '', email: '' });
  const [prestamoForm, setPrestamoForm] = useState<CreatePrestamoDto>({
    clienteId: 0, montoPrestado: 0, tasaInteres: 15, tipoInteres: 'Simple',
    frecuenciaPago: 'Quincenal', duracion: 3, unidadDuracion: 'Meses',
    fechaPrestamo: formatDateInput(new Date()), descripcion: '',
    cobradorId: undefined, porcentajeCobrador: 0, esCongelado: false
  });
  const [pagoForm, setPagoForm] = useState<CreatePagoDto>({
    prestamoId: 0, cuotaId: undefined, montoPago: 0,
    fechaPago: formatDateInput(new Date()), metodoPago: 'Efectivo', observaciones: ''
  });

  // Fixed Quota Mode State
  const [mantenerCuota, setMantenerCuota] = useState(false);
  const [targetCuota, setTargetCuota] = useState(0);


  // Recalculate duration when maintaining quota
  const handleMontoChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newMonto = parseFloat(e.target.value) || 0;

    if (mantenerCuota && targetCuota > 0 && newMonto > 0) {
      // Calculate required duration to keep quota fixed.
      // Assuming Simple Interest for now: Quota = (M + M*r*t) / (t*freq) => t = M / (Q*freq - M*r)
      // Adjust for unit (Duracion is in form units, need to convert)

      // Simpler approach: Iterative or approximation? No, explicit algebra.
      // Formula: Cuota = (Monto + Monto * (Tasa/100) * Meses) / CuotasTotales
      // CuotasTotales = Meses * FreqMes
      // Cuota = (M + M*r*M_dur) / (M_dur * Freq)
      // Cuota * M_dur * Freq = M + M*r*M_dur
      // M_dur * (Cuota*Freq - M*r) = M
      // M_dur = M / (Cuota*Freq - M*r)

      const tasaMensual = prestamoForm.tasaInteres / 100;
      const frecuenciaDias: Record<string, number> = { Diario: 1, Semanal: 7, Quincenal: 15, Mensual: 30 };
      const diasEntreCuotas = frecuenciaDias[prestamoForm.frecuenciaPago] || 30;
      const freqMensual = 30 / diasEntreCuotas; // Payments per month

      // Denominator
      const denom = targetCuota * freqMensual - newMonto * tasaMensual;

      if (denom > 0) {
        const mesesNecesarios = newMonto / denom;
        // Convert months to current unit
        const diasMap: Record<string, number> = { Dias: 1 / 30, Semanas: 1 / 4, Quincenas: 1 / 2, Meses: 1 };
        const factorUnidad = diasMap[prestamoForm.unidadDuracion] || 1;

        const nuevaDuracion = Math.ceil(mesesNecesarios / factorUnidad);
        setPrestamoForm({ ...prestamoForm, montoPrestado: newMonto, duracion: nuevaDuracion > 0 ? nuevaDuracion : 1 });
      } else {
        // Impossible to keep quota (interest exceeds quota)
        setPrestamoForm({ ...prestamoForm, montoPrestado: newMonto });
      }
    } else {
      setPrestamoForm({ ...prestamoForm, montoPrestado: newMonto });
    }
  };

  const toggleMantenerCuota = (checked: boolean) => {
    setMantenerCuota(checked);
    if (checked) {
      // Capture current quota as target
      const preview = calcularPreview();
      if (preview) setTargetCuota(preview.montoCuota);
    }
  };
  const [usuarioForm, setUsuarioForm] = useState({ nombre: '', email: '', password: '', telefono: '', rol: 'Socio', porcentajeParticipacion: 0, tasaInteresMensual: 3 });
  const [aporteForm, setAporteForm] = useState({ usuarioId: 0, monto: 0, descripcion: '', tipo: 'aporte' });

  const openEditCliente = (cliente: Cliente) => {
    setEditingClienteId(cliente.id);
    setClienteForm({
      nombre: cliente.nombre,
      cedula: cliente.cedula,
      telefono: cliente.telefono || '',
      direccion: cliente.direccion || '',
      email: cliente.email || ''
    });
    setShowClienteModal(true);
  };

  const handleCreateCliente = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      if (editingClienteId) {
        const currentCliente = clientes.find(c => c.id === editingClienteId);
        if (!currentCliente) throw new Error('Cliente no encontrado');

        await clientesApi.update(editingClienteId, {
          ...clienteForm,
          estado: currentCliente.estado
        });
        showToast('Cliente actualizado exitosamente', 'success');
      } else {
        await clientesApi.create(clienteForm);
        showToast('Cliente creado exitosamente', 'success');
      }
      setShowClienteModal(false);
      setEditingClienteId(null);
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

    let numeroCuotas = 0;

    // Si el usuario especificó cuotas directamente, usarlas
    if ((prestamoForm as any).numeroCuotasDirecto && (prestamoForm as any).numeroCuotasDirecto > 0) {
      numeroCuotas = (prestamoForm as any).numeroCuotasDirecto;
    } else {
      // Ajuste específico de negocio para Meses (Sync con Backend)
      if (prestamoForm.unidadDuracion === 'Meses') {
        if (prestamoForm.frecuenciaPago === 'Semanal') numeroCuotas = prestamoForm.duracion * 4;
        else if (prestamoForm.frecuenciaPago === 'Quincenal') numeroCuotas = prestamoForm.duracion * 2;
        else if (prestamoForm.frecuenciaPago === 'Mensual') numeroCuotas = prestamoForm.duracion;
      }

      if (numeroCuotas === 0) {
        numeroCuotas = Math.max(1, Math.ceil(diasTotales / diasEntreCuotas));
      }
    }

    let montoIntereses: number, montoTotal: number, montoCuota: number;

    if (prestamoForm.esCongelado) {
      // PRÉSTAMO CONGELADO: Cuota = solo interés por período
      const factorFrecuencia = prestamoForm.frecuenciaPago === 'Diario' ? 1 / 30
        : prestamoForm.frecuenciaPago === 'Semanal' ? 7 / 30
          : prestamoForm.frecuenciaPago === 'Quincenal' ? 15 / 30
            : 1; // Mensual
      montoCuota = Math.round(prestamoForm.montoPrestado * (prestamoForm.tasaInteres / 100) * factorFrecuencia);
      montoTotal = prestamoForm.montoPrestado; // Capital no se suma a intereses
      montoIntereses = montoCuota * numeroCuotas; // Intereses proyectados
    } else if (prestamoForm.tipoInteres === 'Simple') {
      // Convertir días a meses (tasa es mensual)
      const meses = diasTotales / 30;
      // Interés Simple: I = P * (r/100) * meses
      montoIntereses = prestamoForm.montoPrestado * (prestamoForm.tasaInteres / 100) * meses;
      montoTotal = prestamoForm.montoPrestado + montoIntereses;
      montoCuota = montoTotal / numeroCuotas;
    } else {
      const tasaPorPeriodo = (prestamoForm.tasaInteres / 100) / (365 / diasEntreCuotas);
      montoTotal = prestamoForm.montoPrestado * Math.pow(1 + tasaPorPeriodo, numeroCuotas);
      montoIntereses = montoTotal - prestamoForm.montoPrestado;
      montoCuota = montoTotal / numeroCuotas;
    }

    const fechaVencimiento = new Date(prestamoForm.fechaPrestamo);
    fechaVencimiento.setDate(fechaVencimiento.getDate() + diasTotales);
    return { numeroCuotas, montoIntereses, montoTotal, montoCuota, fechaVencimiento: fechaVencimiento.toISOString(), esCongelado: prestamoForm.esCongelado };
  };
  const preview = calcularPreview();

  const openEditPrestamo = async (prestamo: Prestamo) => {
    setEditMode(true);
    setEditingPrestamoId(prestamo.id);

    // Usar la información del cliente que ya viene en el objeto prestamo
    // en lugar de buscar en la lista de clientes que puede no estar cargada
    const clienteData: Cliente = {
      id: prestamo.clienteId,
      nombre: prestamo.clienteNombre,
      cedula: prestamo.clienteCedula,
      telefono: prestamo.clienteTelefono,
      direccion: '',
      email: '',
      fechaRegistro: '',
      estado: 'Activo',
      prestamosActivos: 0,
      totalPrestado: 0
    };
    setSelectedCliente(clienteData);
    setClienteSearch(`${prestamo.clienteNombre} - ${prestamo.clienteCedula}`);

    // Cargar cobrador
    if (prestamo.cobradorId) {
      const cobrador = await usuariosApi.getCobradores().then(res => res.find(c => c.id === prestamo.cobradorId));
      if (cobrador) selectCobrador(cobrador);
    }

    setPrestamoForm({
      clienteId: prestamo.clienteId,
      montoPrestado: prestamo.montoPrestado,
      tasaInteres: prestamo.tasaInteres,
      tipoInteres: prestamo.tipoInteres,
      frecuenciaPago: prestamo.frecuenciaPago,
      // Usamos las cuotas como duración. Unidad dependerá de frecuencia para simplificar.
      duracion: prestamo.numeroCuotas,
      unidadDuracion: prestamo.frecuenciaPago === 'Semanal' ? 'Semanas' : prestamo.frecuenciaPago === 'Mensual' ? 'Meses' : prestamo.frecuenciaPago === 'Quincenal' ? 'Quincenas' : 'Dias',
      fechaPrestamo: prestamo.fechaPrestamo.split('T')[0],
      descripcion: prestamo.descripcion || '',
      porcentajeCobrador: prestamo.porcentajeCobrador,
      diaSemana: prestamo.diaSemana,
      cobradorId: prestamo.cobradorId,
      esCongelado: prestamo.esCongelado || false
    });

    setShowPrestamoModal(true);
  };



  const handleCreatePrestamo = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!prestamoForm.clienteId) { showToast('Seleccione un cliente', 'warning'); return; }

    // Validar fuentes de capital si están configuradas
    if (!editMode && fuentesCapital.length > 0) {
      const totalAsignado = fuentesCapital.reduce((sum, f) => sum + f.montoAportado, 0);
      if (totalAsignado !== prestamoForm.montoPrestado) {
        showToast(`El total de fuentes (${formatMoney(totalAsignado)}) debe ser igual al monto prestado (${formatMoney(prestamoForm.montoPrestado)})`, 'warning');
        return;
      }
    }

    try {
      if (editMode && editingPrestamoId) {
        // MODO EDICIÓN - Obtener el préstamo actual para preservar el estado
        const prestamoActual = prestamos.find(p => p.id === editingPrestamoId);
        // Calcular numeroCuotas correctamente usando la misma lógica que el preview
        const calculatedPreview = calcularPreview();
        // Construir payload explícito con todos los campos requeridos por UpdatePrestamoDto
        await prestamosApi.updateCompleto(editingPrestamoId, {
          montoPrestado: prestamoForm.montoPrestado,
          tasaInteres: prestamoForm.tasaInteres,
          tipoInteres: prestamoForm.tipoInteres,
          frecuenciaPago: prestamoForm.frecuenciaPago,
          numeroCuotas: calculatedPreview?.numeroCuotas || prestamoForm.duracion,
          fechaPrestamo: prestamoForm.fechaPrestamo,
          fechaPrimerPago: prestamoForm.fechaPrestamo,
          estadoPrestamo: prestamoActual?.estadoPrestamo || 'Activo',
          descripcion: prestamoForm.descripcion,
          cobradorId: prestamoForm.cobradorId,
          porcentajeCobrador: prestamoForm.porcentajeCobrador,
          diaSemana: prestamoForm.diaSemana,
          esCongelado: prestamoForm.esCongelado || false
        });
        showToast('Préstamo actualizado exitosamente', 'success');
      } else {
        // MODO CREACIÓN
        // Si hay fuentes configuradas, usar el endpoint con fuentes
        if (fuentesCapital.length > 0) {
          await prestamosConFuentesApi.create({
            clienteId: prestamoForm.clienteId,
            montoPrestado: prestamoForm.montoPrestado,
            tasaInteres: prestamoForm.tasaInteres,
            tipoInteres: prestamoForm.tipoInteres,
            frecuenciaPago: prestamoForm.frecuenciaPago,
            duracion: prestamoForm.duracion,
            unidadDuracion: prestamoForm.unidadDuracion,
            fechaPrestamo: prestamoForm.fechaPrestamo,
            descripcion: prestamoForm.descripcion,
            cobradorId: prestamoForm.cobradorId,
            porcentajeCobrador: prestamoForm.porcentajeCobrador,
            fuentesCapital: fuentesCapital
          });
        } else {
          // Sin fuentes, usar el endpoint normal
          // PREGUNTA: ¿Debo enviar fechaPrimerPago aquí también?
          // El backend CreatePrestamo toma la FechaPrestamo del DTO como fecha base ya modificada por mi.
          // Pero el create DTO no tiene FechaPrimerPago.
          // El backend usa dto.FechaPrestamo.
          // Asi que prestamoForm.fechaPrestamo es suficiente, el backend la usará como inicio.
          await prestamosApi.create(prestamoForm);
        }
        showToast('Préstamo creado exitosamente', 'success');
      }

      setShowPrestamoModal(false);
      setPrestamoForm({ clienteId: 0, montoPrestado: 0, tasaInteres: 15, tipoInteres: 'Simple', frecuenciaPago: 'Quincenal', duracion: 3, unidadDuracion: 'Meses', fechaPrestamo: formatDateInput(new Date()), descripcion: '', cobradorId: undefined, porcentajeCobrador: 5 });
      setFuentesCapital([]);
      setSelectedCliente(null);
      setClienteSearch('');
      setEditMode(false);
      setMantenerCuota(false);
      setTargetCuota(0);
      setFuentesCapital([]);
      setSelectedCliente(null);
      setClienteSearch('');
      setEditMode(false);
      setEditingPrestamoId(null);
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
      loadCobrosDelMes();
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
          <button className="btn btn-primary" onClick={openPrestamoModalWithBalance}>+ Préstamo</button>
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
          <div className="kpi-card"><div className="kpi-header"><span className="kpi-title">Interés del Mes</span></div><span className="kpi-value" style={{ color: '#10b981' }}>{formatMoney(metricas?.interesMes || 0)}</span></div>
          <div className="kpi-card"><div className="kpi-header"><span className="kpi-title">Ganancia Total Mes</span></div><span className="kpi-value" style={{ color: '#3b82f6' }}>{formatMoney(metricas?.gananciaTotalMes || 0)}</span></div>
          <div className="kpi-card"><div className="kpi-header"><span className="kpi-title">Cuotas Vencidas</span></div><span className="kpi-value">{metricas?.cuotasVencidasHoy || 0}</span></div>
        </div>

        {/* Flujo de Capital */}
        <div className="kpi-grid" style={{ marginTop: '1rem' }}>
          <div className="kpi-card" style={{ borderLeft: '4px solid #f59e0b', background: 'linear-gradient(135deg, rgba(245,158,11,0.1) 0%, transparent 100%)' }}>
            <div className="kpi-header"><span className="kpi-title">💰 Capital</span></div>
            <span className="kpi-value" style={{ color: '#f59e0b' }}>{formatMoney(metricas?.capitalInicial || 0)}</span>
            <span className="kpi-sub" style={{ marginTop: '0.5rem', color: '#999' }}>Capital total en préstamos activos</span>
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
            <ResponsiveContainer width="100%" height={250}>
              <LineChart data={metricas?.evolucionPrestamos || []} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="#2a2a2a" />
                <XAxis dataKey="fecha" tickFormatter={v => new Date(v).toLocaleDateString('es-CO', { month: 'short' })} stroke="#666" style={{ fontSize: '0.7rem' }} />
                <YAxis stroke="#666" tickFormatter={v => `${(v / 1000000).toFixed(1)}M`} style={{ fontSize: '0.7rem' }} width={30} />
                <Tooltip formatter={v => formatMoney(Number(v || 0))} contentStyle={{ background: '#1a1a1a', border: '1px solid #2a2a2a', fontSize: '0.8rem' }} />
                <Line type="monotone" dataKey="montoPrestadoAcumulado" stroke="#3b82f6" strokeWidth={2} dot={false} />
                <Line type="monotone" dataKey="montoCobradoAcumulado" stroke="#10b981" strokeWidth={2} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
          <div className="chart-container">
            <h3 className="chart-title">Estados</h3>
            <ResponsiveContainer width="100%" height={250}>
              <PieChart margin={{ top: 0, bottom: 0, left: 0, right: 0 }}>
                <Pie
                  data={[{ name: 'Activos', value: metricas?.distribucionEstados?.activos || 0 }, { name: 'Pagados', value: metricas?.distribucionEstados?.pagados || 0 }, { name: 'Vencidos', value: metricas?.distribucionEstados?.vencidos || 0 }]}
                  cx="50%"
                  cy="50%"
                  innerRadius={50}
                  outerRadius={80}
                  dataKey="value"
                  paddingAngle={5}
                >
                  {COLORS.map((c, i) => <Cell key={i} fill={c} />)}
                </Pie>
                <Legend verticalAlign="bottom" height={36} iconType="circle" wrapperStyle={{ fontSize: '0.8rem' }} />
                <Tooltip contentStyle={{ background: '#1a1a1a', border: '1px solid #2a2a2a', fontSize: '0.8rem' }} />
              </PieChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Tabs */}
        <div className="section">
          <div className="tabs">
            <button className={`tab ${activeTab === 'prestamos' ? 'active' : ''}`} onClick={() => setActiveTab('prestamos')}>Préstamos</button>
            <button className={`tab ${activeTab === 'clientes' ? 'active' : ''}`} onClick={() => setActiveTab('clientes')}>Clientes</button>
            <button className={`tab ${activeTab === 'cobros' ? 'active' : ''}`} onClick={() => setActiveTab('cobros')}>📋 Cobros</button>
            <button className={`tab ${activeTab === 'prestamosdia' ? 'active' : ''}`} onClick={() => setActiveTab('prestamosdia')}>📋 Préstamos/Día</button>
            <button className={`tab ${activeTab === 'pagosdia' ? 'active' : ''}`} onClick={() => setActiveTab('pagosdia')}>💵 Pagos/Día</button>
            <button className={`tab ${activeTab === 'socios' ? 'active' : ''}`} onClick={() => setActiveTab('socios')}>Socios</button>
            <button className={`tab ${activeTab === 'balance' ? 'active' : ''}`} onClick={() => setActiveTab('balance')}>💰 Mi Balance</button>
            <button className={`tab ${activeTab === 'sms' ? 'active' : ''}`} onClick={() => setActiveTab('sms')}>📱 SMS</button>
            <button className={`tab ${activeTab === 'smshistory' ? 'active' : ''}`} onClick={() => setActiveTab('smshistory')}>📨 Historial</button>
            {(currentUser?.rol === 'Socio' || currentUser?.rol === 'Admin') && <button className={`tab ${activeTab === 'usuarios' ? 'active' : ''}`} onClick={() => setActiveTab('usuarios')}>👤 Usuarios</button>}
            <button className={`tab ${activeTab === 'aportadores' ? 'active' : ''}`} onClick={() => setActiveTab('aportadores')}>Aportadores</button>
            <button className={`tab ${activeTab === 'ganancias' ? 'active' : ''}`} onClick={() => { setActiveTab('ganancias'); loadResumenParticipacion(); loadCostos(); }}>📊 Ganancias</button>
            {(currentUser?.rol === 'Socio' || currentUser?.rol === 'Admin') && <button className={`tab ${activeTab === 'metricas' ? 'active' : ''}`} onClick={() => setActiveTab('metricas')}>📈 Métricas</button>}
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
                    <td>
                      {p.cobradorNombre || '-'}
                      {p.cobradorNombre && p.porcentajeCobrador ? <span style={{ fontSize: '0.75rem', color: '#6b7280', marginLeft: '4px' }}>({p.porcentajeCobrador}%)</span> : ''}
                    </td>
                    <td>{p.cuotasPagadas}/{p.numeroCuotas}</td>
                    <td><span className={`badge ${p.estadoPrestamo === 'Activo' ? 'badge-green' : p.estadoPrestamo === 'Pagado' ? 'badge-blue' : 'badge-red'}`}>{p.estadoPrestamo}</span></td>
                    <td><div className="actions"><button className="btn btn-secondary btn-sm" onClick={() => openDetalle(p)}>Ver</button><button className="btn btn-primary btn-sm" onClick={() => openEditPrestamo(p)}>✏️</button><button className="btn btn-danger btn-sm" onClick={() => handleDeletePrestamo(p.id)}>✕</button></div></td>
                  </tr>
                ))}{prestamos.length === 0 && <tr><td colSpan={8} className="empty-state">No hay préstamos</td></tr>}</tbody>
              </table>
            </div>
          )}

          {/* Clientes Tab */}
          {activeTab === 'clientes' && (
            <>
              {/* Client Search Bar */}
              <div className="filters-bar" style={{ marginBottom: '1rem' }}>
                <div className="filter-group" style={{ flex: 1 }}>
                  <label>Buscar Cliente</label>
                  <input
                    type="text"
                    placeholder="Nombre o cédula..."
                    value={filtroClienteBusqueda}
                    onChange={e => setFiltroClienteBusqueda(e.target.value)}
                  />
                </div>
                <button className="btn btn-secondary btn-sm" onClick={() => setFiltroClienteBusqueda('')}>
                  Limpiar
                </button>
              </div>
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
                      <th>Acciones</th>
                    </tr>
                  </thead>
                  <tbody>
                    {clientes
                      .filter(c =>
                        filtroClienteBusqueda === '' ||
                        c.nombre.toLowerCase().includes(filtroClienteBusqueda.toLowerCase()) ||
                        c.cedula.toLowerCase().includes(filtroClienteBusqueda.toLowerCase())
                      )
                      .map(c => (
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
                          <td>
                            <button className="btn btn-secondary btn-sm" onClick={() => openEditCliente(c)}>
                              ✏️ Editar
                            </button>
                          </td>
                        </tr>
                      ))}
                    {clientes.filter(c =>
                      filtroClienteBusqueda === '' ||
                      c.nombre.toLowerCase().includes(filtroClienteBusqueda.toLowerCase()) ||
                      c.cedula.toLowerCase().includes(filtroClienteBusqueda.toLowerCase())
                    ).length === 0 && (
                        <tr>
                          <td colSpan={8} className="empty-state">
                            {filtroClienteBusqueda ? 'No se encontraron clientes' : 'No hay clientes'}
                          </td>
                        </tr>
                      )}
                  </tbody>
                </table>
              </div>
            </>
          )}

          {/* Cobros Tab */}
          {activeTab === 'cobros' && (
            <div>
              {/* Filtro de Cobrador */}
              {currentUser?.rol !== 'Cobrador' && (
                <div className="filters-bar" style={{ marginBottom: '1rem' }}>
                  <div className="filter-group">
                    <label>Filtrar por Cobrador</label>
                    <select
                      value={filtroCobradorId || ''}
                      onChange={e => setFiltroCobradorId(e.target.value ? Number(e.target.value) : undefined)}
                      style={{ minWidth: '200px' }}
                    >
                      <option value="">Todos</option>
                      {cobradoresList.map(c => (
                        <option key={c.id} value={c.id}>{c.nombre}</option>
                      ))}
                    </select>
                  </div>
                </div>
              )}
              {loadingCobros ? (
                <div className="loading"><div className="spinner"></div></div>
              ) : cobrosDelMes ? (
                <>
                  <div className="kpi-grid" style={{ marginBottom: '1rem' }}>
                    <div className="kpi-card"><span className="kpi-title">📅 Hoy ({cobrosDelMes.resumen.totalCuotasHoy})</span><span className="kpi-value">{formatMoney(cobrosDelMes.resumen.montoTotalHoy)}</span></div>
                    <div className="kpi-card" style={{ borderColor: '#ef4444' }}><span className="kpi-title">⚠️ Vencidas ({cobrosDelMes.resumen.totalCuotasVencidas})</span><span className="kpi-value" style={{ color: '#ef4444' }}>{formatMoney(cobrosDelMes.resumen.montoTotalVencido)}</span></div>
                    <div className="kpi-card" style={{ borderColor: '#3b82f6' }}><span className="kpi-title">📆 Próximas ({cobrosDelMes.resumen.totalCuotasProximas})</span><span className="kpi-value" style={{ color: '#3b82f6' }}>{formatMoney(cobrosDelMes.resumen.montoTotalProximas)}</span></div>
                  </div>
                  <h4 style={{ color: '#10b981', margin: '1rem 0 0.5rem' }}>📅 Cobros de Hoy</h4>
                  <div className="table-container">
                    <table><thead><tr><th>✓</th><th>Cliente</th><th>Cuota</th><th>Monto</th><th>Cobrador</th><th>Acciones</th></tr></thead>
                      <tbody>{cobrosDelMes.cuotasHoy.map(c => (
                        <tr key={c.id} style={{ opacity: c.cobrado ? 0.6 : 1 }}>
                          <td><input type="checkbox" checked={c.cobrado} onChange={e => handleMarcarCobrado(c.id, e.target.checked)} disabled={currentUser?.rol !== 'Socio'} /></td>
                          <td><strong>{c.clienteNombre}</strong><div style={{ fontSize: '0.75rem' }}>{c.clienteTelefono}</div></td>
                          <td>#{c.numeroCuota}</td>
                          <td className="money">{formatMoney(c.saldoPendiente)}</td>
                          <td>{c.cobradorNombre || '-'}</td>
                          <td>
                            <div className="actions">
                              <button className="btn btn-secondary btn-sm" onClick={async () => {
                                const p = await prestamosApi.getById(c.prestamoId);
                                openDetalle(p);
                              }}>Ver</button>
                              <button className="btn btn-primary btn-sm" title="Enviar SMS recordatorio" onClick={async () => {
                                try {
                                  await cobrosApi.enviarRecordatorio(c.id);
                                  showToast('SMS enviado', 'success');
                                } catch (e: any) { showToast(e.message || 'Error', 'error'); }
                              }}>📱</button>
                            </div>
                          </td>
                        </tr>
                      ))}{cobrosDelMes.cuotasHoy.length === 0 && <tr><td colSpan={6} className="empty-state">No hay cuotas para hoy</td></tr>}</tbody>
                    </table>
                  </div>

                  <h4 style={{ color: '#3b82f6', margin: '1rem 0 0.5rem' }}>📆 Próximas del Mes</h4>
                  <div className="table-container">
                    <table><thead><tr><th>Cliente</th><th>Fecha</th><th>En</th><th>Monto</th><th>Cobrador</th><th>Acciones</th></tr></thead>
                      <tbody>{cobrosDelMes.cuotasProximas.map(c => (
                        <tr key={c.id}>
                          <td><strong>{c.clienteNombre}</strong><div style={{ fontSize: '0.75rem' }}>{c.clienteTelefono}</div></td>
                          <td>{formatDate(c.fechaCobro)}</td>
                          <td><span className="badge badge-blue">{c.diasParaVencer}d</span></td>
                          <td className="money">{formatMoney(c.saldoPendiente)}</td>
                          <td>{c.cobradorNombre || '-'}</td>
                          <td>
                            <div className="actions">
                              <button className="btn btn-secondary btn-sm" onClick={async () => {
                                const p = await prestamosApi.getById(c.prestamoId);
                                openDetalle(p);
                              }}>Ver</button>
                              <button className="btn btn-primary btn-sm" title="Enviar SMS recordatorio" onClick={async () => {
                                try {
                                  await cobrosApi.enviarRecordatorio(c.id);
                                  showToast('SMS recordatorio enviado', 'success');
                                } catch (e: any) { showToast(e.message || 'Error', 'error'); }
                              }}>📱 Recordar</button>
                            </div>
                          </td>
                        </tr>
                      ))}{cobrosDelMes.cuotasProximas.length === 0 && <tr><td colSpan={6} className="empty-state">No hay cuotas próximas este mes</td></tr>}</tbody>
                    </table>
                  </div>

                  {cobrosDelMes.cuotasVencidas.length > 0 && (
                    <>
                      <h4 style={{ color: '#ef4444', margin: '1rem 0 0.5rem' }}>⚠️ Cuotas Vencidas</h4>
                      <div className="table-container">
                        <table><thead><tr><th>✓</th><th>Cliente</th><th>Fecha</th><th>Días</th><th>Monto</th><th>Cobrador</th><th>Acciones</th></tr></thead>
                          <tbody>{cobrosDelMes.cuotasVencidas.map(c => (
                            <tr key={c.id} style={{ background: 'rgba(239,68,68,0.1)' }}>
                              <td><input type="checkbox" checked={c.cobrado} onChange={e => handleMarcarCobrado(c.id, e.target.checked)} disabled={currentUser?.rol !== 'Socio'} /></td>
                              <td><strong>{c.clienteNombre}</strong><div style={{ fontSize: '0.75rem' }}>{c.clienteTelefono}</div></td>
                              <td style={{ color: '#ef4444' }}>{formatDate(c.fechaCobro)}</td>
                              <td><span className="badge badge-red">{Math.abs(c.diasParaVencer)}d</span></td>
                              <td className="money">{formatMoney(c.saldoPendiente)}</td>
                              <td>{c.cobradorNombre || '-'}</td>
                              <td>
                                <div className="actions">
                                  <button className="btn btn-secondary btn-sm" onClick={async () => {
                                    const p = await prestamosApi.getById(c.prestamoId);
                                    openDetalle(p);
                                  }}>Ver</button>
                                  <button className="btn btn-danger btn-sm" title="Enviar SMS recordatorio" onClick={async () => {
                                    try {
                                      await cobrosApi.enviarRecordatorio(c.id);
                                      showToast('SMS recordatorio enviado', 'success');
                                    } catch (e: any) { showToast(e.message || 'Error', 'error'); }
                                  }}>📱 Cobrar</button>
                                </div>
                              </td>
                            </tr>
                          ))}</tbody>
                        </table>
                      </div>
                    </>
                  )}
                </>
              ) : null}
            </div>
          )}

          {/* Préstamos del Día Tab */}
          {activeTab === 'prestamosdia' && (
            <div>
              <div className="filters-bar" style={{ marginBottom: '1rem', flexWrap: 'wrap', gap: '0.5rem' }}>
                <div className="filter-group" style={{ flex: '0 0 auto', minWidth: '180px' }}>
                  <label>Seleccionar Fecha</label>
                  <input
                    type="date"
                    value={prestamosDelDiaFecha}
                    onChange={e => {
                      setPrestamosDelDiaFecha(e.target.value);
                      loadPrestamosDelDia(e.target.value);
                    }}
                    style={{ fontSize: '1rem', padding: '0.6rem', cursor: 'pointer' }}
                  />
                </div>
                <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', alignItems: 'flex-end' }}>
                  <button className="btn btn-primary" onClick={() => {
                    const hoy = formatDateInput(new Date());
                    setPrestamosDelDiaFecha(hoy);
                    loadPrestamosDelDia(hoy);
                  }}>📅 Hoy</button>
                  <button className="btn btn-secondary" onClick={() => {
                    const ayer = formatDateInput(new Date(Date.now() - 1 * 24 * 60 * 60 * 1000));
                    setPrestamosDelDiaFecha(ayer);
                    loadPrestamosDelDia(ayer);
                  }}>⏮️ Ayer</button>
                  {[2, 3, 4, 5, 6, 7].map(daysAgo => {
                    const targetDate = new Date(Date.now() - daysAgo * 24 * 60 * 60 * 1000);
                    const dateStr = formatDateInput(targetDate);
                    const dayName = targetDate.toLocaleDateString('es-CO', { weekday: 'short' });
                    return (
                      <button
                        key={daysAgo}
                        className="btn btn-secondary btn-sm"
                        onClick={() => {
                          setPrestamosDelDiaFecha(dateStr);
                          loadPrestamosDelDia(dateStr);
                        }}
                        title={formatDate(dateStr)}
                      >
                        {dayName} {targetDate.getDate()}
                      </button>
                    );
                  })}
                </div>
              </div>
              {loadingPrestamosDelDia ? (
                <div className="loading"><div className="spinner"></div></div>
              ) : prestamosDelDia ? (
                <>
                  <div className="kpi-grid" style={{ marginBottom: '1rem' }}>
                    <div className="kpi-card"><span className="kpi-title">📋 Préstamos ({formatDate(prestamosDelDia.fecha)} - {prestamosDelDia.resumen.totalPrestamosHoy})</span><span className="kpi-value">{formatMoney(prestamosDelDia.resumen.montoTotalDesembolsado)}</span></div>
                  </div>
                  <h4 style={{ color: '#10b981', margin: '1rem 0 0.5rem' }}>📋 Préstamos del {formatDate(prestamosDelDia.fecha)}</h4>
                  <div className="table-container">
                    <table><thead><tr><th>ID</th><th>Cliente</th><th>Monto</th><th>Interés</th><th>Cuotas</th><th>Cobrador</th><th>Estado</th><th>Acciones</th></tr></thead>
                      <tbody>{prestamosDelDia.prestamosHoy.map(p => (
                        <tr key={p.id}>
                          <td>#{p.id}</td>
                          <td><strong>{p.clienteNombre}</strong><div style={{ fontSize: '0.75rem' }}>{p.clienteCedula}</div></td>
                          <td className="money">{formatMoney(p.montoPrestado)}</td>
                          <td>{p.tasaInteres}%</td>
                          <td>{p.numeroCuotas}</td>
                          <td>{p.cobradorNombre || '-'}</td>
                          <td><span className={`badge ${p.estadoPrestamo === 'Activo' ? 'badge-green' : 'badge-gray'}`}>{p.estadoPrestamo}</span></td>
                          <td>
                            <div className="actions">
                              <button className="btn btn-secondary btn-sm" onClick={async () => {
                                const prestamo = await prestamosApi.getById(p.id);
                                openDetalle(prestamo);
                              }}>Ver</button>
                            </div>
                          </td>
                        </tr>
                      ))}{prestamosDelDia.prestamosHoy.length === 0 && <tr><td colSpan={8} className="empty-state">No hay préstamos creados en esta fecha</td></tr>}</tbody>
                    </table>
                  </div>
                </>
              ) : null}
            </div>
          )}

          {/* Pagos Por Día Tab */}
          {activeTab === 'pagosdia' && (
            <div>
              <div className="filters-bar" style={{ marginBottom: '1rem' }}>
                <div className="filter-group">
                  <label>Desde</label>
                  <input type="date" value={pagosDiaFechaInicio} onChange={e => setPagosDiaFechaInicio(e.target.value)} />
                </div>
                <div className="filter-group">
                  <label>Hasta</label>
                  <input type="date" value={pagosDiaFechaFin} onChange={e => setPagosDiaFechaFin(e.target.value)} />
                </div>
                <button className="btn btn-primary" onClick={async () => {
                  try {
                    const data = await pagosApi.getPorDia(pagosDiaFechaInicio, pagosDiaFechaFin);
                    setPagosPorDiaData(data);
                  } catch { showToast('Error cargando pagos', 'error'); }
                }}>🔍 Buscar</button>
              </div>

              {pagosPorDiaData && (
                <>
                  <div className="kpi-grid" style={{ marginBottom: '1rem' }}>
                    <div className="kpi-card"><span className="kpi-title">💰 Total Cobrado</span><span className="kpi-value" style={{ color: '#10b981' }}>{formatMoney(pagosPorDiaData.totalGeneral)}</span></div>
                    <div className="kpi-card"><span className="kpi-title">📝 Pagos Registrados</span><span className="kpi-value">{pagosPorDiaData.totalPagos}</span></div>
                    <div className="kpi-card"><span className="kpi-title">📅 Días con Pagos</span><span className="kpi-value">{pagosPorDiaData.diasConPagos}</span></div>
                  </div>

                  {pagosPorDiaData.porDia.map(dia => (
                    <div key={dia.fecha} style={{ marginBottom: '1rem', border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                      <div
                        style={{
                          display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                          padding: '0.75rem 1rem', background: 'var(--card-bg)', cursor: 'pointer'
                        }}
                        onClick={() => {
                          const newSet = new Set(expandedDays);
                          if (newSet.has(dia.fecha)) newSet.delete(dia.fecha);
                          else newSet.add(dia.fecha);
                          setExpandedDays(newSet);
                        }}
                      >
                        <div>
                          <strong>{formatDate(dia.fecha)}</strong>
                          <span style={{ color: '#6b7280', marginLeft: '0.5rem' }}>({dia.cantidadPagos} pagos)</span>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                          <span style={{ color: '#10b981', fontWeight: 'bold' }}>{formatMoney(dia.totalDia)}</span>
                          <span>{expandedDays.has(dia.fecha) ? '▼' : '▶'}</span>
                        </div>
                      </div>
                      {expandedDays.has(dia.fecha) && (
                        <div className="table-container" style={{ margin: 0 }}>
                          <table>
                            <thead><tr><th>Cliente</th><th>Préstamo</th><th>Monto</th><th>Método</th><th>Observaciones</th></tr></thead>
                            <tbody>
                              {dia.pagos.map(p => (
                                <tr key={p.id}>
                                  <td><strong>{p.clienteNombre}</strong></td>
                                  <td>#{p.prestamoId}</td>
                                  <td className="money" style={{ color: '#10b981' }}>{formatMoney(p.montoPago)}</td>
                                  <td>{p.metodoPago || 'Efectivo'}</td>
                                  <td style={{ maxWidth: '200px', overflow: 'hidden', textOverflow: 'ellipsis' }}>{p.observaciones || '-'}</td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      )}
                    </div>
                  ))}

                  {pagosPorDiaData.porDia.length === 0 && (
                    <div className="empty-state" style={{ padding: '2rem', textAlign: 'center' }}>No hay pagos en este rango de fechas</div>
                  )}
                </>
              )}

              {!pagosPorDiaData && (
                <div className="empty-state" style={{ padding: '2rem', textAlign: 'center' }}>Selecciona un rango de fechas y haz clic en Buscar</div>
              )}
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
                <table><thead><tr><th>Nombre</th><th>Email</th><th>Teléfono</th><th>Rol</th><th>%</th><th>Estado</th><th>Acciones</th></tr></thead>
                  <tbody>{usuarios.map(u => (
                    <tr key={u.id}>
                      <td><strong>{u.nombre}</strong></td>
                      <td>{u.email}</td>
                      <td>{u.telefono || '-'}</td>
                      <td><span className="badge badge-blue">{u.rol}</span></td>
                      <td>{u.porcentajeParticipacion}%</td>
                      <td><span className={`badge ${u.activo ? 'badge-green' : 'badge-gray'}`}>{u.activo ? 'Activo' : 'Inactivo'}</span></td>
                      <td><button className="btn btn-secondary btn-sm" onClick={() => openPasswordModal(u.id)}>🔑 Cambiar Contraseña</button></td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
            </div>
          )}

          {/* Aportadores Externos Tab */}
          {activeTab === 'aportadores' && (
            <div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '1rem' }}>
                <button className="btn btn-primary" onClick={() => {
                  setEditingAportadorId(null);
                  setAportadorForm({ nombre: '', telefono: '', email: '', tasaInteres: 3, diasParaPago: 30, notas: '', montoTotalAportado: 0 });
                  setShowAportadorModal(true);
                }}>+ Nuevo Aportador</button>
              </div>
              <div className="table-container">
                <table><thead><tr><th>Nombre</th><th>Teléfono</th><th>Tasa %</th><th>Días Pago</th><th>Aportado</th><th>Pagado</th><th>Saldo</th><th>Estado</th><th>Acciones</th></tr></thead>
                  <tbody>{aportadoresExternos.map(a => (
                    <tr key={a.id}>
                      <td><strong>{a.nombre}</strong></td>
                      <td>{a.telefono || '-'}</td>
                      <td>{a.tasaInteres}%</td>
                      <td>{a.diasParaPago}</td>
                      <td>{formatMoney(a.montoTotalAportado)}</td>
                      <td style={{ color: '#10b981' }}>{formatMoney(a.montoPagado)}</td>
                      <td style={{ color: a.saldoPendiente > 0 ? '#ef4444' : '#10b981' }}>{formatMoney(a.saldoPendiente)}</td>
                      <td><span className={`badge ${a.estado === 'Activo' ? 'badge-green' : 'badge-gray'}`}>{a.estado}</span></td>
                      <td>
                        <button className="btn btn-secondary" style={{ padding: '0.25rem 0.5rem', fontSize: '0.75rem', marginRight: '0.5rem' }} onClick={() => handleEditAportadorButton(a)}>Editar</button>
                        <button className="btn btn-danger" style={{ padding: '0.25rem 0.5rem', fontSize: '0.75rem' }} onClick={() => handleDeleteAportador(a.id)}>Eliminar</button>
                      </td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
            </div>
          )}

          {/* SMS Campaigns Tab */}
          {activeTab === 'sms' && (
            <div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '1rem' }}>
                <button className="btn btn-primary" onClick={() => setShowSmsCampaignModal(true)}>+ Nueva Campaña</button>
              </div>
              <div className="table-container">
                <table><thead><tr><th>Nombre</th><th>Mensaje</th><th>Destinatarios</th><th>Días</th><th>Enviados</th><th>Estado</th><th>Acciones</th></tr></thead>
                  <tbody>{smsCampaigns.map(c => (
                    <tr key={c.id}>
                      <td><strong>{c.nombre}</strong></td>
                      <td style={{ maxWidth: '200px', overflow: 'hidden', textOverflow: 'ellipsis' }}>{c.mensaje}</td>
                      <td><span className="badge badge-blue">{c.tipoDestinatario}</span></td>
                      <td>{JSON.parse(c.diasEnvio).join(', ') || 'Todos'}</td>
                      <td>{c.smsEnviados || 0}</td>
                      <td><span className={`badge ${c.activo ? 'badge-green' : 'badge-gray'}`} onClick={() => handleToggleSmsCampaign(c.id)} style={{ cursor: 'pointer' }}>{c.activo ? 'Activo' : 'Inactivo'}</span></td>
                      <td><button className="btn btn-danger btn-sm" onClick={() => handleDeleteSmsCampaign(c.id)}>✕</button></td>
                    </tr>
                  ))}{smsCampaigns.length === 0 && <tr><td colSpan={7} className="empty-state">No hay campañas SMS configuradas</td></tr>}</tbody>
                </table>
              </div>
            </div>
          )}

          {/* SMS History Tab */}
          {activeTab === 'smshistory' && (
            <div>
              <div className="table-container">
                <table><thead><tr><th>Fecha</th><th>Campaña</th><th>Cliente</th><th>Teléfono</th><th>Mensaje</th><th>Estado</th></tr></thead>
                  <tbody>{smsHistoryData.map(h => (
                    <tr key={h.id}>
                      <td>{formatDate(h.fechaEnvio)}</td>
                      <td>{h.campaignNombre || '-'}</td>
                      <td>{h.clienteNombre || '-'}</td>
                      <td>{h.numeroTelefono}</td>
                      <td style={{ maxWidth: '200px', overflow: 'hidden', textOverflow: 'ellipsis' }}>{h.mensaje}</td>
                      <td><span className={`badge ${h.estado === 'Enviado' || h.estado === 'Entregado' ? 'badge-green' : h.estado === 'Fallido' ? 'badge-red' : 'badge-yellow'}`}>{h.estado}</span></td>
                    </tr>
                  ))}{smsHistoryData.length === 0 && <tr><td colSpan={6} className="empty-state">No hay mensajes SMS enviados</td></tr>}</tbody>
                </table>
              </div>
            </div>
          )}

          {/* Mi Balance Tab */}
          {activeTab === 'balance' && miBalance && (
            <div>
              <div className="kpi-grid" style={{ marginBottom: '1rem' }}>
                <div className="kpi-card" style={{ borderLeft: '4px solid #10b981' }}>
                  <span className="kpi-title">💰 Capital Aportado</span>
                  <span className="kpi-value" style={{ color: '#10b981' }}>{formatMoney(miBalance.capitalAportado)}</span>
                  <span className="kpi-sub">Desde {miBalance.fechaInicio ? formatDate(miBalance.fechaInicio) : 'N/A'}</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #3b82f6' }}>
                  <span className="kpi-title">📈 Interés Ganado ({miBalance.tasaInteresMensual}% mensual)</span>
                  <span className="kpi-value" style={{ color: '#3b82f6' }}>{formatMoney(miBalance.interesGanado)}</span>
                  <span className="kpi-sub">{miBalance.mesesTranscurridos} meses</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #8b5cf6' }}>
                  <span className="kpi-title">🎯 Capital + Intereses</span>
                  <span className="kpi-value" style={{ color: '#8b5cf6' }}>{formatMoney(miBalance.capitalConInteres)}</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #f59e0b' }}>
                  <span className="kpi-title">🥧 Resto de la Torta</span>
                  <span className="kpi-value" style={{ color: '#f59e0b' }}>{formatMoney(miBalance.restoTorta)}</span>
                  <span className="kpi-sub">Total negocio: {formatMoney(miBalance.totalCapitalNegocio)}</span>
                </div>
              </div>
              <h4 style={{ margin: '1.5rem 0 0.5rem' }}>📋 Detalle de Aportes</h4>
              <div className="table-container">
                <table><thead><tr><th>Fecha</th><th>Monto Inicial</th><th>Monto Actual</th><th>Meses</th><th>Interés Generado</th><th>Descripción</th></tr></thead>
                  <tbody>{miBalance.aportes.map(a => (
                    <tr key={a.id}>
                      <td>{formatDate(a.fechaAporte)}</td>
                      <td className="money">{formatMoney(a.montoInicial)}</td>
                      <td className="money" style={{ color: '#10b981' }}>{formatMoney(a.montoActual)}</td>
                      <td>{a.mesesTranscurridos}</td>
                      <td className="money" style={{ color: '#3b82f6' }}>{formatMoney(a.interesGenerado)}</td>
                      <td>{a.descripcion || '-'}</td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
            </div>
          )}

          {/* Ganancias Tab */}
          {activeTab === 'ganancias' && resumenParticipacion && (
            <div>
              {/* Resumen General */}
              <div className="kpi-grid" style={{ marginBottom: '1.5rem' }}>
                <div className="kpi-card" style={{ borderLeft: '4px solid #6366f1' }}>
                  <span className="kpi-title">🏦 Capital en Circulación</span>
                  <span className="kpi-value" style={{ color: '#6366f1' }}>{formatMoney(resumenParticipacion.resumen.totalCapitalPrestado)}</span>
                  <span className="kpi-sub">Total Prestado Activo</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #8b5cf6' }}>
                  <span className="kpi-title">💎 Capital Base (Inversión)</span>
                  <span className="kpi-value" style={{ color: '#8b5cf6' }}>{formatMoney(resumenParticipacion.resumen.totalCapitalBase || 0)}</span>
                  <span className="kpi-sub">Socios + Aportadores</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #ec4899' }}>
                  <span className="kpi-title">🔄 Capital Reinvertido</span>
                  <span className="kpi-value" style={{ color: '#ec4899' }}>{formatMoney(resumenParticipacion.resumen.capitalReinvertido || 0)}</span>
                  <span className="kpi-sub">Crecimiento Orgánico</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #f97316' }}>
                  <span className="kpi-title">🛣️ Capital en Calle</span>
                  <span className="kpi-value" style={{ color: '#f97316' }}>{formatMoney(resumenParticipacion.resumen.capitalEnCalle || 0)}</span>
                  <span className="kpi-sub">Saldo Capital Pendiente</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #10b981' }}>
                  <span className="kpi-title">💰 Interés Total (Proyectado)</span>
                  <span className="kpi-value" style={{ color: '#10b981' }}>{formatMoney(resumenParticipacion.resumen.totalInteresesProyectados)}</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #3b82f6' }}>
                  <span className="kpi-title">📊 Ganancia Intereses Mes</span>
                  <span className="kpi-value" style={{ color: '#3b82f6' }}>{formatMoney(resumenParticipacion.resumen.proyeccionInteresesMesActual || 0)}</span>
                  <span className="kpi-sub">Solo intereses del mes</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #06b6d4' }}>
                  <span className="kpi-title">💵 Flujo Total Mes</span>
                  <span className="kpi-value" style={{ color: '#06b6d4' }}>{formatMoney(resumenParticipacion.resumen.flujoTotalMes || 0)}</span>
                  <span className="kpi-sub">Capital + Intereses</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #f59e0b' }}>
                  <span className="kpi-title">🏃 Comisiones Cobradores</span>
                  <span className="kpi-value" style={{ color: '#f59e0b' }}>{formatMoney(resumenParticipacion.resumen.totalGananciaCobradores)}</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #8b5cf6' }}>
                  <span className="kpi-title">👥 Socios (Ganancia Bruta)</span>
                  <span className="kpi-value" style={{ color: '#8b5cf6' }}>{formatMoney(resumenParticipacion.resumen.totalGananciaSociosBruta)}</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #ec4899' }}>
                  <span className="kpi-title">💸 Gasto Mensual Aportadores</span>
                  <span className="kpi-value" style={{ color: '#ec4899' }}>{formatMoney(resumenParticipacion.resumen.gastoMensualAportadores)}</span>
                  <span className="kpi-sub">Obligación Mensual Fija</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #ef4444' }}>
                  <span className="kpi-title">📋 Costos Operativos Mes</span>
                  <span className="kpi-value" style={{ color: '#ef4444' }}>{formatMoney(resumenParticipacion.resumen.costosTotalesMes || 0)}</span>
                  <span className="kpi-sub">Salarios, cuotas, etc.</span>
                </div>
                <div className="kpi-card" style={{ borderLeft: '4px solid #22c55e', background: 'rgba(34, 197, 94, 0.1)' }}>
                  <span className="kpi-title">✨ Ganancia Interés Neta</span>
                  <span className="kpi-value" style={{ color: '#22c55e', fontSize: '1.5rem' }}>{formatMoney(resumenParticipacion.resumen.gananciaInteresNeta || 0)}</span>
                  <span className="kpi-sub">Interés - Cobradores - Aportadores - Costos</span>
                </div>
              </div>


              {/* Aportadores Externos */}
              <h4 style={{ margin: '1.5rem 0 0.5rem' }}>💵 Aportadores Externos ({resumenParticipacion.aportadores.length})</h4>
              <div className="table-container" style={{ marginBottom: '1.5rem' }}>
                <table>
                  <thead><tr><th>Nombre</th><th>Capital Aportado</th><th>Tasa Interés</th><th>Ganancia Mensual</th><th>Estado</th></tr></thead>
                  <tbody>
                    {resumenParticipacion.aportadores.map(a => (
                      <tr key={a.id}>
                        <td><strong>{a.nombre}</strong></td>
                        <td className="money">
                          {editingGananciaAportadorId === a.id ? (
                            <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                              <input
                                type="number"
                                value={editMontoAportado}
                                onChange={e => setEditMontoAportado(e.target.value)}
                                style={{ width: '120px', padding: '0.25rem', background: '#333', border: '1px solid #444', color: '#fff', borderRadius: '4px' }}
                              />
                              <button onClick={() => handleSaveGananciaAportador(a.id)} style={{ background: 'none', border: 'none', cursor: 'pointer' }} title="Guardar">💾</button>
                              <button onClick={() => setEditingGananciaAportadorId(null)} style={{ background: 'none', border: 'none', cursor: 'pointer' }} title="Cancelar">❌</button>
                            </div>
                          ) : (
                            <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                              {formatMoney(a.capitalAportado)}
                              <span
                                onClick={() => handleStartEditGananciaAportador(a.id, a.capitalAportado)}
                                style={{ cursor: 'pointer', fontSize: '0.9rem', opacity: 0.6 }}
                                title="Editar Capital">
                                ✏️
                              </span>
                              <span
                                onClick={() => handleDeleteAportador(a.id)}
                                style={{ cursor: 'pointer', fontSize: '0.9rem', opacity: 0.6 }}
                                title="Eliminar Aportador">
                                🗑️
                              </span>
                            </div>
                          )}
                        </td>
                        <td>{a.tasaInteres}%</td>
                        <td className="money" style={{ color: '#10b981' }}>{formatMoney(a.gananciaMensual)}</td>
                        <td><span className={`badge ${a.estado === 'Activo' ? 'badge-green' : 'badge-gray'}`}>{a.estado}</span></td>
                      </tr>
                    ))}
                    {resumenParticipacion.aportadores.length === 0 && <tr><td colSpan={5} className="empty-state">No hay aportadores externos</td></tr>}
                  </tbody>
                </table>
              </div>

              {/* Cobradores */}
              <h4 style={{ margin: '1.5rem 0 0.5rem' }}>🏃 Cobradores ({resumenParticipacion.cobradores.length})</h4>
              <div className="table-container" style={{ marginBottom: '1.5rem' }}>
                <table>
                  <thead><tr><th>Cobrador</th><th>Préstamos</th><th>Comisión Proyectada Total</th><th>📅 Interés Mes</th><th>Comisión Realizada</th></tr></thead>
                  <tbody>
                    {resumenParticipacion.cobradores.map(c => (
                      <tr key={c.cobradorId}>
                        <td><strong>{c.nombre}</strong></td>
                        <td>{c.prestamosAsignados}</td>
                        <td className="money" style={{ color: '#f59e0b' }}>{formatMoney(c.gananciaProyectada)}</td>
                        <td className="money" style={{ color: '#3b82f6' }}>{formatMoney(c.gananciaInteresMes)}</td>
                        <td className="money" style={{ color: '#10b981' }}>{formatMoney(c.gananciaRealizada)}</td>
                      </tr>
                    ))}
                    {resumenParticipacion.cobradores.length === 0 && <tr><td colSpan={4} className="empty-state">No hay cobradores con préstamos asignados</td></tr>}
                  </tbody>
                </table>
              </div>

              {/* Socios */}
              <h4 style={{ margin: '1.5rem 0 0.5rem' }}>👥 Socios ({resumenParticipacion.socios.length})</h4>
              <div className="table-container">
                <table>
                  <thead><tr><th>Socio</th><th>Capital Aportado</th><th>Capital Actual</th><th>% Participación</th><th>Ganancia Proyectada Total</th><th>📅 Interés Mes</th><th>💵 Flujo Neto Mes</th><th>Ganancia Realizada</th></tr></thead>
                  <tbody>
                    {resumenParticipacion.socios.map(s => (
                      <tr key={s.id}>
                        <td><strong>{s.nombre}</strong></td>
                        <td className="money">{formatMoney(s.capitalAportado)}</td>
                        <td className="money" style={{ color: '#10b981' }}>
                          {editingGananciaSocioId === s.id ? (
                            <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                              <input
                                type="number"
                                value={editMontoSocio}
                                onChange={e => setEditMontoSocio(e.target.value)}
                                style={{ width: '120px', padding: '0.25rem', background: '#333', border: '1px solid #444', color: '#fff', borderRadius: '4px' }}
                              />
                              <button onClick={() => handleSaveGananciaSocio(s.id)} style={{ background: 'none', border: 'none', cursor: 'pointer' }} title="Guardar">💾</button>
                              <button onClick={() => setEditingGananciaSocioId(null)} style={{ background: 'none', border: 'none', cursor: 'pointer' }} title="Cancelar">❌</button>
                            </div>
                          ) : (
                            <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                              {formatMoney(s.capitalActual)}
                              <span
                                onClick={() => handleStartEditGananciaSocio(s.id, s.capitalActual)}
                                style={{ cursor: 'pointer', fontSize: '0.9rem', opacity: 0.6 }}
                                title="Editar Capital Actual">
                                ✏️
                              </span>
                            </div>
                          )}
                        </td>
                        <td>{s.porcentaje}%</td>
                        <td className="money">{formatMoney(s.gananciaProyectadaTotal)}</td>
                        <td className="money" style={{ color: '#3b82f6' }}>{formatMoney(s.gananciaInteresMes)}</td>
                        <td className="money" style={{ color: '#06b6d4' }}>{formatMoney(s.flujoNetoMes)}</td>
                        <td className="money" style={{ color: '#10b981' }}>{formatMoney(s.gananciaRealizada)}</td>
                      </tr>
                    ))}

                    {resumenParticipacion.socios.length === 0 && <tr><td colSpan={6} className="empty-state">No hay socios registrados</td></tr>}
                  </tbody>
                </table>
              </div>

              {/* Costos Operativos */}
              <h4 style={{ margin: '1.5rem 0 0.5rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span>📋 Costos Operativos ({costos.length})</span>
                <button onClick={() => handleOpenCostoModal()} className="btn btn-primary" style={{ fontSize: '0.85rem', padding: '0.5rem 1rem' }}>
                  + Nuevo Costo
                </button>
              </h4>
              <div className="table-container" style={{ marginBottom: '1.5rem' }}>
                <table>
                  <thead>
                    <tr>
                      <th>Nombre</th>
                      <th>Monto</th>
                      <th>Frecuencia</th>
                      <th>Descripción</th>
                      <th>Estado</th>
                      <th>Acciones</th>
                    </tr>
                  </thead>
                  <tbody>
                    {costos.map(c => (
                      <tr key={c.id} style={{ opacity: c.activo ? 1 : 0.5 }}>
                        <td><strong>{c.nombre}</strong></td>
                        <td className="money" style={{ color: '#ef4444' }}>{formatMoney(c.monto)}</td>
                        <td>
                          <span className="badge" style={{
                            background: c.frecuencia === 'Mensual' ? '#3b82f6' : c.frecuencia === 'Quincenal' ? '#8b5cf6' : '#6b7280',
                            color: 'white',
                            fontSize: '0.75rem'
                          }}>
                            {c.frecuencia}
                          </span>
                        </td>
                        <td style={{ fontSize: '0.85rem', color: '#888' }}>{c.descripcion || '-'}</td>
                        <td>
                          <button
                            onClick={() => handleToggleCostoActivo(c)}
                            className="btn btn-sm"
                            style={{
                              fontSize: '0.7rem',
                              padding: '0.25rem 0.5rem',
                              background: c.activo ? '#10b981' : '#6b7280',
                              color: 'white',
                              border: 'none'
                            }}
                          >
                            {c.activo ? '✓ Activo' : '✗ Inactivo'}
                          </button>
                        </td>
                        <td>
                          <div style={{ display: 'flex', gap: '0.5rem' }}>
                            <button onClick={() => handleOpenCostoModal(c)} className="btn btn-sm" style={{ fontSize: '0.75rem' }}>✏️</button>
                            <button onClick={() => handleDeleteCosto(c.id)} className="btn btn-sm btn-delete" style={{ fontSize: '0.75rem' }}>🗑️</button>
                          </div>
                        </td>
                      </tr>
                    ))}
                    {costos.length === 0 && <tr><td colSpan={6} className="empty-state">No hay costos registrados. Haz clic en "+ Nuevo Costo" para agregar uno.</td></tr>}
                  </tbody>
                </table>
              </div>
            </div>
          )}

        </div>
      </main>

      {/* Modals */}
      {showClienteModal && (
        <div className="modal-overlay" onClick={() => { setShowClienteModal(false); setEditingClienteId(null); setClienteForm({ nombre: '', cedula: '', telefono: '', direccion: '', email: '' }); }}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <div className="modal-header"><h2>{editingClienteId ? 'Editar Cliente' : 'Nuevo Cliente'}</h2><button className="modal-close" onClick={() => { setShowClienteModal(false); setEditingClienteId(null); setClienteForm({ nombre: '', cedula: '', telefono: '', direccion: '', email: '' }); }}>×</button></div>
            <form onSubmit={handleCreateCliente}>
              <div className="modal-body">
                <div className="form-grid">
                  <div className="form-group"><label>Nombre *</label><input type="text" required value={clienteForm.nombre} onChange={e => setClienteForm({ ...clienteForm, nombre: e.target.value })} /></div>
                  <div className="form-group"><label>Cédula *</label><input type="text" required value={clienteForm.cedula} onChange={e => setClienteForm({ ...clienteForm, cedula: e.target.value })} /></div>
                  <div className="form-group"><label>Teléfono</label><input type="tel" value={clienteForm.telefono || ''} onChange={e => setClienteForm({ ...clienteForm, telefono: e.target.value })} /></div>
                  <div className="form-group"><label>Email</label><input type="email" value={clienteForm.email || ''} onChange={e => setClienteForm({ ...clienteForm, email: e.target.value })} /></div>
                  <div className="form-group full-width"><label>Dirección</label><input type="text" value={clienteForm.direccion || ''} onChange={e => setClienteForm({ ...clienteForm, direccion: e.target.value })} /></div>
                </div>
              </div>
              <div className="modal-footer"><button type="button" className="btn btn-secondary" onClick={() => { setShowClienteModal(false); setEditingClienteId(null); setClienteForm({ nombre: '', cedula: '', telefono: '', direccion: '', email: '' }); }}>Cancelar</button><button type="submit" className="btn btn-primary">{editingClienteId ? 'Actualizar' : 'Guardar'}</button></div>
            </form>
          </div>
        </div>
      )}

      {showPrestamoModal && (
        <div className="modal-overlay" onClick={() => setShowPrestamoModal(false)}>
          <div className="modal modal-lg" onClick={e => e.stopPropagation()}>
            <div className="modal-header"><h2>{editMode ? 'Editar Préstamo' : 'Nuevo Préstamo'}</h2><button className="modal-close" onClick={() => setShowPrestamoModal(false)}>×</button></div>
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
                  <div className="form-group">
                    <label>Monto ($) *</label>
                    <input type="number" min="50" required value={prestamoForm.montoPrestado || ''} onChange={handleMontoChange} />
                  </div>
                  {/* Fixed Quota Toggle */}
                  <div className="form-group" style={{ marginTop: '-0.5rem', marginBottom: '1rem' }}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.85rem', color: '#555', cursor: 'pointer' }}>
                      <input
                        type="checkbox"
                        checked={mantenerCuota}
                        onChange={e => toggleMantenerCuota(e.target.checked)}
                        style={{ width: '16px', height: '16px' }}
                      />
                      <span>🔓 Mantener valor de cuota (Recalcular plazo)</span>
                    </label>
                  </div>
                  <div className="form-group" style={{ display: 'flex', alignItems: 'center' }}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer', margin: 0 }}>
                      <input
                        type="checkbox"
                        checked={prestamoForm.esCongelado || false}
                        onChange={e => {
                          setPrestamoForm({
                            ...prestamoForm,
                            esCongelado: e.target.checked,
                            frecuenciaPago: e.target.checked && (prestamoForm.frecuenciaPago === 'Diario' || prestamoForm.frecuenciaPago === 'Semanal') ? 'Mensual' : prestamoForm.frecuenciaPago
                          });
                        }}
                        style={{ width: '18px', height: '18px' }}
                      />
                      <span>❄️ Congelado</span>
                    </label>
                  </div>
                  <div className="form-group">
                    <label>Tasa Interés (%) *</label>
                    <input
                      type="number"
                      min="0"
                      step="0.1"
                      required
                      value={prestamoForm.tasaInteres}
                      onChange={e => {
                        const nuevaTasa = Number(e.target.value);
                        // Solo actualizar la tasa, NO el porcentaje del cobrador automáticamente
                        setPrestamoForm({
                          ...prestamoForm,
                          tasaInteres: nuevaTasa
                        });
                      }}
                    />
                  </div>
                  <div className="form-group">
                    <label>Frecuencia *</label>
                    <select value={prestamoForm.frecuenciaPago} onChange={e => setPrestamoForm({ ...prestamoForm, frecuenciaPago: e.target.value })}>
                      {!prestamoForm.esCongelado && <option>Diario</option>}
                      {!prestamoForm.esCongelado && <option>Semanal</option>}
                      <option>Quincenal</option>
                      <option>Mensual</option>
                    </select>
                  </div>
                  {!prestamoForm.esCongelado && (
                    <div className="form-group">
                      <label>Duración *</label>
                      <div style={{ display: 'flex', gap: '0.5rem' }}>
                        <input type="number" min="1" required value={prestamoForm.duracion} onChange={e => setPrestamoForm({ ...prestamoForm, duracion: Number(e.target.value) })} style={{ width: '80px' }} />
                        <select value={prestamoForm.unidadDuracion} onChange={e => setPrestamoForm({ ...prestamoForm, unidadDuracion: e.target.value })}>
                          <option>Dias</option><option>Semanas</option><option>Quincenas</option><option>Meses</option>
                        </select>
                      </div>
                    </div>
                  )}
                  {!prestamoForm.esCongelado && (
                    <div className="form-group">
                      <label>Cuotas <span style={{ fontSize: '0.7rem', color: '#888' }}>(opcional)</span></label>
                      <input
                        type="number"
                        min="1"
                        placeholder="Auto"
                        value={(prestamoForm as any).numeroCuotasDirecto || ''}
                        onChange={e => setPrestamoForm({ ...prestamoForm, numeroCuotasDirecto: e.target.value ? Number(e.target.value) : undefined } as any)}
                        style={{ width: '100%' }}
                      />
                      <small style={{ display: 'block', fontSize: '0.7rem', color: '#666', marginTop: '2px' }}>Dejar vacío = calcular automático</small>
                    </div>
                  )}
                  <div className="form-group" style={{ position: 'relative' }}>
                    <label>Cobrador (Referido)</label>
                    <div style={{ position: 'relative' }}>
                      <input
                        type="text"
                        placeholder="Buscar cobrador..."
                        value={cobradorSearch}
                        onChange={e => handleCobradorSearch(e.target.value)}
                        onFocus={handleCobradorFocus}
                        style={{ paddingRight: selectedCobrador ? '40px' : undefined }}
                      />
                      {selectedCobrador && (
                        <button
                          type="button"
                          onClick={clearCobradorSelection}
                          style={{ position: 'absolute', right: '10px', top: '50%', transform: 'translateY(-50%)', background: 'none', border: 'none', cursor: 'pointer', fontSize: '1.2rem', color: '#888' }}
                        >×</button>
                      )}
                      {showCobradorDropdown && cobradorSearchResults.length > 0 && (
                        <div style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '8px', maxHeight: '200px', overflow: 'auto', zIndex: 1000, boxShadow: '0 4px 12px rgba(0,0,0,0.15)' }}>
                          {cobradorSearchResults.map(c => (
                            <div
                              key={c.id}
                              onClick={() => selectCobrador(c)}
                              style={{ padding: '0.75rem 1rem', cursor: 'pointer', borderBottom: '1px solid var(--border)' }}
                              onMouseEnter={e => (e.currentTarget.style.background = 'var(--primary)', e.currentTarget.style.color = 'white')}
                              onMouseLeave={e => (e.currentTarget.style.background = 'transparent', e.currentTarget.style.color = 'inherit')}
                            >
                              <strong>{c.nombre}</strong>
                              {c.telefono && <span style={{ marginLeft: '0.5rem', opacity: 0.7 }}>({c.telefono})</span>}
                            </div>
                          ))}
                        </div>
                      )}
                      {cobradorSearch.length >= 1 && cobradorSearchResults.length === 0 && showCobradorDropdown && (
                        <div style={{ position: 'absolute', top: '100%', left: 0, right: 0, background: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '8px', padding: '0.75rem 1rem', color: '#888' }}>
                          No se encontraron cobradores
                        </div>
                      )}
                    </div>
                  </div>
                  <div className="form-group"><label>% Cobrador</label><input type="number" min="0" max="15" step="0.5" value={prestamoForm.porcentajeCobrador} onChange={e => { setPrestamoForm({ ...prestamoForm, porcentajeCobrador: Number(e.target.value) }); }} /></div>
                  {prestamoForm.frecuenciaPago === 'Semanal' && (
                    <div className="form-group">
                      <label>Día de pago *</label>
                      <select value={(prestamoForm as any).diaSemana || 'Lunes'} onChange={e => setPrestamoForm({ ...prestamoForm, diaSemana: e.target.value } as any)}>
                        <option>Lunes</option>
                        <option>Martes</option>
                        <option>Miércoles</option>
                        <option>Jueves</option>
                        <option>Viernes</option>
                        <option>Sábado</option>
                        <option>Domingo</option>
                      </select>
                    </div>
                  )}
                  <div className="form-group">
                    <label>Fecha Primera Cuota *</label>
                    <input type="date" required value={prestamoForm.fechaPrestamo} onChange={e => setPrestamoForm({ ...prestamoForm, fechaPrestamo: e.target.value })} />
                    <small style={{ display: 'block', fontSize: '0.75rem', color: '#666', marginTop: '2px' }}>Fecha inicio de pagos</small>
                  </div>

                </div>

                {/* Sección de Fuentes de Capital */}
                {prestamoForm.montoPrestado > 0 && (
                  <div style={{ marginTop: '1rem', padding: '1rem', background: 'var(--background)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.75rem' }}>
                      <h4 style={{ margin: 0 }}>💰 Fuentes del Capital ({formatMoney(prestamoForm.montoPrestado)})</h4>
                      <button type="button" className="btn btn-secondary" style={{ fontSize: '0.8rem', padding: '0.25rem 0.5rem' }} onClick={() => setShowFuentesSection(!showFuentesSection)}>
                        {showFuentesSection ? 'Ocultar' : 'Configurar'}
                      </button>
                    </div>

                    {balanceCapital && (
                      <div style={{ display: 'flex', gap: '1rem', marginBottom: '0.75rem', fontSize: '0.85rem' }}>
                        <span style={{ color: '#10b981' }}>Reserva: {formatMoney(balanceCapital.reservaDisponible)}</span>
                        <span style={{ color: '#3b82f6' }}>Socios: {balanceCapital.socios.length}</span>
                        <span style={{ color: '#f59e0b' }}>Aportadores: {balanceCapital.aportadoresExternos.length}</span>
                      </div>
                    )}

                    {showFuentesSection && (
                      <>
                        {/* Lista de fuentes agregadas */}
                        {fuentesCapital.map((fuente, index) => (
                          <div key={index} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.5rem', padding: '0.5rem', background: 'var(--surface)', borderRadius: '6px' }}>
                            <span style={{ minWidth: '80px', fontWeight: 500, color: fuente.tipo === 'Reserva' ? '#10b981' : fuente.tipo === 'Interno' ? '#3b82f6' : '#f59e0b' }}>
                              {fuente.tipo}
                            </span>
                            {fuente.tipo === 'Interno' && balanceCapital && (
                              <span style={{ flex: 1, fontSize: '0.85rem' }}>
                                {balanceCapital.socios.find(s => s.id === fuente.usuarioId)?.nombre || 'Socio'}
                              </span>
                            )}
                            {fuente.tipo === 'Externo' && balanceCapital && (
                              <span style={{ flex: 1, fontSize: '0.85rem' }}>
                                {balanceCapital.aportadoresExternos.find(a => a.id === fuente.aportadorExternoId)?.nombre || 'Aportador'}
                              </span>
                            )}
                            <input
                              type="number"
                              min="0"
                              value={fuente.montoAportado || ''}
                              onChange={e => updateFuenteMonto(index, Number(e.target.value))}
                              style={{ width: '120px' }}
                              placeholder="Monto"
                            />
                            <button type="button" onClick={() => removeFuente(index)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#ef4444', fontSize: '1.2rem' }}>×</button>
                          </div>
                        ))}

                        {/* Botones para agregar fuentes */}
                        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginTop: '0.5rem' }}>
                          <button type="button" className="btn btn-secondary" style={{ fontSize: '0.8rem', padding: '0.3rem 0.6rem' }} onClick={() => addFuenteCapital('Reserva')}>
                            + Reserva
                          </button>
                          {balanceCapital?.socios.map(socio => (
                            <button key={socio.id} type="button" className="btn btn-secondary" style={{ fontSize: '0.8rem', padding: '0.3rem 0.6rem' }} onClick={() => addFuenteCapital('Interno', socio.id)}>
                              + {socio.nombre}
                            </button>
                          ))}
                          {balanceCapital?.aportadoresExternos.map(aportador => (
                            <button key={aportador.id} type="button" className="btn btn-secondary" style={{ fontSize: '0.8rem', padding: '0.3rem 0.6rem', borderColor: '#f59e0b' }} onClick={() => addFuenteCapital('Externo', undefined, aportador.id)}>
                              + {aportador.nombre} (Ext)
                            </button>
                          ))}
                        </div>

                        {/* Total asignado */}
                        <div style={{ marginTop: '0.75rem', display: 'flex', justifyContent: 'space-between', padding: '0.5rem', background: totalFuentesAsignado === prestamoForm.montoPrestado ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)', borderRadius: '6px' }}>
                          <span>Total asignado:</span>
                          <strong style={{ color: totalFuentesAsignado === prestamoForm.montoPrestado ? '#10b981' : '#ef4444' }}>
                            {formatMoney(totalFuentesAsignado)} / {formatMoney(prestamoForm.montoPrestado)}
                          </strong>
                        </div>
                      </>
                    )}
                  </div>
                )}

                {preview && (
                  <div className="preview-card">
                    <h4>Vista Previa {preview.esCongelado && <span style={{ fontSize: '0.8rem', color: '#0ea5e9' }}>❄️ Congelado</span>}</h4>
                    {preview.esCongelado ? (
                      <div className="preview-grid">
                        <div className="preview-item"><span>Capital</span><strong>{formatMoney(prestamoForm.montoPrestado)}</strong></div>
                        <div className="preview-item"><span>Interés por {prestamoForm.frecuenciaPago === 'Mensual' ? 'Mes' : 'Quincena'}</span><strong style={{ color: '#0ea5e9' }}>{formatMoney(preview.montoCuota)}</strong></div>
                        <div className="preview-item" style={{ gridColumn: '1 / -1' }}><span style={{ fontSize: '0.75rem', color: '#888' }}>Pago de solo intereses cada período. Capital se reduce solo con abonos adicionales.</span></div>
                      </div>
                    ) : (
                      <div className="preview-grid">
                        <div className="preview-item"><span>Cuotas</span><strong>{preview.numeroCuotas}</strong></div>
                        <div className="preview-item"><span>Intereses</span><strong style={{ color: '#10b981' }}>{formatMoney(preview.montoIntereses)}</strong></div>
                        <div className="preview-item"><span>Total</span><strong>{formatMoney(preview.montoTotal)}</strong></div>
                        <div className="preview-item"><span>Por Cuota</span><strong style={{ color: '#3b82f6' }}>{formatMoney(preview.montoCuota)}</strong></div>
                      </div>
                    )}
                  </div>
                )}
              </div>
              <div className="modal-footer"><button type="button" className="btn btn-secondary" onClick={() => setShowPrestamoModal(false)}>Cancelar</button><button type="submit" className="btn btn-primary">{editMode ? 'Guardar Cambios' : 'Crear'}</button></div>
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
                <div className="detail-item"><label>Monto Prestado</label><span>{formatMoney(selectedPrestamo.montoPrestado)}</span></div>
                {!selectedPrestamo.esCongelado && (
                  <div className="detail-item"><label>Total a Pagar</label><span>{formatMoney(selectedPrestamo.montoTotal)}</span></div>
                )}
                <div className="detail-item"><label>Interés</label><span style={{ color: '#10b981' }}>{selectedPrestamo.tasaInteres}% ({selectedPrestamo.tipoInteres})</span></div>
                <div className="detail-item"><label>Frecuencia</label><span className="badge badge-blue">{selectedPrestamo.frecuenciaPago}</span></div>
                <div className="detail-item"><label>Cuotas</label><span>{selectedPrestamo.cuotasPagadas} / {selectedPrestamo.numeroCuotas}</span></div>
                <div className="detail-item"><label>Pagado</label><span style={{ color: '#10b981' }}>{formatMoney(selectedPrestamo.totalPagado)}</span></div>
                <div className="detail-item"><label>Pendiente</label><span style={{ color: '#ef4444' }}>{formatMoney(selectedPrestamo.saldoPendiente)}</span></div>
                <div className="detail-item"><label>Estado</label><span className={`badge ${selectedPrestamo.estadoPrestamo === 'Activo' ? 'badge-green' : selectedPrestamo.estadoPrestamo === 'Pagado' ? 'badge-blue' : 'badge-red'}`}>{selectedPrestamo.estadoPrestamo}</span></div>
                <div className="detail-item">
                  <label>Cobrador</label>
                  <span>
                    {selectedPrestamo.cobradorNombre || 'No asignado'}
                    {selectedPrestamo.cobradorNombre && selectedPrestamo.porcentajeCobrador ? <span style={{ fontSize: '0.85rem', color: '#6b7280', marginLeft: '5px' }}>({selectedPrestamo.porcentajeCobrador}%)</span> : ''}
                  </span>
                </div>

                {/* Sección de Ganancias por Socio de este préstamo */}
                <div style={{ gridColumn: '1 / -1', marginTop: '1rem', padding: '1rem', background: 'rgba(16, 185, 129, 0.1)', borderRadius: '8px', border: '1px solid #10b981' }}>
                  <h4 style={{ marginBottom: '0.75rem', color: '#10b981' }}>📊 Ganancias de Socios (Este Préstamo)</h4>
                  {(() => {
                    const capitalPorCuota = selectedPrestamo.montoPrestado / (selectedPrestamo.numeroCuotas || 1);
                    const interesPorCuota = selectedPrestamo.montoCuota - capitalPorCuota;

                    const interesPagado = interesPorCuota * selectedPrestamo.cuotasPagadas;

                    // Factor para calcular ganancia del cobrador
                    const factorCobrador = selectedPrestamo.cobradorNombre && selectedPrestamo.porcentajeCobrador > 0 && selectedPrestamo.tasaInteres > 0
                      ? selectedPrestamo.porcentajeCobrador / selectedPrestamo.tasaInteres
                      : 0;

                    const gananciaCobrador = interesPagado * factorCobrador;
                    const gananciaCobradorProyectada = selectedPrestamo.montoIntereses * factorCobrador;

                    // Interés neto para socios (acumulado y proyectado)
                    const interesNetoAcumulado = interesPagado - gananciaCobrador;
                    const interesNetoProyectado = selectedPrestamo.montoIntereses - gananciaCobradorProyectada;

                    const gananciaPorSocioAcumulada = interesNetoAcumulado / 3;
                    const gananciaPorSocioProyectada = interesNetoProyectado / 3;

                    // Progreso (porcentaje completado)
                    const progreso = gananciaPorSocioProyectada > 0 ? (gananciaPorSocioAcumulada / gananciaPorSocioProyectada) * 100 : 0;

                    const socios = ['Jorge Gutierrez', 'Jair Restrepo', 'Jeisson Restrepo'];

                    return (
                      <>
                        {/* Tarjetas de los 3 socios */}
                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem', marginBottom: '1rem' }}>
                          {socios.map((socio, idx) => (
                            <div key={idx} style={{ background: 'rgba(255,255,255,0.1)', padding: '0.75rem', borderRadius: '8px', textAlign: 'center' }}>
                              <div style={{ fontWeight: 'bold', marginBottom: '0.5rem', fontSize: '0.85rem' }}>{socio}</div>
                              <div style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#10b981' }}>
                                {formatMoney(gananciaPorSocioAcumulada)}
                              </div>
                              <div style={{ fontSize: '0.7rem', color: '#888', marginTop: '0.25rem' }}>
                                de {formatMoney(gananciaPorSocioProyectada)}
                              </div>
                              {/* Barra de progreso */}
                              <div style={{ height: '4px', background: '#333', borderRadius: '2px', marginTop: '0.5rem', overflow: 'hidden' }}>
                                <div style={{
                                  height: '100%',
                                  width: `${Math.min(progreso, 100)}%`,
                                  background: 'linear-gradient(90deg, #10b981, #34d399)',
                                  borderRadius: '2px',
                                  transition: 'width 0.3s ease'
                                }} />
                              </div>
                            </div>
                          ))}
                        </div>
                        {/* Resumen */}
                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem', paddingTop: '0.75rem', borderTop: '1px solid rgba(255,255,255,0.1)' }}>
                          <div>
                            <label style={{ fontSize: '0.75rem', color: '#888' }}>Interés Total Acumulado</label>
                            <div style={{ fontSize: '1rem', fontWeight: 'bold', color: '#3b82f6' }}>{formatMoney(interesPagado)}</div>
                          </div>
                          <div>
                            <label style={{ fontSize: '0.75rem', color: '#888' }}>Ganancia Cobrador ({selectedPrestamo.porcentajeCobrador || 0}%)</label>
                            <div style={{ fontSize: '1rem', fontWeight: 'bold', color: '#f59e0b' }}>{formatMoney(gananciaCobrador)}</div>
                          </div>
                          <div>
                            <label style={{ fontSize: '0.75rem', color: '#888' }}>Interés Neto Socios</label>
                            <div style={{ fontSize: '1rem', fontWeight: 'bold', color: '#10b981' }}>{formatMoney(interesNetoAcumulado)}</div>
                          </div>
                        </div>
                      </>
                    );
                  })()}
                </div>


                {selectedPrestamo.esCongelado && (
                  <div className="detail-item" style={{ gridColumn: '1 / -1' }}>
                    <span className="badge" style={{ background: '#0ea5e9', color: 'white', padding: '0.5rem 1rem' }}>❄️ Préstamo Congelado - Solo intereses</span>
                  </div>
                )}
                {selectedPrestamo.esCongelado && selectedPrestamo.estadoPrestamo !== 'Pagado' && (
                  <div style={{ gridColumn: '1 / -1', padding: '1rem', background: 'rgba(14, 165, 233, 0.1)', borderRadius: '8px', border: '1px solid #0ea5e9' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
                      <strong>💰 Abonar al Capital</strong>
                      <span>Capital adeudado: <strong style={{ color: '#ef4444' }}>{formatMoney(selectedPrestamo.montoPrestado)}</strong></span>
                    </div>
                    <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                      <input
                        type="number"
                        placeholder="Monto abono..."
                        min="1"
                        max={selectedPrestamo.montoPrestado}
                        id="abonoCapitalInput"
                        style={{ flex: 1, padding: '0.5rem' }}
                      />
                      <button
                        className="btn btn-primary btn-sm"
                        onClick={async () => {
                          const input = document.getElementById('abonoCapitalInput') as HTMLInputElement;
                          const monto = Number(input?.value || 0);
                          if (monto <= 0) { showToast('Ingrese un monto válido', 'warning'); return; }
                          if (monto > selectedPrestamo.montoPrestado) { showToast('El abono no puede ser mayor al capital', 'warning'); return; }
                          try {
                            const result = await pagosApi.abonoCapital(selectedPrestamo.id, monto);
                            showToast(`Abono aplicado. Nuevo capital: ${formatMoney(result.nuevoCapital)}`, 'success');
                            // Refresh data
                            loadData();
                            setShowDetalleModal(false);
                          } catch (e: any) { showToast(e.message || 'Error', 'error'); }
                        }}
                      >Aplicar Abono</button>
                    </div>
                  </div>
                )}
                <div style={{ marginTop: '1rem', gridColumn: '1 / -1', display: 'flex', gap: '0.5rem' }}>
                  <button className="btn btn-primary btn-sm" onClick={() => { setShowDetalleModal(false); openEditPrestamo(selectedPrestamo); }}>✏️ Editar Préstamo</button>
                  <button className="btn btn-secondary btn-sm" onClick={async () => {
                    try {
                      await cobrosApi.enviarBalanceSms(selectedPrestamo.id);
                      showToast('SMS de balance enviado', 'success');
                    } catch (e: any) { showToast(e.message || 'Error al enviar SMS', 'error'); }
                  }}>📱 Enviar Balance SMS</button>
                </div>
              </div>
              <div className="progress-bar" style={{ margin: '1rem 0' }}><div className="progress-fill" style={{ width: `${(selectedPrestamo.totalPagado / selectedPrestamo.montoTotal) * 100}%` }}></div></div>

              <h4 style={{ marginTop: '1.5rem', marginBottom: '0.5rem' }}>📅 Plan de Cuotas</h4>
              <div className="table-container" style={{ maxHeight: '280px', overflow: 'auto' }}>
                <table>
                  <thead>
                    <tr>
                      <th style={{ width: '40px' }}>✓</th>
                      <th>#</th>
                      <th>Fecha Cobro</th>
                      <th>Fecha Pago</th>
                      <th>Valor Cuota</th>
                      <th>Pagado</th>
                      <th>Pendiente</th>
                      <th>Estado</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {cuotasDetalle.map((c, idx) => {
                      // Resaltar la próxima cuota a pagar (primera no pagada)
                      const esSiguiente = c.estadoCuota !== 'Pagada' && !cuotasDetalle.slice(0, idx).some(prev => prev.estadoCuota !== 'Pagada');
                      return (
                        <tr key={c.id} style={{
                          opacity: c.estadoCuota === 'Pagada' ? 0.6 : 1,
                          background: esSiguiente ? 'rgba(59,130,246,0.15)' : c.estadoCuota === 'Vencida' ? 'rgba(239,68,68,0.1)' : undefined,
                          borderLeft: esSiguiente ? '3px solid #3b82f6' : undefined
                        }}>
                          <td>
                            <input
                              type="checkbox"
                              checked={c.estadoCuota === 'Pagada'}
                              disabled={currentUser?.rol !== 'Socio'}
                              onChange={async (e) => {
                                try {
                                  await cobrosApi.marcarCobrado(c.id, e.target.checked);
                                  showToast(e.target.checked ? 'Cuota marcada como pagada' : 'Marca removida', 'success');
                                  const cuotas = await cuotasApi.getByPrestamo(selectedPrestamo.id);
                                  setCuotasDetalle(cuotas);
                                  loadData();
                                } catch { showToast('Error al marcar cuota', 'error'); }
                              }}
                              style={{ width: '18px', height: '18px', cursor: currentUser?.rol === 'Socio' ? 'pointer' : 'not-allowed', opacity: currentUser?.rol !== 'Socio' ? 0.5 : 1 }}
                            />
                          </td>
                          <td>{esSiguiente ? <strong>→ {c.numeroCuota}</strong> : c.numeroCuota}</td>
                          <td>{formatDate(c.fechaCobro)}</td>
                          <td style={{ color: c.fechaPago ? '#10b981' : '#666' }}>{c.fechaPago ? formatDate(c.fechaPago) : '-'}</td>
                          <td className="money">{formatMoney(c.montoCuota)}</td>
                          <td className="money" style={{ color: '#10b981' }}>{formatMoney(c.montoPagado)}</td>
                          <td className="money" style={{ color: c.saldoPendiente > 0 ? '#ef4444' : '#10b981' }}>{formatMoney(c.saldoPendiente)}</td>
                          <td>
                            <span className={`badge ${c.estadoCuota === 'Pagada' ? 'badge-green' : c.estadoCuota === 'Vencida' ? 'badge-red' : c.estadoCuota === 'Parcial' ? 'badge-orange' : 'badge-gray'}`}>
                              {c.estadoCuota === 'Pagada' ? '✅ Pagada' : c.estadoCuota === 'Vencida' ? '⚠️ Vencida' : c.estadoCuota === 'Parcial' ? '🔄 Parcial' : '⏳ Pendiente'}
                            </span>
                          </td>
                          <td>
                            {c.estadoCuota !== 'Pagada' && (
                              <button className="btn btn-primary btn-sm" onClick={() => openPagoModal(c)}>
                                💰 Pagar
                              </button>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              {pagosDetalle.length > 0 && (
                <>
                  <h4 style={{ marginTop: '1.5rem', marginBottom: '0.5rem' }}>💵 Historial de Pagos</h4>
                  <div className="table-container" style={{ maxHeight: '150px', overflow: 'auto' }}>
                    <table>
                      <thead><tr><th>Fecha</th><th>Monto</th><th>Método</th><th>Cuota</th></tr></thead>
                      <tbody>{pagosDetalle.map(p => (
                        <tr key={p.id}>
                          <td>{formatDate(p.fechaPago)}</td>
                          <td className="money" style={{ color: '#10b981' }}>{formatMoney(p.montoPago)}</td>
                          <td>{p.metodoPago || 'Efectivo'}</td>
                          <td>{p.numeroCuota ? `#${p.numeroCuota}` : '-'}</td>
                        </tr>
                      ))}</tbody>
                    </table>
                  </div>
                </>
              )}
            </div>
            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setShowDetalleModal(false)}>Cerrar</button>
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
                <div style={{ padding: '0.5rem 0.75rem', background: 'rgba(16,185,129,0.1)', borderRadius: '6px', marginTop: '0.5rem', fontSize: '0.85rem', color: '#059669', border: '1px solid rgba(16,185,129,0.2)' }}>
                  💡 <strong>Nota:</strong> Puede pagar más del saldo pendiente. El excedente se aplicará automáticamente a las siguientes cuotas.
                </div>
                {pagoForm.montoPago > selectedCuota.saldoPendiente && (
                  <div style={{ padding: '0.5rem 0.75rem', background: 'rgba(59,130,246,0.1)', borderRadius: '6px', marginTop: '0.5rem', fontSize: '0.85rem', color: '#3b82f6', border: '1px solid rgba(59,130,246,0.2)' }}>
                    ✅ El excedente de <strong>{formatMoney(pagoForm.montoPago - selectedCuota.saldoPendiente)}</strong> se aplicará automáticamente a cuotas futuras.
                  </div>
                )}
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

      {/* Modal Nuevo Aportador Externo */}
      {showAportadorModal && (
        <div className="modal-overlay" onClick={() => setShowAportadorModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <div className="modal-header"><h2>{editingAportadorId ? 'Editar Aportador' : 'Nuevo Aportador Externo'}</h2><button className="modal-close" onClick={() => setShowAportadorModal(false)}>×</button></div>
            <form onSubmit={handleCreateAportador}>
              <div className="modal-body">
                <div className="form-grid">
                  <div className="form-group full-width"><label>Nombre *</label><input type="text" required value={aportadorForm.nombre} onChange={e => setAportadorForm({ ...aportadorForm, nombre: e.target.value })} /></div>
                  <div className="form-group"><label>Teléfono</label><input type="tel" value={aportadorForm.telefono || ''} onChange={e => setAportadorForm({ ...aportadorForm, telefono: e.target.value })} /></div>
                  <div className="form-group"><label>Email</label><input type="email" value={aportadorForm.email || ''} onChange={e => setAportadorForm({ ...aportadorForm, email: e.target.value })} /></div>
                  <div className="form-group"><label>Capital Aportado *</label><input type="number" min="0" required value={aportadorForm.montoTotalAportado || 0} onChange={e => setAportadorForm({ ...aportadorForm, montoTotalAportado: Number(e.target.value) })} /></div>
                  <div className="form-group"><label>Tasa Interés (%)</label><input type="number" min="0" step="0.5" value={aportadorForm.tasaInteres} onChange={e => setAportadorForm({ ...aportadorForm, tasaInteres: Number(e.target.value) })} /></div>
                  <div className="form-group"><label>Días para Pago</label><input type="number" min="1" value={aportadorForm.diasParaPago} onChange={e => setAportadorForm({ ...aportadorForm, diasParaPago: Number(e.target.value) })} /></div>
                  <div className="form-group full-width"><label>Notas</label><textarea value={aportadorForm.notas || ''} onChange={e => setAportadorForm({ ...aportadorForm, notas: e.target.value })} rows={2}></textarea></div>
                </div>
              </div>
              <div className="modal-footer"><button type="button" className="btn btn-secondary" onClick={() => setShowAportadorModal(false)}>Cancelar</button><button type="submit" className="btn btn-primary">{editingAportadorId ? 'Guardar Cambios' : 'Crear'}</button></div>
            </form>
          </div>
        </div>
      )}

      {/* SMS Campaign Modal */}
      {showSmsCampaignModal && (
        <div className="modal-overlay" onClick={() => setShowSmsCampaignModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <div className="modal-header"><h2>Nueva Campaña SMS</h2><button className="modal-close" onClick={() => setShowSmsCampaignModal(false)}>×</button></div>
            <form onSubmit={handleCreateSmsCampaign}>
              <div className="modal-body">
                <div className="form-grid">
                  <div className="form-group full-width"><label>Nombre *</label><input type="text" required value={smsCampaignForm.nombre} onChange={e => setSmsCampaignForm({ ...smsCampaignForm, nombre: e.target.value })} placeholder="Recordatorio de pago" /></div>
                  <div className="form-group full-width"><label>Mensaje *</label><textarea required value={smsCampaignForm.mensaje} onChange={e => setSmsCampaignForm({ ...smsCampaignForm, mensaje: e.target.value })} placeholder="Hola {cliente}, tu cuota de {monto} fue registrada. Cuotas pagadas: {cuotasPagadas}, Restantes: {cuotasRestantes}. Próxima: {proximaCuota} el {fechaProxima}." rows={3} /></div>
                  <div className="form-group"><label>Tipo Destinatario</label>
                    <select value={smsCampaignForm.tipoDestinatario} onChange={e => setSmsCampaignForm({ ...smsCampaignForm, tipoDestinatario: e.target.value })}>
                      <option value="CuotasHoy">Cuotas de Hoy</option>
                      <option value="CuotasVencidas">Cuotas Vencidas</option>
                      <option value="ProximasVencer">Próximas a Vencer</option>
                      <option value="TodosClientesActivos">Todos los Clientes</option>
                      <option value="ConfirmacionPago">✅ Confirmación de Pago</option>
                    </select>
                  </div>
                  <div className="form-group"><label>Veces por Día</label><input type="number" min="1" max="3" value={smsCampaignForm.vecesPorDia} onChange={e => setSmsCampaignForm({ ...smsCampaignForm, vecesPorDia: Number(e.target.value) })} /></div>
                  <div className="form-group"><label>Activo</label>
                    <select value={smsCampaignForm.activo ? 'true' : 'false'} onChange={e => setSmsCampaignForm({ ...smsCampaignForm, activo: e.target.value === 'true' })}>
                      <option value="true">Sí</option>
                      <option value="false">No</option>
                    </select>
                  </div>
                </div>
              </div>
              <div className="modal-footer"><button type="button" className="btn btn-secondary" onClick={() => setShowSmsCampaignModal(false)}>Cancelar</button><button type="submit" className="btn btn-primary">Crear Campaña</button></div>
            </form>
          </div>
        </div>
      )}

      {/* Password Change Modal */}
      {showPasswordModal && (
        <div className="modal-overlay" onClick={() => setShowPasswordModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '400px' }}>
            <div className="modal-header"><h2>🔑 Cambiar Contraseña</h2><button className="modal-close" onClick={() => setShowPasswordModal(false)}>×</button></div>
            <form onSubmit={handleChangePassword}>
              <div className="modal-body">
                <div className="form-group">
                  <label>Nueva Contraseña *</label>
                  <input type="password" required minLength={6} value={newPassword} onChange={e => setNewPassword(e.target.value)} placeholder="Mínimo 6 caracteres" />
                </div>
              </div>
              <div className="modal-footer"><button type="button" className="btn btn-secondary" onClick={() => setShowPasswordModal(false)}>Cancelar</button><button type="submit" className="btn btn-primary">Guardar</button></div>
            </form>
          </div>
        </div>
      )}

      {/* Costo Modal */}
      {showCostoModal && (
        <div className="modal-overlay" onClick={() => setShowCostoModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '500px' }}>
            <div className="modal-header">
              <h2>{editingCostoId ? '✏️ Editar Costo' : '➕ Nuevo Costo Operativo'}</h2>
              <button className="modal-close" onClick={() => setShowCostoModal(false)}>×</button>
            </div>
            <div className="modal-body">
              <div className="form-group">
                <label>Nombre *</label>
                <input
                  type="text"
                  required
                  value={costoForm.nombre}
                  onChange={e => setCostoForm({ ...costoForm, nombre: e.target.value })}
                  placeholder="Ej: Salario Cobrador Juan"
                />
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label>Monto *</label>
                  <input
                    type="number"
                    required
                    min="1"
                    value={costoForm.monto || ''}
                    onChange={e => setCostoForm({ ...costoForm, monto: Number(e.target.value) })}
                    placeholder="500000"
                  />
                </div>
                <div className="form-group">
                  <label>Frecuencia</label>
                  <select
                    value={costoForm.frecuencia}
                    onChange={e => setCostoForm({ ...costoForm, frecuencia: e.target.value })}
                  >
                    <option value="Mensual">Mensual</option>
                    <option value="Quincenal">Quincenal</option>
                    <option value="Único">Único</option>
                  </select>
                </div>
              </div>
              <div className="form-group">
                <label>Descripción</label>
                <textarea
                  value={costoForm.descripcion || ''}
                  onChange={e => setCostoForm({ ...costoForm, descripcion: e.target.value })}
                  placeholder="Descripción opcional del costo..."
                  rows={2}
                  style={{ width: '100%', padding: '0.5rem', borderRadius: '4px', border: '1px solid #333', background: '#1a1a1a', color: 'white' }}
                />
              </div>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn btn-secondary" onClick={() => setShowCostoModal(false)}>Cancelar</button>
              <button type="button" className="btn btn-primary" onClick={handleSaveCosto}>
                {editingCostoId ? 'Guardar Cambios' : 'Crear Costo'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Métricas de Cobradores Tab */}
      {activeTab === 'metricas' && (
        <div className="section">
          <MetricasCobradores />
        </div>
      )}
    </div>
  );
}

export default App;
