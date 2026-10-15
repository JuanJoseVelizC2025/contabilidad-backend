using System;
using System.Collections.Generic;
using Contabilidad.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Contabilidad.Api.Data;

public partial class ContabilidadDbContext : DbContext
{
    public ContabilidadDbContext(DbContextOptions<ContabilidadDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Cuentum> Cuenta { get; set; }

    public virtual DbSet<ParametroSistema> ParametroSistemas { get; set; }

    public virtual DbSet<PartidaDetalle> PartidaDetalles { get; set; }

    public virtual DbSet<Partidum> Partida { get; set; }

    public virtual DbSet<PeriodoContable> PeriodoContables { get; set; }

    public virtual DbSet<Rol> Rols { get; set; }

    public virtual DbSet<Usuario> Usuarios { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cuentum>(entity =>
        {
            entity.HasKey(e => e.IdCuenta).HasName("PK__Cuenta__D41FD70667152F67");

            entity.HasIndex(e => e.Codigo, "UQ__Cuenta__06370DAC61F855CE").IsUnique();

            entity.Property(e => e.Activa).HasDefaultValue(true);
            entity.Property(e => e.Codigo).HasMaxLength(20);
            entity.Property(e => e.Naturaleza).HasMaxLength(10);
            entity.Property(e => e.Nombre).HasMaxLength(150);
            entity.Property(e => e.Tipo).HasMaxLength(10);

            entity.HasOne(d => d.IdCuentaPadreNavigation).WithMany(p => p.InverseIdCuentaPadreNavigation)
                .HasForeignKey(d => d.IdCuentaPadre)
                .HasConstraintName("FK__Cuenta__IdCuenta__4AB81AF0");
        });

        modelBuilder.Entity<ParametroSistema>(entity =>
        {
            entity.HasKey(e => e.IdParametro).HasName("PK__Parametr__37B016F4A9881A98");

            entity.ToTable("ParametroSistema");

            entity.HasIndex(e => e.Clave, "UQ__Parametr__E8181E111A006695").IsUnique();

            entity.Property(e => e.Clave).HasMaxLength(50);
            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.Valor).HasMaxLength(200);
        });

        modelBuilder.Entity<PartidaDetalle>(entity =>
        {
            entity.HasKey(e => e.IdDetalle).HasName("PK__PartidaD__E43646A53A8A6A82");

            entity.ToTable("PartidaDetalle");

            entity.Property(e => e.Debe).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Haber).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.IdCuentaNavigation).WithMany(p => p.PartidaDetalles)
                .HasForeignKey(d => d.IdCuenta)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PartidaDe__IdCue__571DF1D5");

            entity.HasOne(d => d.IdPartidaNavigation).WithMany(p => p.PartidaDetalles)
                .HasForeignKey(d => d.IdPartida)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PartidaDe__IdPar__5629CD9C");
        });

        modelBuilder.Entity<Partidum>(entity =>
        {
            entity.HasKey(e => e.IdPartida).HasName("PK__Partida__6ED660C7543AFC60");

            entity.HasIndex(e => new { e.IdPeriodo, e.Numero }, "UQ_Partida_Numero").IsUnique();

            entity.Property(e => e.Concepto).HasMaxLength(250);
            entity.Property(e => e.Estado)
                .HasMaxLength(12)
                .HasDefaultValue("Registrada");
            entity.Property(e => e.MotivoAnulacion).HasMaxLength(250);

            entity.HasOne(d => d.IdPeriodoNavigation).WithMany(p => p.Partida)
                .HasForeignKey(d => d.IdPeriodo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Partida__IdPerio__5070F446");

            entity.HasOne(d => d.IdUsuarioNavigation).WithMany(p => p.Partida)
                .HasForeignKey(d => d.IdUsuario)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Partida__IdUsuar__5165187F");
        });

        modelBuilder.Entity<PeriodoContable>(entity =>
        {
            entity.HasKey(e => e.IdPeriodo).HasName("PK__PeriodoC__B44699FE3D2F6FFF");

            entity.ToTable("PeriodoContable");

            entity.HasIndex(e => new { e.Anio, e.Mes }, "UQ_Periodo").IsUnique();

            entity.Property(e => e.Estado)
                .HasMaxLength(10)
                .HasDefaultValue("Abierto");
        });

        modelBuilder.Entity<Rol>(entity =>
        {
            entity.HasKey(e => e.IdRol).HasName("PK__Rol__2A49584C46FB609A");

            entity.ToTable("Rol");

            entity.HasIndex(e => e.Nombre, "UQ__Rol__75E3EFCF16576116").IsUnique();

            entity.Property(e => e.Descripcion).HasMaxLength(200);
            entity.Property(e => e.Nombre).HasMaxLength(50);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasKey(e => e.IdUsuario).HasName("PK__Usuario__5B65BF9763874DCD");

            entity.ToTable("Usuario");

            entity.HasIndex(e => e.NombreUsuario, "UQ__Usuario__6B0F5AE0751A7945").IsUnique();

            entity.Property(e => e.Activo).HasDefaultValue(true);
            entity.Property(e => e.ClaveHash).HasMaxLength(200);
            entity.Property(e => e.Correo).HasMaxLength(150);
            entity.Property(e => e.NombreCompleto).HasMaxLength(150);
            entity.Property(e => e.NombreUsuario).HasMaxLength(50);

            entity.HasOne(d => d.IdRolNavigation).WithMany(p => p.Usuarios)
                .HasForeignKey(d => d.IdRol)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Usuario__IdRol__3B75D760");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
