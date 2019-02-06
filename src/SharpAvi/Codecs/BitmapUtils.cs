using System;
using System.Diagnostics.Contracts;

namespace SharpAvi.Codecs
{
    internal static class BitmapUtils
    {
        public static unsafe void Bgr32ToBgr24(Memory<byte> source, Memory<byte> destination, int pixelCount)
        {
            Contract.Requires(pixelCount >= 0);

            fixed (byte* sourcePtr = source, destinationPtr = destination)
            {
                var sourceStart = sourcePtr + srcOffset;
                var destinationStart = destinationPtr + destOffset;
                var sourceEnd = sourceStart + 4 * pixelCount;
                var src = sourceStart;
                var dest = destinationStart;
                while (src < sourceEnd)
                {
                    *(dest++) = *(src++);
                    *(dest++) = *(src++);
                    *(dest++) = *(src++);
                    src++;
                }
            }
        }

        public static void FlipVertical(Memory<byte> source, Memory<byte> destination, int height, int stride)
        {
            Contract.Requires(source != null);
            Contract.Requires(destination != null);
            Contract.Requires(height >= 0);
            Contract.Requires(stride > 0);
            Contract.Requires(srcOffset + stride * height <= source.Length);
            Contract.Requires(destOffset + stride * height <= destination.Length);

            var src = srcOffset;
            var dest = destOffset + (height - 1) * stride;
            for (var y = 0; y < height; y++)
            {
                Buffer.BlockCopy(source, src, destination, dest, stride);
                src += stride;
                dest -= stride;
            }
        }
    }
}
