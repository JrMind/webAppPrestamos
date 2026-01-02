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
            entity.Property(e => e.UsuarioCreacion).HasColumnName("usuariocreacion").HasMaxLength(100);
            entity.Property(e => e.FechaCreacion).HasColumnName("fechacreacion").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UsuarioModificacion).HasColumnName("usuariomodificacion").HasMaxLength(100);
            entity.Property(e => e.FechaModificacion).HasColumnName("fechamodificacion");
            entity.HasIndex(e => e.Cedula).IsUnique();
        });

        // Prestamo
        modelBuilder.Entity<Prestamo>(entity =>
        {
            entity.ToTable("prestamos");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClienteId).HasColumnName("clienteid");
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
            entity.Property(e => e.UsuarioCreacion).HasColumnName("usuariocreacion").HasMaxLength(100);
            entity.Property(e => e.FechaCreacion).HasColumnName("fechacreacion").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UsuarioModificacion).HasColumnName("usuariomodificacion").HasMaxLength(100);
            entity.Property(e => e.FechaModificacion).HasColumnName("fechamodificacion");
            
            entity.HasOne(e => e.Cliente)
                .WithMany(c => c.Prestamos)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ClienteId).HasDatabaseName("idx_prestamos_cliente");
            entity.HasIndex(e => e.EstadoPrestamo).HasDatabaseName("idx_prestamos_estado");
            entity.HasIndex(e => e.FechaPrestamo).HasDatabaseName("idx_prestamos_fecha");
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
            entity.Property(e => e.UsuarioCreacion).HasColumnName("usuariocreacion").HasMaxLength(100);
            entity.Property(e => e.FechaCreacion).HasColumnName("fechacreacion").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UsuarioModificacion).HasColumnName("usuariomodificacion").HasMaxLength(100);
            entity.Property(e => e.FechaModificacion).HasColumnName("fechamodificacion");

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
            entity.Property(e => e.UsuarioCreacion).HasColumnName("usuariocreacion").HasMaxLength(100);
            entity.Property(e => e.FechaCreacion).HasColumnName("fechacreacion").HasDefaultValueSql("CURRENT_TIMESTAMP");

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
    }
}
