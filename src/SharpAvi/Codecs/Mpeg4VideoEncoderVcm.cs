using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using SharpAvi.Enums;
using SharpAvi.Vfw;
using SharpAvi.Vfw.Enums;
using SharpAvi.Vfw.Structs;

namespace SharpAvi.Codecs
{
    /// <summary>
    /// Encodes video stream in MPEG-4 format using one of VCM codecs installed on the system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Supported codecs include Microsoft MPEG-4 V3 and V2, Xvid, DivX and x264vfw.
    /// The codec to be used is selected from the ones installed on the system.
    /// The encoder can be forced to use MPEG-4 codecs that are not explicitly supported. However, in this case it is not guaranteed to work properly.
    /// </para>
    /// <para>
    /// For <c>x264vfw</c> codec, it is recommended to enable <c>Zero Latency</c> option in its settings.
    /// 64-bit support is limited, as there are no 64-bit versions of Microsoft and DivX codecs, and Xvid can produce some errors.
    /// </para>
    /// <para>
    /// In multi-threaded scenarios, like asynchronous encoding, it is recommended to wrap this encoder into
    /// <see cref="SingleThreadedVideoEncoderWrapper"/> for the stable work.
    /// </para>
    /// </remarks>
    public class Mpeg4VideoEncoderVcm : IVideoEncoder, IDisposable
    {
        /// <summary>
        /// Default preferred order of the supported codecs.
        /// </summary>
        public static ReadOnlyCollection<FourCC> DefaultCodecPreference { get; } = new ReadOnlyCollection<FourCC>(
            new[]
            {
                KnownFourCCs.Codecs.MicrosoftMpeg4V3,
                KnownFourCCs.Codecs.MicrosoftMpeg4V2,
                KnownFourCCs.Codecs.Xvid,
                KnownFourCCs.Codecs.X264,
                KnownFourCCs.Codecs.DivX,
            });

        /// <summary>
        /// Gets info about the supported codecs that are installed on the system.
        /// </summary>
        public static CodecInfo[] GetAvailableCodecs()
        {
            var result = new List<CodecInfo>();

            var inBitmapInfo = CreateBitmapInfo(8, 8, 32, KnownFourCCs.Codecs.Uncompressed);
            inBitmapInfo.ImageSize = 4;

            foreach (var codec in DefaultCodecPreference)
            {
                var outBitmapInfo = CreateBitmapInfo(8, 8, 24, codec);
                var compressorHandle = GetCompressor(inBitmapInfo, outBitmapInfo, out var compressorInfo);
                if (compressorHandle == IntPtr.Zero)
                {
                    continue;
                }

                VfwApi.ICClose(compressorHandle);
                result.Add(new CodecInfo(codec, compressorInfo.Description));
            }

            return result.ToArray();
        }

        private static IntPtr GetCompressor(BitmapInfoHeader inBitmapInfo, BitmapInfoHeader outBitmapInfo, out CompressorInfo compressorInfo)
        {
            // Using ICLocate is time-consuming. Besides, it does not clean up something, so the process does not terminate on exit.
            // Instead open a specific codec and query it for needed features.

            var compressorHandle = VfwApi.ICOpen((uint)KnownFourCCs.CodecTypes.Video, outBitmapInfo.Compression, IcModes.ICMODE_COMPRESS);

            if (compressorHandle != IntPtr.Zero)
            {
                var inHeader = inBitmapInfo;
                var outHeader = outBitmapInfo;
                var result = (IcResults)VfwApi.ICSendMessage(compressorHandle, IcMessages.ICM_COMPRESS_QUERY, ref inHeader, ref outHeader);

                if (result.IsSuccess())
                {
                    var infoSize = VfwApi.ICGetInfo(compressorHandle, out compressorInfo, Marshal.SizeOf(typeof(CompressorInfo)));
                    if (infoSize > 0 && compressorInfo.SupportsFastTemporalCompression)
                        return compressorHandle;
                }

                VfwApi.ICClose(compressorHandle);
            }

            compressorInfo = new CompressorInfo();
            return IntPtr.Zero;
        }

        private static BitmapInfoHeader CreateBitmapInfo(int width, int height, ushort bitCount, FourCC codec)
        {
            return new BitmapInfoHeader
            {
                SizeOfStruct = (uint)Marshal.SizeOf(typeof(BitmapInfoHeader)),
                Width = width,
                Height = height,
                BitCount = bitCount,
                Planes = 1,
                Compression = (uint)codec,
            };
        }


        private readonly int _width;
        private readonly int _height;
        private readonly byte[] _sourceBuffer;
        private readonly BitmapInfoHeader _inBitmapInfo;
        private readonly BitmapInfoHeader _outBitmapInfo;
        private readonly IntPtr _compressorHandle;
        private readonly CompressorInfo _compressorInfo;
        private readonly int _maxEncodedSize;
        private readonly int _quality;
        private readonly int _keyFrameRate;


        private int _frameIndex;
        private int _framesFromLastKey;
        private bool _isDisposed;
        private bool _needEnd;

        /// <summary>
        /// Creates a new instance of <see cref="Mpeg4VideoEncoderVcm"/>.
        /// </summary>
        /// <param name="width">Frame width.</param>
        /// <param name="height">Frame height.</param>
        /// <param name="fps">Frame rate.</param>
        /// <param name="frameCount">
        /// Number of frames to be encoded.
        /// If not known, specify 0.
        /// </param>
        /// <param name="quality">
        /// Compression quality in the range [1..100].
        /// Less values mean less size and lower image quality.
        /// </param>
        /// <param name="codecPreference">
        /// List of codecs that can be used by this encoder, in preferred order.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// No compatible codec was found in the system.
        /// </exception>
        /// <remarks>
        /// <para>
        /// It is not guaranteed that the codec will respect the specified <paramref name="quality"/> value.
        /// This depends on its implementation.
        /// </para>
        /// <para>
        /// If no preferred codecs are specified, then <see cref="DefaultCodecPreference"/> is used.
        /// MPEG-4 codecs that are not explicitly supported can be specified. However, in this case
        /// the encoder is not guaranteed to work properly.
        /// </para>
        /// </remarks>
        public Mpeg4VideoEncoderVcm(int width, int height, double fps, int frameCount, int quality, params FourCC[] codecPreference)
        {
            Contract.Requires(width > 0);
            Contract.Requires(height > 0);
            Contract.Requires(fps > 0);
            Contract.Requires(frameCount >= 0);
            Contract.Requires(1 <= quality && quality <= 100);

            _width = width;
            _height = height;
            _sourceBuffer = new byte[width * height * 4];

            _inBitmapInfo = CreateBitmapInfo(width, height, 32, KnownFourCCs.Codecs.Uncompressed);
            _inBitmapInfo.ImageSize = (uint)_sourceBuffer.Length;

            if (codecPreference == null || codecPreference.Length == 0)
            {
                codecPreference = DefaultCodecPreference.ToArray();
            }
            foreach (var codec in codecPreference)
            {
                _outBitmapInfo = CreateBitmapInfo(width, height, 24, codec);
                _compressorHandle = GetCompressor(_inBitmapInfo, _outBitmapInfo, out _compressorInfo);
                if (_compressorHandle != IntPtr.Zero)
                {
                    break;
                }
            }

            if (_compressorHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("No compatible MPEG-4 encoder found.");
            }

            try
            {
                _maxEncodedSize = GetMaxEncodedSize();

                // quality for ICM ranges from 0 to 10000
                _quality = _compressorInfo.SupportsQuality ? quality * 100 : 0;

                // typical key frame rate ranges from FPS to 2*FPS
                _keyFrameRate = (int)Math.Round((2 - 0.01 * quality) * fps);

                if (_compressorInfo.RequestsCompressFrames)
                {
                    InitCompressFramesInfo(fps, frameCount);
                }

                StartCompression();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Performs any necessary cleanup before this instance is garbage-collected.
        /// </summary>
        ~Mpeg4VideoEncoderVcm()
        {
            Dispose();
        }

        private int GetMaxEncodedSize()
        {
            var inHeader = _inBitmapInfo;
            var outHeader = _outBitmapInfo;
            return VfwApi.ICSendMessage(_compressorHandle, IcMessages.ICM_COMPRESS_GET_SIZE, ref inHeader, ref outHeader);
        }

        private void InitCompressFramesInfo(double fps, int frameCount)
        {
            var info = new CompressFramesInfo
            {
                StartFrame = 0,
                FrameCount = frameCount,
                Quality = _quality,
                KeyRate = _keyFrameRate,
            };
            AviUtils.SplitFrameRate((decimal)fps, out info.FrameRateNumerator, out info.FrameRateDenominator);

            var result = VfwApi.ICSendMessage(_compressorHandle, IcMessages.ICM_COMPRESS_FRAMES_INFO, ref info, Marshal.SizeOf(typeof(CompressFramesInfo)));
            CheckICResult(result);
        }

        private void StartCompression()
        {
            var inHeader = _inBitmapInfo;
            var outHeader = _outBitmapInfo;
            var result = (IcResults)VfwApi.ICSendMessage(_compressorHandle, IcMessages.ICM_COMPRESS_BEGIN, ref inHeader, ref outHeader);
            CheckICResult(result);

            _needEnd = true;
            _framesFromLastKey = _keyFrameRate;
        }

        private void EndCompression()
        {
            var result = VfwApi.ICSendMessage(_compressorHandle, IcMessages.ICM_COMPRESS_END, IntPtr.Zero, IntPtr.Zero);
            CheckICResult(result);
        }


        #region IVideoEncoder Members

        /// <summary>Video codec.</summary>
        public FourCC Codec => _outBitmapInfo.Compression;

        /// <summary>Number of bits per pixel in the encoded image.</summary>
        public BitsPerPixel BitsPerPixel => BitsPerPixel.Bpp24;

        /// <summary>
        /// Maximum size of the encoded frame.
        /// </summary>
        public int MaxEncodedSize => _maxEncodedSize;

        /// <summary>Encodes a frame.</summary>
        /// <seealso cref="IVideoEncoder.EncodeFrame"/>
        public int EncodeFrame(Memory<byte> source, Memory<byte> destination, out bool isKeyFrame)
        {
            // TODO: Introduce Width and Height in IVideoRecorder and add Requires to EncodeFrame contract
            Contract.Assert(srcOffset + 4 * _width * _height <= source.Length);

            BitmapUtils.FlipVertical(source, srcOffset, _sourceBuffer, 0, _height, _width * 4);

            var sourceHandle = GCHandle.Alloc(_sourceBuffer, GCHandleType.Pinned);
            var encodedHandle = GCHandle.Alloc(destination, GCHandleType.Pinned);
            try
            {
                var outInfo = _outBitmapInfo;
                outInfo.ImageSize = (uint)destination.Length;
                var inInfo = _inBitmapInfo;
                var flags = _framesFromLastKey >= _keyFrameRate ? IcCompressFlags.ICCOMPRESS_KEYFRAME : IcCompressFlags.ICCOMPRESS_NONE;

                var result = VfwApi.ICCompress(_compressorHandle, flags,
                    ref outInfo, encodedHandle.AddrOfPinnedObject(), ref inInfo, sourceHandle.AddrOfPinnedObject(),
                    out _, out var outFlags, _frameIndex,
                    0, _quality, IntPtr.Zero, IntPtr.Zero);
                CheckICResult(result);
                _frameIndex++;


                isKeyFrame = (outFlags & AviIndexFlags.AVIIF_KEYFRAME) == AviIndexFlags.AVIIF_KEYFRAME;
                if (isKeyFrame)
                {
                    _framesFromLastKey = 1;
                }
                else
                {
                    _framesFromLastKey++;
                }

                return (int)outInfo.ImageSize;
            }
            finally
            {
                sourceHandle.Free();
                encodedHandle.Free();
            }
        }

        #endregion


        #region IDisposable Members

        /// <summary>
        /// Releases all unmanaged resources used by the encoder.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_needEnd)
            {
                EndCompression();
            }

            if (_compressorHandle != IntPtr.Zero)
            {
                VfwApi.ICClose(_compressorHandle);
            }

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        #endregion


        private void CheckICResult(IcResults result)
        {
            if (result.IsSuccess())
            {
                return;
            }

            var errorDesc = result.GetErrorDescription();
            var resultStr = errorDesc == null
                ? result.ToString()
                : $"{result} ({errorDesc})";
            throw new InvalidOperationException($"Encoder operation returned an error: {resultStr}.");
        }
    }
}
