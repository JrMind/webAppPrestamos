// Réplica de PrestamoService.CalcularFechasCuotas (backend) para la vista previa del préstamo.
// Las fechas se calculan sobre el calendario desde la fecha de inicio (no encadenadas):
// - Quincenal: día de inicio y +15 (o -15 si el inicio es después del 15), ajustado al último día del mes.
// - Mensual: mismo día de cada mes, ajustado al último día del mes.
// - Semanal: cada 7 días, o el día de la semana elegido desde la segunda cuota.
// - Diario: días consecutivos sin domingos.
// Si una fecha cae domingo se cobra el lunes, sin correr las siguientes.

const DIAS_SEMANA: Record<string, number> = {
  Domingo: 0, Lunes: 1, Martes: 2, 'Miércoles': 3, Jueves: 4, Viernes: 5, 'Sábado': 6,
};

const diaAjustado = (anio: number, mes: number, dia: number): Date => {
  const diasEnMes = new Date(anio, mes + 1, 0).getDate();
  return new Date(anio, mes, Math.min(dia, diasEnMes));
};

const moverDomingoALunes = (d: Date): Date =>
  d.getDay() === 0 ? new Date(d.getFullYear(), d.getMonth(), d.getDate() + 1) : d;

const fechaCalendario = (inicio: Date, frecuencia: string, diaSemana: string | undefined, i: number): Date => {
  const y = inicio.getFullYear(), m = inicio.getMonth(), d = inicio.getDate();
  switch (frecuencia) {
    case 'Semanal': {
      if (i === 0 || !diaSemana) return new Date(y, m, d + i * 7);
      const objetivo = DIAS_SEMANA[diaSemana] ?? 1;
      let hasta = (objetivo - inicio.getDay() + 7) % 7;
      if (hasta === 0) hasta = 7;
      return new Date(y, m, d + hasta + (i - 1) * 7);
    }
    case 'Quincenal':
      if (d <= 15) return diaAjustado(y, m + Math.floor(i / 2), i % 2 === 0 ? d : d + 15);
      return i % 2 === 0
        ? diaAjustado(y, m + Math.floor(i / 2), d)
        : diaAjustado(y, m + Math.floor(i / 2) + 1, d - 15);
    default: // Mensual
      return diaAjustado(y, m + i, d);
  }
};

/** fechaInicio en formato 'YYYY-MM-DD'. Devuelve las fechas de las primeras `cantidad` cuotas. */
export const calcularFechasCuotas = (fechaInicio: string, frecuencia: string, diaSemana: string | undefined, cantidad: number): Date[] => {
  const [y, m, d] = fechaInicio.split('-').map(Number);
  if (!y || !m || !d) return [];
  const inicio = new Date(y, m - 1, d);
  const fechas: Date[] = [];

  if (frecuencia === 'Diario') {
    let f = moverDomingoALunes(inicio);
    for (let i = 0; i < cantidad; i++) {
      if (i > 0) f = moverDomingoALunes(new Date(f.getFullYear(), f.getMonth(), f.getDate() + 1));
      fechas.push(f);
    }
    return fechas;
  }

  for (let i = 0; i < cantidad; i++) fechas.push(moverDomingoALunes(fechaCalendario(inicio, frecuencia, diaSemana, i)));
  return fechas;
};
