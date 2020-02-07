using System;
using System.Diagnostics.Contracts;

namespace SharpAvi.Codecs
{
    internal static class BitmapUtils
    {

        public static void FlipVertical(Memory<byte> source, Memory<byte> destination, int height, int stride)
        {
            Contract.Requires(!source.IsEmpty);
            Contract.Requires(!destination.IsEmpty);
            Contract.Requires(height >= 0);
            Contract.Requires(stride > 0);
            Contract.Requires(stride * height <= source.Length);
            Contract.Requires(stride * height <= destination.Length);

            var srcSpan = source.Span;
            var dstSpan = destination.Span;

            var src = 0;
            var dest = (height - 1) * stride;
            for (var y = 0; y < height; y++)
            {
                srcSpan.Slice(src, stride).CopyTo(dstSpan.Slice(dest, stride));
                src += stride;
                dest -= stride;
            }
        }
    }
}
