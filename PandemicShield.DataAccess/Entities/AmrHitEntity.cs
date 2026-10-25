using System;

namespace PandemicShield.DataAccess.Entities
{
    /// <summary>
    /// Один рядок звіту AMRFinderPlus: ген або точкова мутація резистентності, стресу чи вірулентності.
    /// Координати 1-based у контигу збірки.
    /// </summary>
    public class AmrHitEntity
    {
        public Guid Id { get; private set; }
        public Guid RunId { get; private set; }
        public string ContigId { get; private set; }
        public long Start { get; private set; }
        public long Stop { get; private set; }
        public char Strand { get; private set; }

        // blaKPC-2, mecA, gyrA_S83L
        public string ElementSymbol { get; private set; }
        public string ElementName { get; private set; }

        // core / plus
        public string Scope { get; private set; }

        // AMR / STRESS / VIRULENCE; підтип: AMR, POINT, BIOCIDE, METAL, ...
        public string Type { get; private set; }
        public string Subtype { get; private set; }
        public string Class { get; private set; }
        public string Subclass { get; private set; }

        // ALLELEX, EXACTX, BLASTX, PARTIALX, POINTX, HMM, ...
        public string Method { get; private set; }
        public double? CoveragePercent { get; private set; }
        public double? IdentityPercent { get; private set; }
        public string? ClosestReference { get; private set; }

        public AmrHitEntity(
            Guid runId, string contigId, long start, long stop, char strand,
            string elementSymbol, string elementName, string scope, string type, string subtype,
            string @class, string subclass, string method,
            double? coveragePercent, double? identityPercent, string? closestReference)
        {
            Id = Guid.NewGuid();
            RunId = runId;
            ContigId = contigId;
            Start = start;
            Stop = stop;
            Strand = strand;
            ElementSymbol = elementSymbol;
            ElementName = elementName;
            Scope = scope;
            Type = type;
            Subtype = subtype;
            Class = @class;
            Subclass = subclass;
            Method = method;
            CoveragePercent = coveragePercent;
            IdentityPercent = identityPercent;
            ClosestReference = closestReference;
        }
    }
}
