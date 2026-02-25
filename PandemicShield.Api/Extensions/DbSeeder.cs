using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using PandemicShield.Contracts; // Підключи свій простір імен для ThreatCategory
using System;
using System.Linq;

namespace PandemicShield.Api.Extensions
{
    public static class DbSeeder
    {
        public static void Seed()
        {
            using var db = new PandemicDbContext();

            db.Database.EnsureCreated();


            if (!db.Mutation.Any())
            {
                Console.WriteLine("База порожня. Завантажую реальні біологічні мутації...");

                db.Mutation.AddRange(

                    // --- ВІРУСИ ---
                    new ReferenceMutationEntity(
                        "SARS-CoV-2 (COVID-19) Spike D614G Mutation",
                        "VYYHKNNKSWMESGVYSSANNC",
                        (ThreatCategory)0),

                    new ReferenceMutationEntity(
                        "Influenza A H1N1 (Swine Flu) Hemagglutinin",
                        "DTLCIGYHANNSTDTVD",
                        (ThreatCategory)0),

                    // --- ЛЮДИНА ---
                    new ReferenceMutationEntity(
                        "Homo Sapiens - Sickle Cell Anemia (HBB Gene E6V)",
                        "VHLTPVEKS",
                        (ThreatCategory)1),

                    new ReferenceMutationEntity(
                        "Homo Sapiens - BRCA1 Cancer Risk Marker",
                        "CPICLELIKE",
                        (ThreatCategory)1)
                );

                db.SaveChanges();
                Console.WriteLine("Мутації успішно завантажено в базу даних!");
            }
        }
    }
}