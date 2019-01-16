using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace SharpAvi.Output
{
    internal class AviVideoStream : AviStreamBase, IAviVideoStreamInternal
    {
        private readonly IAviStreamWriteHandler _writeHandler;
        private FourCC _streamCodec;
        private int _width;
        private int _height;
        private BitsPerPixel _bitsPerPixel;
        private int _framesWritten;

        public AviVideoStream(int index, IAviStreamWriteHandler writeHandler, 
            int width, int height, BitsPerPixel bitsPerPixel)
            : base(index)
        {
            Contract.Requires(index >= 0);
            Contract.Requires(writeHandler != null);
            Contract.Requires(width > 0);
            Contract.Requires(height > 0);
            Contract.Requires(Enum.IsDefined(typeof(BitsPerPixel), bitsPerPixel));

            _writeHandler = writeHandler;
            _width = width;
            _height = height;
            _bitsPerPixel = bitsPerPixel;
            _streamCodec = KnownFourCCs.Codecs.Uncompressed;
        }


        public int Width
        {
            get { return _width; }
            set
            {
                CheckNotFrozen();
                _width = value;
            }
        }

        public int Height
        {
            get { return _height; }
            set
            {
                CheckNotFrozen();
                _height = value;
            }
        }

        public BitsPerPixel BitsPerPixel
        {
            get { return _bitsPerPixel; }
            set
            {
                CheckNotFrozen();
                _bitsPerPixel = value;
            }
        }

        public FourCC Codec
        {
            get { return _streamCodec; }
            set
            {
                CheckNotFrozen();
                _streamCodec = value;
            }
        }

        public void WriteFrame(bool isKeyFrame, byte[] frameData, int startIndex, int count)
        {
            _writeHandler.WriteVideoFrame(this, isKeyFrame, frameData, startIndex, count);
            System.Threading.Interlocked.Increment(ref _framesWritten);
        }

        public Task WriteFrameAsync(bool isKeyFrame, byte[] frameData, int startIndex, int count)
        {
            throw new NotSupportedException("Asynchronous writes are not supported.");
        }

        public int FramesWritten => _framesWritten;


        public override FourCC StreamType => KnownFourCCs.StreamTypes.Video;

        protected override FourCC GenerateChunkId()
        {
            return KnownFourCCs.Chunks.VideoFrame(Index, Codec != KnownFourCCs.Codecs.Uncompressed);
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
