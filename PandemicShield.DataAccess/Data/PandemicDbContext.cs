using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using PandemicShield.DataAccess.Entities;

namespace PandemicShield.DataAccess.Data
{
    public class PandemicDbContext : DbContext
    {
        public DbSet<ThreatAlertEntity> Threats { get; set; }
        public DbSet<ReferenceMutationEntity> Mutation { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ThreatAlertEntity>().Property(e => e.Category).HasConversion<string>();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string PostgreHost = Environment.GetEnvironmentVariable("POSTGRESDB_HOST") ?? "localhost";
            optionsBuilder.UseNpgsql($"Host={PostgreHost};Port=5433;Database=pandemic_shield_db;Username=adminpostgres;Password=rootpassword");
            base.OnConfiguring(optionsBuilder);
        }
    }
}
