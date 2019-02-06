using System;
using System.Diagnostics.Contracts;
using SharpAvi.Enums;

namespace SharpAvi.Codecs
{
    /// <summary>
    /// Encoder for video AVI stream.
    /// </summary>
    [ContractClass(typeof(Contracts.VideoEncoderContract))]
    public interface IVideoEncoder
    {
        /// <summary>Codec ID.</summary>
        FourCC Codec { get; }

        /// <summary>
        /// Number of bits per pixel in encoded image.
        /// </summary>
        BitsPerPixel BitsPerPixel { get; }

        /// <summary>
        /// Determines the amount of space needed in the destination buffer for storing the encoded data of a single frame.
        /// </summary>
        int MaxEncodedSize { get; }

        /// <summary>
        /// Encodes video frame.
        /// </summary>
        /// <param name="source">Frame bitmap data. The expected bitmap format is BGR32 top-to-bottom. Alpha component is not used.</param>
        /// <param name="destination">Buffer for storing the encoded frame data.</param>
        /// <param name="isKeyFrame">When the method returns, contains the value indicating whether this frame was encoded as a key frame.</param>
        /// <returns>The actual number of bytes written to the <paramref name="destination"/> buffer.</returns>
        int EncodeFrame(Memory<byte> source, Memory<byte> destination, out bool isKeyFrame);
    }


    namespace Contracts
    {
        [ContractClassFor(typeof(IVideoEncoder))]
        internal abstract class VideoEncoderContract : IVideoEncoder
        {
            public FourCC Codec => throw new NotImplementedException();

            public BitsPerPixel BitsPerPixel => throw new NotImplementedException();

            public int MaxEncodedSize
            {
                get 
                {
                    Contract.Ensures(Contract.Result<int>() > 0);
                    throw new NotImplementedException(); 
                }
            }

            public int EncodeFrame(Memory<byte> source, Memory<byte> destination, out bool isKeyFrame)
            {
                Contract.Requires(source.Length > 0);
                Contract.Requires(destination.Length > 0);
                Contract.Ensures(Contract.Result<int>() >= 0);
                throw new NotImplementedException();
            }
        }
    }
}
