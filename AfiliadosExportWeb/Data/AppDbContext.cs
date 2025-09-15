using Microsoft.EntityFrameworkCore;
using AfiliadosExportWeb.Data.Entities;

namespace AfiliadosExportWeb.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Operation> Operations { get; set; }
    public DbSet<DownloadHistory> DownloadHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuración de Operation
        modelBuilder.Entity<Operation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ConnectionString).IsRequired().HasMaxLength(500);
            
            // Relación con DownloadHistory
            entity.HasMany(e => e.Downloads)
                  .WithOne(d => d.Operation)
                  .HasForeignKey(d => d.OperationId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuración de DownloadHistory
        modelBuilder.Entity<DownloadHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AffiliateCode);
            entity.HasIndex(e => e.Username);
            entity.HasIndex(e => e.DownloadedAt);
            entity.HasIndex(e => e.IsDeleted);
            
            entity.Property(e => e.AffiliateCode).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.FileName).HasMaxLength(200);
            entity.Property(e => e.FilePath).HasMaxLength(500);
        });

        // Sin datos iniciales - Las operaciones se gestionan desde appsettings.json
        // La tabla Operations en SQLite solo se usa para el historial del panel admin si se decide implementar en el futuro
    }
}