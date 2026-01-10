using PrestamosApi.Models;

namespace PrestamosApi.Services;

public interface IPrestamoService
{
    List<CuotaPrestamo> GenerarCuotas(Prestamo prestamo, DateTime? fechaPrimerPago = null);
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
            
            // Para préstamos congelados, el "MontoTotal" es teóricamente infinito
            // pero usamos el capital como referencia (se paga cuando abonan extra)
            montoTotal = montoPrestado; // Solo el capital, intereses son recurrentes
            montoIntereses = montoCuota * numeroCuotas; // Intereses proyectados para el período inicial
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
        
        // Calcular fecha de vencimiento (fecha de la última cuota)
        DateTime fechaVencimiento = CalcularFechaCuota(fechaPrestamo, frecuenciaPago, numeroCuotas);
        
        return (Math.Round(montoTotal, 2), Math.Round(montoIntereses, 2), montoCuota, numeroCuotas, fechaVencimiento);
    }

    public List<CuotaPrestamo> GenerarCuotas(Prestamo prestamo, DateTime? fechaPrimerPago = null)
    {
        var cuotas = new List<CuotaPrestamo>();
        // La primera cuota es la fecha indicada, o se calcula desde la fecha del préstamo
        DateTime fechaBase = fechaPrimerPago ?? prestamo.FechaPrestamo; 
        
        // Si no se especificó fechaPrimerPago, calculamos la primera cuota según frecuencia
        if (fechaPrimerPago == null)
        {
             fechaBase = CalcularProximaFecha(prestamo.FechaPrestamo, prestamo.FrecuenciaPago, prestamo.DiaSemana);
        }

        DateTime fechaActual = fechaBase;

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
             // Para la primera cuota usamos la fecha base decidida
             // Para las siguientes, sumamos el periodo a la fecha anterior
            if (i > 1)
            {
                fechaActual = CalcularProximaFecha(fechaActual, prestamo.FrecuenciaPago, prestamo.DiaSemana);
            }

             // Asegurar UTC
            var fechaCobro = DateTime.SpecifyKind(fechaActual, DateTimeKind.Utc);
            
            // Aplicar ajuste de redondeo en la última cuota
            decimal interesCuota = i == prestamo.NumeroCuotas ? interesPorCuota + ajusteInteres : interesPorCuota;
            decimal capitalCuota = i == prestamo.NumeroCuotas ? capitalPorCuota + ajusteCapital : capitalPorCuota;
            
            cuotas.Add(new CuotaPrestamo
            {
                PrestamoId = prestamo.Id,
                NumeroCuota = i,
                FechaCobro = fechaCobro,
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

    private DateTime CalcularProximaFecha(DateTime fechaAnterior, string frecuencia, string? diaSemana)
    {
        if (frecuencia == "Semanal" && !string.IsNullOrEmpty(diaSemana))
        {
             // Si ya estamos en una fecha alineada al día, sumamos 7. Si no, buscamos el día.
             return CalcularFechaSemanalPorDia(fechaAnterior.AddDays(1), 1, diaSemana);
        }

         return frecuencia switch
        {
            "Diario" => fechaAnterior.AddDays(1),
            "Semanal" => fechaAnterior.AddDays(7),
            "Quincenal" => fechaAnterior.AddDays(15),
            "Mensual" => fechaAnterior.AddMonths(1),
            _ => fechaAnterior.AddMonths(1)
        };
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

    private DateTime CalcularFechaCuota(DateTime fechaInicial, string frecuenciaPago, int numeroCuota, string? diaSemana = null)
    {
        DateTime fecha;
        
        if (frecuenciaPago == "Semanal" && !string.IsNullOrEmpty(diaSemana))
        {
            // Calcular basado en día de semana específico
            fecha = CalcularFechaSemanalPorDia(fechaInicial, numeroCuota, diaSemana);
        }
        else
        {
            fecha = frecuenciaPago switch
            {
                "Diario" => fechaInicial.AddDays(numeroCuota),
                "Semanal" => fechaInicial.AddDays(numeroCuota * 7),
                "Quincenal" => CalcularFechaQuincenal(fechaInicial, numeroCuota),
                "Mensual" => CalcularFechaMensual(fechaInicial, numeroCuota),
                _ => fechaInicial.AddDays(numeroCuota * 30)
            };
        }
        
        // Asegurar que la fecha sea UTC para PostgreSQL
        return DateTime.SpecifyKind(fecha, DateTimeKind.Utc);
    }

    private DateTime CalcularFechaSemanalPorDia(DateTime fechaInicial, int numeroCuota, string diaSemana)
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

    /// <summary>
    /// Calcula la fecha de una cuota quincenal sumando exactamente 15 días calendario
    /// por cada cuota desde la fecha inicial.
    /// </summary>
    private DateTime CalcularFechaQuincenal(DateTime fechaInicial, int numeroCuota)
    {
        // Quincenal = exactamente 15 días calendario por cuota
        return fechaInicial.AddDays(numeroCuota * 15);
    }

    private DateTime CalcularFechaMensual(DateTime fechaInicial, int numeroCuota)
    {
        var fecha = fechaInicial.AddMonths(numeroCuota);
        
        // Ajustar si el día no existe en el mes destino
        int diaOriginal = fechaInicial.Day;
        int diasEnMes = DateTime.DaysInMonth(fecha.Year, fecha.Month);
        
        if (diaOriginal > diasEnMes)
        {
            fecha = new DateTime(fecha.Year, fecha.Month, diasEnMes, 0, 0, 0, DateTimeKind.Utc);
        }
        
        return fecha;
    }
}
