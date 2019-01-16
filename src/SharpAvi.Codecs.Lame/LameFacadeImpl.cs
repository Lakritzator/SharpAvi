using System;
using System.Runtime.InteropServices;

namespace SharpAvi.Codecs.Lame
{
    /// <summary>
    /// This is the implementation of the ILameFacade, which assumes that lame_enc.dll is already loaded!
    /// </summary>
    public class LameFacadeImpl : ILameFacade, IDisposable
    {
        // This is the name of the lame encoded dll, which needs to be on the path of loaded before via LoadLibrary with the same name. 
        private const string DLL_NAME = "lame_enc.dll";
        private readonly IntPtr _context;
        private bool _closed;

        public LameFacadeImpl()
        {
            _context = lame_init();
            CheckResult(_context != IntPtr.Zero, "lame_init");
        }

        ~LameFacadeImpl()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_closed)
            {
                return;
            }

            lame_close(_context);
            _closed = true;
        }


        public int ChannelCount
        {
            get { return lame_get_num_channels(_context); }
            set { lame_set_num_channels(_context, value); }
        }

        public int InputSampleRate
        {
            get { return lame_get_in_samplerate(_context); }
            set { lame_set_in_samplerate(_context, value); }
        }

        public int OutputBitRate
        {
            get { return lame_get_brate(_context); }
            set { lame_set_brate(_context, value); }
        }

        public int OutputSampleRate => lame_get_out_samplerate(_context);

        public int FrameSize => lame_get_framesize(_context);

        public int EncoderDelay => lame_get_encoder_delay(_context);

        public void PrepareEncoding()
        {
            // Set mode
            switch (ChannelCount)
            {
                case 1:
                    lame_set_mode(_context, MpegMode.Mono);
                    break;
                case 2:
                    lame_set_mode(_context, MpegMode.Stereo);
                    break;
                default:
                    ThrowInvalidChannelCount();
                    break;
            }

            // Disable VBR
            lame_set_VBR(_context, VbrMode.Off);

            // Prevent output of redundant headers
            lame_set_write_id3tag_automatic(_context, false);
            lame_set_bWriteVbrTag(_context, 0);

            // Ensure not decoding
            lame_set_decode_only(_context, 0);

            // Finally, initialize encoding process
            int result = lame_init_params(_context);
            CheckResult(result == 0, "lame_init_params");
        }

        public int Encode(byte[] source, int sourceIndex, int sampleCount, byte[] dest, int destIndex)
        {
            GCHandle sourceHandle = GCHandle.Alloc(source, GCHandleType.Pinned);
            GCHandle destHandle = GCHandle.Alloc(dest, GCHandleType.Pinned);
            try
            {
                IntPtr sourcePtr = new IntPtr(sourceHandle.AddrOfPinnedObject().ToInt64() + sourceIndex);
                IntPtr destPtr = new IntPtr(destHandle.AddrOfPinnedObject().ToInt64() + destIndex);
                int outputSize = dest.Length - destIndex;
                int result = -1;
                switch (ChannelCount)
                {
                    case 1:
                        result = lame_encode_buffer(_context, sourcePtr, sourcePtr, sampleCount, destPtr, outputSize);
                        break;
                    case 2:
                        result = lame_encode_buffer_interleaved(_context, sourcePtr, sampleCount / 2, destPtr, outputSize);
                        break;
                    default:
                        ThrowInvalidChannelCount();
                        break;
                }

                CheckResult(result >= 0, "lame_encode_buffer");
                return result;
            }
            finally
            {
                sourceHandle.Free();
                destHandle.Free();
            }
        }

        public int FinishEncoding(byte[] dest, int destIndex)
        {
            GCHandle destHandle = GCHandle.Alloc(dest, GCHandleType.Pinned);
            try
            {
                IntPtr destPtr = new IntPtr(destHandle.AddrOfPinnedObject().ToInt64() + destIndex);
                int destLength = dest.Length - destIndex;
                int result = lame_encode_flush(_context, destPtr, destLength);
                CheckResult(result >= 0, "lame_encode_flush");
                return result;
            }
            finally
            {
                destHandle.Free();
            }
        }


        private static void CheckResult(bool passCondition, string routineName)
        {
            if (!passCondition)
            {
                throw new ExternalException($"{routineName} failed");
            }
        }

        private static void ThrowInvalidChannelCount()
        {
            throw new InvalidOperationException("Set ChannelCount to 1 or 2");
        }


        #region LAME DLL API

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr lame_init();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_close(IntPtr context);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_set_in_samplerate(IntPtr context, int value);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_get_in_samplerate(IntPtr context);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_set_num_channels(IntPtr context, int value);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_get_num_channels(IntPtr context);

        private enum MpegMode
        {
            Stereo = 0,
            JointStereo = 1,
            DualChannel = 2,
            Mono = 3,
            NotSet = 4,
        }

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_set_mode(IntPtr context, MpegMode value);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern MpegMode lame_get_mode(IntPtr context);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_set_brate(IntPtr context, int value);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_get_brate(IntPtr context);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_set_out_samplerate(IntPtr context, int value);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_get_out_samplerate(IntPtr context);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void lame_set_write_id3tag_automatic(IntPtr context, bool value);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool lame_get_write_id3tag_automatic(IntPtr context);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_set_bWriteVbrTag(IntPtr context, int value);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_get_bWriteVbrTag(IntPtr context);

        private enum VbrMode
        {
            Off = 0,
            MarkTaylor = 1,
            RogerHegemann = 2,
            AverageBitRate = 3,
            MarkTaylorRogerHegemann = 4,
        }

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_set_VBR(IntPtr context, VbrMode value);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern VbrMode lame_get_VBR(IntPtr context);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_set_decode_only(IntPtr context, int value);
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_get_decode_only(IntPtr context);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_get_encoder_delay(IntPtr context);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_get_framesize(IntPtr context);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_init_params(IntPtr context);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_encode_buffer(IntPtr context, 
            IntPtr bufferL, IntPtr bufferR, int nSamples,
            IntPtr mp3Buf, int mp3BufSize);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_encode_buffer_interleaved(IntPtr context,
            IntPtr buffer, int nSamples,
            IntPtr mp3Buf, int mp3BufSize);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int lame_encode_flush(IntPtr context, IntPtr mp3Buf, int mp3BufSize);

        #endregion
    }
}
