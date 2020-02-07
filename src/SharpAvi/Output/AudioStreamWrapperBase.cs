using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace SharpAvi.Output
{
    /// <summary>
    /// Base class for wrappers around <see cref="IAviAudioStreamInternal"/>.
    /// </summary>
    /// <remarks>
    /// Simply delegates all operations to wrapped stream.
    /// </remarks>
    internal abstract class AudioStreamWrapperBase : IAviAudioStreamInternal, IDisposable
    {
        protected AudioStreamWrapperBase(IAviAudioStreamInternal baseStream)
        {
            Contract.Requires(baseStream != null);

            _baseStream = baseStream;
        }

        protected IAviAudioStreamInternal BaseStream => _baseStream;
        private readonly IAviAudioStreamInternal _baseStream;

        public virtual void Dispose()
        {
            if (_baseStream is IDisposable baseStreamDisposable)
            {
                baseStreamDisposable.Dispose();
            }
        }

        public virtual int ChannelCount
        {
            get { return _baseStream.ChannelCount; }
            set { _baseStream.ChannelCount = value; }
        }

        public virtual int SamplesPerSecond
        {
            get { return _baseStream.SamplesPerSecond; }
            set { _baseStream.SamplesPerSecond = value; }
        }

        public virtual int BitsPerSample
        {
            get { return _baseStream.BitsPerSample; }
            set { _baseStream.BitsPerSample = value; }
        }

        public virtual short Format
        {
            get { return _baseStream.Format; }
            set { _baseStream.Format = value; }
        }

        public virtual int BytesPerSecond
        {
            get { return _baseStream.BytesPerSecond; }
            set { _baseStream.BytesPerSecond = value; }
        }

        public virtual int Granularity
        {
            get { return _baseStream.Granularity; }
            set { _baseStream.Granularity = value; }
        }

        public virtual byte[] FormatSpecificData
        {
            get { return _baseStream.FormatSpecificData; }
            set { _baseStream.FormatSpecificData = value; }
        }

        public virtual void WriteBlock(Memory<byte> data)
        {
            _baseStream.WriteBlock(data);
        }

        public virtual Task WriteBlockAsync(Memory<byte> data)
        {
            return _baseStream.WriteBlockAsync(data);
        }

        public int BlocksWritten => _baseStream.BlocksWritten;

        public int Index => _baseStream.Index;

        public virtual string Name
        {
            get { return _baseStream.Name; }
            set { _baseStream.Name = value; }
        }

        public FourCC StreamType => _baseStream.StreamType;

        public FourCC ChunkId => _baseStream.ChunkId;

        public virtual void PrepareForWriting()
        {
            _baseStream.PrepareForWriting();
        }

        public virtual void FinishWriting()
        {
            _baseStream.FinishWriting();
        }

        public void WriteHeader()
        {
            _baseStream.WriteHeader();
        }

        public void WriteFormat()
        {
            _baseStream.WriteFormat();
        }
    }
}
