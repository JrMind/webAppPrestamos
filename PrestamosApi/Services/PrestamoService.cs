using PrestamosApi.Models;

namespace PrestamosApi.Services;

public interface IPrestamoService
{
    List<CuotaPrestamo> GenerarCuotas(Prestamo prestamo, DateTime? fechaPrimerPago = null);
    CuotaPrestamo GenerarSiguienteCuotaCongelada(Prestamo prestamo, CuotaPrestamo ultimaCuota);
    List<DateTime> CalcularFechasCuotas(DateTime fechaInicio, string frecuenciaPago, string? diaSemana, int cantidad);
    CuotaPrestamo? GenerarCuotaCongeladaSiFalta(Prestamo prestamo);
    (decimal MontoTotal, decimal MontoIntereses, decimal MontoCuota, int NumeroCuotas, DateTime FechaVencimiento) 
        CalcularPrestamo(decimal montoPrestado, decimal tasaInteres, string tipoInteres, 
                         string frecuenciaPago, int duracion, string unidadDuracion, DateTime fechaPrestamo,
                         bool esCongelado = false, int? numeroCuotasDirecto = null);
}

public class PrestamoService : IPrestamoService
{
    public (decimal MontoTotal, decimal MontoIntereses, decimal MontoCuota, int NumeroCuotas, DateTime FechaVencimiento) 
        CalcularPrestamo(decimal montoPrestado, decimal tasaInteres, string tipoInteres, 
                         string frecuenciaPago, int duracion, string unidadDuracion, DateTime fechaPrestamo,
                         bool esCongelado = false, int? numeroCuotasDirecto = null)
    {
        // Calcular días totales
        int diasTotales = CalcularDiasTotales(duracion, unidadDuracion);
        
        // Calcular número de cuotas según frecuencia, o usar el valor directo si se especificó
        int numeroCuotas = numeroCuotasDirecto ?? CalcularNumeroCuotas(diasTotales, frecuenciaPago, duracion, unidadDuracion);
        
        decimal montoIntereses;
        decimal montoTotal;
        decimal montoCuota;
        
        if (esCongelado)
        {
            // PRÉSTAMO CONGELADO: Solo paga intereses, capital no reduce
            // Cuota = interés por período (mensual, quincenal, etc.)
            // La tasa es mensual, ajustar según frecuencia
            decimal factorFrecuencia = frecuenciaPago switch
            {
                "Diario" => 1m / 30m,      // 1/30 del interés mensual
                "Semanal" => 7m / 30m,     // 7/30 del interés mensual  
                "Quincenal" => 15m / 30m,  // 15/30 = 0.5 del interés mensual
                "Mensual" => 1m,           // Interés mensual completo
                _ => 1m
            };
            
            // Cuota = Capital * (Tasa/100) * factor de frecuencia
            montoCuota = Math.Round(montoPrestado * (tasaInteres / 100m) * factorFrecuencia, 0);
            
            // Para préstamos congelados:
            // - MontoTotal = Solo el capital (lo que se debe pagar para liquidar)
            // - MontoIntereses = Solo el interés de UN período (ya que es recurrente)
            // - NumeroCuotas = 1 (solo se genera una cuota a la vez)
            montoTotal = montoPrestado; 
            montoIntereses = montoCuota; // Solo interés de un período, no acumulativo
            numeroCuotas = 1; // Solo una cuota se genera inicialmente
        }
        else if (tipoInteres == "Simple")
        {
            // Convertir días a meses para el cálculo (tasa es mensual)
            decimal meses = diasTotales / 30m;
            // Interés Simple con tasa mensual: I = P * (r/100) * meses
            montoIntereses = montoPrestado * (tasaInteres / 100m) * meses;
            montoTotal = montoPrestado + montoIntereses;
            montoCuota = Math.Round(montoTotal / numeroCuotas, 2);
        }
        else // Compuesto
        {
            // Calcular días entre cuotas
            int diasEntreCuotas = CalcularDiasEntreCuotas(frecuenciaPago);
            decimal tasaPorPeriodo = (tasaInteres / 100m) / (365m / diasEntreCuotas);
            
            // Interés Compuesto: M = P * (1 + r)^n
            montoTotal = montoPrestado * (decimal)Math.Pow((double)(1 + tasaPorPeriodo), numeroCuotas);
            montoIntereses = montoTotal - montoPrestado;
            montoCuota = Math.Round(montoTotal / numeroCuotas, 2);
        }
        
        // Fecha de vencimiento = fecha de la última cuota del calendario (la primera cuota cae en la fecha del préstamo).
        // Congelados: solo existe una cuota a la vez; el vencimiento sigue a la cuota pendiente (ver GenerarCuotaCongeladaSiFalta).
        var fechasCalendario = CalcularFechasCuotas(fechaPrestamo, frecuenciaPago, null, numeroCuotas);
        DateTime fechaVencimiento = fechasCalendario[^1];
        
        return (Math.Round(montoTotal, 2), Math.Round(montoIntereses, 2), montoCuota, numeroCuotas, fechaVencimiento);
    }

    public List<CuotaPrestamo> GenerarCuotas(Prestamo prestamo, DateTime? fechaPrimerPago = null)
    {
        var cuotas = new List<CuotaPrestamo>();
        // La primera cuota es la fecha indicada, o un periodo después de la fecha del préstamo
        var fechas = fechaPrimerPago.HasValue
            ? CalcularFechasCuotas(fechaPrimerPago.Value, prestamo.FrecuenciaPago, prestamo.DiaSemana, prestamo.NumeroCuotas)
            : CalcularFechasCuotas(prestamo.FechaPrestamo, prestamo.FrecuenciaPago, prestamo.DiaSemana, prestamo.NumeroCuotas + 1)
                .Skip(1).ToList();

        // Calcular interés y capital por cuota (distribución lineal)
        decimal interesPorCuota = Math.Round(prestamo.MontoIntereses / prestamo.NumeroCuotas, 2);
        decimal capitalPorCuota = Math.Round(prestamo.MontoPrestado / prestamo.NumeroCuotas, 2);
        
        // Ajustar la última cuota para cualquier diferencia de redondeo
        decimal totalInteresCalculado = interesPorCuota * prestamo.NumeroCuotas;
        decimal totalCapitalCalculado = capitalPorCuota * prestamo.NumeroCuotas;
        decimal ajusteInteres = prestamo.MontoIntereses - totalInteresCalculado;
        decimal ajusteCapital = prestamo.MontoPrestado - totalCapitalCalculado;
        
        for (int i = 1; i <= prestamo.NumeroCuotas; i++)
        {
            DateTime fechaActual = fechas[i - 1];

            if (prestamo.EsCongelado)
            {
                // Para préstamos congelados, SOLO generar la primera cuota
                // Las siguientes se generan automáticamente al pagar la anterior
                if (i > 1) break;

                // Asegurar UTC
                var fechaCobro = DateTime.SpecifyKind(fechaActual, DateTimeKind.Utc);
                
                decimal interesCongelado = prestamo.MontoCuota; // La cuota es todo interés
                
                cuotas.Add(new CuotaPrestamo
                {
                    PrestamoId = prestamo.Id,
                    NumeroCuota = 1, // Siempre es la "próxima" cuota
                    FechaCobro = fechaCobro,
                    MontoCuota = interesCongelado,
                    MontoCapital = 0, // No amortiza capital
                    MontoInteres = interesCongelado,
                    MontoPagado = 0,
                    SaldoPendiente = interesCongelado,
                    EstadoCuota = "Pendiente"
                });
                break;
            }

            // Lógica normal para préstamos amortizables
             // Asegurar UTC
            var fechaCobroNormal = DateTime.SpecifyKind(fechaActual, DateTimeKind.Utc);
            
            // Aplicar ajuste de redondeo en la última cuota
            decimal interesCuota = i == prestamo.NumeroCuotas ? interesPorCuota + ajusteInteres : interesPorCuota;
            decimal capitalCuota = i == prestamo.NumeroCuotas ? capitalPorCuota + ajusteCapital : capitalPorCuota;
            
            cuotas.Add(new CuotaPrestamo
            {
                PrestamoId = prestamo.Id,
                NumeroCuota = i,
                FechaCobro = fechaCobroNormal,
                MontoCuota = prestamo.MontoCuota,
                MontoCapital = capitalCuota,
                MontoInteres = interesCuota,
                MontoPagado = 0,
                SaldoPendiente = prestamo.MontoCuota,
                EstadoCuota = "Pendiente"
            });
        }
        
        return cuotas;
    }

    /// <summary>
    /// Si un préstamo congelado sigue vigente (Activo o Vencido) y ya no tiene cuota de interés pendiente,
    /// genera la siguiente y mueve el vencimiento a esa cuota. Requiere prestamo.Cuotas cargadas.
    /// El llamador debe agregar la cuota devuelta al contexto.
    /// Antes solo se generaba con el préstamo en "Activo": un congelado que el worker pasaba a "Vencido"
    /// (o que se cobraba con "Marcar cobrado"/abonos) se quedaba sin cuotas y desaparecía de Cobros e intereses.
    /// </summary>
    public CuotaPrestamo? GenerarCuotaCongeladaSiFalta(Prestamo prestamo)
    {
        if (!prestamo.EsCongelado || prestamo.MontoPrestado <= 0) return null;
        if (prestamo.EstadoPrestamo != "Activo" && prestamo.EstadoPrestamo != "Vencido") return null;
        if (prestamo.Cuotas.Any(c => c.EstadoCuota != "Pagada")) return null;

        var ultima = prestamo.Cuotas
            .OrderByDescending(c => c.FechaCobro)
            .ThenByDescending(c => c.NumeroCuota)
            .FirstOrDefault();
        if (ultima == null) return null;

        var nueva = GenerarSiguienteCuotaCongelada(prestamo, ultima);
        nueva.NumeroCuota = prestamo.Cuotas.Max(c => c.NumeroCuota) + 1;

        var hoy = DateTime.UtcNow.Date;
        if (nueva.FechaCobro.Date < hoy) nueva.EstadoCuota = "Vencida";
        prestamo.FechaVencimiento = nueva.FechaCobro;
        prestamo.EstadoPrestamo = nueva.FechaCobro.Date < hoy ? "Vencido" : "Activo";

        return nueva;
    }

    public CuotaPrestamo GenerarSiguienteCuotaCongelada(Prestamo prestamo, CuotaPrestamo ultimaCuota)
    {
        // Siguiente fecha del calendario anclado a la fecha del préstamo (no se encadena desde la cuota
        // anterior para que un corrimiento por domingo no arrastre las fechas siguientes)
        DateTime fechaCobro = DateTime.SpecifyKind(
            SiguienteFechaCalendario(prestamo.FechaPrestamo, prestamo.FrecuenciaPago, prestamo.DiaSemana, ultimaCuota.FechaCobro),
            DateTimeKind.Utc);

        // Recalcular monto por si hubo abonos a capital
        // Cuota = Capital * (Tasa/100) * factor
        decimal factorFrecuencia = prestamo.FrecuenciaPago switch
        {
            "Diario" => 1m / 30m,
            "Semanal" => 7m / 30m,
            "Quincenal" => 15m / 30m,
            "Mensual" => 1m,
            _ => 1m
        };
        
        decimal nuevoInteres = Math.Round(prestamo.MontoPrestado * (prestamo.TasaInteres / 100m) * factorFrecuencia, 0);

        return new CuotaPrestamo
        {
            PrestamoId = prestamo.Id,
            NumeroCuota = ultimaCuota.NumeroCuota + 1,
            FechaCobro = fechaCobro,
            MontoCuota = nuevoInteres,
            MontoCapital = 0,
            MontoInteres = nuevoInteres,
            MontoPagado = 0,
            SaldoPendiente = nuevoInteres,
            EstadoCuota = "Pendiente"
        };
    }

    /// <summary>
    /// Fechas de cobro de las primeras <paramref name="cantidad"/> cuotas, empezando en <paramref name="fechaInicio"/>.
    /// Cada fecha se calcula sobre el calendario a partir de la fecha de inicio (no encadenada desde la anterior):
    /// - Quincenal: dos días fijos por mes (día de inicio y +15, o -15 si el inicio es después del 15),
    ///   ajustados al último día en meses cortos. Ej: inicio 15 → 15 y 30 (28/29 en febrero).
    /// - Mensual: mismo día de cada mes, ajustado al último día en meses cortos.
    /// - Semanal: cada 7 días, o el día de la semana indicado a partir de la segunda cuota.
    /// - Diario: días consecutivos sin domingos.
    /// Si una fecha cae domingo se cobra el lunes, sin correr las fechas siguientes.
    /// </summary>
    public List<DateTime> CalcularFechasCuotas(DateTime fechaInicio, string frecuenciaPago, string? diaSemana, int cantidad)
    {
        var inicio = fechaInicio.Date;
        var fechas = new List<DateTime>(Math.Max(cantidad, 0));

        if (frecuenciaPago == "Diario")
        {
            var fecha = MoverDomingoALunes(inicio);
            for (int i = 0; i < cantidad; i++)
            {
                if (i > 0) fecha = MoverDomingoALunes(fecha.AddDays(1));
                fechas.Add(DateTime.SpecifyKind(fecha, DateTimeKind.Utc));
            }
            return fechas;
        }

        for (int i = 0; i < cantidad; i++)
            fechas.Add(DateTime.SpecifyKind(MoverDomingoALunes(FechaCalendario(inicio, frecuenciaPago, diaSemana, i)), DateTimeKind.Utc));

        return fechas;
    }

    /// <summary>
    /// Fecha (sin corrimiento por domingo) de la cuota número <paramref name="indice"/> (0 = la de inicio).
    /// </summary>
    private static DateTime FechaCalendario(DateTime inicio, string frecuenciaPago, string? diaSemana, int indice)
    {
        switch (frecuenciaPago)
        {
            case "Diario":
                return inicio.AddDays(indice);

            case "Semanal":
                if (indice == 0 || string.IsNullOrEmpty(diaSemana))
                    return inicio.AddDays(indice * 7);
                return CalcularFechaSemanalPorDia(inicio, indice, diaSemana);

            case "Quincenal":
                return FechaQuincenal(inicio, indice);

            default: // Mensual
                // AddMonths desde el inicio (no encadenado): 31 ene → 28 feb → 31 mar
                return inicio.AddMonths(indice);
        }
    }

    private static DateTime FechaQuincenal(DateTime inicio, int indice)
    {
        int dia = inicio.Day;
        var mesBase = new DateTime(inicio.Year, inicio.Month, 1);

        if (dia <= 15)
        {
            // Días del mes: dia y dia+15 (ej. 15 → 15 y 30; 5 → 5 y 20)
            var mes = mesBase.AddMonths(indice / 2);
            return indice % 2 == 0 ? DiaAjustado(mes, dia) : DiaAjustado(mes, dia + 15);
        }

        // Días del mes: dia y dia-15 del mes siguiente (ej. 20 → 20 y 5; 31 → último día y 16)
        return indice % 2 == 0
            ? DiaAjustado(mesBase.AddMonths(indice / 2), dia)
            : DiaAjustado(mesBase.AddMonths(indice / 2 + 1), dia - 15);
    }

    private static DateTime DiaAjustado(DateTime primeroDelMes, int dia) =>
        primeroDelMes.AddDays(Math.Min(dia, DateTime.DaysInMonth(primeroDelMes.Year, primeroDelMes.Month)) - 1);

    private static DateTime MoverDomingoALunes(DateTime fecha) =>
        fecha.DayOfWeek == DayOfWeek.Sunday ? fecha.AddDays(1) : fecha;

    /// <summary>
    /// Primera fecha del calendario anclado en <paramref name="fechaAncla"/> que sea posterior a <paramref name="despuesDe"/>.
    /// </summary>
    private static DateTime SiguienteFechaCalendario(DateTime fechaAncla, string frecuenciaPago, string? diaSemana, DateTime despuesDe)
    {
        var ancla = fechaAncla.Date;
        var limite = despuesDe.Date;

        if (frecuenciaPago != "Diario")
        {
            for (int i = 0; i < 10000; i++)
            {
                var fecha = MoverDomingoALunes(FechaCalendario(ancla, frecuenciaPago, diaSemana, i));
                if (fecha > limite) return fecha;
            }
        }
        return MoverDomingoALunes(limite.AddDays(1));
    }

    private int CalcularDiasTotales(int duracion, string unidadDuracion)
    {
        return unidadDuracion switch
        {
            "Dias" => duracion,
            "Semanas" => duracion * 7,
            "Quincenas" => duracion * 15,
            "Meses" => duracion * 30,
            _ => duracion * 30
        };
    }

    private int CalcularNumeroCuotas(int diasTotales, string frecuenciaPago, int duracion, string unidadDuracion)
    {
        // Si la unidad de duración coincide con la frecuencia, usar la duración directamente
        if (unidadDuracion == "Quincenas" && frecuenciaPago == "Quincenal") return duracion;
        if (unidadDuracion == "Meses" && frecuenciaPago == "Mensual") return duracion;
        if (unidadDuracion == "Semanas" && frecuenciaPago == "Semanal") return duracion;
        if (unidadDuracion == "Dias" && frecuenciaPago == "Diario") return duracion;
        
        // Ajustes específicos por negocio
        if (unidadDuracion == "Meses")
        {
            if (frecuenciaPago == "Semanal") return duracion * 4; // 1 mes = 4 semanas
            if (frecuenciaPago == "Quincenal") return duracion * 2; // 1 mes = 2 quincenas
        }

        // Calcular basado en días totales
        int diasEntreCuotas = CalcularDiasEntreCuotas(frecuenciaPago);
        return Math.Max(1, (int)Math.Ceiling((double)diasTotales / diasEntreCuotas));
    }

    private int CalcularDiasEntreCuotas(string frecuenciaPago)
    {
        return frecuenciaPago switch
        {
            "Diario" => 1,
            "Semanal" => 7,
            "Quincenal" => 15,
            "Mensual" => 30,
            _ => 30
        };
    }

    private static DateTime CalcularFechaSemanalPorDia(DateTime fechaInicial, int numeroCuota, string diaSemana)
    {
        // Mapear nombre del día a DayOfWeek
        DayOfWeek diaObjetivo = diaSemana switch
        {
            "Lunes" => DayOfWeek.Monday,
            "Martes" => DayOfWeek.Tuesday,
            "Miércoles" => DayOfWeek.Wednesday,
            "Jueves" => DayOfWeek.Thursday,
            "Viernes" => DayOfWeek.Friday,
            "Sábado" => DayOfWeek.Saturday,
            "Domingo" => DayOfWeek.Sunday,
            _ => DayOfWeek.Monday
        };
        
        // Encontrar el próximo día de la semana después de la fecha inicial
        int diasHastaProximo = ((int)diaObjetivo - (int)fechaInicial.DayOfWeek + 7) % 7;
        if (diasHastaProximo == 0) diasHastaProximo = 7; // Si hoy es el día, ir al próximo
        
        // Primera cuota es el próximo día objetivo
        DateTime primeraCuota = fechaInicial.AddDays(diasHastaProximo);
        
        // Las siguientes cuotas son cada 7 días
        return primeraCuota.AddDays((numeroCuota - 1) * 7);
    }
}
