import { useEffect, useState } from 'react';
import { dashboardApi } from '../api';
import { MetricasGenerales } from '../types';

const formatMoney = (amount: number): string =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(amount);

const formatPercent = (value: number): string => `${value.toFixed(2)}%`;

const s = {
  container: { padding: '1.5rem 0' } as React.CSSProperties,
  title: { fontSize: '1.25rem', fontWeight: 700, color: 'var(--text-primary)', marginBottom: '1.5rem' } as React.CSSProperties,

  // KPI grid - igual que .kpi-grid de la app
  kpiGrid: { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem', marginBottom: '1.5rem' } as React.CSSProperties,
  kpiCard: (accent: string): React.CSSProperties => ({
    background: 'var(--bg-card)',
    border: `1px solid var(--border)`,
    borderLeft: `4px solid ${accent}`,
    borderRadius: 'var(--radius-md)',
    padding: '1.25rem',
    display: 'flex',
    flexDirection: 'column',
    gap: '0.4rem',
  }),
  kpiTitle: { fontSize: '0.75rem', fontWeight: 600, color: 'var(--text-secondary)', textTransform: 'uppercase' as const, letterSpacing: '0.05em' },
  kpiValue: (accent: string): React.CSSProperties => ({ fontSize: '1.75rem', fontWeight: 800, color: accent }),
  kpiSub: { fontSize: '0.78rem', color: 'var(--text-muted)' },

  // Tabla
  tableWrap: { background: 'var(--bg-card)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', overflow: 'hidden', marginBottom: '1rem' } as React.CSSProperties,
  tableHeader: { padding: '1rem 1.25rem', borderBottom: '1px solid var(--border)', display: 'flex', alignItems: 'center', gap: '0.5rem' } as React.CSSProperties,
  tableHeaderTitle: { fontSize: '0.9rem', fontWeight: 600, color: 'var(--text-primary)' } as React.CSSProperties,
  table: { width: '100%', borderCollapse: 'collapse' as const },
  th: { padding: '0.75rem 1.25rem', textAlign: 'left' as const, fontSize: '0.7rem', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase' as const, letterSpacing: '0.05em', borderBottom: '1px solid var(--border)', background: 'var(--bg-secondary)' },
  td: { padding: '0.9rem 1.25rem', fontSize: '0.875rem', color: 'var(--text-primary)', borderBottom: '1px solid var(--border)' },
  tdFoot: { padding: '0.9rem 1.25rem', fontSize: '0.875rem', fontWeight: 700, color: 'var(--text-primary)', background: 'var(--bg-secondary)' },

  // Avatar
  avatar: (color: string): React.CSSProperties => ({
    width: 36, height: 36, borderRadius: '50%', background: color,
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    fontWeight: 700, fontSize: '0.85rem', color: '#fff', flexShrink: 0,
  }),
  aliasCell: { display: 'flex', alignItems: 'center', gap: '0.75rem' } as React.CSSProperties,
  aliasName: { fontWeight: 600, color: 'var(--text-primary)', fontSize: '0.875rem' } as React.CSSProperties,
  aliasId: { fontSize: '0.7rem', color: 'var(--text-muted)' } as React.CSSProperties,

  badge: (bg: string): React.CSSProperties => ({
    display: 'inline-block', padding: '0.2rem 0.6rem', borderRadius: '999px',
    fontSize: '0.78rem', fontWeight: 600, background: bg, color: '#fff',
  }),

  // Info box
  infoBox: { background: 'rgba(59,130,246,0.08)', border: '1px solid rgba(59,130,246,0.2)', borderRadius: 'var(--radius-sm)', padding: '0.875rem 1rem', marginTop: '1rem' } as React.CSSProperties,
  infoTitle: { fontSize: '0.8rem', fontWeight: 600, color: 'var(--accent-blue)', marginBottom: '0.4rem' } as React.CSSProperties,
  infoItem: { fontSize: '0.8rem', color: 'var(--text-secondary)', marginBottom: '0.25rem' } as React.CSSProperties,

  // Loading / error
  center: { padding: '3rem', textAlign: 'center' as const, color: 'var(--text-secondary)' },
  spinner: { width: 32, height: 32, border: '3px solid var(--border)', borderTop: '3px solid var(--accent-blue)', borderRadius: '50%', animation: 'spin 0.8s linear infinite', margin: '0 auto 1rem' } as React.CSSProperties,
  errorBox: { background: 'rgba(239,68,68,0.1)', border: '1px solid rgba(239,68,68,0.3)', borderRadius: 'var(--radius-sm)', padding: '1rem', color: 'var(--accent-red)' } as React.CSSProperties,
};

const AVATAR_COLORS = ['#3b82f6', '#10b981', '#f59e0b', '#8b5cf6', '#f97316', '#ec4899'];

export const MetricasCobradores = () => {
  const [metricas, setMetricas] = useState<MetricasGenerales | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    dashboardApi.getMetricasCobradores()
      .then(data => { setMetricas(data); setError(null); })
      .catch(err => setError(err instanceof Error ? err.message : 'Error al cargar métricas'))
      .finally(() => setLoading(false));
  }, []);

  if (loading) return (
    <div style={s.center}>
      <div style={s.spinner} />
      <p>Cargando métricas...</p>
    </div>
  );

  if (error) return <div style={s.errorBox}>❌ {error}</div>;
  if (!metricas) return <div style={s.center}>Sin datos disponibles</div>;

  return (
    <div style={s.container}>
      <h3 style={s.title}>📊 Métricas por Cobrador</h3>

      {/* KPI Cards */}
      <div style={s.kpiGrid}>
        <div style={s.kpiCard('var(--accent-blue)')}>
          <span style={s.kpiTitle}>Promedio Tasas Activas</span>
          <span style={s.kpiValue('var(--accent-blue)')}>{formatPercent(metricas.promedioTasasActivos)}</span>
          <span style={s.kpiSub}>Todos los préstamos activos</span>
        </div>
        <div style={s.kpiCard('var(--accent-purple)')}>
          <span style={s.kpiTitle}>Capital Fantasma</span>
          <span style={s.kpiValue('var(--accent-purple)')}>{formatMoney(metricas.capitalFantasma)}</span>
          <span style={s.kpiSub}>{metricas.totalPrestamosActivos} préstamos activos</span>
        </div>
        <div style={s.kpiCard('var(--accent-green)')}>
          <span style={s.kpiTitle}>Cobradores Activos</span>
          <span style={s.kpiValue('var(--accent-green)')}>{metricas.estadisticasCobradores.length}</span>
          <span style={s.kpiSub}>Con créditos asignados</span>
        </div>
      </div>

      {/* Tabla */}
      <div style={s.tableWrap}>
        <div style={s.tableHeader}>
          <span style={s.tableHeaderTitle}>📋 Detalle por Cobrador</span>
        </div>
        <div style={{ overflowX: 'auto' }}>
          <table style={s.table}>
            <thead>
              <tr>
                <th style={s.th}>Cobrador</th>
                <th style={s.th}>Créditos</th>
                <th style={s.th}>% Promedio</th>
                <th style={s.th}>% Neto (−8%)</th>
                <th style={s.th}>Capital Total</th>
              </tr>
            </thead>
            <tbody>
              {metricas.estadisticasCobradores.map((c, i) => (
                <tr key={c.cobradorId} style={{ background: i % 2 === 0 ? 'transparent' : 'rgba(255,255,255,0.02)' }}>
                  <td style={s.td}>
                    <div style={s.aliasCell}>
                      <div style={s.avatar(AVATAR_COLORS[i % AVATAR_COLORS.length])}>{i + 1}</div>
                      <div>
                        <div style={s.aliasName}>{c.alias}</div>
                        <div style={s.aliasId}>ID #{c.cobradorId}</div>
                      </div>
                    </div>
                  </td>
                  <td style={s.td}>
                    <span style={s.badge('rgba(99,102,241,0.25)')}>{c.totalCreditosActivos}</span>
                  </td>
                  <td style={{ ...s.td, fontWeight: 600 }}>{formatPercent(c.promedioTasaInteres)}</td>
                  <td style={s.td}>
                    <span style={{ fontWeight: 700, color: c.promedioTasaInteresNeto > 0 ? 'var(--accent-green)' : 'var(--accent-red)' }}>
                      {formatPercent(c.promedioTasaInteresNeto)}
                    </span>
                  </td>
                  <td style={{ ...s.td, fontWeight: 700 }}>{formatMoney(c.capitalTotalPrestado)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <td style={s.tdFoot}>TOTALES</td>
                <td style={s.tdFoot}>
                  {metricas.estadisticasCobradores.reduce((sum, c) => sum + c.totalCreditosActivos, 0)}
                </td>
                <td style={s.tdFoot}>{formatPercent(metricas.promedioTasasActivos)}</td>
                <td style={s.tdFoot}>{formatPercent(metricas.promedioTasasActivos - 8)}</td>
                <td style={s.tdFoot}>{formatMoney(metricas.capitalFantasma)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>

      {/* Info */}
      <div style={s.infoBox}>
        <p style={s.infoTitle}>ℹ️ Información</p>
        <p style={s.infoItem}>• <strong>% Neto (−8%)</strong>: Porcentaje promedio después de restar el 8% base</p>
        <p style={s.infoItem}>• <strong>Capital Fantasma</strong>: Suma del monto original prestado en activos (sin considerar pagos)</p>
        <p style={s.infoItem}>• Los alias <strong>Cobrador 1, 2...</strong> protegen la identidad de los cobradores</p>
      </div>
    </div>
  );
};
