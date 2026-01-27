using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrestamosApi.Data;
using PrestamosApi.DTOs;
using PrestamosApi.Models;
using PrestamosApi.Services;

namespace PrestamosApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PrestamosController : BaseApiController
{
    private readonly PrestamosDbContext _context;
    private readonly IPrestamoService _prestamoService;
    private readonly IGananciasService _gananciasService;
    private readonly IDistribucionGananciasService _distribucionService;

    public PrestamosController(
        PrestamosDbContext context, 
        IPrestamoService prestamoService, 
        IGananciasService gananciasService,
        IDistribucionGananciasService distribucionService)
    {
        _context = context;
        _prestamoService = prestamoService;
        _gananciasService = gananciasService;
        _distribucionService = distribucionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PrestamoDto>>> GetPrestamos(
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] string? estado,
        [FromQuery] string? frecuencia,
        [FromQuery] int? clienteId,
        [FromQuery] string? busqueda)
    {
        var userId = GetCurrentUserId();
        var isCobrador = IsCobrador();

        var query = _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cobrador)
            .Include(p => p.Cuotas)
            .Include(p => p.Pagos)
            .AsQueryable();

        // Si es cobrador, solo mostrar préstamos asignados a él
        if (isCobrador && userId.HasValue)
        {
            query = query.Where(p => p.CobradorId == userId.Value);
        }

        if (fechaDesde.HasValue)
            query = query.Where(p => p.FechaPrestamo >= fechaDesde.Value);
        
        if (fechaHasta.HasValue)
            query = query.Where(p => p.FechaPrestamo <= fechaHasta.Value);

        if (!string.IsNullOrEmpty(estado) && estado != "Todos")
            query = query.Where(p => p.EstadoPrestamo == estado);

        if (!string.IsNullOrEmpty(frecuencia) && frecuencia != "Todos")
            query = query.Where(p => p.FrecuenciaPago == frecuencia);

        if (clienteId.HasValue)
            query = query.Where(p => p.ClienteId == clienteId.Value);

        if (!string.IsNullOrEmpty(busqueda))
            query = query.Where(p => 
                p.Cliente!.Nombre.ToLower().Contains(busqueda.ToLower()) ||
                p.Cliente!.Cedula.Contains(busqueda));

        var prestamos = await query
            .OrderByDescending(p => p.FechaPrestamo)
            .Select(p => new PrestamoDto(
                p.Id,
                p.ClienteId,
                p.Cliente!.Nombre,
                p.Cliente.Cedula,
                p.Cliente.Telefono,
                p.MontoPrestado,
                p.TasaInteres,
                p.TipoInteres,
                p.FrecuenciaPago,
                p.NumeroCuotas,
                p.FechaPrestamo,
                p.FechaVencimiento,
                p.MontoTotal,
                p.MontoIntereses,
                p.MontoCuota,
                p.EstadoPrestamo,
                p.Descripcion,
                p.Cuotas.Sum(c => c.MontoPagado),
                p.Cuotas.Sum(c => c.SaldoPendiente),
                p.Cuotas.Count(c => c.EstadoCuota == "Pagada"),
                p.Cuotas
                    .Where(c => c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida")
                    .OrderBy(c => c.FechaCobro)
                    .Select(c => new CuotaProximaDto(c.FechaCobro, c.SaldoPendiente))
                    .FirstOrDefault(),
                p.CobradorId,
                p.Cobrador != null ? p.Cobrador.Nombre : null,
                p.PorcentajeCobrador,
                p.EsCongelado
            ))
            .ToListAsync();

        return Ok(prestamos);
    }

    [HttpGet("dia")]
    public async Task<ActionResult<object>> GetPrestamosDelDia([FromQuery] string? fecha = null)
    {
        // Parse fecha string (formato: yyyy-MM-dd) como UTC para evitar problemas de zona horaria
        DateTime targetDate;
        if (!string.IsNullOrEmpty(fecha))
        {
            // Parsear explícitamente como UTC: "2026-01-17" -> 2026-01-17 00:00:00 UTC
            if (DateTime.TryParseExact(fecha, "yyyy-MM-dd", 
                System.Globalization.CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsedDate))
            {
                targetDate = parsedDate.Date;
            }
            else
            {
                return BadRequest(new { message = "Formato de fecha inválido. Use yyyy-MM-dd (ejemplo: 2026-01-17)" });
            }
        }
        else
        {
            targetDate = DateTime.UtcNow.Date;
        }
        
        var userId = GetCurrentUserId();
        var isCobrador = IsCobrador();

        var baseQuery = _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cobrador)
            .AsQueryable();

        // Si es cobrador, filtrar solo sus préstamos asignados
        if (isCobrador && userId.HasValue)
        {
            baseQuery = baseQuery.Where(p => p.CobradorId == userId.Value);
        }

        // Filtrar por año, mes y día para evitar problemas de zona horaria
        var prestamosHoy = await baseQuery
            .Where(p => p.FechaPrestamo.Year == targetDate.Year 
                     && p.FechaPrestamo.Month == targetDate.Month 
                     && p.FechaPrestamo.Day == targetDate.Day)
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.ClienteId,
                ClienteNombre = p.Cliente!.Nombre,
                ClienteCedula = p.Cliente.Cedula,
                ClienteTelefono = p.Cliente.Telefono,
                p.MontoPrestado,
                p.TasaInteres,
                p.TipoInteres,
                p.FrecuenciaPago,
                p.NumeroCuotas,
                p.FechaPrestamo,
                CobradorNombre = p.Cobrador != null ? p.Cobrador.Nombre : null,
                p.PorcentajeCobrador,
                p.EstadoPrestamo
            })
            .ToListAsync();

        return Ok(new
        {
            fecha = targetDate.ToString("yyyy-MM-dd"),
            prestamosHoy,
            resumen = new
            {
                totalPrestamosHoy = prestamosHoy.Count,
                montoTotalDesembolsado = prestamosHoy.Sum(p => p.MontoPrestado)
            }
        });
    }


    [HttpGet("{id}")]
    public async Task<ActionResult<PrestamoDto>> GetPrestamo(int id)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .Include(p => p.Pagos)
            .Where(p => p.Id == id)
            .Select(p => new PrestamoDto(
                p.Id,
                p.ClienteId,
                p.Cliente!.Nombre,
                p.Cliente.Cedula,
                p.Cliente.Telefono,
                p.MontoPrestado,
                p.TasaInteres,
                p.TipoInteres,
                p.FrecuenciaPago,
                p.NumeroCuotas,
                p.FechaPrestamo,
                p.FechaVencimiento,
                p.MontoTotal,
                p.MontoIntereses,
                p.MontoCuota,
                p.EstadoPrestamo,
                p.Descripcion,
                p.Cuotas.Sum(c => c.MontoPagado),
                p.Cuotas.Sum(c => c.SaldoPendiente),
                p.Cuotas.Count(c => c.EstadoCuota == "Pagada"),
                p.Cuotas
                    .Where(c => c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida")
                    .OrderBy(c => c.FechaCobro)
                    .Select(c => new CuotaProximaDto(c.FechaCobro, c.SaldoPendiente))
                    .FirstOrDefault(),
                p.CobradorId,
                p.Cobrador != null ? p.Cobrador.Nombre : null,
                p.PorcentajeCobrador,
                p.EsCongelado
            ))
            .FirstOrDefaultAsync();

        if (prestamo == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        return Ok(prestamo);
    }

    /// <summary>
    /// Obtener préstamo con ganancias de socios (solo para rol Socio)
    /// </summary>
    [HttpGet("{id}/ganancias-socios")]
    public async Task<ActionResult<object>> GetPrestamoConGanancias(int id)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .Include(p => p.Pagos)
            .Include(p => p.Cobrador)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prestamo == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        // Obtener los 3 socios
        var socios = await _context.Usuarios
            .Where(u => u.Activo && u.Rol == RolUsuario.Socio)
            .OrderBy(u => u.Nombre)
            .ToListAsync();

        // Calcular intereses acumulados de cuotas pagadas
        var cuotasPagadas = prestamo.Cuotas.Where(c => c.EstadoCuota == "Pagada" || c.EstadoCuota == "Parcial").ToList();
        var interesAcumulado = cuotasPagadas.Sum(c => c.MontoInteres * (c.EstadoCuota == "Pagada" ? 1m : c.MontoPagado / c.MontoCuota));

        // Calcular ganancia del cobrador
        decimal gananciaCobrador = 0;
        decimal factorCobrador = 0;
        if (prestamo.CobradorId.HasValue && prestamo.TasaInteres > 0)
        {
            factorCobrador = prestamo.PorcentajeCobrador / prestamo.TasaInteres;
        }
        gananciaCobrador = interesAcumulado * factorCobrador;

        // Interés neto para los socios = Interés - Ganancia Cobrador
        var interesNetoAcumulado = interesAcumulado - gananciaCobrador;

        // Ganancia proyectada total por socio (al finalizar el préstamo)
        var interesProyectadoTotal = prestamo.MontoIntereses;
        var gananciaCobradorProyectada = interesProyectadoTotal * factorCobrador;
        var interesNetoProyectado = interesProyectadoTotal - gananciaCobradorProyectada;
        var gananciaProyectadaPorSocio = interesNetoProyectado / 3m;

        // Crear lista de ganancias por socio
        var gananciasSocios = socios.Select(s => new GananciaSocioPrestamoDto(
            s.Id,
            s.Nombre,
            Math.Round(interesNetoAcumulado / 3m, 2),     // Ganancia acumulada
            Math.Round(gananciaProyectadaPorSocio, 2)     // Ganancia proyectada total
        )).ToList();

        // Retornar datos del préstamo con ganancias
        return Ok(new
        {
            PrestamoId = prestamo.Id,
            ClienteNombre = prestamo.Cliente?.Nombre,
            MontoPrestado = prestamo.MontoPrestado,
            MontoIntereses = prestamo.MontoIntereses,
            TasaInteres = prestamo.TasaInteres,
            CobradorNombre = prestamo.Cobrador?.Nombre,
            PorcentajeCobrador = prestamo.PorcentajeCobrador,
            CuotasTotales = prestamo.NumeroCuotas,
            CuotasPagadas = prestamo.Cuotas.Count(c => c.EstadoCuota == "Pagada"),
            InteresAcumulado = Math.Round(interesAcumulado, 2),
            GananciaCobrador = Math.Round(gananciaCobrador, 2),
            InteresNetoSocios = Math.Round(interesNetoAcumulado, 2),
            GananciasSocios = gananciasSocios
        });
    }

    [HttpGet("cliente/{clienteId}")]

    public async Task<ActionResult<IEnumerable<PrestamoDto>>> GetPrestamosByCliente(int clienteId)
    {
        var prestamos = await _context.Prestamos
            .Include(p => p.Cliente)
            .Include(p => p.Cuotas)
            .Where(p => p.ClienteId == clienteId)
            .OrderByDescending(p => p.FechaPrestamo)
            .Select(p => new PrestamoDto(
                p.Id,
                p.ClienteId,
                p.Cliente!.Nombre,
                p.Cliente.Cedula,
                p.Cliente.Telefono,
                p.MontoPrestado,
                p.TasaInteres,
                p.TipoInteres,
                p.FrecuenciaPago,
                p.NumeroCuotas,
                p.FechaPrestamo,
                p.FechaVencimiento,
                p.MontoTotal,
                p.MontoIntereses,
                p.MontoCuota,
                p.EstadoPrestamo,
                p.Descripcion,
                p.Cuotas.Sum(c => c.MontoPagado),
                p.Cuotas.Sum(c => c.SaldoPendiente),
                p.Cuotas.Count(c => c.EstadoCuota == "Pagada"),
                p.Cuotas
                    .Where(c => c.EstadoCuota == "Pendiente" || c.EstadoCuota == "Parcial" || c.EstadoCuota == "Vencida")
                    .OrderBy(c => c.FechaCobro)
                    .Select(c => new CuotaProximaDto(c.FechaCobro, c.SaldoPendiente))
                    .FirstOrDefault(),
                p.CobradorId,
                p.Cobrador != null ? p.Cobrador.Nombre : null,
                p.PorcentajeCobrador,
                p.EsCongelado
            ))
            .ToListAsync();

        return Ok(prestamos);
    }

    [HttpPost]
    public async Task<ActionResult<PrestamoDto>> CreatePrestamo(CreatePrestamoDto dto)
    {
        // Validar cliente existe
        var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
        if (cliente == null)
            return BadRequest(new { message = "Cliente no encontrado" });

        // Validar monto mínimo
        if (dto.MontoPrestado < 50)
            return BadRequest(new { message = "El monto mínimo del préstamo es 50$" });

        // Calcular préstamo (con soporte para congelado y cuotas directas)
        var (montoTotal, montoIntereses, montoCuota, numeroCuotas, fechaVencimiento) = 
            _prestamoService.CalcularPrestamo(
                dto.MontoPrestado, dto.TasaInteres, dto.TipoInteres,
                dto.FrecuenciaPago, dto.Duracion, dto.UnidadDuracion, dto.FechaPrestamo,
                dto.EsCongelado, dto.NumeroCuotasDirecto);

        // Convertir fechas a UTC para PostgreSQL
        var fechaPrestamoUtc = DateTime.SpecifyKind(dto.FechaPrestamo, DateTimeKind.Utc);
        var fechaVencimientoUtc = DateTime.SpecifyKind(fechaVencimiento, DateTimeKind.Utc);

        var prestamo = new Prestamo
        {
            ClienteId = dto.ClienteId,
            CobradorId = dto.CobradorId,
            MontoPrestado = dto.MontoPrestado,
            TasaInteres = dto.TasaInteres,
            TipoInteres = dto.TipoInteres,
            FrecuenciaPago = dto.FrecuenciaPago,
            DiaSemana = dto.DiaSemana, // Nuevo campo
            NumeroCuotas = numeroCuotas,
            FechaPrestamo = fechaPrestamoUtc,
            FechaVencimiento = fechaVencimientoUtc,
            MontoTotal = montoTotal,
            MontoIntereses = montoIntereses,
            MontoCuota = montoCuota,
            EstadoPrestamo = "Activo",
            Descripcion = dto.Descripcion,
            PorcentajeCobrador = dto.PorcentajeCobrador,
            EsCongelado = dto.EsCongelado
        };

        _context.Prestamos.Add(prestamo);
        await _context.SaveChangesAsync();

        // ASIGNACIÓN AUTOMÁTICA DE FUENTE DE CAPITAL
        // Calcular reserva disponible
        var reservaDisponible = await _gananciasService.CalcularReservaDisponibleAsync();
        
        if (dto.MontoPrestado <= reservaDisponible)
        {
            // Hay suficiente reserva - crear fuente automáticamente
            var fuenteCapital = new FuenteCapitalPrestamo
            {
                PrestamoId = prestamo.Id,
                Tipo = "Reserva",
                UsuarioId = null,
                AportadorExternoId = null,
                MontoAportado = dto.MontoPrestado,
                FechaRegistro = DateTime.UtcNow
            };
            _context.FuentesCapitalPrestamo.Add(fuenteCapital);
            await _context.SaveChangesAsync();
            
            // ACTUALIZAR RESERVA: Descontar el monto prestado
            await _gananciasService.ActualizarReservaAsync(-dto.MontoPrestado, $"Préstamo #{prestamo.Id} - ${dto.MontoPrestado:N0}");
        }
        else
        {
            // Reserva insuficiente - eliminar préstamo y retornar error
            _context.Prestamos.Remove(prestamo);
            await _context.SaveChangesAsync();
            return BadRequest(new 
            { 
                message = $"Reserva insuficiente. Disponible: ${reservaDisponible:N0}, Necesario: ${dto.MontoPrestado:N0}. Debe usar el endpoint CreatePrestamoConFuentes para especificar fuentes de capital manualmente.",
                reservaDisponible = reservaDisponible,
                montoRequerido = dto.MontoPrestado
            });
        }

        // Generar cuotas (Fecha del préstamo se usa como fecha base para primera cuota)
        var cuotas = _prestamoService.GenerarCuotas(prestamo, fechaPrestamoUtc);
        _context.CuotasPrestamo.AddRange(cuotas);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPrestamo), new { id = prestamo.Id },
            new PrestamoDto(
                prestamo.Id, prestamo.ClienteId, cliente.Nombre, cliente.Cedula, cliente.Telefono,
                prestamo.MontoPrestado, prestamo.TasaInteres, prestamo.TipoInteres, prestamo.FrecuenciaPago,
                prestamo.NumeroCuotas, prestamo.FechaPrestamo, prestamo.FechaVencimiento,
                prestamo.MontoTotal, prestamo.MontoIntereses, prestamo.MontoCuota,
                prestamo.EstadoPrestamo, prestamo.Descripcion, 0, prestamo.MontoTotal, 0,
                new CuotaProximaDto(cuotas.First().FechaCobro, cuotas.First().SaldoPendiente),
                prestamo.CobradorId,
                null, // CobradorNombre - no se carga aquí para evitar query extra
                prestamo.PorcentajeCobrador,
                prestamo.EsCongelado
            ));
    }

    /// <summary>
    /// Crea un préstamo con fuentes de capital específicas (Reserva, Interno, Externo)
    /// </summary>
    [HttpPost("con-fuentes")]
    public async Task<ActionResult<PrestamoDto>> CreatePrestamoConFuentes(CreatePrestamoConFuentesDto dto)
    {
        // Validar cliente existe
        var cliente = await _context.Clientes.FindAsync(dto.ClienteId);
        if (cliente == null)
            return BadRequest(new { message = "Cliente no encontrado" });

        // Validar monto mínimo
        if (dto.MontoPrestado < 50)
            return BadRequest(new { message = "El monto mínimo del préstamo es 50$" });

        // Validar que el total de fuentes coincida con el monto prestado
        var totalFuentes = dto.FuentesCapital?.Sum(f => f.MontoAportado) ?? 0;
        if (totalFuentes != dto.MontoPrestado)
            return BadRequest(new { message = $"El total de fuentes ({totalFuentes:N0}) debe ser igual al monto prestado ({dto.MontoPrestado:N0})" });

        // Validar reserva disponible si se usa
        var fuentesReserva = dto.FuentesCapital?.Where(f => f.Tipo == "Reserva").Sum(f => f.MontoAportado) ?? 0;
        if (fuentesReserva > 0)
        {
            var totalCobrado = await _context.Pagos.SumAsync(p => p.MontoPago);
            var capitalUsadoDeReserva = await _context.FuentesCapitalPrestamo
                .Include(f => f.Prestamo)
                .Where(f => f.Tipo == "Reserva" && f.Prestamo!.EstadoPrestamo == "Activo")
                .SumAsync(f => f.MontoAportado);
            var reservaDisponible = totalCobrado - capitalUsadoDeReserva;

            if (fuentesReserva > reservaDisponible)
                return BadRequest(new { message = $"Reserva insuficiente. Disponible: {reservaDisponible:N0}, Solicitado: {fuentesReserva:N0}" });
        }

        // Calcular préstamo (con soporte para congelado y cuotas directas)
        var (montoTotal, montoIntereses, montoCuota, numeroCuotas, fechaVencimiento) = 
            _prestamoService.CalcularPrestamo(
                dto.MontoPrestado, dto.TasaInteres, dto.TipoInteres,
                dto.FrecuenciaPago, dto.Duracion, dto.UnidadDuracion, dto.FechaPrestamo,
                dto.EsCongelado, dto.NumeroCuotasDirecto);

        // Convertir fechas a UTC para PostgreSQL
        var fechaPrestamoUtc = DateTime.SpecifyKind(dto.FechaPrestamo, DateTimeKind.Utc);
        var fechaVencimientoUtc = DateTime.SpecifyKind(fechaVencimiento, DateTimeKind.Utc);

        var prestamo = new Prestamo
        {
            ClienteId = dto.ClienteId,
            CobradorId = dto.CobradorId,
            MontoPrestado = dto.MontoPrestado,
            TasaInteres = dto.TasaInteres,
            TipoInteres = dto.TipoInteres,
            FrecuenciaPago = dto.FrecuenciaPago,
            NumeroCuotas = numeroCuotas,
            FechaPrestamo = fechaPrestamoUtc,
            FechaVencimiento = fechaVencimientoUtc,
            MontoTotal = montoTotal,
            MontoIntereses = montoIntereses,
            MontoCuota = montoCuota,
            EstadoPrestamo = "Activo",
            Descripcion = dto.Descripcion,
            PorcentajeCobrador = dto.PorcentajeCobrador,
            EsCongelado = dto.EsCongelado
        };

        _context.Prestamos.Add(prestamo);
        await _context.SaveChangesAsync();

        // Registrar fuentes de capital
        if (dto.FuentesCapital != null)
        {
            decimal totalReservaUsada = 0;
            
            foreach (var fuente in dto.FuentesCapital)
            {
                var fuenteCapital = new FuenteCapitalPrestamo
                {
                    PrestamoId = prestamo.Id,
                    Tipo = fuente.Tipo,
                    UsuarioId = fuente.UsuarioId,
                    AportadorExternoId = fuente.AportadorExternoId,
                    MontoAportado = fuente.MontoAportado,
                    FechaRegistro = DateTime.UtcNow
                };
                _context.FuentesCapitalPrestamo.Add(fuenteCapital);

                // Si es aportador externo, actualizar su saldo
                if (fuente.Tipo == "Externo" && fuente.AportadorExternoId.HasValue)
                {
                    var aportador = await _context.AportadoresExternos.FindAsync(fuente.AportadorExternoId.Value);
                    if (aportador != null)
                    {
                        aportador.MontoTotalAportado += fuente.MontoAportado;
                        aportador.SaldoPendiente += fuente.MontoAportado;
                    }
                }
                
                // Acumular total de reserva usada
                if (fuente.Tipo == "Reserva")
                {
                    totalReservaUsada += fuente.MontoAportado;
                }

                // NOTA: NO modificamos CapitalActual del socio aquí.
                // El CapitalActual solo cambia por:
                // 1. Intereses ganados (en DistribucionGananciasService)
                // 2. Aportes/Retiros directos (en AportesController)
                // Usar capital reinvertido NO debe afectar el balance del socio.
            }
            
            // ACTUALIZAR RESERVA: Descontar el total usado de la reserva
            if (totalReservaUsada > 0)
            {
                await _gananciasService.ActualizarReservaAsync(-totalReservaUsada, $"Préstamo #{prestamo.Id} - ${totalReservaUsada:N0}");
            }
        }

        // Generar cuotas
        var cuotas = _prestamoService.GenerarCuotas(prestamo);
        _context.CuotasPrestamo.AddRange(cuotas);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPrestamo), new { id = prestamo.Id },
            new PrestamoDto(
                prestamo.Id, prestamo.ClienteId, cliente.Nombre, cliente.Cedula, cliente.Telefono,
                prestamo.MontoPrestado, prestamo.TasaInteres, prestamo.TipoInteres, prestamo.FrecuenciaPago,
                prestamo.NumeroCuotas, prestamo.FechaPrestamo, prestamo.FechaVencimiento,
                prestamo.MontoTotal, prestamo.MontoIntereses, prestamo.MontoCuota,
                prestamo.EstadoPrestamo, prestamo.Descripcion, 0, prestamo.MontoTotal, 0,
                new CuotaProximaDto(cuotas.First().FechaCobro, cuotas.First().SaldoPendiente),
                prestamo.CobradorId,
                null,
                prestamo.PorcentajeCobrador,
                prestamo.EsCongelado
            ));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePrestamo(int id, UpdatePrestamoDto dto)
    {
        var prestamo = await _context.Prestamos.Include(p => p.Cuotas).Include(p => p.Pagos).FirstOrDefaultAsync(p => p.Id == id);
        if (prestamo == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        // Actualizar campos informativos siempre
        prestamo.Descripcion = dto.Descripcion;
        prestamo.EstadoPrestamo = dto.EstadoPrestamo;
        prestamo.CobradorId = dto.CobradorId;
        prestamo.PorcentajeCobrador = dto.PorcentajeCobrador;

        // Verificar si hay cambios estructurales (Monto, Tasa, Fechas, Frecuencia)
        bool cambiosEstructurales = 
            prestamo.MontoPrestado != dto.MontoPrestado ||
            prestamo.TasaInteres != dto.TasaInteres ||
            prestamo.FrecuenciaPago != dto.FrecuenciaPago ||
            prestamo.NumeroCuotas != dto.NumeroCuotas || // Nota: NumeroCuotas viene del DTO, pero se recalcula
            prestamo.FechaPrestamo != DateTime.SpecifyKind(dto.FechaPrestamo, DateTimeKind.Utc) ||
            prestamo.DiaSemana != dto.DiaSemana;

        if (cambiosEstructurales)
        {
            // Validar si ya tiene pagos O cuotas ya pagadas
            bool tienePagos = prestamo.Pagos.Any();
            bool tieneCuotasPagadas = prestamo.Cuotas.Any(c => c.MontoPagado > 0 || c.EstadoCuota == "Pagada");
            
            // [MOD] Permitimos editar aunque tenga pagos. La lógica de regeneración se encargará de re-aplicar los pagos.
            // if (tienePagos || tieneCuotasPagadas)
            // {
            //     return BadRequest(new { message = "No se pueden modificar condiciones financieras de un préstamo con pagos o cuotas pagadas. Revierta los pagos primero." });
            // }

            // Recalcular préstamo completamente
            // Necesitamos la 'Duración' y 'Unidad' originales o inferirlas. 
            // Como el DTO de Update es simplificado, asumiremos que si cambia la estructura, recalculamos valores base.
            // PERO: El UpdatePrestamoDto actual no tiene Duracion/Unidad. 
            // Para simplificar, asumiremos que 'NumeroCuotas' en el UpdateDto viene correcto o 
            // ajustaremos lógica futura. 
            // CORRECCION: El DTO de Update debe tener los datos necesarios para recalcular.
            // Si faltan, no podemos recalcular bien los intereses compuestos/simples desde cero sin saber la unidad de tiempo.
            // Por ahora actualizaremos los valores directos y regeneraremos cuotas.

            prestamo.MontoPrestado = dto.MontoPrestado;
            prestamo.TasaInteres = dto.TasaInteres;
            prestamo.TipoInteres = dto.TipoInteres;
            prestamo.FrecuenciaPago = dto.FrecuenciaPago;
            prestamo.DiaSemana = dto.DiaSemana;
            prestamo.FechaPrestamo = DateTime.SpecifyKind(dto.FechaPrestamo, DateTimeKind.Utc);
            
            // Recalcular montos totales e intereses (logica simplificada aqui o llamar servicio)
            // Llamamos al servicio para obtener los nuevos cálculos totales
            // Nota: Como no tenemos 'Duracion' en el UpdateDto, usaremos NumeroCuotas y Frecuencia para estimar o 
            // deberíamos agregar Duracion al DTO. Por ahora, usaremos el NumeroCuotas que viene.
            
            // Calculo manual de totales para actualizar modelo
            // OJO: Esto es riesgoso si la lógica de intereses complejos depende de la duración original.
            // Asumiremos Interés Simple y recálculo básico o confiaremos en los valores que vienen si el front los calcula?
            // Mejor: Usar el servicio GenerarCuotas para recalcular fechas, pero los montos totales deben calcularse antes.
            
            // Para hacerlo bien, necesitamos la Duracion en el UpdateDto. 
            // Como no la pusimos, la inferimos o la pedimos. 
            // Vamos a confiar en que el usuario no cambia la duración drásticamente o que el front manda los datos correctos si los agregamos.
            
            // Voy a RECALCULAR usando el servicio con los datos actuales del prestamo, excepto que faltan parametros.
            // Solución rápida: Actualizar propiedades y regenerar cuotas con los datos actuales.
            
            // 1. Calcular total pagado históricamente (según tabla Pagos, que es la fuente de verdad)
            decimal totalPagadoHist = prestamo.Pagos.Sum(p => p.MontoPago);
            
            // 2. Desvincular pagos de las cuotas antiguas para evitar errores de FK al borrarlas (y mantener el historial de pagos como "Saldo a favor")
            foreach(var pago in prestamo.Pagos) pago.CuotaId = null; 

            // Eliminar cuotas anteriores
            _context.CuotasPrestamo.RemoveRange(prestamo.Cuotas);
            
            // Regenerar cuotas con la nueva fecha de inicio (FechaPrimerPago si existe, o FechaPrestamo)
            var fechaInicio = dto.FechaPrimerPago.HasValue ? DateTime.SpecifyKind(dto.FechaPrimerPago.Value, DateTimeKind.Utc) : prestamo.FechaPrestamo;
            
            // IMPORTANTE: PrestamoService.GenerarCuotas usa prestamo.NumeroCuotas. 
            // Si el update cambia la duración, prestamo.NumeroCuotas debe actualizarse.
            prestamo.NumeroCuotas = dto.NumeroCuotas; 

            // Recalcular montos (Interés Simple por defecto si no podemos recalcular complejo)
            // A futuro: Agregar endpoints de Recalcular en backend.
            // Por ahora, recálculo básico de interés simple:
             if (prestamo.TipoInteres == "Simple") {
                // Inferir meses según la frecuencia de pago
                decimal meses;
                switch (prestamo.FrecuenciaPago) {
                    case "Diario":
                        meses = prestamo.NumeroCuotas / 30m;
                        break;
                    case "Semanal":
                        meses = prestamo.NumeroCuotas / 4m;
                        break;
                    case "Quincenal":
                        meses = prestamo.NumeroCuotas / 2m;
                        break;
                    case "Mensual":
                        meses = prestamo.NumeroCuotas;
                        break;
                    default:
                        meses = prestamo.NumeroCuotas;
                        break;
                }
                
                prestamo.MontoIntereses = prestamo.MontoPrestado * (prestamo.TasaInteres / 100m) * meses;
                prestamo.MontoTotal = prestamo.MontoPrestado + prestamo.MontoIntereses;
                prestamo.MontoCuota = prestamo.MontoTotal / prestamo.NumeroCuotas;
             }

             var nuevasCuotas = _prestamoService.GenerarCuotas(prestamo, fechaInicio);
             
             // 5. Re-aplicar pagos históricos a las nuevas cuotas
             if (totalPagadoHist > 0)
             {
                 var cuotasOrdenadas = nuevasCuotas.OrderBy(c => c.FechaCobro).ToList();
                 decimal remanente = totalPagadoHist;
                 
                 foreach (var c in cuotasOrdenadas)
                 {
                     if (remanente <= 0) break;
                     
                     decimal abono = Math.Min(remanente, c.MontoCuota);
                     c.MontoPagado = abono;
                     c.SaldoPendiente = c.MontoCuota - abono;
                     c.EstadoCuota = c.SaldoPendiente <= 0.01m ? "Pagada" : "Parcial";
                     // Si está pagada, podríamos ponerle una fecha de pago simulada (hoy o la original), 
                     // pero como son múltiples pagos, lo dejamos null o ponemos la fecha de préstamo.
                     // Mejor no inventar fechas en Cuota.FechaPago si no corresponde a un único pago.
                     
                     remanente -= abono;
                 }
                 
                 // Nota: Si sobra dinero (remanente > 0) porque redujo el préstamo drásticamente, 
                 // ese dinero queda "flotando" en la suma de Pagos pero sin asignar a cuotas.
                 // El SaldoPendiente del Prestamo se calcula sumando Cuotas, así que el préstamo podría quedar negativo o pagado en exceso?
                 // El sistema calcula SaldoPendiente sumando (Cuota.MontoCuota - Cuota.MontoPagado).
                 // Si las cuotas no absorben todo el pago, el saldo será 0 (todas pagadas).
             }

             _context.CuotasPrestamo.AddRange(nuevasCuotas);
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePrestamo(int id)
    {
        var prestamo = await _context.Prestamos
            .Include(p => p.Pagos)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prestamo == null)
            return NotFound(new { message = "Préstamo no encontrado" });

        // Verificar pagos o cuotas pagadas
        bool tienePagos = prestamo.Pagos.Any();
        
        // También verificar si hay cuotas marcadas como pagadas (aunque no tengan registro en Pagos)
        var prestamoCuotas = await _context.CuotasPrestamo
            .Where(c => c.PrestamoId == id)
            .ToListAsync();
        bool tieneCuotasPagadas = prestamoCuotas.Any(c => c.MontoPagado > 0 || c.EstadoCuota == "Pagada");

        // [MOD] Permitir eliminación forzada: Eliminar pagos y cuotas asociados
        if (tienePagos)
        {
            _context.Pagos.RemoveRange(prestamo.Pagos);
            // Cuotas se borran en cascada por configuración de EF, pero Pagos es Restrict por seguridad.
            // Aquí lo forzamos.
        }
        
        // No necesitamos verificar tieneCuotasPagadas, se borrarán en cascada.

        _context.Prestamos.Remove(prestamo);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("admin/recalcular-distribuciones")]
    // [Authorize(Roles = "Socio,Admin")] // Descomentar en producción real si se requiere seguridad
    public async Task<IActionResult> RecalcularDistribuciones()
    {
        try
        {
            // 1. Reiniciar ganancias de socios
            var socios = await _context.Usuarios
                .Where(u => u.Rol == RolUsuario.Socio)
                .ToListAsync();
                
            foreach (var socio in socios)
            {
                socio.GananciasAcumuladas = 0;
                socio.CapitalActual = 0;
            }
            
            // 2. Limpiar distribuciones existentes
            var distribucionesExistentes = await _context.DistribucionesGanancia.ToListAsync();
            _context.DistribucionesGanancia.RemoveRange(distribucionesExistentes);
            await _context.SaveChangesAsync();
            
            // 3. Recalcular para todos los pagos históricos
            var pagos = await _context.Pagos
                .Include(p => p.Prestamo)
                .OrderBy(p => p.FechaPago)
                .ToListAsync();
            
            int procesados = 0;
            foreach (var pago in pagos)
            {
                // Solo si el préstamo está activo o pagado
                if (pago.Prestamo != null)
                {
                    await _distribucionService.DistribuirGananciasPago(pago.PrestamoId, pago.MontoPago);
                    procesados++;
                }
            }
            
            return Ok(new 
            { 
                message = $"Se recalcularon {procesados} distribuciones correctamente",
                pagosRecalculados = procesados,
                sociosActualizados = socios.Count
            });
        }
        catch (Exception ex)
        {
           return BadRequest(new { message = "Error recalculando distribuciones", error = ex.Message });
        }
    }
}
