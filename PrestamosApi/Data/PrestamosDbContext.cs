using Microsoft.EntityFrameworkCore;
using PrestamosApi.Models;

namespace PrestamosApi.Data;

public class PrestamosDbContext : DbContext
{
    public DbSet<CierreMesLog> CierreMesLogs { get; set; }

    public PrestamosDbContext(DbContextOptions<PrestamosDbContext> options) : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Prestamo> Prestamos => Set<Prestamo>();
    public DbSet<CuotaPrestamo> CuotasPrestamo => Set<CuotaPrestamo>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Aporte> Aportes => Set<Aporte>();
    public DbSet<MovimientoCapital> MovimientosCapital => Set<MovimientoCapital>();
    public DbSet<DistribucionGanancia> DistribucionesGanancia => Set<DistribucionGanancia>();
    public DbSet<AportadorExterno> AportadoresExternos => Set<AportadorExterno>();
    public DbSet<FuenteCapitalPrestamo> FuentesCapitalPrestamo => Set<FuenteCapitalPrestamo>();
    public DbSet<PagoAportadorExterno> PagosAportadoresExternos => Set<PagoAportadorExterno>();
    public DbSet<SmsCampaign> SmsCampaigns => Set<SmsCampaign>();
    public DbSet<SmsHistory> SmsHistories => Set<SmsHistory>();
    public DbSet<Costo> Costos => Set<Costo>();
    public DbSet<PagoCosto> PagosCostos => Set<PagoCosto>();
    public DbSet<ConfiguracionSistema> ConfiguracionesSistema => Set<ConfiguracionSistema>();
    public DbSet<NotaPrestamo> NotasPrestamo => Set<NotaPrestamo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cliente
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("clientes");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Cedula).HasColumnName("cedula").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Telefono).HasColumnName("telefono").HasMaxLength(20);
            entity.Property(e => e.Direccion).HasColumnName("direccion").HasMaxLength(300);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100);
            entity.Property(e => e.FechaRegistro).HasColumnName("fecharegistro").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(20).HasDefaultValue("Activo");
            entity.HasIndex(e => e.Cedula).IsUnique();
        });

        // Prestamo
        modelBuilder.Entity<Prestamo>(entity =>
        {
            entity.ToTable("prestamos");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClienteId).HasColumnName("clienteid");
            entity.Property(e => e.CobradorId).HasColumnName("cobradorid");
            entity.Property(e => e.MontoPrestado).HasColumnName("montoprestado").HasColumnType("decimal(18,2)");
            entity.Property(e => e.TasaInteres).HasColumnName("tasainteres").HasColumnType("decimal(5,2)");
            entity.Property(e => e.TipoInteres).HasColumnName("tipointeres").HasMaxLength(20).HasDefaultValue("Simple");
            entity.Property(e => e.FrecuenciaPago).HasColumnName("frecuenciapago").HasMaxLength(20);
            entity.Property(e => e.DiaSemana).HasColumnName("diasemana");
            entity.Property(e => e.NumeroCuotas).HasColumnName("numerocuotas");
            entity.Property(e => e.FechaPrestamo).HasColumnName("fechaprestamo");
            entity.Property(e => e.FechaVencimiento).HasColumnName("fechavencimiento");
            entity.Property(e => e.MontoTotal).HasColumnName("montototal").HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoIntereses).HasColumnName("montointereses").HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoCuota).HasColumnName("montocuota").HasColumnType("decimal(18,2)");
            entity.Property(e => e.EstadoPrestamo).HasColumnName("estadoprestamo").HasMaxLength(20).HasDefaultValue("Activo");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.PorcentajeCobrador).HasColumnName("porcentajecobrador").HasColumnType("decimal(5,2)").HasDefaultValue(5);
            entity.Property(e => e.EsCongelado).HasColumnName("EsCongelado").HasDefaultValue(false);
            
            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Prestamos)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Cobrador)
                .WithMany(u => u.PrestamosComoCobrador)
                .HasForeignKey(e => e.CobradorId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.ClienteId).HasDatabaseName("idx_prestamos_cliente");
            entity.HasIndex(e => e.EstadoPrestamo).HasDatabaseName("idx_prestamos_estado");
            entity.HasIndex(e => e.FechaPrestamo).HasDatabaseName("idx_prestamos_fecha");
            entity.HasIndex(e => e.CobradorId).HasDatabaseName("idx_prestamos_cobrador");
        });

        // CuotaPrestamo
        modelBuilder.Entity<CuotaPrestamo>(entity =>
        {
            entity.ToTable("cuotasprestamo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PrestamoId).HasColumnName("prestamoid");
            entity.Property(e => e.NumeroCuota).HasColumnName("numerocuota");
            entity.Property(e => e.FechaCobro).HasColumnName("fechacobro");
            entity.Property(e => e.MontoCuota).HasColumnName("montocuota").HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoPagado).HasColumnName("montopagado").HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.SaldoPendiente).HasColumnName("saldopendiente").HasColumnType("decimal(18,2)");
            entity.Property(e => e.EstadoCuota).HasColumnName("estadocuota").HasMaxLength(20).HasDefaultValue("Pendiente");
            entity.Property(e => e.FechaPago).HasColumnName("fechapago");
            entity.Property(e => e.Observaciones).HasColumnName("observaciones");
            entity.Property(e => e.Cobrado).HasColumnName("cobrado").HasDefaultValue(false);

            entity.HasOne(e => e.Prestamo)
                .WithMany(p => p.Cuotas)
                .HasForeignKey(e => e.PrestamoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.PrestamoId, e.NumeroCuota }).IsUnique();
            entity.HasIndex(e => e.PrestamoId).HasDatabaseName("idx_cuotas_prestamo");
            entity.HasIndex(e => e.EstadoCuota).HasDatabaseName("idx_cuotas_estado");
            entity.HasIndex(e => e.FechaCobro).HasDatabaseName("idx_cuotas_fecha");
        });

        // Pago
        modelBuilder.Entity<Pago>(entity =>
        {
            entity.ToTable("pagos");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PrestamoId).HasColumnName("prestamoid");
            entity.Property(e => e.CuotaId).HasColumnName("cuotaid");
            entity.Property(e => e.MontoPago).HasColumnName("montopago").HasColumnType("decimal(18,2)");
            entity.Property(e => e.FechaPago).HasColumnName("fechapago");
            entity.Property(e => e.MetodoPago).HasColumnName("metodopago").HasMaxLength(50);
            entity.Property(e => e.Comprobante).HasColumnName("comprobante").HasMaxLength(200);
            entity.Property(e => e.Observaciones).HasColumnName("observaciones");

            entity.HasOne(e => e.Prestamo)
                .WithMany(p => p.Pagos)
                .HasForeignKey(e => e.PrestamoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Cuota)
                .WithMany(c => c.Pagos)
                .HasForeignKey(e => e.CuotaId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.PrestamoId).HasDatabaseName("idx_pagos_prestamo");
            entity.HasIndex(e => e.CuotaId).HasDatabaseName("idx_pagos_cuota");
        });

        // Usuario
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("usuarios");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(100).IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("passwordhash").HasMaxLength(500).IsRequired();
            entity.Property(e => e.Telefono).HasColumnName("telefono").HasMaxLength(20);
            entity.Property(e => e.Rol).HasColumnName("rol").HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.PorcentajeParticipacion).HasColumnName("porcentajeparticipacion").HasColumnType("decimal(5,2)").HasDefaultValue(0);
            entity.Property(e => e.TasaInteresMensual).HasColumnName("tasainteresmensual").HasColumnType("decimal(5,2)").HasDefaultValue(3);
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);
            entity.Property(e => e.CapitalActual).HasColumnName("capitalactual").HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.GananciasAcumuladas).HasColumnName("gananciasacumuladas").HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.UltimoCalculoInteres).HasColumnName("ultimocalculointeres");
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Aporte
        modelBuilder.Entity<Aporte>(entity =>
        {
            entity.ToTable("aportes");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UsuarioId).HasColumnName("usuarioid");
            entity.Property(e => e.MontoInicial).HasColumnName("montoinicial").HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoActual).HasColumnName("montoactual").HasColumnType("decimal(18,2)");
            entity.Property(e => e.FechaAporte).HasColumnName("fechaaporte");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasMaxLength(500);

            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.Aportes)
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UsuarioId).HasDatabaseName("idx_aportes_usuario");
        });

        // MovimientoCapital
        modelBuilder.Entity<MovimientoCapital>(entity =>
        {
            entity.ToTable("movimientoscapital");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UsuarioId).HasColumnName("usuarioid");
            entity.Property(e => e.TipoMovimiento).HasColumnName("tipomovimiento").HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Monto).HasColumnName("monto").HasColumnType("decimal(18,2)");
            entity.Property(e => e.SaldoAnterior).HasColumnName("saldoanterior").HasColumnType("decimal(18,2)");
            entity.Property(e => e.SaldoNuevo).HasColumnName("saldonuevo").HasColumnType("decimal(18,2)");
            entity.Property(e => e.FechaMovimiento).HasColumnName("fechamovimiento");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion").HasMaxLength(500);

            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.Movimientos)
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UsuarioId).HasDatabaseName("idx_movimientos_usuario");
            entity.HasIndex(e => e.FechaMovimiento).HasDatabaseName("idx_movimientos_fecha");
        });

        // DistribucionGanancia
        modelBuilder.Entity<DistribucionGanancia>(entity =>
        {
            entity.ToTable("distribucionesganancias");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PrestamoId).HasColumnName("prestamoid");
            entity.Property(e => e.UsuarioId).HasColumnName("usuarioid");
            entity.Property(e => e.PorcentajeAsignado).HasColumnName("porcentajeasignado").HasColumnType("decimal(5,2)");
            entity.Property(e => e.MontoGanancia).HasColumnName("montoganancia").HasColumnType("decimal(18,2)");
            entity.Property(e => e.FechaDistribucion).HasColumnName("fechadistribucion");
            entity.Property(e => e.Liquidado).HasColumnName("liquidado").HasDefaultValue(false);

            entity.HasOne(e => e.Prestamo)
                .WithMany(p => p.Distribuciones)
                .HasForeignKey(e => e.PrestamoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.Ganancias)
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.PrestamoId).HasDatabaseName("idx_distribuciones_prestamo");
            entity.HasIndex(e => e.UsuarioId).HasDatabaseName("idx_distribuciones_usuario");
        });

        // AportadorExterno
        modelBuilder.Entity<AportadorExterno>(entity =>
        {
            entity.ToTable("aportadoresexternos");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Telefono).HasColumnName("telefono").HasMaxLength(50);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.TasaInteres).HasColumnName("tasainteres").HasColumnType("decimal(5,2)").HasDefaultValue(3);
            entity.Property(e => e.DiasParaPago).HasColumnName("diasparapago").HasDefaultValue(30);
            entity.Property(e => e.MontoTotalAportado).HasColumnName("montototalaportado").HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.MontoPagado).HasColumnName("montopagado").HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.SaldoPendiente).HasColumnName("saldopendiente").HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.Estado).HasColumnName("estado").HasMaxLength(50).HasDefaultValue("Activo");
            entity.Property(e => e.FechaCreacion).HasColumnName("fechacreacion").HasDefaultValueSql("NOW()");
            entity.Property(e => e.Notas).HasColumnName("notas");
        });

        // FuenteCapitalPrestamo
        modelBuilder.Entity<FuenteCapitalPrestamo>(entity =>
        {
            entity.ToTable("fuentescapitalprestamo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PrestamoId).HasColumnName("prestamoid");
            entity.Property(e => e.Tipo).HasColumnName("tipo").HasMaxLength(50).IsRequired();
            entity.Property(e => e.UsuarioId).HasColumnName("usuarioid");
            entity.Property(e => e.AportadorExternoId).HasColumnName("aportadorexternoid");
            entity.Property(e => e.MontoAportado).HasColumnName("montoaportado").HasColumnType("decimal(18,2)");
            entity.Property(e => e.PorcentajeParticipacion).HasColumnName("porcentajeparticipacion").HasColumnType("decimal(5,2)").HasDefaultValue(0);
            entity.Property(e => e.FechaRegistro).HasColumnName("fecharegistro").HasDefaultValueSql("NOW()");

            entity.HasOne(e => e.Prestamo)
                .WithMany(p => p.FuentesCapital)
                .HasForeignKey(e => e.PrestamoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Usuario)
                .WithMany(u => u.FuentesCapital)
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.AportadorExterno)
                .WithMany(a => a.FuentesCapital)
                .HasForeignKey(e => e.AportadorExternoId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.PrestamoId).HasDatabaseName("idx_fuentescapital_prestamo");
            entity.HasIndex(e => e.UsuarioId).HasDatabaseName("idx_fuentescapital_usuario");
            entity.HasIndex(e => e.AportadorExternoId).HasDatabaseName("idx_fuentescapital_aportador");
        });

        // PagoAportadorExterno
        modelBuilder.Entity<PagoAportadorExterno>(entity =>
        {
            entity.ToTable("pagosaportadoresexternos");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AportadorExternoId).HasColumnName("aportadorexternoid");
            entity.Property(e => e.Monto).HasColumnName("monto").HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoCapital).HasColumnName("montocapital").HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.MontoIntereses).HasColumnName("montointereses").HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.FechaPago).HasColumnName("fechapago");
            entity.Property(e => e.MetodoPago).HasColumnName("metodopago").HasMaxLength(50);
            entity.Property(e => e.Comprobante).HasColumnName("comprobante").HasMaxLength(255);
            entity.Property(e => e.Notas).HasColumnName("notas");

            entity.HasOne(e => e.AportadorExterno)
                .WithMany(a => a.Pagos)
                .HasForeignKey(e => e.AportadorExternoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.AportadorExternoId).HasDatabaseName("idx_pagosaportadores_aportador");
        });

        // SmsCampaign
        modelBuilder.Entity<SmsCampaign>(entity =>
        {
            entity.ToTable("smscampaigns");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Mensaje).HasColumnName("mensaje").IsRequired();
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);
            entity.Property(e => e.DiasEnvio).HasColumnName("diasenvio").HasDefaultValue("[]");
            entity.Property(e => e.HorasEnvio).HasColumnName("horasenvio").HasDefaultValue("[]");
            entity.Property(e => e.VecesPorDia).HasColumnName("vecespordia").HasDefaultValue(1);
            entity.Property(e => e.TipoDestinatario).HasColumnName("tipodestinatario").HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.FechaCreacion).HasColumnName("fechacreacion").HasDefaultValueSql("NOW()");
            entity.Property(e => e.FechaModificacion).HasColumnName("fechamodificacion");
        });

        // SmsHistory
        modelBuilder.Entity<SmsHistory>(entity =>
        {
            entity.ToTable("smshistory");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.SmsCampaignId).HasColumnName("smscampaignid");
            entity.Property(e => e.ClienteId).HasColumnName("clienteid");
            entity.Property(e => e.NumeroTelefono).HasColumnName("numerotelefono").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Mensaje).HasColumnName("mensaje").IsRequired();
            entity.Property(e => e.FechaEnvio).HasColumnName("fechaenvio").HasDefaultValueSql("NOW()");
            entity.Property(e => e.Estado).HasColumnName("estado").HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.TwilioSid).HasColumnName("twiliosid").HasMaxLength(100);
            entity.Property(e => e.ErrorMessage).HasColumnName("errormessage");

            entity.HasOne(e => e.SmsCampaign)
                .WithMany(c => c.HistorialSms)
                .HasForeignKey(e => e.SmsCampaignId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Cliente)
                .WithMany()
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.SmsCampaignId).HasDatabaseName("idx_smshistory_campaign");
            entity.HasIndex(e => e.FechaEnvio).HasDatabaseName("idx_smshistory_fecha");
        });

        // Costo (Gastos operativos)
        modelBuilder.Entity<Costo>(entity =>
        {
            entity.ToTable("costos");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Nombre).HasColumnName("nombre").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Monto).HasColumnName("monto").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Frecuencia).HasColumnName("frecuencia").HasMaxLength(20).HasDefaultValue("Mensual");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.Activo).HasColumnName("activo").HasDefaultValue(true);
            entity.Property(e => e.FechaCreacion).HasColumnName("fechacreacion").HasDefaultValueSql("NOW()");
            entity.Property(e => e.FechaFin).HasColumnName("fechafin");
            entity.Property(e => e.TotalPagado).HasColumnName("totalpagado").HasColumnType("decimal(18,2)").HasDefaultValue(0);
        });

        // PagoCosto
        modelBuilder.Entity<PagoCosto>(entity =>
        {
            entity.ToTable("pagoscostos");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CostoId).HasColumnName("costoid");
            entity.Property(e => e.MontoPagado).HasColumnName("montopagado").HasColumnType("decimal(18,2)");
            entity.Property(e => e.FechaPago).HasColumnName("fechapago").HasDefaultValueSql("NOW()");
            entity.Property(e => e.MetodoPago).HasColumnName("metodopago").HasMaxLength(50);
            entity.Property(e => e.Comprobante).HasColumnName("comprobante").HasMaxLength(255);
            entity.Property(e => e.Observaciones).HasColumnName("observaciones");

            entity.HasOne(e => e.Costo)
                .WithMany(c => c.Pagos)
                .HasForeignKey(e => e.CostoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.CostoId).HasDatabaseName("idx_pagoscostos_costo");
            entity.HasIndex(e => e.FechaPago).HasDatabaseName("idx_pagoscostos_fecha");
        });

        // ConfiguracionSistema
        modelBuilder.Entity<ConfiguracionSistema>(entity =>
        {
            entity.ToTable("configuracionsistema");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Clave).HasColumnName("clave").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Valor).HasColumnName("valor").IsRequired();
            entity.Property(e => e.FechaActualizacion).HasColumnName("fechaactualizacion").HasDefaultValueSql("NOW()");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.HasIndex(e => e.Clave).IsUnique();
        });

        // NotasPrestamo
        modelBuilder.Entity<NotaPrestamo>(entity =>
        {
            entity.ToTable("notasprestamo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.PrestamoId).HasColumnName("prestamoid");
            entity.Property(e => e.Contenido).HasColumnName("contenido").IsRequired();
            entity.Property(e => e.FechaCreacion).HasColumnName("fechacreacion").HasDefaultValueSql("NOW()");
            entity.Property(e => e.UsuarioId).HasColumnName("usuarioid");

            entity.HasOne(e => e.Prestamo)
                .WithMany(p => p.Notas)
                .HasForeignKey(e => e.PrestamoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Usuario)
                .WithMany()
                .HasForeignKey(e => e.UsuarioId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.PrestamoId).HasDatabaseName("idx_notas_prestamo");
        });
    }
}
