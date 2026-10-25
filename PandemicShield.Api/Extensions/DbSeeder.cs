using PandemicShield.DataAccess.Data;
using PandemicShield.DataAccess.Entities;
using PandemicShield.Contracts;
using Microsoft.EntityFrameworkCore;

namespace PandemicShield.Api.Extensions
{
    public static class DbSeeder
    {
        public static void Seed()
        {
            using var db = new PandemicDbContext();

            db.Database.Migrate();


            if (!db.Mutation.Any())
            {
                Console.WriteLine("База порожня. Завантажую біологічні маркери...");

                // Маркери — це точні амінокислотні мотиви, які шукаються в білках знайдених ORF.
                // Кожен перевірено на референсі (див. PandemicShield.Bio.Tests/SarsCov2ReferenceTests.cs
                // і docs/BIOLOGY_REVIEW.md).
                db.Mutation.AddRange(

                    // --- ВІРУСИ ---
                    new ReferenceMutationEntity(
                        "SARS-CoV-2 Spike D614G (залишки 607-621)",
                        "QVAVLYQGVNCTEVP",
                        ThreatCategory.Virus),

                    new ReferenceMutationEntity(
                        "SARS-CoV-2 Delta (B.1.617.2): Spike E156G + del157-158",
                        "VYYHKNNKSWMESGVYSSANNC",
                        ThreatCategory.Virus),

                    new ReferenceMutationEntity(
                        "Influenza A(H1): N-кінець HA1 (маркер субтипу, не мутація)",
                        "DTLCIGYHANNSTDTVD",
                        ThreatCategory.Virus),

                    // --- ЛЮДИНА ---
                    // Шукається в CDS/мРНК (наприклад, NM_000518). У геномній ДНК ген HBB лежить
                    // на мінус-ланцюзі й містить інтрони; FASTA не відрізняє носія від хворого.
                    new ReferenceMutationEntity(
                        "Homo sapiens HBB p.Glu7Val (HbS, традиційно E6V): алель серпоподібноклітинної анемії",
                        "MVHLTPVEKS",
                        ThreatCategory.HumanGenetics)
                );

                db.SaveChanges();
                Console.WriteLine("Маркери успішно завантажено в базу даних!");
            }
        }
    }
}
