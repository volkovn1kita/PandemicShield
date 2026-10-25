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
        public DbSet<ImportBatchEntity> Imports { get; set; }
        public DbSet<SurveillanceSyncEntity> SurveillanceSyncs { get; set; }
        public DbSet<SurveillanceIsolateEntity> SurveillanceIsolates { get; set; }
        public DbSet<SurveillanceGeneEntity> SurveillanceGenes { get; set; }

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
            modelBuilder.Entity<AnalysisRunEntity>().Property(e => e.MlstStatus).HasConversion<string>();
            // Пошук «у яких ізолятах ST147?» між запусками.
            modelBuilder.Entity<AnalysisRunEntity>().HasIndex(e => e.MlstSequenceType);
            modelBuilder.Entity<AnalysisRunEntity>().HasIndex(e => e.ImportId);
            // Видалення пакета імпорту не видаляє вже проаналізовані зразки.
            modelBuilder.Entity<AnalysisRunEntity>()
                .HasOne<ImportBatchEntity>()
                .WithMany()
                .HasForeignKey(e => e.ImportId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ImportBatchEntity>().Property(e => e.Category).HasConversion<string>();
            modelBuilder.Entity<ImportBatchEntity>().Property(e => e.Status).HasConversion<string>();

            modelBuilder.Entity<SurveillanceSyncEntity>().Property(e => e.Status).HasConversion<string>();
            modelBuilder.Entity<SurveillanceIsolateEntity>().HasIndex(e => new { e.Taxgroup, e.Country });
            modelBuilder.Entity<SurveillanceIsolateEntity>().HasIndex(e => e.AssemblyAccession);
            modelBuilder.Entity<SurveillanceGeneEntity>().HasIndex(e => e.IsolateId);
            modelBuilder.Entity<SurveillanceGeneEntity>().HasIndex(e => e.Family);
            modelBuilder.Entity<SurveillanceGeneEntity>()
                .HasOne<SurveillanceIsolateEntity>()
                .WithMany()
                .HasForeignKey(e => e.IsolateId)
                .OnDelete(DeleteBehavior.Cascade);

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
