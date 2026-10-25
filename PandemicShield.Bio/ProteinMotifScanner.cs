namespace PandemicShield.Bio
{
    /// <summary>Збіг амінокислотного мотиву всередині ORF.</summary>
    /// <param name="AminoAcidIndex">0-based позиція мотиву в білку ORF.</param>
    /// <param name="NucleotideStart">0-based початок на прямому ланцюзі запису.</param>
    /// <param name="NucleotideEnd">Кінець на прямому ланцюзі (не включається).</param>
    public sealed record MotifMatch(Orf Orf, int AminoAcidIndex, long NucleotideStart, long NucleotideEnd);

    /// <summary>
    /// Пошук точного збігу амінокислотного мотиву в білках, перекладених з ORF.
    /// <para>
    /// Це спрощений «навчальний» режим. Він не бачить синонімічних і некодуючих мутацій і перестає
    /// знаходити мотив, якщо поруч з'являється ще одна заміна. Коректний виклик варіантів робиться
    /// вирівнюванням на референс (наприклад, Nextclade).
    /// </para>
    /// </summary>
    public static class ProteinMotifScanner
    {
        public static IEnumerable<MotifMatch> Scan(Orf orf, string motif)
        {
            if (string.IsNullOrEmpty(motif)) yield break;

            int index = orf.Protein.IndexOf(motif, StringComparison.Ordinal);
            while (index >= 0)
            {
                var (start, end) = orf.AminoAcidsToNucleotides(index, motif.Length);
                yield return new MotifMatch(orf, index, start, end);
                index = orf.Protein.IndexOf(motif, index + 1, StringComparison.Ordinal);
            }
        }
    }
}
