using Microsoft.EntityFrameworkCore.Query;
using PandemicShield.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.DataAccess.Entities
{
    public class ReferenceMutationEntity
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string Sequence { get; private set; }
        public ThreatCategory Category { get; private set; }

        public ReferenceMutationEntity(
            string name,
            string sequence,
            ThreatCategory category)
        {
            Id = Guid.NewGuid();
            Name = name;
            Sequence = sequence;
            Category = category;
        }
    }
}
