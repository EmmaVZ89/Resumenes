using Microsoft.EntityFrameworkCore;

namespace Resumenes.Licencias.Api.Datos;

public class LicenciasDbContext(DbContextOptions<LicenciasDbContext> opciones)
    : DbContext(opciones)
{
    public DbSet<Licencia> Licencias => Set<Licencia>();
    public DbSet<Activacion> Activaciones => Set<Activacion>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Licencia>().HasIndex(l => l.Clave).IsUnique();
        mb.Entity<Activacion>().HasIndex(a => new { a.LicenciaId, a.Hwid }).IsUnique();
        mb.Entity<Licencia>()
            .HasMany(l => l.Activaciones)
            .WithOne(a => a.Licencia)
            .HasForeignKey(a => a.LicenciaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
