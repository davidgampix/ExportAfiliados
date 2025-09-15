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

        // Datos iniciales
        modelBuilder.Entity<Operation>().HasData(
            new Operation
            {
                Id = 1,
                Code = "sportsbet",
                Name = "SportsBet Afiliados",
                Server = "54.226.82.137",
                Database = "SportsBet_Afiliados",
                ConnectionString = "Server=54.226.82.137;Database=SportsBet_Afiliados;User Id=SportsBetLogin;Password=8B24BDF8-9541-47F8-9957-B63DD87FEFCE;TrustServerCertificate=true;Connection Timeout=30;Command Timeout=600;",
                IsActive = true,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow
            },
            new Operation
            {
                Id = 2,
                Code = "cdl",
                Name = "CDL Afiliados",
                Server = "54.226.82.137",
                Database = "CDL_Afiliados",
                ConnectionString = "Server=54.226.82.137;Database=CDL_Afiliados;User Id=SportsBetLogin;Password=8B24BDF8-9541-47F8-9957-B63DD87FEFCE;TrustServerCertificate=true;Connection Timeout=30;Command Timeout=600;",
                IsActive = true,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            },
            new Operation
            {
                Id = 3,
                Code = "formowin",
                Name = "FormoWin Afiliados",
                Server = "54.226.82.137",
                Database = "FormoWin_Afiliados",
                ConnectionString = "Server=54.226.82.137;Database=FormoWin_Afiliados;User Id=SportsBetLogin;Password=8B24BDF8-9541-47F8-9957-B63DD87FEFCE;TrustServerCertificate=true;Connection Timeout=30;Command Timeout=600;",
                IsActive = true,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            }
        );
    }
}