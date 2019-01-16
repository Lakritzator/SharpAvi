using SharpAvi.Output;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpAvi.Codecs.MotionJpeg
{
    public static class MotionJpegAviWriterExtensions
    {
        /// <summary>
        /// Adds new video stream with <see cref="MotionJpegVideoEncoderWpf"/>.
        /// </summary>
        /// <param name="writer">Writer object to which new stream is added.</param>
        /// <param name="width">Frame width.</param>
        /// <param name="height">Frame height.</param>
        /// <param name="quality">Requested quality of compression.</param>
        /// <seealso cref="AviWriter.AddEncodingVideoStream"/>
        /// <seealso cref="MotionJpegVideoEncoderWpf"/>
        public static IAviVideoStream AddMotionJpegVideoStream(this AviWriter writer, int width, int height, int quality = 70)
        {
            Contract.Requires(writer != null);
            Contract.Requires(width > 0);
            Contract.Requires(height > 0);
            Contract.Requires(1 <= quality && quality <= 100);
            Contract.Ensures(Contract.Result<IAviVideoStream>() != null);

            var encoder = new MotionJpegVideoEncoderWpf(width, height, quality);

            return writer.AddEncodingVideoStream(encoder, true, width, height);
        }
    }
}
