using System;
using System.Diagnostics.Contracts;
using SharpAvi.Codecs;
using SharpAvi.Enums;

namespace SharpAvi.Output
{
    /// <summary>
    /// Wrapper on the <see cref="IAviVideoStreamInternal"/> object to provide encoding.
    /// </summary>
    internal class EncodingVideoStreamWrapper : VideoStreamWrapperBase
    {
        private readonly IVideoEncoder _encoder;
        private readonly bool _ownsEncoder;
        private readonly byte[] _encodedBuffer;
        private readonly object _syncBuffer = new object();

        /// <summary>
        /// Creates a new instance of <see cref="EncodingVideoStreamWrapper"/>.
        /// </summary>
        /// <param name="baseStream">Video stream to be wrapped.</param>
        /// <param name="encoder">Encoder to be used.</param>
        /// <param name="ownsEncoder">Whether to dispose the encoder.</param>
        public EncodingVideoStreamWrapper(IAviVideoStreamInternal baseStream, IVideoEncoder encoder, bool ownsEncoder)
            : base(baseStream)
        {
            Contract.Requires(baseStream != null);
            Contract.Requires(encoder != null);

            _encoder = encoder;
            _ownsEncoder = ownsEncoder;
            _encodedBuffer = new byte[encoder.MaxEncodedSize];
        }

        public override void Dispose()
        {
            if (_ownsEncoder)
            {
                if (_encoder is IDisposable encoderDisposable)
                {
                    encoderDisposable.Dispose();
                }
            }

            base.Dispose();
        }


        /// <summary> Video codec. </summary>
        public override FourCC Codec
        {
            get => _encoder.Codec;
            set => ThrowPropertyDefinedByEncoder();
        }

        /// <summary> Bits per pixel. </summary>
        public override BitsPerPixel BitsPerPixel
        {
            get => _encoder.BitsPerPixel;
            set => ThrowPropertyDefinedByEncoder();
        }

        /// <summary>Encodes and writes a frame.</summary>
        public override void WriteFrame(bool isKeyFrame, byte[] frameData, int startIndex, int count)
        {
            // Prevent accessing encoded buffer by multiple threads simultaneously
            lock (_syncBuffer)
            {
                count = _encoder.EncodeFrame(frameData, startIndex, _encodedBuffer, 0, out isKeyFrame);
                base.WriteFrame(isKeyFrame, _encodedBuffer, 0, count);
            }
        }

        public override System.Threading.Tasks.Task WriteFrameAsync(bool isKeyFrame, byte[] frameData, int startIndex, int length)
        {
            throw new NotSupportedException("Asynchronous writes are not supported.");
        }

        public override void PrepareForWriting()
        {
            // Set properties of the base stream
            BaseStream.Codec = _encoder.Codec;
            BaseStream.BitsPerPixel = _encoder.BitsPerPixel;

            base.PrepareForWriting();
        }


        private void ThrowPropertyDefinedByEncoder()
        {
            throw new NotSupportedException("The value of the property is defined by the encoder.");
        }
    }
}
