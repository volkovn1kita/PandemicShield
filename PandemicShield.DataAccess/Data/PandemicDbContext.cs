using Microsoft.EntityFrameworkCore;
using PandemicShield.DataAccess.Entities;

namespace PandemicShield.DataAccess.Data
{
    public class PandemicDbContext : DbContext
    {
        public DbSet<ThreatAlertEntity> Threats { get; set; }
        public DbSet<ReferenceMutationEntity> Mutation { get; set; }
        public DbSet<AnalysisRunEntity> Runs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ThreatAlertEntity>().Property(e => e.Category).HasConversion<string>();
            modelBuilder.Entity<ThreatAlertEntity>().HasIndex(e => e.RunId);
            modelBuilder.Entity<ThreatAlertEntity>()
                .HasOne<AnalysisRunEntity>()
                .WithMany()
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AnalysisRunEntity>().Property(e => e.Category).HasConversion<string>();
            modelBuilder.Entity<AnalysisRunEntity>().Property(e => e.Status).HasConversion<string>();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                    ?? "Host=localhost;Port=5433;Database=pandemic_shield_db;Username=adminpostgres;Password=rootpassword";

                optionsBuilder.UseNpgsql(connectionString);
            }
        }
    }
}
