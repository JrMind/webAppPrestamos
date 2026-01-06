import { useState, useEffect, useCallback, useRef } from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, Legend } from 'recharts';
import { clientesApi, prestamosApi, cuotasApi, pagosApi, dashboardApi, authApi, usuariosApi, cobrosApi, aportesApi, getAuthToken, capitalApi, prestamosConFuentesApi, aportadoresExternosApi, smsCampaignsApi, smsHistoryApi, cobrosDelMesApi, miBalanceApi } from './api';
import { Cliente, CreateClienteDto, CreatePrestamoDto, CreatePagoDto, Cuota, DashboardMetricas, Pago, Prestamo, Usuario, Cobrador, CobrosHoy, BalanceSocio, FuenteCapital, BalanceCapital, AportadorExterno, CreateAportadorExternoDto, SmsCampaign, CreateSmsCampaignDto, SmsHistory, CobrosDelMes, MiBalance } from './types';
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
  const [activeTab, setActiveTab] = useState<'prestamos' | 'clientes' | 'cuotas' | 'cobros' | 'tareas' | 'sms' | 'smshistory' | 'socios' | 'balance' | 'usuarios' | 'aportadores'>('prestamos');

  // Data states
  const [metricas, setMetricas] = useState<DashboardMetricas | null>(null);
  const [prestamos, setPrestamos] = useState<Prestamo[]>([]);
  const [clientes, setClientes] = useState<Cliente[]>([]);
  const [cobrosHoy, setCobrosHoy] = useState<CobrosHoy | null>(null);
  const [balanceSocios, setBalanceSocios] = useState<BalanceSocio[]>([]);
  const [aportadoresExternos, setAportadoresExternos] = useState<AportadorExterno[]>([]);
  const [showAportadorModal, setShowAportadorModal] = useState(false);
  const [aportadorForm, setAportadorForm] = useState<CreateAportadorExternoDto>({ nombre: '', telefono: '', email: '', tasaInteres: 3, diasParaPago: 30, notas: '' });
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);

  // New feature states
  const [smsCampaigns, setSmsCampaigns] = useState<SmsCampaign[]>([]);
  const [smsHistoryData, setSmsHistoryData] = useState<SmsHistory[]>([]);
  const [cobrosDelMes, setCobrosDelMes] = useState<CobrosDelMes | null>(null);
  const [miBalance, setMiBalance] = useState<MiBalance | null>(null);
  const [showSmsCampaignModal, setShowSmsCampaignModal] = useState(false);
  const [smsCampaignForm, setSmsCampaignForm] = useState<CreateSmsCampaignDto>({
    nombre: '', mensaje: '', activo: true, diasEnvio: '[]', horasEnvio: '[]', vecesPorDia: 1, tipoDestinatario: 'CuotasHoy'
  });

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
  useEffect(() => { if (activeTab === 'aportadores') loadAportadoresExternos(); }, [activeTab]);
  useEffect(() => { if (activeTab === 'sms') loadSmsCampaigns(); }, [activeTab]);
  useEffect(() => { if (activeTab === 'smshistory') loadSmsHistory(); }, [activeTab]);
  useEffect(() => { if (activeTab === 'tareas') loadCobrosDelMes(); }, [activeTab]);
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

  const loadCobrosDelMes = async () => {
    try {
      const data = await cobrosDelMesApi.getCobrosDelMes();
      setCobrosDelMes(data);
    } catch (error) { console.error('Error loading cobros del mes:', error); }
  };

  const loadMiBalance = async () => {
    try {
      const data = await miBalanceApi.getMiBalance(currentUser?.id);
      setMiBalance(data);
    } catch (error) { console.error('Error loading balance:', error); }
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
      await aportadoresExternosApi.create(aportadorForm);
      showToast('Aportador creado exitosamente', 'success');
      setShowAportadorModal(false);
      setAportadorForm({ nombre: '', telefono: '', email: '', tasaInteres: 3, diasParaPago: 30, notas: '' });
      loadAportadoresExternos();
    } catch (error: unknown) {
      showToast(error instanceof Error ? error.message : 'Error', 'error');
    }
  };

  const handleDeleteAportador = async (id: number) => {
    if (!confirm('¿Está seguro de eliminar este aportador?')) return;
    try {
      await aportadoresExternosApi.delete(id);
      showToast('Aportador eliminado', 'success');
      loadAportadoresExternos();
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
      porcentajeCobrador: 5,
      diaSemana: undefined
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

    let numeroCuotas = 0;
    // Ajuste específico de negocio para Meses (Sync con Backend)
    if (prestamoForm.unidadDuracion === 'Meses') {
      if (prestamoForm.frecuenciaPago === 'Semanal') numeroCuotas = prestamoForm.duracion * 4;
      else if (prestamoForm.frecuenciaPago === 'Quincenal') numeroCuotas = prestamoForm.duracion * 2;
      else if (prestamoForm.frecuenciaPago === 'Mensual') numeroCuotas = prestamoForm.duracion;
    }

    if (numeroCuotas === 0) {
      numeroCuotas = Math.max(1, Math.ceil(diasTotales / diasEntreCuotas));
    }
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

  const openEditPrestamo = async (prestamo: Prestamo) => {
    setEditMode(true);
    setEditingPrestamoId(prestamo.id);

    // Cargar cliente
    const cliente = clientes.find(c => c.id === prestamo.clienteId);
    if (cliente) selectCliente(cliente);

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
      diaSemana: prestamo.diaSemana
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
        // MODO EDICIÓN
        await prestamosApi.updateCompleto(editingPrestamoId, {
          ...prestamoForm,
          numeroCuotas: prestamoForm.duracion, // Asumiendo duracion = numeroCuotas
          // La fecha del formulario se envía como FechaPrestamo Y como FechaPrimerPago si queremos forzar el inicio
          // Para que el servicio sepa que queremos iniciar en esa fecha, la pasamos como FechaPrimerPago
          fechaPrimerPago: prestamoForm.fechaPrestamo // Enviamos la fecha seleccionada como inicio explicito
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
          <div className="kpi-card"><div className="kpi-header"><span className="kpi-title">Intereses Ganados</span></div><span className="kpi-value" style={{ color: '#10b981' }}>{formatMoney(metricas?.totalGanadoIntereses || 0)}</span></div>
          <div className="kpi-card"><div className="kpi-header"><span className="kpi-title">Intereses Proyectados</span></div><span className="kpi-value" style={{ color: '#3b82f6' }}>{formatMoney(metricas?.interesesProyectados || 0)}</span></div>
          <div className="kpi-card"><div className="kpi-header"><span className="kpi-title">Capital Proyectado</span></div><span className="kpi-value" style={{ color: '#8b5cf6' }}>{formatMoney((metricas?.totalPrestado || 0) + (metricas?.interesesProyectados || 0))}</span></div>
          <div className="kpi-card"><div className="kpi-header"><span className="kpi-title">Cuotas Vencidas</span></div><span className="kpi-value">{metricas?.cuotasVencidasHoy || 0}</span></div>
        </div>

        {/* Flujo de Capital */}
        <div className="kpi-grid" style={{ marginTop: '1rem' }}>
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
            <button className={`tab ${activeTab === 'tareas' ? 'active' : ''}`} onClick={() => setActiveTab('tareas')}>📋 Tareas</button>
            <button className={`tab ${activeTab === 'cobros' ? 'active' : ''}`} onClick={() => setActiveTab('cobros')}>Cobros Día</button>
            <button className={`tab ${activeTab === 'socios' ? 'active' : ''}`} onClick={() => setActiveTab('socios')}>Socios</button>
            <button className={`tab ${activeTab === 'balance' ? 'active' : ''}`} onClick={() => setActiveTab('balance')}>💰 Mi Balance</button>
            <button className={`tab ${activeTab === 'sms' ? 'active' : ''}`} onClick={() => setActiveTab('sms')}>📱 SMS</button>
            <button className={`tab ${activeTab === 'smshistory' ? 'active' : ''}`} onClick={() => setActiveTab('smshistory')}>📨 Historial</button>
            <button className={`tab ${activeTab === 'usuarios' ? 'active' : ''}`} onClick={() => setActiveTab('usuarios')}>Usuarios</button>
            <button className={`tab ${activeTab === 'aportadores' ? 'active' : ''}`} onClick={() => setActiveTab('aportadores')}>Aportadores</button>
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
                    <td><div className="actions"><button className="btn btn-secondary btn-sm" onClick={() => openDetalle(p)}>Ver</button><button className="btn btn-primary btn-sm" onClick={() => openEditPrestamo(p)}>✏️</button><button className="btn btn-danger btn-sm" onClick={() => handleDeletePrestamo(p.id)}>✕</button></div></td>
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

          {/* Aportadores Externos Tab */}
          {activeTab === 'aportadores' && (
            <div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '1rem' }}>
                <button className="btn btn-primary" onClick={() => setShowAportadorModal(true)}>+ Nuevo Aportador</button>
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
                        <button className="btn btn-danger" style={{ padding: '0.25rem 0.5rem', fontSize: '0.75rem' }} onClick={() => handleDeleteAportador(a.id)}>Eliminar</button>
                      </td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
            </div>
          )}

          {/* Tareas Diarias Tab */}
          {activeTab === 'tareas' && cobrosDelMes && (
            <div>
              <div className="kpi-grid" style={{ marginBottom: '1rem' }}>
                <div className="kpi-card"><span className="kpi-title">📅 Hoy ({cobrosDelMes.resumen.totalCuotasHoy})</span><span className="kpi-value">{formatMoney(cobrosDelMes.resumen.montoTotalHoy)}</span></div>
                <div className="kpi-card" style={{ borderColor: '#ef4444' }}><span className="kpi-title">⚠️ Vencidas ({cobrosDelMes.resumen.totalCuotasVencidas})</span><span className="kpi-value" style={{ color: '#ef4444' }}>{formatMoney(cobrosDelMes.resumen.montoTotalVencido)}</span></div>
                <div className="kpi-card" style={{ borderColor: '#3b82f6' }}><span className="kpi-title">📆 Próximas ({cobrosDelMes.resumen.totalCuotasProximas})</span><span className="kpi-value" style={{ color: '#3b82f6' }}>{formatMoney(cobrosDelMes.resumen.montoTotalProximas)}</span></div>
              </div>
              <h4 style={{ color: '#10b981', margin: '1rem 0 0.5rem' }}>📅 Cobros de Hoy</h4>
              <div className="table-container">
                <table><thead><tr><th>✓</th><th>Cliente</th><th>Cuota</th><th>Monto</th><th>Cobrador</th></tr></thead>
                  <tbody>{cobrosDelMes.cuotasHoy.map(c => (
                    <tr key={c.id} style={{ opacity: c.cobrado ? 0.6 : 1 }}>
                      <td><input type="checkbox" checked={c.cobrado} onChange={e => handleMarcarCobrado(c.id, e.target.checked)} /></td>
                      <td><strong>{c.clienteNombre}</strong><div style={{ fontSize: '0.75rem' }}>{c.clienteTelefono}</div></td>
                      <td>#{c.numeroCuota}</td>
                      <td className="money">{formatMoney(c.saldoPendiente)}</td>
                      <td>{c.cobradorNombre || '-'}</td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
              {cobrosDelMes.cuotasVencidas.length > 0 && (
                <>
                  <h4 style={{ color: '#ef4444', margin: '1rem 0 0.5rem' }}>⚠️ Cuotas Vencidas</h4>
                  <div className="table-container">
                    <table><thead><tr><th>✓</th><th>Cliente</th><th>Fecha</th><th>Días</th><th>Monto</th></tr></thead>
                      <tbody>{cobrosDelMes.cuotasVencidas.map(c => (
                        <tr key={c.id} style={{ background: 'rgba(239,68,68,0.1)' }}>
                          <td><input type="checkbox" checked={c.cobrado} onChange={e => handleMarcarCobrado(c.id, e.target.checked)} /></td>
                          <td><strong>{c.clienteNombre}</strong></td>
                          <td style={{ color: '#ef4444' }}>{formatDate(c.fechaCobro)}</td>
                          <td><span className="badge badge-red">{Math.abs(c.diasParaVencer)}d</span></td>
                          <td className="money">{formatMoney(c.saldoPendiente)}</td>
                        </tr>
                      ))}</tbody>
                    </table>
                  </div>
                </>
              )}
              <h4 style={{ color: '#3b82f6', margin: '1rem 0 0.5rem' }}>📆 Próximas del Mes</h4>
              <div className="table-container">
                <table><thead><tr><th>Cliente</th><th>Fecha</th><th>En</th><th>Monto</th><th>Cobrador</th></tr></thead>
                  <tbody>{cobrosDelMes.cuotasProximas.map(c => (
                    <tr key={c.id}>
                      <td><strong>{c.clienteNombre}</strong></td>
                      <td>{formatDate(c.fechaCobro)}</td>
                      <td><span className="badge badge-blue">{c.diasParaVencer}d</span></td>
                      <td className="money">{formatMoney(c.saldoPendiente)}</td>
                      <td>{c.cobradorNombre || '-'}</td>
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
                  <div className="form-group"><label>Monto ($) *</label><input type="number" min="50" required value={prestamoForm.montoPrestado || ''} onChange={e => setPrestamoForm({ ...prestamoForm, montoPrestado: Number(e.target.value) })} /></div>
                  <div className="form-group"><label>Tasa Interés (%) *</label><input type="number" min="0" step="0.1" required value={prestamoForm.tasaInteres} onChange={e => setPrestamoForm({ ...prestamoForm, tasaInteres: Number(e.target.value) })} /></div>
                  <div className="form-group"><label>Frecuencia *</label><select value={prestamoForm.frecuenciaPago} onChange={e => setPrestamoForm({ ...prestamoForm, frecuenciaPago: e.target.value })}><option>Diario</option><option>Semanal</option><option>Quincenal</option><option>Mensual</option></select></div>
                  <div className="form-group"><label>Duración *</label><div style={{ display: 'flex', gap: '0.5rem' }}><input type="number" min="1" required value={prestamoForm.duracion} onChange={e => setPrestamoForm({ ...prestamoForm, duracion: Number(e.target.value) })} style={{ width: '80px' }} /><select value={prestamoForm.unidadDuracion} onChange={e => setPrestamoForm({ ...prestamoForm, unidadDuracion: e.target.value })}><option>Dias</option><option>Semanas</option><option>Quincenas</option><option>Meses</option></select></div></div>
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
                  <div className="form-group"><label>% Cobrador</label><input type="number" min="0" max="15" step="0.5" value={prestamoForm.porcentajeCobrador} onChange={e => setPrestamoForm({ ...prestamoForm, porcentajeCobrador: Number(e.target.value) })} /></div>
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

                {preview && <div className="preview-card"><h4>Vista Previa</h4><div className="preview-grid"><div className="preview-item"><span>Cuotas</span><strong>{preview.numeroCuotas}</strong></div><div className="preview-item"><span>Intereses</span><strong style={{ color: '#10b981' }}>{formatMoney(preview.montoIntereses)}</strong></div><div className="preview-item"><span>Total</span><strong>{formatMoney(preview.montoTotal)}</strong></div><div className="preview-item"><span>Por Cuota</span><strong style={{ color: '#3b82f6' }}>{formatMoney(preview.montoCuota)}</strong></div></div></div>}
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
                <div className="detail-item"><label>Total a Pagar</label><span>{formatMoney(selectedPrestamo.montoTotal)}</span></div>
                <div className="detail-item"><label>Interés</label><span style={{ color: '#10b981' }}>{selectedPrestamo.tasaInteres}% ({selectedPrestamo.tipoInteres})</span></div>
                <div className="detail-item"><label>Frecuencia</label><span className="badge badge-blue">{selectedPrestamo.frecuenciaPago}</span></div>
                <div className="detail-item"><label>Cuotas</label><span>{selectedPrestamo.cuotasPagadas} / {selectedPrestamo.numeroCuotas}</span></div>
                <div className="detail-item"><label>Pagado</label><span style={{ color: '#10b981' }}>{formatMoney(selectedPrestamo.totalPagado)}</span></div>
                <div className="detail-item"><label>Pendiente</label><span style={{ color: '#ef4444' }}>{formatMoney(selectedPrestamo.saldoPendiente)}</span></div>
                <div className="detail-item"><label>Estado</label><span className={`badge ${selectedPrestamo.estadoPrestamo === 'Activo' ? 'badge-green' : selectedPrestamo.estadoPrestamo === 'Pagado' ? 'badge-blue' : 'badge-red'}`}>{selectedPrestamo.estadoPrestamo}</span></div>
                <div className="detail-item"><label>Cobrador</label><span>{selectedPrestamo.cobradorNombre || 'No asignado'}</span></div>
                <div style={{ marginTop: '1rem', gridColumn: '1 / -1' }}>
                  <button className="btn btn-primary btn-sm" onClick={() => { setShowDetalleModal(false); openEditPrestamo(selectedPrestamo); }}>✏️ Editar Préstamo</button>
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
                      <th>Fecha</th>
                      <th>Valor Cuota</th>
                      <th>Pagado</th>
                      <th>Pendiente</th>
                      <th>Estado</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {cuotasDetalle.map(c => (
                      <tr key={c.id} style={{ opacity: c.estadoCuota === 'Pagada' ? 0.6 : 1, background: c.estadoCuota === 'Vencida' ? 'rgba(239,68,68,0.1)' : undefined }}>
                        <td>
                          <input
                            type="checkbox"
                            checked={c.estadoCuota === 'Pagada'}
                            onChange={async (e) => {
                              try {
                                await cobrosApi.marcarCobrado(c.id, e.target.checked);
                                showToast(e.target.checked ? 'Cuota marcada como pagada' : 'Marca removida', 'success');
                                // Recargar cuotas
                                const cuotas = await cuotasApi.getByPrestamo(selectedPrestamo.id);
                                setCuotasDetalle(cuotas);
                                // Recargar préstamos para actualizar totales
                                loadData();
                              } catch { showToast('Error al marcar cuota', 'error'); }
                            }}
                            style={{ width: '18px', height: '18px', cursor: 'pointer' }}
                          />
                        </td>
                        <td>{c.numeroCuota}</td>
                        <td>{formatDate(c.fechaCobro)}</td>
                        <td className="money">{formatMoney(c.montoCuota)}</td>
                        <td className="money" style={{ color: '#10b981' }}>{formatMoney(c.montoPagado)}</td>
                        <td className="money" style={{ color: c.saldoPendiente > 0 ? '#ef4444' : '#10b981' }}>{formatMoney(c.saldoPendiente)}</td>
                        <td>
                          <span className={`badge ${c.estadoCuota === 'Pagada' ? 'badge-green' : c.estadoCuota === 'Vencida' ? 'badge-red' : c.estadoCuota === 'Parcial' ? 'badge-orange' : 'badge-gray'}`}>
                            {c.estadoCuota}
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
                    ))}
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
            <div className="modal-header"><h2>Nuevo Aportador Externo</h2><button className="modal-close" onClick={() => setShowAportadorModal(false)}>×</button></div>
            <form onSubmit={handleCreateAportador}>
              <div className="modal-body">
                <div className="form-grid">
                  <div className="form-group full-width"><label>Nombre *</label><input type="text" required value={aportadorForm.nombre} onChange={e => setAportadorForm({ ...aportadorForm, nombre: e.target.value })} /></div>
                  <div className="form-group"><label>Teléfono</label><input type="tel" value={aportadorForm.telefono || ''} onChange={e => setAportadorForm({ ...aportadorForm, telefono: e.target.value })} /></div>
                  <div className="form-group"><label>Email</label><input type="email" value={aportadorForm.email || ''} onChange={e => setAportadorForm({ ...aportadorForm, email: e.target.value })} /></div>
                  <div className="form-group"><label>Tasa Interés (%)</label><input type="number" min="0" step="0.5" value={aportadorForm.tasaInteres} onChange={e => setAportadorForm({ ...aportadorForm, tasaInteres: Number(e.target.value) })} /></div>
                  <div className="form-group"><label>Días para Pago</label><input type="number" min="1" value={aportadorForm.diasParaPago} onChange={e => setAportadorForm({ ...aportadorForm, diasParaPago: Number(e.target.value) })} /></div>
                  <div className="form-group full-width"><label>Notas</label><textarea value={aportadorForm.notas || ''} onChange={e => setAportadorForm({ ...aportadorForm, notas: e.target.value })} rows={2}></textarea></div>
                </div>
              </div>
              <div className="modal-footer"><button type="button" className="btn btn-secondary" onClick={() => setShowAportadorModal(false)}>Cancelar</button><button type="submit" className="btn btn-primary">Crear</button></div>
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
                  <div className="form-group full-width"><label>Mensaje *</label><textarea required value={smsCampaignForm.mensaje} onChange={e => setSmsCampaignForm({ ...smsCampaignForm, mensaje: e.target.value })} placeholder="Hola {cliente}, recuerda tu cuota de {monto} para hoy." rows={3} /></div>
                  <div className="form-group"><label>Tipo Destinatario</label>
                    <select value={smsCampaignForm.tipoDestinatario} onChange={e => setSmsCampaignForm({ ...smsCampaignForm, tipoDestinatario: e.target.value })}>
                      <option value="CuotasHoy">Cuotas de Hoy</option>
                      <option value="CuotasVencidas">Cuotas Vencidas</option>
                      <option value="ProximasVencer">Próximas a Vencer</option>
                      <option value="TodosClientesActivos">Todos los Clientes</option>
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
    </div>
  );
}

export default App;
