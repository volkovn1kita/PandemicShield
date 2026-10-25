using System;

namespace PandemicShield.Contracts
{
    /// <summary>
    /// Завдання для воркера AMRFinderPlus: оновити дані нагляду з NCBI Pathogen Detection.
    /// Група бактерій і країни зберігаються в записі оновлення в БД.
    /// </summary>
    public class SurveillanceJobMessage
    {
        public Guid SyncId { get; set; }
    }
}
