import { useCallback, useEffect, useMemo, useState } from 'react';
import { prestamosApi, tableroApi } from '../api';
import { CuentaCobrador, MovimientoCuentaCobrador, NotaTablero, Prestamo, TipoMovimientoCuenta } from '../types';

const formatMoney = (amount: number): string =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(amount);

/** Fecha local de hoy en formato yyyy-MM-dd */
const hoyISO = (): string => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

const formatFecha = (iso: string): string => {
  const [y, m, d] = iso.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString('es-CO', { weekday: 'short', day: '2-digit', month: 'short', year: 'numeric' });
};

const diasEntre = (desdeISO: string, hastaISO: string): number => {
  const a = new Date(desdeISO.slice(0, 10) + 'T00:00:00');
  const b = new Date(hastaISO.slice(0, 10) + 'T00:00:00');
  return Math.round((b.getTime() - a.getTime()) / 86400000);
};

type EstadoNota = 'vencida' | 'hoy' | 'proxima' | 'sinfecha' | 'cumplida';

const estadoNota = (n: NotaTablero, hoy: string): EstadoNota => {
  if (n.cumplida) return 'cumplida';
  if (!n.fechaCompromiso) return 'sinfecha';
  const f = n.fechaCompromiso.slice(0, 10);
  if (f < hoy) return 'vencida';
  if (f === hoy) return 'hoy';
  return 'proxima';
};

const ORDEN_ESTADO: Record<EstadoNota, number> = { vencida: 0, hoy: 1, proxima: 2, sinfecha: 3, cumplida: 4 };

const ESTILO_ESTADO: Record<EstadoNota, { color: string; bg: string; label: string }> = {
  vencida: { color: '#ef4444', bg: 'rgba(239,68,68,0.10)', label: 'Vencida' },
  hoy: { color: '#f59e0b', bg: 'rgba(245,158,11,0.10)', label: 'Hoy' },
  proxima: { color: '#3b82f6', bg: 'transparent', label: 'Próxima' },
  sinfecha: { color: '#9ca3af', bg: 'transparent', label: 'Nota' },
  cumplida: { color: '#10b981', bg: 'transparent', label: 'Cumplida' },
};

const inputStyle: React.CSSProperties = {
  padding: '0.45rem 0.6rem', background: 'var(--bg-secondary)', border: '1px solid var(--border)',
  borderRadius: '6px', color: 'var(--text-primary)', fontSize: '0.875rem',
};
const th: React.CSSProperties = { padding: '0.65rem 0.9rem', textAlign: 'left', fontSize: '0.7rem', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', borderBottom: '1px solid var(--border)', background: 'var(--bg-secondary)' };
const td: React.CSSProperties = { padding: '0.65rem 0.9rem', fontSize: '0.875rem', color: 'var(--text-primary)', borderBottom: '1px solid var(--border)', verticalAlign: 'top' };
const pill = (color: string): React.CSSProperties => ({ display: 'inline-block', padding: '0.15rem 0.55rem', borderRadius: '999px', fontSize: '0.72rem', fontWeight: 700, color, border: `1px solid ${color}`, whiteSpace: 'nowrap' });
const iconBtn: React.CSSProperties = { background: 'none', border: '1px solid var(--border)', borderRadius: '6px', cursor: 'pointer', padding: '0.25rem 0.45rem', fontSize: '0.8rem', color: 'var(--text-primary)' };

interface Props {
  onVerPrestamo?: (prestamoId: number) => void;
}

export const Tablero = ({ onVerPrestamo }: Props) => {
  const [vista, setVista] = useState<'notas' | 'cobradores'>('notas');

  return (
    <div>
      <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
        <button className={`btn ${vista === 'notas' ? 'btn-primary' : 'btn-secondary'}`} onClick={() => setVista('notas')}>📝 Notas de préstamos</button>
        <button className={`btn ${vista === 'cobradores' ? 'btn-primary' : 'btn-secondary'}`} onClick={() => setVista('cobradores')}>💼 Cuentas de cobradores</button>
      </div>
      {vista === 'notas' ? <NotasPrestamos onVerPrestamo={onVerPrestamo} /> : <CuentasCobradores />}
    </div>
  );
};

// ─────────────────────────── Notas de préstamos ───────────────────────────

interface NotaForm {
  id?: number;
  prestamoId: number | null;
  contenido: string;
  fechaCompromiso: string;
  montoCompromiso: string;
}

const NOTA_VACIA: NotaForm = { prestamoId: null, contenido: '', fechaCompromiso: '', montoCompromiso: '' };

const NotasPrestamos = ({ onVerPrestamo }: Props) => {
  const [notas, setNotas] = useState<NotaTablero[]>([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filtroEstado, setFiltroEstado] = useState<'pendientes' | 'vencidas' | 'cumplidas' | 'todas'>('pendientes');
  const [filtroCobrador, setFiltroCobrador] = useState('todos');
  const [busqueda, setBusqueda] = useState('');
  const [form, setForm] = useState<NotaForm | null>(null);
  const hoy = hoyISO();

  const cargar = useCallback(async () => {
    try {
      setError(null);
      setNotas(await tableroApi.getNotas());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error cargando notas');
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => { cargar(); }, [cargar]);

  const cobradores = useMemo(() =>
    [...new Map(notas.map(n => [n.cobradorId != null ? String(n.cobradorId) : 'sin', n.cobradorNombre ?? 'Sin cobrador'])).entries()]
      .sort((a, b) => a[1].localeCompare(b[1])), [notas]);

  const pendientes = notas.filter(n => !n.cumplida);
  const resumen = {
    vencidas: pendientes.filter(n => estadoNota(n, hoy) === 'vencida'),
    hoy: pendientes.filter(n => estadoNota(n, hoy) === 'hoy'),
    proximas: pendientes.filter(n => estadoNota(n, hoy) === 'proxima'),
  };
  const sumaMontos = (lista: NotaTablero[]) => lista.reduce((s, n) => s + (n.montoCompromiso ?? 0), 0);

  const lista = notas
    .filter(n => {
      const e = estadoNota(n, hoy);
      if (filtroEstado === 'pendientes' && e === 'cumplida') return false;
      if (filtroEstado === 'vencidas' && e !== 'vencida') return false;
      if (filtroEstado === 'cumplidas' && e !== 'cumplida') return false;
      if (filtroCobrador !== 'todos' && (n.cobradorId != null ? String(n.cobradorId) : 'sin') !== filtroCobrador) return false;
      if (busqueda.trim()) {
        const q = busqueda.trim().toLowerCase();
        const texto = `#${n.prestamoId} ${n.clienteNombre} ${n.clienteCedula} ${n.contenido}`.toLowerCase();
        if (!texto.includes(q)) return false;
      }
      return true;
    })
    .sort((a, b) => {
      const ea = estadoNota(a, hoy), eb = estadoNota(b, hoy);
      if (ORDEN_ESTADO[ea] !== ORDEN_ESTADO[eb]) return ORDEN_ESTADO[ea] - ORDEN_ESTADO[eb];
      if (a.fechaCompromiso && b.fechaCompromiso && a.fechaCompromiso !== b.fechaCompromiso)
        return a.fechaCompromiso.localeCompare(b.fechaCompromiso);
      return b.fechaCreacion.localeCompare(a.fechaCreacion);
    });

  const toggleCumplida = async (n: NotaTablero) => {
    try { await tableroApi.marcarCumplida(n.id, !n.cumplida); await cargar(); }
    catch (e) { alert(e instanceof Error ? e.message : 'Error'); }
  };

  const eliminar = async (n: NotaTablero) => {
    if (!confirm(`¿Eliminar la nota del préstamo #${n.prestamoId} (${n.clienteNombre})?`)) return;
    try { await tableroApi.eliminarNota(n.id); await cargar(); }
    catch (e) { alert(e instanceof Error ? e.message : 'Error'); }
  };

  const editar = (n: NotaTablero) => setForm({
    id: n.id,
    prestamoId: n.prestamoId,
    contenido: n.contenido,
    fechaCompromiso: n.fechaCompromiso?.slice(0, 10) ?? '',
    montoCompromiso: n.montoCompromiso != null ? String(n.montoCompromiso) : '',
  });

  const tarjeta = (titulo: string, items: NotaTablero[], color: string, filtro?: typeof filtroEstado) => (
    <div className="kpi-card" style={{ borderLeft: `3px solid ${color}`, cursor: filtro ? 'pointer' : undefined }} onClick={() => filtro && setFiltroEstado(filtro)}>
      <div className="kpi-header"><span className="kpi-title">{titulo}</span></div>
      <span className="kpi-value" style={{ color }}>{items.length}</span>
      <span className="kpi-sub" style={{ marginTop: '0.4rem', color: '#999' }}>{formatMoney(sumaMontos(items))} pactados</span>
    </div>
  );

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '0.75rem', marginBottom: '1rem' }}>
        {tarjeta('⚠️ Compromisos vencidos', resumen.vencidas, '#ef4444', 'vencidas')}
        {tarjeta('📅 Pagan hoy', resumen.hoy, '#f59e0b')}
        {tarjeta('🗓️ Próximos', resumen.proximas, '#3b82f6')}
      </div>

      <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', alignItems: 'center', marginBottom: '0.75rem' }}>
        <select value={filtroEstado} onChange={e => setFiltroEstado(e.target.value as typeof filtroEstado)} style={inputStyle}>
          <option value="pendientes">Pendientes</option>
          <option value="vencidas">Solo vencidas</option>
          <option value="cumplidas">Cumplidas</option>
          <option value="todas">Todas</option>
        </select>
        {cobradores.length > 1 && (
          <select value={filtroCobrador} onChange={e => setFiltroCobrador(e.target.value)} style={inputStyle}>
            <option value="todos">Todos los cobradores</option>
            {cobradores.map(([id, nombre]) => <option key={id} value={id}>{nombre}</option>)}
          </select>
        )}
        <input placeholder="Buscar cliente, cédula, # préstamo o texto…" value={busqueda} onChange={e => setBusqueda(e.target.value)}
          style={{ ...inputStyle, flex: '1 1 220px', minWidth: 0 }} />
        <button className="btn btn-primary" onClick={() => setForm({ ...NOTA_VACIA })}>+ Nueva nota</button>
      </div>

      {error && <div className="empty-state" style={{ color: '#ef4444' }}>{error}</div>}
      {cargando ? (
        <div className="empty-state">Cargando notas…</div>
      ) : lista.length === 0 ? (
        <div className="empty-state">{notas.length === 0 ? 'Aún no hay notas. Crea la primera con “+ Nueva nota”.' : 'No hay notas con estos filtros.'}</div>
      ) : (
        <div className="table-container">
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th style={th}>Estado</th>
                <th style={th}>Préstamo / Cliente</th>
                <th style={th}>Nota</th>
                <th style={th}>Quedó de pagar</th>
                <th style={th}>Monto pactado</th>
                <th style={th}>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {lista.map(n => {
                const e = estadoNota(n, hoy);
                const est = ESTILO_ESTADO[e];
                const dias = n.fechaCompromiso ? diasEntre(n.fechaCompromiso, hoy) : 0;
                return (
                  <tr key={n.id} style={{ background: est.bg, boxShadow: e === 'vencida' ? 'inset 3px 0 0 #ef4444' : e === 'hoy' ? 'inset 3px 0 0 #f59e0b' : undefined, opacity: e === 'cumplida' ? 0.6 : 1 }}>
                    <td style={td}>
                      <span style={pill(est.color)}>{est.label}</span>
                      {e === 'vencida' && <div style={{ fontSize: '0.75rem', color: '#ef4444', marginTop: '0.3rem', fontWeight: 600 }}>hace {dias} {dias === 1 ? 'día' : 'días'}</div>}
                      {e === 'proxima' && <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginTop: '0.3rem' }}>en {-dias} {dias === -1 ? 'día' : 'días'}</div>}
                    </td>
                    <td style={td}>
                      <div style={{ fontWeight: 600 }}>
                        <span style={{ color: 'var(--text-muted)' }}>#{n.prestamoId}</span> {n.clienteNombre}
                      </div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                        {n.clienteCedula}{n.clienteTelefono ? ` · ${n.clienteTelefono}` : ''}
                      </div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                        Cobrador: {n.cobradorNombre ?? '—'} · {n.estadoPrestamo}
                      </div>
                    </td>
                    <td style={{ ...td, maxWidth: '380px' }}>
                      <div style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>{n.contenido}</div>
                      <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)', marginTop: '0.3rem' }}>
                        {n.usuarioNombre} · {new Date(n.fechaCreacion).toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric' })}
                        {n.cumplida && n.fechaCumplida && ` · cumplida el ${new Date(n.fechaCumplida).toLocaleDateString('es-CO', { day: '2-digit', month: 'short' })}`}
                      </div>
                    </td>
                    <td style={{ ...td, whiteSpace: 'nowrap', color: e === 'vencida' ? '#ef4444' : e === 'hoy' ? '#f59e0b' : undefined, fontWeight: e === 'vencida' || e === 'hoy' ? 700 : undefined }}>
                      {n.fechaCompromiso ? formatFecha(n.fechaCompromiso) : '—'}
                    </td>
                    <td style={{ ...td, whiteSpace: 'nowrap', fontWeight: 600 }}>
                      {n.montoCompromiso != null ? formatMoney(n.montoCompromiso) : '—'}
                    </td>
                    <td style={{ ...td, whiteSpace: 'nowrap' }}>
                      <div style={{ display: 'flex', gap: '0.35rem' }}>
                        <button style={{ ...iconBtn, color: n.cumplida ? 'var(--text-muted)' : '#10b981' }} onClick={() => toggleCumplida(n)}
                          title={n.cumplida ? 'Marcar como pendiente' : 'Marcar como cumplida'}>{n.cumplida ? '↩️' : '✓'}</button>
                        <button style={iconBtn} onClick={() => editar(n)} title="Editar">✏️</button>
                        {onVerPrestamo && <button style={iconBtn} onClick={() => onVerPrestamo(n.prestamoId)} title="Ver préstamo">👁️</button>}
                        <button style={{ ...iconBtn, color: '#ef4444' }} onClick={() => eliminar(n)} title="Eliminar">✕</button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {form && <NotaModal form={form} onClose={() => setForm(null)} onGuardado={async () => { setForm(null); await cargar(); }} />}
    </div>
  );
};

const NotaModal = ({ form: inicial, onClose, onGuardado }: { form: NotaForm; onClose: () => void; onGuardado: () => void }) => {
  const [form, setForm] = useState<NotaForm>(inicial);
  const [prestamos, setPrestamos] = useState<Prestamo[]>([]);
  const [buscar, setBuscar] = useState('');
  const [guardando, setGuardando] = useState(false);

  useEffect(() => {
    prestamosApi.getAll().then(setPrestamos).catch(() => setPrestamos([]));
  }, []);

  const seleccionado = prestamos.find(p => p.id === form.prestamoId);
  const q = buscar.trim().toLowerCase();
  const opciones = (q
    ? prestamos.filter(p => `#${p.id} ${p.id} ${p.clienteNombre} ${p.clienteCedula}`.toLowerCase().includes(q))
    : prestamos.filter(p => p.estadoPrestamo !== 'Pagado')
  ).slice(0, 30);

  const guardar = async (ev: React.FormEvent) => {
    ev.preventDefault();
    if (!form.prestamoId) { alert('Selecciona el préstamo'); return; }
    if (!form.contenido.trim()) { alert('Escribe la nota'); return; }
    const monto = form.montoCompromiso.trim() ? Number(form.montoCompromiso.replace(/[^\d]/g, '')) : null;
    if (monto !== null && !(monto > 0)) { alert('El monto debe ser mayor a cero'); return; }
    const data = {
      prestamoId: form.prestamoId,
      contenido: form.contenido.trim(),
      fechaCompromiso: form.fechaCompromiso || null,
      montoCompromiso: monto,
    };
    setGuardando(true);
    try {
      if (form.id) await tableroApi.actualizarNota(form.id, data);
      else await tableroApi.crearNota(data);
      onGuardado();
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Error guardando la nota');
    } finally {
      setGuardando(false);
    }
  };

  const montoMostrado = form.montoCompromiso ? Number(form.montoCompromiso.replace(/[^\d]/g, '')).toLocaleString('es-CO') : '';

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '560px' }}>
        <div className="modal-header">
          <h2>{form.id ? 'Editar nota' : 'Nueva nota de préstamo'}</h2>
          <button className="modal-close" onClick={onClose}>×</button>
        </div>
        <form onSubmit={guardar}>
          <div className="modal-body" style={{ display: 'flex', flexDirection: 'column', gap: '0.9rem' }}>
            <div className="form-group">
              <label>Préstamo</label>
              {seleccionado || (form.id && form.prestamoId) ? (
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '0.5rem', ...inputStyle }}>
                  <span>
                    <strong>#{form.prestamoId}</strong> {seleccionado?.clienteNombre ?? ''}
                    {seleccionado && <span style={{ color: 'var(--text-muted)', fontSize: '0.8rem' }}> · {formatMoney(seleccionado.montoPrestado)} · {seleccionado.cobradorNombre ?? 'sin cobrador'}</span>}
                  </span>
                  {!form.id && <button type="button" style={iconBtn} onClick={() => setForm({ ...form, prestamoId: null })}>Cambiar</button>}
                </div>
              ) : (
                <>
                  <input autoFocus placeholder="Buscar por cliente, cédula o # de préstamo…" value={buscar} onChange={e => setBuscar(e.target.value)} style={inputStyle} />
                  <div style={{ maxHeight: '200px', overflowY: 'auto', border: '1px solid var(--border)', borderRadius: '6px', marginTop: '0.35rem' }}>
                    {opciones.length === 0 ? (
                      <div style={{ padding: '0.6rem', color: 'var(--text-muted)', fontSize: '0.85rem' }}>{prestamos.length === 0 ? 'Cargando préstamos…' : 'Sin resultados'}</div>
                    ) : opciones.map(p => (
                      <div key={p.id} onClick={() => setForm({ ...form, prestamoId: p.id })}
                        style={{ padding: '0.5rem 0.65rem', cursor: 'pointer', borderBottom: '1px solid var(--border)', fontSize: '0.85rem' }}
                        onMouseEnter={e => (e.currentTarget.style.background = 'rgba(255,255,255,0.05)')}
                        onMouseLeave={e => (e.currentTarget.style.background = 'transparent')}>
                        <strong>#{p.id}</strong> {p.clienteNombre}
                        <span style={{ color: 'var(--text-muted)' }}> · {p.clienteCedula} · {formatMoney(p.montoPrestado)} · {p.cobradorNombre ?? 'sin cobrador'} · {p.estadoPrestamo}</span>
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
            <div className="form-group">
              <label>Nota / lo pactado con el cliente</label>
              <textarea rows={4} value={form.contenido} onChange={e => setForm({ ...form, contenido: e.target.value })}
                placeholder="Ej: Quedó de abonar el viernes porque le pagan la quincena" style={{ ...inputStyle, resize: 'vertical', fontFamily: 'inherit' }} />
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '0.75rem' }}>
              <div className="form-group">
                <label>Día que quedó de pagar <span>(opcional)</span></label>
                <input type="date" value={form.fechaCompromiso} onChange={e => setForm({ ...form, fechaCompromiso: e.target.value })} style={inputStyle} />
              </div>
              <div className="form-group">
                <label>Cuánto quedó de dar <span>(opcional)</span></label>
                <input inputMode="numeric" placeholder="$ 0" value={montoMostrado ? `$ ${montoMostrado}` : ''}
                  onChange={e => setForm({ ...form, montoCompromiso: e.target.value.replace(/[^\d]/g, '') })} style={inputStyle} />
              </div>
            </div>
          </div>
          <div className="modal-footer">
            <button type="button" className="btn btn-secondary" onClick={onClose}>Cancelar</button>
            <button type="submit" className="btn btn-primary" disabled={guardando}>{guardando ? 'Guardando…' : form.id ? 'Guardar cambios' : 'Crear nota'}</button>
          </div>
        </form>
      </div>
    </div>
  );
};

// ─────────────────────────── Cuentas de cobradores ───────────────────────────

const describirSaldo = (saldo: number) => {
  if (saldo > 0) return { color: '#ef4444', texto: 'Le debe a la oficina' };
  if (saldo < 0) return { color: '#3b82f6', texto: 'La oficina le debe' };
  return { color: '#10b981', texto: 'A paz y salvo' };
};

const CuentasCobradores = () => {
  const [cuentas, setCuentas] = useState<CuentaCobrador[]>([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [seleccionado, setSeleccionado] = useState<number | null>(null);

  const cargar = useCallback(async () => {
    try {
      setError(null);
      setCuentas(await tableroApi.getCuentasCobradores());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Error cargando cuentas');
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => { cargar(); }, [cargar]);

  const totalDebenOficina = cuentas.filter(c => c.saldo > 0).reduce((s, c) => s + c.saldo, 0);
  const totalOficinaDebe = cuentas.filter(c => c.saldo < 0).reduce((s, c) => s - c.saldo, 0);
  const cuentaSel = cuentas.find(c => c.cobradorId === seleccionado);

  if (cargando) return <div className="empty-state">Cargando cuentas…</div>;
  if (error) return <div className="empty-state" style={{ color: '#ef4444' }}>{error}</div>;

  return (
    <div>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '0.75rem', marginBottom: '1rem' }}>
        <div className="kpi-card" style={{ borderLeft: '3px solid #ef4444' }}>
          <div className="kpi-header"><span className="kpi-title">Cobradores le deben a la oficina</span></div>
          <span className="kpi-value" style={{ color: '#ef4444' }}>{formatMoney(totalDebenOficina)}</span>
        </div>
        <div className="kpi-card" style={{ borderLeft: '3px solid #3b82f6' }}>
          <div className="kpi-header"><span className="kpi-title">La oficina les debe</span></div>
          <span className="kpi-value" style={{ color: '#3b82f6' }}>{formatMoney(totalOficinaDebe)}</span>
        </div>
      </div>

      {cuentas.length === 0 ? (
        <div className="empty-state">No hay cobradores registrados.</div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(220px, 1fr))', gap: '0.75rem', marginBottom: '1.25rem' }}>
          {cuentas.map(c => {
            const s = describirSaldo(c.saldo);
            const activo = c.cobradorId === seleccionado;
            return (
              <div key={c.cobradorId} onClick={() => setSeleccionado(activo ? null : c.cobradorId)}
                style={{ padding: '0.9rem 1rem', borderRadius: '10px', cursor: 'pointer', background: 'var(--bg-secondary)', border: `1px solid ${activo ? s.color : 'var(--border)'}`, boxShadow: activo ? `0 0 0 1px ${s.color}` : undefined }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: '0.5rem' }}>
                  <strong>{c.cobradorNombre}</strong>
                  {!c.activo && <span style={pill('#9ca3af')}>Inactivo</span>}
                </div>
                <div style={{ fontSize: '1.35rem', fontWeight: 700, color: s.color, margin: '0.35rem 0 0.1rem' }}>{formatMoney(Math.abs(c.saldo))}</div>
                <div style={{ fontSize: '0.78rem', color: s.color }}>{s.texto}</div>
                <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)', marginTop: '0.35rem' }}>
                  {c.cantidadMovimientos} {c.cantidadMovimientos === 1 ? 'movimiento' : 'movimientos'}
                  {c.ultimoMovimiento && ` · último ${formatFecha(c.ultimoMovimiento)}`}
                </div>
              </div>
            );
          })}
        </div>
      )}

      {cuentaSel && <DetalleCuenta key={cuentaSel.cobradorId} cuenta={cuentaSel} onCambio={cargar} />}
    </div>
  );
};

const DetalleCuenta = ({ cuenta, onCambio }: { cuenta: CuentaCobrador; onCambio: () => Promise<void> }) => {
  const [movimientos, setMovimientos] = useState<MovimientoCuentaCobrador[] | null>(null);
  const [tipo, setTipo] = useState<TipoMovimientoCuenta>('CobradorDebe');
  const [monto, setMonto] = useState('');
  const [descripcion, setDescripcion] = useState('');
  const [fecha, setFecha] = useState(hoyISO());
  const [guardando, setGuardando] = useState(false);

  const cargar = useCallback(async () => {
    try { setMovimientos(await tableroApi.getMovimientos(cuenta.cobradorId)); }
    catch { setMovimientos([]); }
  }, [cuenta.cobradorId]);

  useEffect(() => { cargar(); }, [cargar]);

  const registrar = async (ev: React.FormEvent) => {
    ev.preventDefault();
    const valor = Number(monto.replace(/[^\d]/g, ''));
    if (!(valor > 0)) { alert('Ingresa un monto mayor a cero'); return; }
    if (!descripcion.trim()) { alert('Escribe una descripción'); return; }
    setGuardando(true);
    try {
      await tableroApi.crearMovimiento(cuenta.cobradorId, { tipo, monto: valor, descripcion: descripcion.trim(), fecha });
      setMonto(''); setDescripcion('');
      await Promise.all([cargar(), onCambio()]);
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Error registrando el movimiento');
    } finally {
      setGuardando(false);
    }
  };

  const eliminar = async (m: MovimientoCuentaCobrador) => {
    if (!confirm(`¿Eliminar el movimiento "${m.descripcion}" por ${formatMoney(m.monto)}?`)) return;
    try { await tableroApi.eliminarMovimiento(m.id); await Promise.all([cargar(), onCambio()]); }
    catch (e) { alert(e instanceof Error ? e.message : 'Error'); }
  };

  const s = describirSaldo(cuenta.saldo);
  const montoMostrado = monto ? Number(monto).toLocaleString('es-CO') : '';

  return (
    <div style={{ border: '1px solid var(--border)', borderRadius: '10px', padding: '1rem', background: 'var(--bg-secondary)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '0.9rem' }}>
        <h3 style={{ margin: 0 }}>Cuenta de {cuenta.cobradorNombre}</h3>
        <span style={{ fontWeight: 700, color: s.color }}>{s.texto}: {formatMoney(Math.abs(cuenta.saldo))}</span>
      </div>

      <form onSubmit={registrar} style={{ display: 'flex', flexDirection: 'column', gap: '0.6rem', marginBottom: '1rem' }}>
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <button type="button" className="btn" onClick={() => setTipo('CobradorDebe')}
            style={{ flex: '1 1 220px', border: `1px solid ${tipo === 'CobradorDebe' ? '#ef4444' : 'var(--border)'}`, background: tipo === 'CobradorDebe' ? 'rgba(239,68,68,0.12)' : 'transparent', color: 'var(--text-primary)' }}>
            ➕ El cobrador debe a la oficina
          </button>
          <button type="button" className="btn" onClick={() => setTipo('OficinaDebe')}
            style={{ flex: '1 1 220px', border: `1px solid ${tipo === 'OficinaDebe' ? '#3b82f6' : 'var(--border)'}`, background: tipo === 'OficinaDebe' ? 'rgba(59,130,246,0.12)' : 'transparent', color: 'var(--text-primary)' }}>
            ➖ La oficina debe al cobrador / abono del cobrador
          </button>
        </div>
        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
          <input inputMode="numeric" placeholder="$ Monto" value={montoMostrado ? `$ ${montoMostrado}` : ''}
            onChange={e => setMonto(e.target.value.replace(/[^\d]/g, ''))} style={{ ...inputStyle, flex: '1 1 140px', minWidth: 0 }} />
          <input placeholder={tipo === 'CobradorDebe' ? 'Ej: Cobros del lunes que no entregó' : 'Ej: Entregó $200.000 / comisión pendiente'}
            value={descripcion} onChange={e => setDescripcion(e.target.value)} style={{ ...inputStyle, flex: '3 1 260px', minWidth: 0 }} />
          <input type="date" value={fecha} onChange={e => setFecha(e.target.value)} style={{ ...inputStyle, flex: '0 1 160px' }} />
          <button type="submit" className="btn btn-primary" disabled={guardando}>{guardando ? 'Guardando…' : 'Registrar'}</button>
        </div>
      </form>

      {movimientos === null ? (
        <div className="empty-state">Cargando movimientos…</div>
      ) : movimientos.length === 0 ? (
        <div className="empty-state">Sin movimientos todavía.</div>
      ) : (
        <div className="table-container">
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th style={th}>Fecha</th>
                <th style={th}>Descripción</th>
                <th style={{ ...th, textAlign: 'right' }}>Cobrador debe (+)</th>
                <th style={{ ...th, textAlign: 'right' }}>Oficina debe / abono (−)</th>
                <th style={{ ...th, textAlign: 'right' }}>Saldo</th>
                <th style={th}></th>
              </tr>
            </thead>
            <tbody>
              {movimientos.map(m => {
                const sa = describirSaldo(m.saldoAcumulado);
                return (
                  <tr key={m.id}>
                    <td style={{ ...td, whiteSpace: 'nowrap' }}>{formatFecha(m.fecha)}</td>
                    <td style={td}>
                      {m.descripcion}
                      <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>{m.usuarioNombre}</div>
                    </td>
                    <td style={{ ...td, textAlign: 'right', color: '#ef4444', whiteSpace: 'nowrap' }}>{m.tipo === 'CobradorDebe' ? formatMoney(m.monto) : ''}</td>
                    <td style={{ ...td, textAlign: 'right', color: '#3b82f6', whiteSpace: 'nowrap' }}>{m.tipo === 'OficinaDebe' ? formatMoney(m.monto) : ''}</td>
                    <td style={{ ...td, textAlign: 'right', fontWeight: 700, color: sa.color, whiteSpace: 'nowrap' }}>
                      {m.saldoAcumulado < 0 ? '−' : ''}{formatMoney(Math.abs(m.saldoAcumulado))}
                    </td>
                    <td style={td}><button style={{ ...iconBtn, color: '#ef4444' }} onClick={() => eliminar(m)} title="Eliminar">✕</button></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
