using System;

namespace PandemicShield.DataAccess.Entities
{
    /// <summary>
    /// Маркер-варіант для сповіщень за результатами Nextclade. Notation — позначення у форматі
    /// PandemicShield.Bio.Variants.VariantPattern: S:D614G, A23403G, S:F157-, clade:Delta, lineage:BA.2.86*.
    /// </summary>
    public class VariantMarkerEntity
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string Notation { get; private set; }

        // Набір даних Nextclade, для якого діє маркер; null — для будь-якого.
        public string? Dataset { get; private set; }
        public string? Description { get; private set; }
        public string? Source { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public VariantMarkerEntity(string name, string notation, string? dataset, string? description, string? source)
        {
            Id = Guid.NewGuid();
            Name = name;
            Notation = notation;
            Dataset = dataset;
            Description = description;
            Source = source;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
