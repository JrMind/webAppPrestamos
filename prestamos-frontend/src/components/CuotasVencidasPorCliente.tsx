import { useMemo, useState } from 'react';
import { CuotaCobroMes } from '../types';

const formatMoney = (amount: number): string =>
  new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(amount);

const formatFecha = (iso: string): string =>
  new Date(iso).toLocaleDateString('es-CO', { day: 'numeric', month: 'numeric', year: 'numeric', timeZone: 'UTC' });

interface Props {
  cuotas: CuotaCobroMes[];
  puedeMarcar: boolean;
  onMarcarCobrado: (cuotaId: number, cobrado: boolean) => void;
  onVerPrestamo: (prestamoId: number) => void;
  onRecordar: (cuotaId: number) => void;
}

interface PrestamoAgrupado {
  prestamoId: number;
  cuotas: CuotaCobroMes[];
  adeudado: number;
  diasMax: number;
}

interface ClienteAgrupado {
  clave: string;
  nombre: string;
  telefono?: string;
  cobradores: string[];
  prestamos: PrestamoAgrupado[];
  totalCuotas: number;
  adeudado: number;
  diasMax: number;
}

const dias = (c: CuotaCobroMes) => Math.abs(c.diasParaVencer);

const colorAtraso = (d: number) =>
  d > 60 ? 'rgba(239,68,68,0.45)' : d > 30 ? 'rgba(249,115,22,0.45)' : 'rgba(245,158,11,0.4)';

/**
 * Cuotas vencidas agrupadas por cliente: una fila por cliente con cuotas, atraso y total adeudado.
 * Al abrir el detalle se ve cada préstamo del cliente por separado, con sus cuotas.
 */
export const CuotasVencidasPorCliente = ({ cuotas, puedeMarcar, onMarcarCobrado, onVerPrestamo, onRecordar }: Props) => {
  const [abiertos, setAbiertos] = useState<Set<string>>(new Set());

  const clientes = useMemo<ClienteAgrupado[]>(() => {
    const porCliente = new Map<string, CuotaCobroMes[]>();
    for (const c of cuotas) {
      const clave = c.clienteId != null ? `id:${c.clienteId}` : `nombre:${c.clienteNombre}|${c.clienteTelefono ?? ''}`;
      porCliente.set(clave, [...(porCliente.get(clave) ?? []), c]);
    }
    return [...porCliente.entries()].map(([clave, lista]) => {
      const porPrestamo = new Map<number, CuotaCobroMes[]>();
      for (const c of lista) porPrestamo.set(c.prestamoId, [...(porPrestamo.get(c.prestamoId) ?? []), c]);
      const prestamos = [...porPrestamo.entries()]
        .map(([prestamoId, cs]) => ({
          prestamoId,
          cuotas: [...cs].sort((a, b) => a.fechaCobro.localeCompare(b.fechaCobro)),
          adeudado: cs.reduce((s, c) => s + c.saldoPendiente, 0),
          diasMax: Math.max(...cs.map(dias)),
        }))
        .sort((a, b) => b.diasMax - a.diasMax);
      return {
        clave,
        nombre: lista[0].clienteNombre,
        telefono: lista[0].clienteTelefono,
        cobradores: [...new Set(lista.map(c => c.cobradorNombre || 'Sin cobrador'))],
        prestamos,
        totalCuotas: lista.length,
        adeudado: lista.reduce((s, c) => s + c.saldoPendiente, 0),
        diasMax: Math.max(...lista.map(dias)),
      };
    }).sort((a, b) => b.diasMax - a.diasMax);
  }, [cuotas]);

  const alternar = (clave: string) => setAbiertos(prev => {
    const n = new Set(prev);
    if (n.has(clave)) n.delete(clave); else n.add(clave);
    return n;
  });

  const totalAdeudado = clientes.reduce((s, c) => s + c.adeudado, 0);

  return (
    <div className="table-container">
      <table>
        <thead>
          <tr>
            <th style={{ width: 32 }}></th>
            <th>Cliente</th>
            <th>Préstamos</th>
            <th>Cuotas vencidas</th>
            <th>Atraso</th>
            <th>Total adeudado</th>
            <th>Cobrador</th>
          </tr>
        </thead>
        <tbody>
          {clientes.map(cl => {
            const abierto = abiertos.has(cl.clave);
            return [
              <tr key={cl.clave} onClick={() => alternar(cl.clave)} style={{ background: 'rgba(239,68,68,0.1)', cursor: 'pointer' }}
                title={abierto ? 'Ocultar detalle' : 'Ver detalle'}>
                <td style={{ color: '#888' }}>{abierto ? '▾' : '▸'}</td>
                <td><strong>{cl.nombre}</strong><div style={{ fontSize: '0.75rem' }}>{cl.telefono}</div></td>
                <td>{cl.prestamos.length === 1 ? `#${cl.prestamos[0].prestamoId}` : `${cl.prestamos.length} préstamos`}</td>
                <td><span className="badge badge-red">{cl.totalCuotas} {cl.totalCuotas === 1 ? 'cuota' : 'cuotas'}</span></td>
                <td><span className="badge" style={{ background: colorAtraso(cl.diasMax), color: '#fff' }}>{cl.diasMax} días</span></td>
                <td className="money" style={{ fontWeight: 700, color: '#ef4444' }}>{formatMoney(cl.adeudado)}</td>
                <td>{cl.cobradores.join(', ')}</td>
              </tr>,
              abierto && (
                <tr key={`${cl.clave}-detalle`}>
                  <td colSpan={7} style={{ padding: '0.5rem 0.75rem 1rem 2.5rem', background: 'rgba(255,255,255,0.02)' }}>
                    {cl.prestamos.map(p => (
                      <div key={p.prestamoId} style={{ marginBottom: '0.75rem' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap', margin: '0.25rem 0 0.4rem' }}>
                          <strong>Préstamo #{p.prestamoId}</strong>
                          <span style={{ fontSize: '0.85rem', color: '#aaa' }}>
                            {p.cuotas.length} {p.cuotas.length === 1 ? 'cuota vencida' : 'cuotas vencidas'} · adeuda {formatMoney(p.adeudado)} · hasta {p.diasMax} días de atraso
                          </span>
                          <button className="btn btn-secondary btn-sm" onClick={() => onVerPrestamo(p.prestamoId)}>Ver préstamo</button>
                        </div>
                        <table>
                          <thead><tr><th>✓</th><th>Cuota</th><th>Fecha</th><th>Días</th><th>Monto</th><th>Acciones</th></tr></thead>
                          <tbody>
                            {p.cuotas.map(c => (
                              <tr key={c.id}>
                                <td><input type="checkbox" checked={c.cobrado} disabled={!puedeMarcar}
                                  onChange={e => onMarcarCobrado(c.id, e.target.checked)} /></td>
                                <td>#{c.numeroCuota}</td>
                                <td style={{ color: '#ef4444' }}>{formatFecha(c.fechaCobro)}</td>
                                <td><span className="badge badge-red">{dias(c)}d</span></td>
                                <td className="money">{formatMoney(c.saldoPendiente)}</td>
                                <td>
                                  <button className="btn btn-danger btn-sm" title="Enviar SMS recordatorio" onClick={() => onRecordar(c.id)}>📱 Cobrar</button>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    ))}
                  </td>
                </tr>
              ),
            ];
          })}
        </tbody>
        <tfoot>
          <tr>
            <td></td>
            <td><strong>{clientes.length} {clientes.length === 1 ? 'cliente' : 'clientes'}</strong></td>
            <td></td>
            <td><strong>{cuotas.length} cuotas</strong></td>
            <td></td>
            <td className="money"><strong>{formatMoney(totalAdeudado)}</strong></td>
            <td></td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
};
