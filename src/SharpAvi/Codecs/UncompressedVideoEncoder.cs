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
        private readonly byte[] _sourceBuffer;

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
            _sourceBuffer = new byte[width * height * 4];
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
        public int EncodeFrame(byte[] source, int srcOffset, byte[] destination, int destOffset, out bool isKeyFrame)
        {
            BitmapUtils.FlipVertical(source, srcOffset, _sourceBuffer, 0, _height, _width * 4);
            BitmapUtils.Bgr32ToBgr24(_sourceBuffer, 0, destination, destOffset, _width * _height);
            isKeyFrame = true;
            return MaxEncodedSize;
        }

        #endregion
    }
}
