using System.Text;

namespace PandemicShield.Bio
{
    /// <summary>Отримувач подій від <see cref="FastaStreamReader"/>.</summary>
    public interface IFastaSink
    {
        void OnRecordStart(string id, string description);
        void OnBases(ReadOnlySpan<char> bases);
        void OnRecordEnd();
    }

    /// <summary>
    /// Потоковий розбір FASTA, який розуміє записи. Байти можна подавати шматками довільного розміру:
    /// заголовок чи перенос рядка може бути розрізаний між шматками.
    /// <para>
    /// Заголовок ('>' на початку рядка) не потрапляє в послідовність. Переноси рядків, пробіли, цифри
    /// і символи вирівнювання відкидаються, основи переводяться у верхній регістр (U→T).
    /// </para>
    /// </summary>
    public sealed class FastaStreamReader
    {
        private readonly IFastaSink _sink;
        private readonly StringBuilder _header = new();
        private readonly char[] _bases = new char[16 * 1024];

        private bool _inHeader;
        private bool _atLineStart = true;
        private bool _inRecord;
        private bool _completed;

        public int RecordCount { get; private set; }
        public long BaseCount { get; private set; }

        /// <summary>Символи, які не є нуклеотидами IUPAC; у послідовності вони замінюються на 'N'.</summary>
        public long InvalidSymbolCount { get; private set; }

        public FastaStreamReader(IFastaSink sink)
        {
            _sink = sink;
        }

        public void Feed(ReadOnlySpan<byte> data)
        {
            if (_completed) throw new InvalidOperationException("Reader is already completed.");

            int pending = 0;
            foreach (byte b in data)
            {
                if (_inHeader)
                {
                    if (b == (byte)'\n')
                    {
                        _inHeader = false;
                        _atLineStart = true;
                        StartRecord();
                    }
                    else if (b != (byte)'\r')
                    {
                        _header.Append((char)b);
                    }
                    continue;
                }

                if (b == (byte)'>' && _atLineStart)
                {
                    Flush(ref pending);
                    EndRecord();
                    _inHeader = true;
                    _header.Clear();
                    continue;
                }

                _atLineStart = b == (byte)'\n';

                char c = Nucleotides.Normalize(b);
                if (c == Nucleotides.Skip) continue;

                if (c == Nucleotides.Invalid)
                {
                    InvalidSymbolCount++;
                    c = 'N';
                }

                if (!_inRecord)
                {
                    // Послідовність без заголовка: вважаємо її записом без назви.
                    _header.Clear();
                    _header.Append("unnamed");
                    StartRecord();
                }

                _bases[pending++] = c;
                if (pending == _bases.Length) Flush(ref pending);
            }

            Flush(ref pending);
        }

        public void Complete()
        {
            if (_completed) return;

            if (_inHeader)
            {
                _inHeader = false;
                StartRecord();
            }
            EndRecord();
            _completed = true;
        }

        private void Flush(ref int pending)
        {
            if (pending == 0) return;
            BaseCount += pending;
            _sink.OnBases(_bases.AsSpan(0, pending));
            pending = 0;
        }

        private void StartRecord()
        {
            string header = _header.ToString().Trim();
            int space = header.IndexOfAny([' ', '\t']);
            string id = space < 0 ? header : header[..space];
            string description = space < 0 ? string.Empty : header[(space + 1)..].Trim();

            if (id.Length == 0) id = $"record_{RecordCount + 1}";

            _inRecord = true;
            RecordCount++;
            _sink.OnRecordStart(id, description);
        }

        private void EndRecord()
        {
            if (!_inRecord) return;
            _inRecord = false;
            _sink.OnRecordEnd();
        }
    }
}
