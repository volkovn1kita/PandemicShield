using System;

namespace PandemicShield.DataAccess.Entities
{
    /// <summary>
    /// Оновлення даних нагляду з NCBI Pathogen Detection для однієї групи бактерій (наприклад, Klebsiella):
    /// файл <c>PDG….amr.metadata.tsv</c> читається потоком, зберігаються лише ізоляти з обраних країн.
    /// </summary>
    public class SurveillanceSyncEntity
    {
        public Guid Id { get; private set; }
        public string Taxgroup { get; private set; }

        /// <summary>Країни через кому, як у geo_loc_name NCBI: "Ukraine" або "Ukraine,Poland".</summary>
        public string Countries { get; private set; }

        public ImportStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        /// <summary>Версія вибірки Pathogen Detection (PDG000000012.2562): з нею результат відтворюваний.</summary>
        public string? PdgVersion { get; private set; }
        public long BytesTotal { get; private set; }
        public long BytesRead { get; private set; }
        public int RowsScanned { get; private set; }
        public int IsolatesStored { get; private set; }
        public string? Message { get; private set; }

        public SurveillanceSyncEntity(string taxgroup, string countries)
        {
            Id = Guid.NewGuid();
            Taxgroup = taxgroup;
            Countries = countries;
            Status = ImportStatus.Queued;
            CreatedAt = DateTime.UtcNow;
        }

        public void MarkRunning(string pdgVersion, long bytesTotal)
        {
            Status = ImportStatus.Running;
            PdgVersion = pdgVersion;
            BytesTotal = bytesTotal;
        }

        public void MarkProgress(long bytesRead, int rowsScanned, int isolatesFound)
        {
            BytesRead = bytesRead;
            RowsScanned = rowsScanned;
            IsolatesStored = isolatesFound;
        }

        public void MarkCompleted(int isolatesStored, string message)
        {
            Status = ImportStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            IsolatesStored = isolatesStored;
            BytesRead = BytesTotal;
            Message = message;
        }

        public void MarkFailed(string message)
        {
            Status = ImportStatus.Failed;
            CompletedAt = DateTime.UtcNow;
            Message = message;
        }
    }

    /// <summary>Ізолят з NCBI Pathogen Detection: метадані й гени резистентності, пораховані NCBI тим самим AMRFinderPlus.</summary>
    public class SurveillanceIsolateEntity
    {
        public Guid Id { get; private set; }
        public string Taxgroup { get; private set; }
        public string? PdgVersion { get; private set; }

        /// <summary>Номер ізоляту в Pathogen Detection (PDT…), стабільний між версіями.</summary>
        public string TargetAccession { get; private set; }

        /// <summary>Збірка геному (GCA_…); може бути відсутня, якщо NCBI зібрав лише рідси.</summary>
        public string? AssemblyAccession { get; private set; }
        public string? BioSample { get; private set; }
        public string? Organism { get; private set; }
        public string? Strain { get; private set; }
        public string Country { get; private set; }
        public string? Region { get; private set; }
        public string? CollectionDate { get; private set; }
        public int? CollectionYear { get; private set; }
        public string? IsolationSource { get; private set; }
        public string? Host { get; private set; }

        /// <summary>Сирий список генів з NCBI (AMR_genotypes), наприклад "blaOXA-48,gyrA_S83I=POINT".</summary>
        public string? AmrGenotypes { get; private set; }
        public int AmrGeneCount { get; private set; }
        public string? AmrFinderVersion { get; private set; }
        public string? DatabaseVersion { get; private set; }

        public SurveillanceIsolateEntity(
            string taxgroup, string? pdgVersion, string targetAccession, string? assemblyAccession, string? bioSample,
            string? organism, string? strain, string country, string? region, string? collectionDate, int? collectionYear,
            string? isolationSource, string? host, string? amrGenotypes, int amrGeneCount, string? amrFinderVersion,
            string? databaseVersion)
        {
            Id = Guid.NewGuid();
            Taxgroup = taxgroup;
            PdgVersion = pdgVersion;
            TargetAccession = targetAccession;
            AssemblyAccession = assemblyAccession;
            BioSample = bioSample;
            Organism = organism;
            Strain = strain;
            Country = country;
            Region = region;
            CollectionDate = collectionDate;
            CollectionYear = collectionYear;
            IsolationSource = isolationSource;
            Host = host;
            AmrGenotypes = amrGenotypes;
            AmrGeneCount = amrGeneCount;
            AmrFinderVersion = amrFinderVersion;
            DatabaseVersion = databaseVersion;
        }
    }

    /// <summary>Один ген (або точкова мутація) ізоляту з класом антибіотиків за каталогом AMRFinderPlus.</summary>
    public class SurveillanceGeneEntity
    {
        public Guid Id { get; private set; }
        public Guid IsolateId { get; private set; }

        /// <summary>Символ як у NCBI без уточнення: blaOXA-48, gyrA_S83I.</summary>
        public string Symbol { get; private set; }

        /// <summary>Родина алелів: blaOXA-181 і blaOXA-48 → blaOXA-48; blaNDM-5 → blaNDM.</summary>
        public string Family { get; private set; }

        /// <summary>Уточнення після «=»: POINT (точкова мутація), PARTIAL, HMM… або null для повного гена.</summary>
        public string? Qualifier { get; private set; }
        public string? Class { get; private set; }
        public string? Subclass { get; private set; }

        public SurveillanceGeneEntity(Guid isolateId, string symbol, string family, string? qualifier, string? @class, string? subclass)
        {
            Id = Guid.NewGuid();
            IsolateId = isolateId;
            Symbol = symbol;
            Family = family;
            Qualifier = qualifier;
            Class = @class;
            Subclass = subclass;
        }
    }
}
