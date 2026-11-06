import { useCallback, useEffect, useState } from 'react';
import { balanceEconomicoApi, dashboardApi } from '../api';
import { BalanceCobrador, BalanceEconomico as BalanceEconomicoData, CostoBalance } from '../types';

const formatMoney = (amount: number): string =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(amount);

const MESES = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

// Colores de cada costo en la barra, en orden de prioridad
const COLOR_COSTO: Record<string, string> = {
  capital: '#3b82f6',
  ruta: '#8b5cf6',
  moto: '#f59e0b',
  prestamo: '#06b6d4',
  aportadores: '#3b82f6',
  operativos: '#ec4899',
  motos: '#f59e0b',
  prestamos: '#06b6d4',
};

const card: React.CSSProperties = { background: 'var(--surface, var(--bg-card))', border: '1px solid var(--border)', borderRadius: '10px', padding: '1rem 1.25rem', marginBottom: '1rem' };
const muted: React.CSSProperties = { fontSize: '0.8rem', color: '#999' };
const inputStyle: React.CSSProperties = { width: 120, padding: '0.3rem 0.5rem', background: 'var(--background, var(--bg-secondary))', border: '1px solid var(--border)', borderRadius: '6px', color: 'inherit' };

const parseNumero = (v: string) => Number(v.replace(/[^\d]/g, '')) || 0;

/** Barra con segmentos en orden de prioridad: cada costo se llena con lo que alcance a cubrir lo recaudado. */
const BarraCostos = ({ costos, total }: { costos: CostoBalance[]; total: number }) => (
  <div style={{ display: 'flex', height: 22, borderRadius: 6, overflow: 'hidden', background: 'rgba(255,255,255,0.06)', border: '1px solid var(--border)' }}>
    {costos.filter(c => c.monto > 0).map(c => {
      const ancho = total > 0 ? (c.monto / total) * 100 : 0;
      const lleno = c.monto > 0 ? (c.cubierto / c.monto) * 100 : 0;
      const color = COLOR_COSTO[c.clave] ?? '#888';
      return (
        <div key={c.clave} title={`${c.nombre}: ${formatMoney(c.cubierto)} de ${formatMoney(c.monto)}`}
          style={{ width: `${ancho}%`, position: 'relative', borderRight: '1px solid rgba(0,0,0,0.35)' }}>
          <div style={{ position: 'absolute', inset: 0, background: color, opacity: 0.18 }} />
          <div style={{ position: 'absolute', top: 0, bottom: 0, left: 0, width: `${lleno}%`, background: color, transition: 'width .4s' }} />
        </div>
      );
    })}
  </div>
);

const BarraSimple = ({ valor, tope, color }: { valor: number; tope: number; color: string }) => (
  <div style={{ height: 22, borderRadius: 6, overflow: 'hidden', background: 'rgba(255,255,255,0.06)', border: '1px solid var(--border)' }}>
    <div style={{ width: `${tope > 0 ? Math.min(100, (valor / tope) * 100) : 0}%`, height: '100%', background: color, transition: 'width .4s' }} />
  </div>
);

const LeyendaCostos = ({ costos }: { costos: CostoBalance[] }) => (
  <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.4rem 1.25rem', marginTop: '0.5rem' }}>
    {costos.map((c, i) => (
      <span key={c.clave} style={{ fontSize: '0.8rem', display: 'inline-flex', alignItems: 'center', gap: '0.35rem' }}>
        <span style={{ width: 10, height: 10, borderRadius: 2, background: COLOR_COSTO[c.clave] ?? '#888', display: 'inline-block' }} />
        <span style={{ color: '#aaa' }}>{i + 1}. {c.nombre}:</span>
        <strong>{formatMoney(c.monto)}</strong>
        <span style={{ color: c.cubierto >= c.monto ? '#10b981' : c.cubierto > 0 ? '#f59e0b' : '#ef4444' }}>
          {c.cubierto >= c.monto ? '✓' : `${formatMoney(c.cubierto)} cubierto`}
        </span>
      </span>
    ))}
  </div>
);

const ESTADO: Record<BalanceCobrador['estado'], { texto: string; color: string }> = {
  Rentable: { texto: '✅ Rentable', color: 'rgba(16,185,129,0.35)' },
  SalarioParcial: { texto: '🟡 Cubre costos, salario parcial', color: 'rgba(245,158,11,0.4)' },
  NoCubreCostos: { texto: '❌ No cubre costos', color: 'rgba(239,68,68,0.4)' },
};

export const BalanceEconomico = () => {
  const hoy = new Date();
  const [periodo, setPeriodo] = useState({ anio: hoy.getFullYear(), mes: hoy.getMonth() + 1 });
  const [data, setData] = useState<BalanceEconomicoData | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [editando, setEditando] = useState<{ campo: string; valor: string } | null>(null);

  const cargar = useCallback(() => {
    balanceEconomicoApi.get(periodo.anio, periodo.mes)
      .then(d => { setData(d); setError(null); })
      .catch(e => setError(e instanceof Error ? e.message : 'Error al cargar el balance'));
  }, [periodo]);

  useEffect(() => { cargar(); }, [cargar]);

  const guardar = async (accion: () => Promise<void>) => {
    try { await accion(); setEditando(null); cargar(); }
    catch (e) { setError(e instanceof Error ? e.message : 'Error al guardar'); }
  };

  const moverMes = (delta: number) => setPeriodo(p => {
    const d = new Date(p.anio, p.mes - 1 + delta, 1);
    return { anio: d.getFullYear(), mes: d.getMonth() + 1 };
  });

  // Campo editable en línea: muestra el valor y al hacer clic permite cambiarlo
  // Se llama como función (no como componente) para que el input no se vuelva a montar en cada tecla
  const editable = ({ campo, valor, vacio, onGuardar, onBorrar, prefijo = '' }: {
    campo: string; valor: number | null; vacio: string; prefijo?: string;
    onGuardar: (v: number) => Promise<void>; onBorrar?: () => Promise<void>;
  }) => editando?.campo === campo ? (
    <span style={{ display: 'inline-flex', gap: '0.3rem', alignItems: 'center' }}>
      <input autoFocus inputMode="numeric" style={inputStyle} value={editando.valor}
        onChange={e => setEditando({ campo, valor: e.target.value })}
        onKeyDown={e => {
          if (e.key === 'Enter') guardar(() => onGuardar(parseNumero(editando.valor)));
          if (e.key === 'Escape') setEditando(null);
        }} />
      <button className="btn btn-primary btn-sm" onClick={() => guardar(() => onGuardar(parseNumero(editando.valor)))}>✓</button>
      <button className="btn btn-secondary btn-sm" onClick={() => setEditando(null)}>✕</button>
    </span>
  ) : (
    <span style={{ display: 'inline-flex', gap: '0.3rem', alignItems: 'center' }}>
      <button onClick={() => setEditando({ campo, valor: valor ? String(valor) : '' })}
        style={{ background: 'none', border: 'none', color: valor ? 'inherit' : '#888', cursor: 'pointer', padding: 0, fontSize: '0.875rem' }}>
        {valor ? `${prefijo}${prefijo ? valor : formatMoney(valor)}` : vacio} ✏️
      </button>
      {valor && onBorrar ? (
        <button title="Quitar" onClick={() => guardar(onBorrar)}
          style={{ background: 'none', border: 'none', color: '#ef4444', cursor: 'pointer', padding: 0 }}>🗑️</button>
      ) : null}
    </span>
  );

  if (error && !data) return <div style={{ ...card, color: '#ef4444' }}>❌ {error}</div>;
  if (!data) return <div style={card}>Cargando balance económico...</div>;

  const n = data.negocio;
  const mitadCostos = n.ingresos - n.totalCostos;

  return (
    <div style={{ marginBottom: '1.5rem' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.75rem', margin: '0.5rem 0 1rem' }}>
        <h3 style={{ margin: 0 }}>⚖️ Balance económico · {MESES[data.mes - 1]} {data.anio}</h3>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}>
          <button className="btn btn-secondary btn-sm" onClick={() => moverMes(-1)}>◀</button>
          <button className="btn btn-secondary btn-sm" onClick={() => setPeriodo({ anio: hoy.getFullYear(), mes: hoy.getMonth() + 1 })}>Mes actual</button>
          <button className="btn btn-secondary btn-sm" onClick={() => moverMes(1)}>▶</button>
          <span style={{ ...muted, marginLeft: '0.75rem' }}>Costo de ruta:</span>
          {editable({ campo: 'ruta', valor: data.costoRuta, vacio: 'Definir',
            onGuardar: v => balanceEconomicoApi.actualizarCostoRuta(v) })}
          <span style={muted}>· Costo del capital: {data.tasaCostoCapital}% (promedio de aportadores)</span>
        </div>
      </div>
      {error && <div style={{ color: '#ef4444', marginBottom: '0.75rem' }}>❌ {error}</div>}

      {data.cobradores.map(c => {
        const est = ESTADO[c.estado];
        const topeSalario = c.salario > 0 ? c.salario : 0;
        return (
          <div key={c.cobradorId} style={card}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
              <div>
                <strong style={{ fontSize: '1.05rem' }}>{c.cobradorNombre}</strong>
                <span style={{ ...muted, marginLeft: '0.75rem' }}>Capital a cargo: <strong style={{ color: 'inherit' }}>{formatMoney(c.capitalACargo)}</strong></span>
              </div>
              <span style={{ padding: '0.2rem 0.7rem', borderRadius: 999, fontSize: '0.8rem', fontWeight: 600, background: est.color }}>{est.texto}</span>
            </div>
            <div style={{ ...muted, margin: '0.35rem 0 0.75rem' }}>
              Recogió <strong style={{ color: '#10b981' }}>{formatMoney(c.recaudo)}</strong>
              {' '}(interés {formatMoney(c.interesCobrado)} + mora {formatMoney(c.moraCobrada)})
              {' '}· Interés programado del mes: {formatMoney(c.interesProgramado)}
            </div>

            {/* 1. Costos en orden de prioridad */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
              <span style={{ width: 70, fontSize: '0.8rem', color: '#aaa' }}>Costos</span>
              <div style={{ flex: 1 }}><BarraCostos costos={c.costos} total={c.totalCostos} /></div>
              <span style={{ minWidth: 190, textAlign: 'right', fontSize: '0.875rem' }}>
                <strong>{formatMoney(c.costosCubiertos)}</strong> <span style={{ color: '#888' }}>de {formatMoney(c.totalCostos)}</span>
              </span>
            </div>
            <LeyendaCostos costos={c.costos} />

            {/* 2. Salario: se llena solo después de cubrir los costos */}
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginTop: '0.9rem' }}>
              <span style={{ width: 70, fontSize: '0.8rem', color: '#aaa' }}>Salario</span>
              <div style={{ flex: 1 }}><BarraSimple valor={c.salarioCorresponde} tope={topeSalario} color="#10b981" /></div>
              <span style={{ minWidth: 190, textAlign: 'right', fontSize: '0.875rem' }}>
                Le corresponde <strong style={{ color: '#10b981' }}>{formatMoney(c.salarioCorresponde)}</strong>
                {c.salario > 0 && <span style={{ color: '#888' }}> de {formatMoney(c.salario)}</span>}
              </span>
            </div>
            {c.estado === 'Rentable' && c.excedente > 0 && (
              <div style={{ fontSize: '0.8rem', color: '#10b981', marginTop: '0.35rem', textAlign: 'right' }}>
                Después de costos y salario completo sobran {formatMoney(c.excedente)}
              </div>
            )}

            {/* Configuración del cobrador */}
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem 1.5rem', marginTop: '0.9rem', paddingTop: '0.75rem', borderTop: '1px solid var(--border)', fontSize: '0.85rem' }}>
              <span>Salario: {editable({ campo: `salario-${c.cobradorId}`, valor: c.salario || null, vacio: 'Definir',
                onGuardar: v => dashboardApi.actualizarSalarioCobrador(c.cobradorId, v) })}</span>
              <span>Moto: {editable({ campo: `moto-${c.cobradorId}`, valor: c.moto || null, vacio: 'Agregar',
                onGuardar: v => balanceEconomicoApi.actualizarMoto(c.cobradorId, v),
                onBorrar: () => balanceEconomicoApi.actualizarMoto(c.cobradorId, null) })}</span>
              <span>Préstamo asociado: {editable({ campo: `prestamo-${c.cobradorId}`, valor: c.prestamoAsociadoId, vacio: 'Asociar #', prefijo: '#',
                onGuardar: v => balanceEconomicoApi.actualizarPrestamo(c.cobradorId, v || null),
                onBorrar: () => balanceEconomicoApi.actualizarPrestamo(c.cobradorId, null) })}</span>
            </div>
          </div>
        );
      })}
      {data.cobradores.length === 0 && <div style={card}>No hay cobradores activos.</div>}

      {/* Barra general del negocio: todos los préstamos, con y sin cobrador */}
      <div style={{ ...card, borderLeft: `4px solid ${n.resultado >= 0 ? '#10b981' : '#ef4444'}` }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
          <strong style={{ fontSize: '1.05rem' }}>🏢 Negocio en total</strong>
          <span style={{ padding: '0.2rem 0.7rem', borderRadius: 999, fontSize: '0.85rem', fontWeight: 700, background: n.resultado >= 0 ? 'rgba(16,185,129,0.35)' : 'rgba(239,68,68,0.4)' }}>
            {n.resultado >= 0 ? `✅ Rentable: ganamos ${formatMoney(n.resultado)}` : `❌ Nos comimos capital: ${formatMoney(-n.resultado)}`}
          </span>
        </div>
        <div style={{ ...muted, margin: '0.35rem 0 0.75rem' }}>
          Ingresos del mes <strong style={{ color: '#10b981' }}>{formatMoney(n.ingresos)}</strong>
          {' '}(interés {formatMoney(n.interesCobrado)} + mora {formatMoney(n.moraCobrada)}), incluye préstamos sin cobrador
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <span style={{ width: 70, fontSize: '0.8rem', color: '#aaa' }}>Costos</span>
          <div style={{ flex: 1 }}><BarraCostos costos={n.costos} total={n.totalCostos} /></div>
          <span style={{ minWidth: 190, textAlign: 'right', fontSize: '0.875rem' }}>
            <strong>{formatMoney(Math.min(Math.max(n.ingresos, 0), n.totalCostos))}</strong> <span style={{ color: '#888' }}>de {formatMoney(n.totalCostos)}</span>
          </span>
        </div>
        <LeyendaCostos costos={n.costos} />
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '0.75rem', marginTop: '1rem', fontSize: '0.875rem' }}>
          <div>Ingresos − costos: <strong style={{ color: mitadCostos >= 0 ? '#10b981' : '#ef4444' }}>{formatMoney(mitadCostos)}</strong></div>
          <div>Salarios de cobradores: <strong>{formatMoney(n.salariosCobradores)}</strong></div>
          <div>Resultado del mes: <strong style={{ color: n.resultado >= 0 ? '#10b981' : '#ef4444' }}>{formatMoney(n.resultado)}</strong></div>
        </div>
        <p style={{ ...muted, marginTop: '0.75rem', marginBottom: 0 }}>
          Los costos del negocio usan el interés real de los aportadores externos, no el % del capital a cargo de cada cobrador.
          Si en Costos operativos hay costos creados a mano para aportadores o salarios de cobradores, se cuentan dos veces: elimínalos o desactívalos.
        </p>
      </div>
    </div>
  );
};
