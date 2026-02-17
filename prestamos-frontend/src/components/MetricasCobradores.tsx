import { useEffect, useState } from 'react';
import { dashboardApi } from '../api';
import { MetricasGenerales } from '../types';

const formatMoney = (amount: number): string =>
  new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency: 'COP',
    maximumFractionDigits: 0
  }).format(amount);

const formatPercent = (value: number): string => `${value.toFixed(2)}%`;

export const MetricasCobradores = () => {
  const [metricas, setMetricas] = useState<MetricasGenerales | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchMetricas = async () => {
      try {
        setLoading(true);
        const data = await dashboardApi.getMetricasCobradores();
        setMetricas(data);
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Error al cargar métricas');
      } finally {
        setLoading(false);
      }
    };

    fetchMetricas();
  }, []);

  if (loading) {
    return (
      <div className="p-6 text-center">
        <div className="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
        <p className="mt-2 text-gray-600">Cargando métricas...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6 bg-red-50 border border-red-200 rounded-lg">
        <p className="text-red-700">❌ {error}</p>
      </div>
    );
  }

  if (!metricas) {
    return <div className="p-6 text-gray-600">No hay datos disponibles</div>;
  }

  return (
    <div className="space-y-6">
      <h2 className="text-2xl font-bold text-gray-800 mb-4">📊 Métricas de Cobradores</h2>

      {/* Tarjetas de Métricas Generales */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
        {/* Promedio Tasas Activas */}
        <div className="bg-gradient-to-br from-blue-500 to-blue-600 rounded-lg shadow-lg p-6 text-white">
          <div className="flex items-center justify-between mb-2">
            <h3 className="text-sm font-medium opacity-90">Promedio Tasas Activas</h3>
            <svg className="w-8 h-8 opacity-80" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
          </div>
          <p className="text-3xl font-bold">{formatPercent(metricas.promedioTasasActivos)}</p>
          <p className="text-sm mt-2 opacity-90">Todos los préstamos activos</p>
        </div>

        {/* Capital Fantasma */}
        <div className="bg-gradient-to-br from-purple-500 to-purple-600 rounded-lg shadow-lg p-6 text-white">
          <div className="flex items-center justify-between mb-2">
            <h3 className="text-sm font-medium opacity-90">Capital Fantasma</h3>
            <svg className="w-8 h-8 opacity-80" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <p className="text-3xl font-bold">{formatMoney(metricas.capitalFantasma)}</p>
          <p className="text-sm mt-2 opacity-90">{metricas.totalPrestamosActivos} préstamos activos</p>
        </div>

        {/* Total Cobradores */}
        <div className="bg-gradient-to-br from-green-500 to-green-600 rounded-lg shadow-lg p-6 text-white">
          <div className="flex items-center justify-between mb-2">
            <h3 className="text-sm font-medium opacity-90">Cobradores Activos</h3>
            <svg className="w-8 h-8 opacity-80" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
            </svg>
          </div>
          <p className="text-3xl font-bold">{metricas.estadisticasCobradores.length}</p>
          <p className="text-sm mt-2 opacity-90">Con créditos asignados</p>
        </div>
      </div>

      {/* Tabla de Estadísticas por Cobrador */}
      <div className="bg-white rounded-lg shadow-md overflow-hidden">
        <div className="px-6 py-4 bg-gray-50 border-b border-gray-200">
          <h3 className="text-lg font-semibold text-gray-800">
            📋 Detalle por Cobrador
          </h3>
        </div>

        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Cobrador
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Créditos Activos
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  % Promedio
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  % Neto (−8%)
                </th>
                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Capital Total
                </th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {metricas.estadisticasCobradores.map((cobrador, index) => (
                <tr key={cobrador.cobradorId} className={index % 2 === 0 ? 'bg-white' : 'bg-gray-50'}>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="flex items-center">
                      <div className={`w-10 h-10 rounded-full flex items-center justify-center text-white font-bold mr-3 ${
                        index === 0 ? 'bg-blue-500' : 'bg-green-500'
                      }`}>
                        {cobrador.alias.split(' ')[1]}
                      </div>
                      <div>
                        <div className="text-sm font-medium text-gray-900">{cobrador.alias}</div>
                        <div className="text-xs text-gray-500">ID: {cobrador.cobradorId}</div>
                      </div>
                    </div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium bg-indigo-100 text-indigo-800">
                      {cobrador.totalCreditosActivos} créditos
                    </span>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm font-semibold text-gray-900">
                      {formatPercent(cobrador.promedioTasaInteres)}
                    </div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="flex items-center">
                      <div className={`text-sm font-semibold ${
                        cobrador.promedioTasaInteresNeto > 0 ? 'text-green-600' : 'text-red-600'
                      }`}>
                        {formatPercent(cobrador.promedioTasaInteresNeto)}
                      </div>
                      {cobrador.promedioTasaInteresNeto > 0 && (
                        <svg className="w-4 h-4 ml-1 text-green-500" fill="currentColor" viewBox="0 0 20 20">
                          <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-8.707l-3-3a1 1 0 00-1.414 0l-3 3a1 1 0 001.414 1.414L9 9.414V13a1 1 0 102 0V9.414l1.293 1.293a1 1 0 001.414-1.414z" clipRule="evenodd" />
                        </svg>
                      )}
                    </div>
                  </td>
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="text-sm font-bold text-gray-900">
                      {formatMoney(cobrador.capitalTotalPrestado)}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot className="bg-gray-100 font-semibold">
              <tr>
                <td className="px-6 py-4 text-sm text-gray-900">TOTALES</td>
                <td className="px-6 py-4 text-sm text-gray-900">
                  {metricas.estadisticasCobradores.reduce((sum, c) => sum + c.totalCreditosActivos, 0)} créditos
                </td>
                <td className="px-6 py-4 text-sm text-gray-900">
                  {formatPercent(metricas.promedioTasasActivos)}
                </td>
                <td className="px-6 py-4 text-sm text-gray-900">
                  {formatPercent(metricas.promedioTasasActivos - 8)}
                </td>
                <td className="px-6 py-4 text-sm text-gray-900">
                  {formatMoney(metricas.capitalFantasma)}
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>

      {/* Notas Informativas */}
      <div className="bg-blue-50 border-l-4 border-blue-400 p-4 rounded">
        <div className="flex">
          <div className="flex-shrink-0">
            <svg className="h-5 w-5 text-blue-400" fill="currentColor" viewBox="0 0 20 20">
              <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
            </svg>
          </div>
          <div className="ml-3">
            <h3 className="text-sm font-medium text-blue-800">Información Importante</h3>
            <div className="mt-2 text-sm text-blue-700 space-y-1">
              <p>• <strong>% Neto (−8%):</strong> Porcentaje promedio después de restar el 8% base</p>
              <p>• <strong>Capital Fantasma:</strong> Suma total del monto prestado en todos los créditos activos (sin considerar pagos)</p>
              <p>• <strong>Promedio Tasas Activas:</strong> Promedio de tasas de interés de todos los préstamos activos</p>
              <p>• Los alias "Cobrador 1", "Cobrador 2" protegen la identidad de los cobradores</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
