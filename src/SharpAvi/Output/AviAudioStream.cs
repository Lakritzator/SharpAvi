using System;
using System.Diagnostics.Contracts;

namespace SharpAvi.Output
{
    internal class AviAudioStream : AviStreamBase, IAviAudioStreamInternal
    {
        private readonly IAviStreamWriteHandler _writeHandler;
        private int _channelCount;
        private int _samplesPerSecond;
        private int _bitsPerSample;
        private short _format;
        private int _bytesPerSecond;
        private int _granularity;
        private byte[] _formatData;
        private int _blocksWritten;

        public AviAudioStream(int index, IAviStreamWriteHandler writeHandler, int channelCount, int samplesPerSecond, int bitsPerSample) : base(index)
        {
            Contract.Requires(index >= 0);
            Contract.Requires(writeHandler != null);

            _writeHandler = writeHandler;

            _format = AudioFormats.Pcm;
            _formatData = null;

            _channelCount = channelCount;
            _samplesPerSecond = samplesPerSecond;
            _bitsPerSample = bitsPerSample;
            _granularity = (bitsPerSample * channelCount + 7) / 8;
            _bytesPerSecond = _granularity * samplesPerSecond;
        }

        
        public int ChannelCount
        {
            get { return _channelCount; }
            set
            {
                CheckNotFrozen();
                _channelCount = value;
            }
        }

        public int SamplesPerSecond
        {
            get { return _samplesPerSecond; }
            set
            {
                CheckNotFrozen();
                _samplesPerSecond = value;
            }
        }

        public int BitsPerSample
        {
            get { return _bitsPerSample; }
            set
            {
                CheckNotFrozen();
                _bitsPerSample = value;
            }
        }

        public short Format
        {
            get { return _format; }
            set
            {
                CheckNotFrozen();
                _format = value;
            }
        }

        public int BytesPerSecond 
        {
            get { return _bytesPerSecond; }
            set
            {
                CheckNotFrozen();
                _bytesPerSecond = value;
            }
        }

        public int Granularity 
        {
            get { return _granularity; }
            set
            {
                CheckNotFrozen();
                _granularity = value;
            }
        }

        public byte[] FormatSpecificData
        {
            get { return _formatData; }
            set
            {
                CheckNotFrozen();
                _formatData = value;
            }
        }

        public void WriteBlock(Memory<byte> buffer)
        {
            _writeHandler.WriteAudioBlock(this, buffer);
            System.Threading.Interlocked.Increment(ref _blocksWritten);
        }

        public System.Threading.Tasks.Task WriteBlockAsync(Memory<byte> data)
        {
            throw new NotSupportedException("Asynchronous writes are not supported.");
        }

        public int BlocksWritten => _blocksWritten;


        public override FourCC StreamType => KnownFourCCs.StreamTypes.Audio;

        protected override FourCC GenerateChunkId()
        {
 	        return KnownFourCCs.Chunks.AudioData(Index);
        }

        public override void WriteHeader()
        {
            _writeHandler.WriteStreamHeader(this);
        }

        public override void WriteFormat()
        {
            _writeHandler.WriteStreamFormat(this);
        }
    }
}
