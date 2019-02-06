using System;
using System.Diagnostics.Contracts;
using SharpAvi.Enums;

namespace SharpAvi.Codecs
{
    /// <summary>
    /// Encodes frames in BGR24 format without compression.
    /// </summary>
    /// <remarks>
    /// The main purpose of this encoder is to flip bitmap vertically (from top-down to bottom-up)
    /// and to convert pixel format to 24 bits.
    /// </remarks>
    public class UncompressedVideoEncoder : IVideoEncoder
    {
        private readonly int _width;
        private readonly int _height;

        /// <summary>
        /// Creates a new instance of <see cref="UncompressedVideoEncoder"/>.
        /// </summary>
        /// <param name="width">Frame width.</param>
        /// <param name="height">Frame height.</param>
        public UncompressedVideoEncoder(int width, int height)
        {
            Contract.Requires(width > 0);
            Contract.Requires(height > 0);

            _width = width;
            _height = height;
        }

        #region IVideoEncoder Members

        /// <summary>Video codec.</summary>
        public FourCC Codec => KnownFourCCs.Codecs.Uncompressed;

        /// <summary>
        /// Number of bits per pixel in encoded image.
        /// </summary>
        public BitsPerPixel BitsPerPixel => BitsPerPixel.Bpp24;

        /// <summary>
        /// Maximum size of encoded frame.
        /// </summary>
        public int MaxEncodedSize => _width * _height * 3;

        /// <summary>
        /// Encodes a frame.
        /// </summary>
        /// <seealso cref="IVideoEncoder.EncodeFrame"/>
        public int EncodeFrame(Memory<byte> source, Memory<byte> destination, out bool isKeyFrame)
        {
            source.CopyTo(destination);
            isKeyFrame = true;
            return MaxEncodedSize;
        }

        #endregion
    }
}
