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
    }
}