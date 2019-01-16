﻿using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SharpAvi.Codecs.MotionJpeg
{
    /// <summary>
    /// Encodes frames in Motion JPEG format.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The implementation relies on <see cref="JpegBitmapEncoder"/>.
    /// </para>
    /// <para>
    /// Note for .NET 3.5:
    /// This encoder is designed for single-threaded use. If you use it in multi-threaded scenarios 
    /// (like asynchronous calls), then consider wrapping it in <see cref="SingleThreadedVideoEncoderWrapper"/>.
    /// </para>
    /// <para>
    /// This encoder is not fully conformant to the Motion JPEG standard, as each encoded frame is a full JPEG picture 
    /// with its own Huffman tables, and not those fixed Huffman tables defined by the Motion JPEG standard. 
    /// However, (at least most) modern decoders for Motion JPEG properly handle this situation.
    /// This also produces a little overhead on the file size.
    /// </para>
    /// </remarks>
    public class MotionJpegVideoEncoderWpf : IVideoEncoder
    {
        private readonly Int32Rect _rect;
        private readonly int _quality;
        private readonly ThreadLocal<WriteableBitmap> _bitmapHolder;

        /// <summary>
        /// Creates a new instance of <see cref="MotionJpegVideoEncoderWpf"/>.
        /// </summary>
        /// <param name="width">Frame width.</param>
        /// <param name="height">Frame height.</param>
        /// <param name="quality">
        /// Compression quality in the range [1..100].
        /// Less values mean less size and lower image quality.
        /// </param>
        public MotionJpegVideoEncoderWpf(int width, int height, int quality)
        {
            Contract.Requires(width > 0);
            Contract.Requires(height > 0);
            Contract.Requires(1 <= quality && quality <= 100);

            _rect = new Int32Rect(0, 0, width, height);
            _quality = quality;

            _bitmapHolder = new ThreadLocal<WriteableBitmap>(
                () => new WriteableBitmap(_rect.Width, _rect.Height, 96, 96, PixelFormats.Bgr32, null),
                false);
        }


        #region IVideoEncoder Members

        /// <summary>Video codec.</summary>
        public FourCC Codec => KnownFourCCs.Codecs.MotionJpeg;

        /// <summary>
        /// Number of bits per pixel in encoded image.
        /// </summary>
        public BitsPerPixel BitsPerPixel => BitsPerPixel.Bpp24;

        /// <summary>
        /// Maximum size of encoded frame.
        /// Assume that JPEG is always less than raw bitmap when dimensions are not tiny
        /// </summary>
        public int MaxEncodedSize => Math.Max(_rect.Width * _rect.Height * 4, 1024);

        /// <summary>
        /// Encodes a frame.
        /// </summary>
        /// <seealso cref="IVideoEncoder.EncodeFrame"/>
        public int EncodeFrame(byte[] source, int srcOffset, byte[] destination, int destOffset, out bool isKeyFrame)
        {
            var bitmap = _bitmapHolder.Value;
            bitmap.WritePixels(_rect, source, _rect.Width * 4, srcOffset);

            var encoderImpl = new JpegBitmapEncoder
            {
                QualityLevel = _quality
            };
            encoderImpl.Frames.Add(BitmapFrame.Create(bitmap));

            using (var stream = new MemoryStream(destination))
            {
                stream.Position = srcOffset;
                encoderImpl.Save(stream);
                stream.Flush();
                var length = stream.Position - srcOffset;
                stream.Close();

                isKeyFrame = true;

                return (int)length;
            }
        }

        #endregion
    }
}