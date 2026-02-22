using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PandemicShield.Aggregator.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.Aggregator.Data
{
    public class PandemicDbContext : DbContext
    {
        public DbSet<ThreatAlertEntity> Threats { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string PostgreHost = Environment.GetEnvironmentVariable("POSTGRESDB_HOST") ?? "localhost";
            optionsBuilder.UseNpgsql($"Host={PostgreHost};Port=5433;Database=pandemic_shield_db;Username=adminpostgres;Password=rootpassword");
            base.OnConfiguring(optionsBuilder);
        }
    }
}
