namespace PandemicShield.Bio
{
    /// <summary>
    /// Шматок одного FASTA-запису для паралельної обробки.
    /// <para>
    /// Кожна позиція запису належить «ядру» рівно одного вікна. Навколо ядра є «фланги» — перекриття
    /// з сусідніми вікнами, щоб ORF чи мотив на межі був видимий повністю. Знахідка звітується лише
    /// тим вікном, у ядрі якого лежить її початок, тому дублікатів немає.
    /// </para>
    /// Усі координати 0-based у межах запису, кінці не включаються.
    /// </summary>
    public sealed record SequenceWindow(
        string RecordId,
        int RecordIndex,
        string Sequence,
        long WindowStart,
        long CoreStart,
        long CoreEnd,
        bool AtRecordStart,
        bool AtRecordEnd)
    {
        public long WindowEnd => WindowStart + Sequence.Length;

        /// <summary>Увесь запис як одне вікно.</summary>
        public static SequenceWindow WholeRecord(string recordId, string sequence, int recordIndex = 0) =>
            new(recordId, recordIndex, sequence, 0, 0, sequence.Length, true, true);
    }
}
