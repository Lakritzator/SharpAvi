using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using SharpAvi.Enums;

namespace SharpAvi.Output
{
    /// <summary>
    /// Base class for wrappers around <see cref="IAviVideoStreamInternal"/>.
    /// </summary>
    /// <remarks>
    /// Simply delegates all operations to wrapped stream.
    /// </remarks>
    internal abstract class VideoStreamWrapperBase : IAviVideoStreamInternal, IDisposable
    {
        protected VideoStreamWrapperBase(IAviVideoStreamInternal baseStream)
        {
            Contract.Requires(baseStream != null);

            _baseStream = baseStream;
        }

        protected IAviVideoStreamInternal BaseStream => _baseStream;
        private readonly IAviVideoStreamInternal _baseStream;

        public virtual void Dispose()
        {
            if (_baseStream is IDisposable baseStreamDisposable)
            {
                baseStreamDisposable.Dispose();
            }
        }

        public virtual int Width
        {
            get { return _baseStream.Width; }
            set { _baseStream.Width = value; }
        }

        public virtual int Height
        {
            get { return _baseStream.Height; }
            set { _baseStream.Height = value; }
        }

        public virtual BitsPerPixel BitsPerPixel
        {
            get { return _baseStream.BitsPerPixel; }
            set { _baseStream.BitsPerPixel = value; }
        }

        public virtual FourCC Codec
        {
            get { return _baseStream.Codec; }
            set { _baseStream.Codec = value; }
        }

        public virtual void WriteFrame(bool isKeyFrame, Memory<byte> frameData)
        {
            _baseStream.WriteFrame(isKeyFrame, frameData);
        }

        public virtual Task WriteFrameAsync(bool isKeyFrame, Memory<byte> frameData)
        {
            return _baseStream.WriteFrameAsync(isKeyFrame, frameData);
        }

        public int FramesWritten => _baseStream.FramesWritten;

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
