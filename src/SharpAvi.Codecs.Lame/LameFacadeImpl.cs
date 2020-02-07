using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpAvi.Codecs.Lame.Enums;

namespace SharpAvi.Codecs.Lame
{
    /// <summary>
    /// This is the implementation of the ILameFacade, which assumes that lame_enc.dll is already loaded!
    /// </summary>
    public class LameFacadeImpl : ILameFacade, IDisposable
    {
        // This is the name of the lame encoded dll, which needs to be on the path of loaded before via LoadLibrary with the same name. 
        private const string LameEncDllName = "lame_enc.dll";
        private readonly IntPtr _context;
        private bool _closed;

        public LameFacadeImpl()
        {
            _context = LameInit();
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

            LameClose(_context);
            _closed = true;
        }


        public int ChannelCount
        {
            get { return LameGetNumChannels(_context); }
            set { LameSetNumChannels(_context, value); }
        }

        public int InputSampleRate
        {
            get { return LameGetInSampleRate(_context); }
            set { LameSetInSampleRate(_context, value); }
        }

        public int OutputBitRate
        {
            get { return LameGetBitRate(_context); }
            set { LameSetBitRate(_context, value); }
        }

        public int OutputSampleRate => LameGetOutSampleRate(_context);

        public int FrameSize => LameGetFramesize(_context);

        public int EncoderDelay => LameGetEncoderDelay(_context);

        public void PrepareEncoding()
        {
            // Set mode
            switch (ChannelCount)
            {
                case 1:
                    LameSetMode(_context, MpegMode.Mono);
                    break;
                case 2:
                    LameSetMode(_context, MpegMode.Stereo);
                    break;
                default:
                    ThrowInvalidChannelCount();
                    break;
            }

            // Disable VBR
            LameSetVBR(_context, VbrMode.Off);

            // Prevent output of redundant headers
            LameSetWriteId3TagAutomatic(_context, false);
            LameSetBWriteVbrTag(_context, 0);

            // Ensure not decoding
            LameSetDecodeOnly(_context, 0);

            // Finally, initialize encoding process
            int result = LameInitParams(_context);
            CheckResult(result == 0, "lame_init_params");
        }

        public int Encode(Memory<byte> source, Memory<byte> dest)
        {
            IntPtr sourcePtr = (IntPtr)source.Span.GetPinnableReference();
            IntPtr destPtr = (IntPtr)dest.Span.GetPinnableReference();
            int result = -1;
            switch (ChannelCount)
            {
                case 1:
                    result = LameEncodeBuffer(_context, sourcePtr, sourcePtr, source.Length, destPtr, dest.Length);
                    break;
                case 2:
                    result = LameEncodeBufferInterleaved(_context, sourcePtr, source.Length / 2, destPtr, dest.Length);
                    break;
                default:
                    ThrowInvalidChannelCount();
                    break;
            }

            CheckResult(result >= 0, "lame_encode_buffer");
            return result;
        }

        public int FinishEncoding(Memory<byte> dest)
        {
            IntPtr destPtr = (IntPtr)dest.Span.GetPinnableReference();
            int destLength = dest.Length;
            int result = LameEncodeFlush(_context, destPtr, destLength);
            CheckResult(result >= 0, "lame_encode_flush");
            return result;
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

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_init")]
        private static extern IntPtr LameInit();

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_close")]
        private static extern int LameClose(IntPtr context);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_set_in_samplerate")]
        private static extern int LameSetInSampleRate(IntPtr context, int value);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_get_in_samplerate")]
        private static extern int LameGetInSampleRate(IntPtr context);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_set_num_channels")]
        private static extern int LameSetNumChannels(IntPtr context, int value);
        
        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_get_num_channels")]
        private static extern int LameGetNumChannels(IntPtr context);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_set_mode")]
        private static extern int LameSetMode(IntPtr context, MpegMode value);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_get_mode")]
        private static extern MpegMode LameGetMode(IntPtr context);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_set_brate")]
        private static extern int LameSetBitRate(IntPtr context, int value);
        
        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_get_brate")]
        private static extern int LameGetBitRate(IntPtr context);
        
        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_set_out_samplerate")]
        private static extern int LameSetOutSampleRate(IntPtr context, int value);
        
        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_get_out_samplerate")]
        private static extern int LameGetOutSampleRate(IntPtr context);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_set_write_id3tag_automatic")]
        private static extern void LameSetWriteId3TagAutomatic(IntPtr context, bool value);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_get_write_id3tag_automatic")]
        private static extern bool LameGetWriteId3TagAutomatic(IntPtr context);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_set_bWriteVbrTag")]
        private static extern int LameSetBWriteVbrTag(IntPtr context, int value);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_get_bWriteVbrTag")]
        private static extern int LameGetBWriteVbrTag(IntPtr context);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_set_VBR")]
        private static extern int LameSetVBR(IntPtr context, VbrMode value);
        
        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_get_VBR")]
        private static extern VbrMode LameGetVBR(IntPtr context);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_set_decode_only")]
        private static extern int LameSetDecodeOnly(IntPtr context, int value);
        
        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_get_decode_only")]
        private static extern int LameGetDecodeOnly(IntPtr context);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_get_encoder_delay")]
        private static extern int LameGetEncoderDelay(IntPtr context);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_get_framesize")]
        private static extern int LameGetFramesize(IntPtr context);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_init_params")]
        private static extern int LameInitParams(IntPtr context);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_encode_buffer")]
        private static extern int LameEncodeBuffer(IntPtr context, 
            IntPtr bufferL, IntPtr bufferR, int nSamples,
            IntPtr mp3Buf, int mp3BufSize);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_encode_buffer_interleaved")]
        private static extern int LameEncodeBufferInterleaved(IntPtr context,
            IntPtr buffer, int nSamples,
            IntPtr mp3Buf, int mp3BufSize);

        [DllImport(LameEncDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "lame_encode_flush")]
        private static extern int LameEncodeFlush(IntPtr context, IntPtr mp3Buf, int mp3BufSize);

        #endregion
    }
}
