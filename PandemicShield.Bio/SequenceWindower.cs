namespace PandemicShield.Bio
{
    /// <summary>
    /// Ріже записи FASTA на вікна <see cref="SequenceWindow"/> з перекриттям. Пам'ять обмежена
    /// розміром одного вікна (ядро + 2 фланги), тож можна обробляти записи будь-якої довжини.
    /// Готові вікна накопичуються в черзі, звідки їх забирає <see cref="TakeReady"/>.
    /// </summary>
    public sealed class SequenceWindower : IFastaSink
    {
        private readonly int _coreSize;
        private readonly int _flankSize;
        private readonly Queue<SequenceWindow> _ready = new();

        private char[] _buffer;
        private int _bufferLength;
        private long _bufferStart;     // позиція в записі першого символу буфера
        private long _received;        // скільки основ поточного запису отримано
        private long _nextCoreStart;

        private string _recordId = string.Empty;
        private int _recordIndex = -1;

        public int WindowCount { get; private set; }

        /// <param name="coreSize">Довжина ядра вікна.</param>
        /// <param name="flankSize">
        /// Перекриття з кожного боку. ORF, коротші за фланг, ніколи не губляться на межі вікон.
        /// </param>
        public SequenceWindower(int coreSize, int flankSize)
        {
            if (coreSize <= 0) throw new ArgumentOutOfRangeException(nameof(coreSize));
            if (flankSize < 0) throw new ArgumentOutOfRangeException(nameof(flankSize));

            _coreSize = coreSize;
            _flankSize = flankSize;
            _buffer = new char[Math.Min(coreSize + 2 * flankSize, 1 << 20)];
        }

        public IEnumerable<SequenceWindow> TakeReady()
        {
            while (_ready.Count > 0)
            {
                yield return _ready.Dequeue();
            }
        }

        public void OnRecordStart(string id, string description)
        {
            _recordId = id;
            _recordIndex++;
            _bufferLength = 0;
            _bufferStart = 0;
            _received = 0;
            _nextCoreStart = 0;
        }

        public void OnBases(ReadOnlySpan<char> bases)
        {
            EnsureCapacity(_bufferLength + bases.Length);
            bases.CopyTo(_buffer.AsSpan(_bufferLength));
            _bufferLength += bases.Length;
            _received += bases.Length;

            // Вікно випускається, лише коли вже є хоча б одна основа після його правого флангу.
            // Якщо запис закінчиться рівно на межі флангу, вікно випуститься в OnRecordEnd
            // з правильним AtRecordEnd = true.
            while (_received > _nextCoreStart + _coreSize + _flankSize)
            {
                Emit(_nextCoreStart + _coreSize, _nextCoreStart + _coreSize + _flankSize, atRecordEnd: false);
            }
        }

        public void OnRecordEnd()
        {
            while (_nextCoreStart < _received)
            {
                long coreEnd = Math.Min(_nextCoreStart + _coreSize, _received);
                long windowEnd = Math.Min(coreEnd + _flankSize, _received);
                Emit(coreEnd, windowEnd, atRecordEnd: windowEnd == _received);
            }
        }

        private void Emit(long coreEnd, long windowEnd, bool atRecordEnd)
        {
            long windowStart = Math.Max(0, _nextCoreStart - _flankSize);
            int offset = (int)(windowStart - _bufferStart);
            int length = (int)(windowEnd - windowStart);

            _ready.Enqueue(new SequenceWindow(
                _recordId,
                _recordIndex,
                new string(_buffer, offset, length),
                windowStart,
                _nextCoreStart,
                coreEnd,
                AtRecordStart: windowStart == 0,
                AtRecordEnd: atRecordEnd));
            WindowCount++;

            _nextCoreStart = coreEnd;
            DropBefore(Math.Max(0, _nextCoreStart - _flankSize));
        }

        private void DropBefore(long position)
        {
            int drop = (int)(position - _bufferStart);
            if (drop <= 0) return;

            _bufferLength -= drop;
            Array.Copy(_buffer, drop, _buffer, 0, _bufferLength);
            _bufferStart = position;
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _buffer.Length) return;
            Array.Resize(ref _buffer, Math.Max(required, _buffer.Length * 2));
        }
    }
}
