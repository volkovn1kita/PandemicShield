using System;
using System.Collections.Generic;
using System.Text;

namespace PandemicShield.Worker.Data
{
    public static class ThreatDatabase
    {
        public static readonly Dictionary<string, string> KnownMutations = new Dictionary<string, string>
        {
            // Реальний мотив: Furin Cleavage Site (підвищує здатність вірусу проникати в клітину)
            { "PRRA", "Сайт розщеплення фурину (Висока заразність)" },
        
            // Вигадані, але типові мотиви
            { "FAQY", "Імунне ухилення (Ховається від антитіл)" },
            { "KCYT", "Стійкість до високих температур" }
        
            // ПАСХАЛКА для твого test.fasta (щоб спрацювало на білку MIRACLE)
            //{ "MIR", "Штам швидкої реплікації (MIR-варіант)" }
        };
    }
}
