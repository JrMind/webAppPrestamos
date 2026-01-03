using Microsoft.EntityFrameworkCore;
using PrestamosApi.Models;

namespace PrestamosApi.Data;

public class PrestamosDbContext : DbContext
{
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
            entity.Property(e => e.NumeroCuotas).HasColumnName("numerocuotas");
            entity.Property(e => e.FechaPrestamo).HasColumnName("fechaprestamo");
            entity.Property(e => e.FechaVencimiento).HasColumnName("fechavencimiento");
            entity.Property(e => e.MontoTotal).HasColumnName("montototal").HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoIntereses).HasColumnName("montointereses").HasColumnType("decimal(18,2)");
            entity.Property(e => e.MontoCuota).HasColumnName("montocuota").HasColumnType("decimal(18,2)");
            entity.Property(e => e.EstadoPrestamo).HasColumnName("estadoprestamo").HasMaxLength(20).HasDefaultValue("Activo");
            entity.Property(e => e.Descripcion).HasColumnName("descripcion");
            entity.Property(e => e.PorcentajeCobrador).HasColumnName("porcentajecobrador").HasColumnType("decimal(5,2)").HasDefaultValue(5);
            
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
    }
}
