using PrestamosApi.Models;

namespace PrestamosApi.Services;

public interface IPrestamoService
{
    List<CuotaPrestamo> GenerarCuotas(Prestamo prestamo);
    (decimal MontoTotal, decimal MontoIntereses, decimal MontoCuota, int NumeroCuotas, DateTime FechaVencimiento) 
        CalcularPrestamo(decimal montoPrestado, decimal tasaInteres, string tipoInteres, 
                         string frecuenciaPago, int duracion, string unidadDuracion, DateTime fechaPrestamo);
}

public class PrestamoService : IPrestamoService
{
    public (decimal MontoTotal, decimal MontoIntereses, decimal MontoCuota, int NumeroCuotas, DateTime FechaVencimiento) 
        CalcularPrestamo(decimal montoPrestado, decimal tasaInteres, string tipoInteres, 
                         string frecuenciaPago, int duracion, string unidadDuracion, DateTime fechaPrestamo)
    {
        // Calcular días totales
        int diasTotales = CalcularDiasTotales(duracion, unidadDuracion);
        
        // Calcular número de cuotas según frecuencia
        int numeroCuotas = CalcularNumeroCuotas(diasTotales, frecuenciaPago, duracion, unidadDuracion);
        
        // Calcular intereses
        decimal montoIntereses;
        decimal montoTotal;
        
        if (tipoInteres == "Simple")
        {
            // Convertir días a meses para el cálculo (tasa es mensual)
            decimal meses = diasTotales / 30m;
            // Interés Simple con tasa mensual: I = P * (r/100) * meses
            montoIntereses = montoPrestado * (tasaInteres / 100m) * meses;
            montoTotal = montoPrestado + montoIntereses;
        }
        else // Compuesto
        {
            // Calcular días entre cuotas
            int diasEntreCuotas = CalcularDiasEntreCuotas(frecuenciaPago);
            decimal tasaPorPeriodo = (tasaInteres / 100m) / (365m / diasEntreCuotas);
            
            // Interés Compuesto: M = P * (1 + r)^n
            montoTotal = montoPrestado * (decimal)Math.Pow((double)(1 + tasaPorPeriodo), numeroCuotas);
            montoIntereses = montoTotal - montoPrestado;
        }
        
        decimal montoCuota = Math.Round(montoTotal / numeroCuotas, 2);
        
        // Calcular fecha de vencimiento (fecha de la última cuota)
        DateTime fechaVencimiento = CalcularFechaCuota(fechaPrestamo, frecuenciaPago, numeroCuotas);
        
        return (Math.Round(montoTotal, 2), Math.Round(montoIntereses, 2), montoCuota, numeroCuotas, fechaVencimiento);
    }

    public List<CuotaPrestamo> GenerarCuotas(Prestamo prestamo)
    {
        var cuotas = new List<CuotaPrestamo>();
        
        for (int i = 1; i <= prestamo.NumeroCuotas; i++)
        {
            DateTime fechaCobro = CalcularFechaCuota(prestamo.FechaPrestamo, prestamo.FrecuenciaPago, i);
            
            cuotas.Add(new CuotaPrestamo
            {
                PrestamoId = prestamo.Id,
                NumeroCuota = i,
                FechaCobro = fechaCobro,
                MontoCuota = prestamo.MontoCuota,
                MontoPagado = 0,
                SaldoPendiente = prestamo.MontoCuota,
                EstadoCuota = "Pendiente"
            });
        }
        
        return cuotas;
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

    private DateTime CalcularFechaCuota(DateTime fechaInicial, string frecuenciaPago, int numeroCuota)
    {
        return frecuenciaPago switch
        {
            "Diario" => fechaInicial.AddDays(numeroCuota),
            "Semanal" => fechaInicial.AddDays(numeroCuota * 7),
            "Quincenal" => CalcularFechaQuincenal(fechaInicial, numeroCuota),
            "Mensual" => CalcularFechaMensual(fechaInicial, numeroCuota),
            _ => fechaInicial.AddDays(numeroCuota * 30)
        };
    }

    private DateTime CalcularFechaQuincenal(DateTime fechaInicial, int numeroCuota)
    {
        var fecha = fechaInicial;
        int cuotasGeneradas = 0;
        
        while (cuotasGeneradas < numeroCuota)
        {
            if (fecha.Day < 15)
            {
                fecha = new DateTime(fecha.Year, fecha.Month, 15);
            }
            else if (fecha.Day < DateTime.DaysInMonth(fecha.Year, fecha.Month))
            {
                int ultimoDia = DateTime.DaysInMonth(fecha.Year, fecha.Month);
                fecha = new DateTime(fecha.Year, fecha.Month, ultimoDia);
            }
            else
            {
                fecha = fecha.AddMonths(1);
                fecha = new DateTime(fecha.Year, fecha.Month, 15);
            }
            
            cuotasGeneradas++;
        }
        
        return fecha;
    }

    private DateTime CalcularFechaMensual(DateTime fechaInicial, int numeroCuota)
    {
        var fecha = fechaInicial.AddMonths(numeroCuota);
        
        // Ajustar si el día no existe en el mes destino
        int diaOriginal = fechaInicial.Day;
        int diasEnMes = DateTime.DaysInMonth(fecha.Year, fecha.Month);
        
        if (diaOriginal > diasEnMes)
        {
            fecha = new DateTime(fecha.Year, fecha.Month, diasEnMes);
        }
        
        return fecha;
    }
}
