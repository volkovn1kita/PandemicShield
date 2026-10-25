using Microsoft.EntityFrameworkCore;
using PandemicShield.DataAccess.Entities;

namespace PandemicShield.DataAccess.Data
{
    public class PandemicDbContext : DbContext
    {
        public DbSet<ThreatAlertEntity> Threats { get; set; }
        public DbSet<ReferenceMutationEntity> Mutation { get; set; }
        public DbSet<AnalysisRunEntity> Runs { get; set; }
        public DbSet<SequenceResultEntity> SequenceResults { get; set; }
        public DbSet<MutationEntity> Mutations { get; set; }
        public DbSet<VariantMarkerEntity> VariantMarkers { get; set; }
        public DbSet<AmrHitEntity> AmrHits { get; set; }

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
            modelBuilder.Entity<AnalysisRunEntity>().Property(e => e.NextcladeStatus).HasConversion<string>();
            modelBuilder.Entity<AnalysisRunEntity>().Property(e => e.AmrStatus).HasConversion<string>();

            modelBuilder.Entity<AmrHitEntity>().HasIndex(e => e.RunId);
            modelBuilder.Entity<AmrHitEntity>().HasIndex(e => e.ElementSymbol);
            modelBuilder.Entity<AmrHitEntity>()
                .HasOne<AnalysisRunEntity>()
                .WithMany()
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SequenceResultEntity>()
                .HasOne<AnalysisRunEntity>()
                .WithMany()
                .HasForeignKey(e => e.RunId)
                .OnDelete(DeleteBehavior.Cascade);

            // Мутації шукаються «по всіх зразках»: у яких зразках є S:E484K?
            modelBuilder.Entity<MutationEntity>().HasIndex(e => e.Label);
            modelBuilder.Entity<MutationEntity>().HasIndex(e => e.RunId);
            modelBuilder.Entity<MutationEntity>()
                .HasOne<SequenceResultEntity>()
                .WithMany()
                .HasForeignKey(e => e.SequenceResultId)
                .OnDelete(DeleteBehavior.Cascade);
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
