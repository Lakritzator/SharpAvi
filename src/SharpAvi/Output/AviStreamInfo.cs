using System;
using System.Collections.Generic;

namespace SharpAvi.Output
{
    internal class StreamInfo
    {
        private readonly List<StandardIndexEntry> _standardIndex = new List<StandardIndexEntry>();
        private readonly List<SuperIndexEntry> _superIndex = new List<SuperIndexEntry>();
        private readonly List<Index1Entry> _index1 = new List<Index1Entry>();

        public StreamInfo(FourCC standardIndexChunkId)
        {
            StandardIndexChunkId = standardIndexChunkId;
            FrameCount = 0;
            MaxChunkDataSize = 0;
            TotalDataSize = 0;
        }

        public int FrameCount { get; private set; }
        
        public int MaxChunkDataSize { get; private set; }

        public long TotalDataSize { get; private set; }

        public IList<SuperIndexEntry> SuperIndex => _superIndex;

        public IList<StandardIndexEntry> StandardIndex => _standardIndex;

        public IList<Index1Entry> Index1 => _index1;

        public FourCC StandardIndexChunkId { get; }

        public void OnFrameWritten(int chunkDataSize)
        {
            FrameCount++;
            MaxChunkDataSize = Math.Max(MaxChunkDataSize, chunkDataSize);
            TotalDataSize += chunkDataSize;
        }
    }
}
