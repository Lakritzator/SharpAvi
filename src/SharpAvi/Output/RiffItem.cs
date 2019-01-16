using System.Diagnostics.Contracts;

namespace SharpAvi.Output
{
    /// <summary>
    /// Item of a RIFF file - either list or chunk.
    /// </summary>
    internal struct RiffItem
    {
        public const int ItemHeaderSize = 2 * sizeof(uint);

        private readonly long _dataStart;
        private int _dataSize;

        public RiffItem(long dataStart, int dataSize = -1)
        {
            Contract.Requires(dataStart >= ItemHeaderSize);
            Contract.Requires(dataSize <= int.MaxValue - ItemHeaderSize);

            _dataStart = dataStart;
            _dataSize = dataSize;
        }

        public long DataStart => _dataStart;

        public long ItemStart => _dataStart - ItemHeaderSize;

        public long DataSizeStart => _dataStart - sizeof(uint);

        public int DataSize
        {
            get { return _dataSize; }
            set
            {
                Contract.Requires(value >= 0);
                Contract.Requires(DataSize < 0);

                _dataSize = value;
            }
        }

        public int ItemSize => _dataSize < 0 ? -1 : _dataSize + ItemHeaderSize;
    }
}
