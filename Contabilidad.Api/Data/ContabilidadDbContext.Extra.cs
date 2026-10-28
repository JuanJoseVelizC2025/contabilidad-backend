using Contabilidad.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Data;

public partial class ContabilidadDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bitacora>(e =>
        {
            e.ToTable("Bitacora");
            e.HasKey(x => x.IdBitacora);
            e.Property(x => x.NombreUsuario).HasMaxLength(50);
            e.Property(x => x.Modulo).HasMaxLength(40);
            e.Property(x => x.Accion).HasMaxLength(60);
            e.Property(x => x.Detalle).HasMaxLength(500);
        });

        modelBuilder.Entity<PartidaOrigen>(e =>
        {
            e.ToTable("PartidaOrigen");
            e.HasKey(x => x.IdPartida);
            e.Property(x => x.IdPartida).ValueGeneratedNever();
            e.Property(x => x.Origen).HasMaxLength(20);
        });

        modelBuilder.Entity<CuentaBancaria>(e =>
        {
            e.ToTable("CuentaBancaria");
            e.HasKey(x => x.IdCuentaBancaria);
            e.Property(x => x.Banco).HasMaxLength(80);
            e.Property(x => x.NumeroCuenta).HasMaxLength(40);
            e.Property(x => x.TipoCuenta).HasMaxLength(20);
        });

        modelBuilder.Entity<MovimientoBancario>(e =>
        {
            e.ToTable("MovimientoBancario");
            e.HasKey(x => x.IdMovimiento);
            e.Property(x => x.Tipo).HasMaxLength(20);
            e.Property(x => x.Monto).HasPrecision(18, 2);
            e.Property(x => x.Referencia).HasMaxLength(60);
            e.Property(x => x.Concepto).HasMaxLength(250);
            e.Property(x => x.Estado).HasMaxLength(12);
        });

        modelBuilder.Entity<CodigoVerificacion>(e =>
        {
            e.ToTable("CodigoVerificacion");
            e.HasKey(x => x.IdCodigo);
            e.Property(x => x.Proposito).HasMaxLength(20);
            e.Property(x => x.CodigoHash).HasMaxLength(100);
        });
    }
}