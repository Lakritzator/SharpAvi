using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;
using SharpAvi.Codecs;

namespace SharpAvi.Output
{
    /// <summary>
    /// Wrapper on the <see cref="IAviAudioStreamInternal"/> object to provide encoding.
    /// </summary>
    internal class EncodingAudioStreamWrapper : AudioStreamWrapperBase
    {
        private readonly IAudioEncoder _encoder;
        private readonly bool _ownsEncoder;
        private byte[] _encodedBuffer;
        private readonly object _syncBuffer = new object();

        public EncodingAudioStreamWrapper(IAviAudioStreamInternal baseStream, IAudioEncoder encoder, bool ownsEncoder) : base(baseStream)
        {
            Contract.Requires(baseStream != null);
            Contract.Requires(encoder != null);

            _encoder = encoder;
            _ownsEncoder = ownsEncoder;
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

        /// <summary>
        /// Number of channels in this audio stream.
        /// </summary>
        public override int ChannelCount
        {
            get { return _encoder.ChannelCount; }
            set { ThrowPropertyDefinedByEncoder(); }
        }

        /// <summary>
        /// Sample rate, in samples per second (herz).
        /// </summary>
        public override int SamplesPerSecond
        {
            get { return _encoder.SamplesPerSecond; }
            set { ThrowPropertyDefinedByEncoder(); }
        }

        /// <summary>
        /// Number of bits per sample per single channel (usually 8 or 16).
        /// </summary>
        public override int BitsPerSample
        {
            get { return _encoder.BitsPerSample; }
            set { ThrowPropertyDefinedByEncoder(); }
        }

        /// <summary>
        /// Format of the audio data.
        /// </summary>
        public override short Format
        {
            get { return _encoder.Format; }
            set { ThrowPropertyDefinedByEncoder(); }
        }

        /// <summary>
        /// Average byte rate of the stream.
        /// </summary>
        public override int BytesPerSecond
        {
            get { return _encoder.BytesPerSecond; }
            set { ThrowPropertyDefinedByEncoder(); }
        }

        /// <summary>
        /// Size in bytes of minimum item of data in the stream.
        /// </summary>
        public override int Granularity
        {
            get { return _encoder.Granularity; }
            set { ThrowPropertyDefinedByEncoder(); }
        }

        /// <summary>
        /// Extra data defined by a specific format which should be added to the stream header.
        /// </summary>
        public override byte[] FormatSpecificData
        {
            get { return _encoder.FormatSpecificData; }
            set { ThrowPropertyDefinedByEncoder(); }
        }

        /// <summary>
        /// Encodes and writes a block of audio data.
        /// </summary>
        public override void WriteBlock(byte[] data, int startIndex, int length)
        {
            // Prevent accessing encoded buffer by multiple threads simultaneously
            lock (_syncBuffer)
            {
                EnsureBufferIsSufficient(length);
                var encodedLength = _encoder.EncodeBlock(data, startIndex, length, _encodedBuffer, 0);
                if (encodedLength > 0)
                {
                    base.WriteBlock(_encodedBuffer, 0, encodedLength);
                }
            }
        }

        public override Task WriteBlockAsync(byte[] data, int startIndex, int length)
        {
            throw new NotSupportedException("Asynchronous writes are not supported.");
        }

        public override void PrepareForWriting()
        {
            // Set properties of the base stream
            BaseStream.ChannelCount = ChannelCount;
            BaseStream.SamplesPerSecond = SamplesPerSecond;
            BaseStream.BitsPerSample = BitsPerSample;
            BaseStream.Format = Format;
            BaseStream.FormatSpecificData = FormatSpecificData;
            BaseStream.BytesPerSecond = BytesPerSecond;
            BaseStream.Granularity = Granularity;

            base.PrepareForWriting();
        }

        public override void FinishWriting()
        {
            // Prevent accessing encoded buffer by multiple threads simultaneously
            lock (_syncBuffer)
            {
                // Flush the encoder
                EnsureBufferIsSufficient(0);
                var encodedLength = _encoder.Flush(_encodedBuffer, 0);
                if (encodedLength > 0)
                {
                    base.WriteBlock(_encodedBuffer, 0, encodedLength);
                }
            }

            base.FinishWriting();
        }


        private void EnsureBufferIsSufficient(int sourceCount)
        {
            var maxLength = _encoder.GetMaxEncodedLength(sourceCount);
            if (_encodedBuffer != null && _encodedBuffer.Length >= maxLength)
            {
                return;
            }

            var newLength = _encodedBuffer?.Length * 2 ?? 1024;
            while (newLength < maxLength)
            {
                newLength *= 2;
            }

            _encodedBuffer = new byte[newLength];
        }

        private void ThrowPropertyDefinedByEncoder()
        {
            throw new NotSupportedException("The value of the property is defined by the encoder.");
        }
    }
}
