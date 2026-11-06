import { useEffect, useState } from 'react';
import { dashboardApi } from '../api';
import { PrestamoVencido } from '../types';

const formatMoney = (amount: number): string =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(amount);

const formatFecha = (iso: string): string =>
  new Date(iso).toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' });

const th: React.CSSProperties = { padding: '0.75rem 1rem', textAlign: 'left', fontSize: '0.7rem', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em', borderBottom: '1px solid var(--border)', background: 'var(--bg-secondary)' };
const td: React.CSSProperties = { padding: '0.75rem 1rem', fontSize: '0.875rem', color: 'var(--text-primary)', borderBottom: '1px solid var(--border)' };
const tdFoot: React.CSSProperties = { padding: '0.75rem 1rem', fontSize: '0.875rem', fontWeight: 700, color: 'var(--text-primary)', background: 'var(--bg-secondary)' };
const badge = (bg: string): React.CSSProperties => ({ display: 'inline-block', padding: '0.2rem 0.6rem', borderRadius: '999px', fontSize: '0.78rem', fontWeight: 600, background: bg, color: '#fff' });

interface Props {
  onVerPrestamo?: (prestamoId: number) => void;
}

/**
 * Préstamos vencidos: ya no tienen cuotas este mes ni después y todavía deben cuotas de meses pasados.
 * Solo lo ven los roles con acceso a métricas; para los demás no se muestra.
 */
export const PrestamosVencidos = ({ onVerPrestamo }: Props) => {
  const [vencidos, setVencidos] = useState<PrestamoVencido[] | null>(null);
  const [filtro, setFiltro] = useState('todos');

  useEffect(() => {
    dashboardApi.getPrestamosVencidos().then(setVencidos).catch(() => setVencidos(null));
  }, []);

  if (!vencidos) return null;

  const claveCobrador = (v: PrestamoVencido) => (v.cobradorId != null ? String(v.cobradorId) : 'sin');
  const cobradores = [...new Map(vencidos.map(v => [claveCobrador(v), v.cobradorNombre ?? 'Sin cobrador'])).entries()]
    .sort((a, b) => a[1].localeCompare(b[1]));
  const lista = filtro === 'todos' ? vencidos : vencidos.filter(v => claveCobrador(v) === filtro);
  const suma = (f: (v: PrestamoVencido) => number) => lista.reduce((s, v) => s + f(v), 0);

  return (
    <div style={{ marginTop: '1.5rem' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem', margin: '1rem 0 0.5rem' }}>
        <h4 style={{ color: '#ef4444', margin: 0 }}>
          ⏰ Préstamos vencidos · {lista.length} {lista.length === 1 ? 'préstamo' : 'préstamos'} · {formatMoney(suma(v => v.saldoPendiente))}
        </h4>
        {cobradores.length > 1 && (
          <select value={filtro} onChange={e => setFiltro(e.target.value)}
            style={{ padding: '0.3rem 0.5rem', background: 'var(--bg-secondary)', border: '1px solid var(--border)', borderRadius: '6px', color: 'var(--text-primary)' }}>
            <option value="todos">Todos los cobradores</option>
            {cobradores.map(([id, nombre]) => <option key={id} value={id}>{nombre}</option>)}
          </select>
        )}
      </div>
      {lista.length === 0 ? (
        <div className="empty-state">No hay préstamos vencidos 🎉</div>
      ) : (
        <div className="table-container">
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th style={th}>Préstamo</th>
                <th style={th}>Cliente</th>
                <th style={th}>Cobrador</th>
                <th style={th}>Última cuota</th>
                <th style={th}>Debe desde</th>
                <th style={th}>Días vencido</th>
                <th style={th}>Cuotas sin pagar</th>
                <th style={th}>Capital</th>
                <th style={th}>Interés</th>
                <th style={th}>Saldo</th>
              </tr>
            </thead>
            <tbody>
              {lista.map((v, i) => (
                <tr key={v.prestamoId} style={{ background: i % 2 === 0 ? 'transparent' : 'rgba(255,255,255,0.02)', cursor: onVerPrestamo ? 'pointer' : undefined }}
                  onClick={() => onVerPrestamo?.(v.prestamoId)} title={onVerPrestamo ? 'Ver préstamo' : undefined}>
                  <td style={{ ...td, color: 'var(--text-muted)' }}>#{v.prestamoId}</td>
                  <td style={{ ...td, fontWeight: 600 }}>
                    {v.clienteNombre}
                    {v.esCongelado && <span style={{ ...badge('rgba(139,92,246,0.35)'), marginLeft: '0.5rem' }}>Congelado</span>}
                  </td>
                  <td style={td}>{v.cobradorNombre ?? '—'}</td>
                  <td style={td}>{formatFecha(v.ultimaFechaCobro)}</td>
                  <td style={td}>{formatFecha(v.cuotaMasAntiguaSinPagar)}</td>
                  <td style={td}>
                    <span style={badge(v.diasVencido > 60 ? 'rgba(239,68,68,0.45)' : v.diasVencido > 30 ? 'rgba(249,115,22,0.45)' : 'rgba(245,158,11,0.4)')}>
                      {v.diasVencido} días
                    </span>
                  </td>
                  <td style={td}>{v.cuotasPendientes}</td>
                  <td style={td}>{formatMoney(v.capitalPendiente)}</td>
                  <td style={td}>{formatMoney(v.interesPendiente)}</td>
                  <td style={{ ...td, fontWeight: 700, color: '#ef4444' }}>{formatMoney(v.saldoPendiente)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td style={tdFoot} colSpan={6}>TOTAL</td>
                <td style={tdFoot}>{suma(v => v.cuotasPendientes)}</td>
                <td style={tdFoot}>{formatMoney(suma(v => v.capitalPendiente))}</td>
                <td style={tdFoot}>{formatMoney(suma(v => v.interesPendiente))}</td>
                <td style={tdFoot}>{formatMoney(suma(v => v.saldoPendiente))}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      )}
      <p style={{ fontSize: '0.78rem', color: 'var(--text-muted)', marginTop: '0.5rem' }}>
        Préstamos que ya no tienen cuotas en este mes (todas eran de meses anteriores) y todavía deben saldo. Ordenados del más atrasado al más reciente.
      </p>
    </div>
  );
};
