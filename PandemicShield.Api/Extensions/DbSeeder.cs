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

            if (!db.VariantMarkers.Any())
            {
                // Маркери-варіанти для результатів Nextclade (позначення — див. PandemicShield.Bio.Variants.VariantPattern).
                // Кожен перевірено на прикладах послідовностей набору даних Nextstrain (див. PandemicShield.Nextclade.Tests).
                const string sarsCov2 = "nextstrain/sars-cov-2/wuhan-hu-1/orfs";
                db.VariantMarkers.AddRange(
                    new VariantMarkerEntity("SARS-CoV-2 Spike N501Y: сильніше зв'язування з рецептором ACE2",
                        "S:N501Y", sarsCov2,
                        "Є в Alpha, Beta, Gamma та в усіх Omicron.", "https://covariants.org"),
                    new VariantMarkerEntity("SARS-CoV-2 Spike E484K: ухилення від нейтралізуючих антитіл",
                        "S:E484K", sarsCov2,
                        "Beta, Gamma, Eta, Mu; також BA.2.86 (у BA.2 тут зазвичай E484A).", "https://covariants.org"),
                    new VariantMarkerEntity("SARS-CoV-2 Spike L452R: ухилення від антитіл",
                        "S:L452R", sarsCov2,
                        "Delta, Epsilon, Kappa, BA.4/BA.5.", "https://covariants.org"),
                    new VariantMarkerEntity("SARS-CoV-2 Spike P681R: сайт розщеплення фурином",
                        "S:P681R", sarsCov2,
                        "Delta, Kappa; пов'язують з ефективнішим злиттям клітин.", "https://covariants.org"),
                    new VariantMarkerEntity("SARS-CoV-2 nsp5 (Mpro) E166V: знижена чутливість до нірматрелвіру (Paxlovid)",
                        "ORF1a:E3429V", sarsCov2,
                        "nsp5 займає залишки 3264–3569 ORF1a, тож E166 = ORF1a:3429. Описано в дослідженнях резистентності.", null),
                    new VariantMarkerEntity("SARS-CoV-2 Delta (усі клади: 21A, 21I, 21J)",
                        "clade:Delta", sarsCov2, null, "https://covariants.org"),
                    new VariantMarkerEntity("SARS-CoV-2 BA.2.86 і нащадки (зокрема JN.1)",
                        "lineage:BA.2.86*", sarsCov2,
                        "«Стрибок» на понад 30 мутацій Spike відносно BA.2; від JN.1 походять лінії 2024–2025 рр.", null)
                );

                db.SaveChanges();
                Console.WriteLine("Маркери-варіанти для Nextclade завантажено.");
            }

            if (!db.VariantMarkers.Any(m => m.Notation.StartsWith("gene:") || m.Notation.StartsWith("class:") || m.Notation.StartsWith("subclass:")))
            {
                // Правила для бактерій (результати AMRFinderPlus). Перевірено на звітах у PandemicShield.AmrFinder.Tests.
                db.VariantMarkers.AddRange(
                    new VariantMarkerEntity("Карбапенемази: стійкість до карбапенемів",
                        "subclass:CARBAPENEM", null,
                        "KPC, NDM, OXA-48-подібні, VIM, IMP. Карбапенеми — антибіотики резерву; такі бактерії часто знаходять у бойових пораненнях.",
                        "https://www.ncbi.nlm.nih.gov/pathogens/antimicrobial-resistance/AMRFinder/"),
                    new VariantMarkerEntity("Стійкість до колістину",
                        "subclass:COLISTIN", null,
                        "Гени mcr і мутації pmrB/mgrB. Колістин — один з останніх варіантів проти карбапенем-резистентних бактерій.", null),
                    new VariantMarkerEntity("ESBL CTX-M: стійкість до цефалоспоринів",
                        "gene:blaCTX-M*", null,
                        "Бета-лактамази розширеного спектра, найпоширеніший механізм стійкості до цефалоспоринів 3-го покоління.", null),
                    new VariantMarkerEntity("MRSA: ген mecA",
                        "gene:mecA", null,
                        "Метицилін-резистентний Staphylococcus aureus.", null),
                    new VariantMarkerEntity("Стійкість до ванкоміцину",
                        "subclass:VANCOMYCIN", null,
                        "Гени van (VRE, VRSA).", null),
                    new VariantMarkerEntity("Шига-токсин 2 (STEC)",
                        "class:STX2", null,
                        "Гени токсину Stx2 кишкової палички: ризик гемолітико-уремічного синдрому.", null)
                );

                db.SaveChanges();
                Console.WriteLine("Правила AMRFinderPlus завантажено.");
            }

            if (!db.VariantMarkers.Any(m => m.Notation.StartsWith("st:")))
            {
                // Міжнародні клони високого ризику (high-risk clones): ST описує «чий це штам», а не лише які в нього гени.
                // Схема вказана явно, бо номери ST повторюються між схемами PubMLST.
                db.VariantMarkers.AddRange(
                    new VariantMarkerEntity("K. pneumoniae ST147: клон високого ризику з NDM та OXA-48",
                        "st:klebsiella/147", null,
                        "Один з найпоширеніших карбапенем-резистентних клонів; його знаходять у пацієнтах, евакуйованих з України до шпиталів ЄС.",
                        "https://pubmlst.org/organisms/klebsiella-pneumoniae"),
                    new VariantMarkerEntity("K. pneumoniae ST395: клон високого ризику",
                        "st:klebsiella/395", null, "Поширений у Східній Європі, часто несе OXA-48 і NDM.", null),
                    new VariantMarkerEntity("K. pneumoniae ST307: клон високого ризику",
                        "st:klebsiella/307", null, "Глобальний клон, зазвичай з CTX-M-15, дедалі частіше з карбапенемазами.", null),
                    new VariantMarkerEntity("K. pneumoniae ST23: гіпервірулентний клон (hvKp)",
                        "st:klebsiella/23", null,
                        "Класичний гіпервірулентний клон: уражає й здорових людей (абсцеси печінки). У поєднанні з карбапенемазою " +
                        "(конвергентний штам) — одна з найнебезпечніших комбінацій.", null),
                    new VariantMarkerEntity("K. pneumoniae ST258: клональна група CG258 (KPC)",
                        "st:klebsiella/258", null, "Класичний носій карбапенемази KPC у США та Південній Європі.", null),
                    new VariantMarkerEntity("E. coli ST131: глобальний клон з ESBL",
                        "st:ecoli_achtman_4/131", null, "Найпоширеніша причина позалікарняних інфекцій сечових шляхів з CTX-M-15.", null),
                    new VariantMarkerEntity("A. baumannii ST2 (Pasteur): міжнародний клон IC2",
                        "st:abaumannii_2/2", null, "Домінує серед карбапенем-резистентних A. baumannii, часто з OXA-23.", null)
                );

                db.SaveChanges();
                Console.WriteLine("Правила MLST завантажено.");
            }
        }
    }
}
