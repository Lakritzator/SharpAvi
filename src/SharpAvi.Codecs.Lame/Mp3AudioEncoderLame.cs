// Thanks to NAudio.Lame project (by Corey Murtagh) for inspiration
// https://github.com/Corey-M/NAudio.Lame

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SharpAvi.Codecs.Lame
{
    /// <summary>
    /// Mpeg Layer 3 (MP3) audio encoder using the LAME codec in external DLL.
    /// </summary>
    /// <remarks>
    /// The class is designed for using only a single instance at a time.
    /// Find information about and downloads of the LAME project at http://lame.sourceforge.net/
    /// </remarks>
    public class Mp3AudioEncoderLame : IAudioEncoder, IDisposable
    {
        /// <summary>
        /// Supported output bit rates (in kilobits per second).
        /// </summary>
        /// <remarks>
        /// Currently supported are 64, 96, 128, 160, 192 and 320 kbps.
        /// </remarks>
        public static readonly int[] SupportedBitRates = new[] { 64, 96, 128, 160, 192, 320 };


        #region Loading LAME DLL

        private static Type _lameFacadeType;

        /// <summary>
        /// Sets the location of LAME DLL for using by this class.
        /// </summary>
        /// <remarks>
        /// This method may be called before creating any instances of this class.
        /// The LAME DLL should have the appropriate bitness (32/64), depending on the current process.
        /// If it is not already loaded into the process, the method loads it automatically.
        /// </remarks>
        public static void SetLameDllLocation(string lameDllPath)
        {
            Contract.Requires(!string.IsNullOrEmpty(lameDllPath));

            var libraryName = Path.GetFileName(lameDllPath);
            if (!IsLibraryLoaded(libraryName))
            {
                var loadResult = LoadLibrary(lameDllPath);
                if (loadResult == IntPtr.Zero)
                {
                    throw new DllNotFoundException($"Library '{lameDllPath}' could not be loaded.");
                }
            }

            var facadeAsm = GenerateLameFacadeAssembly(libraryName);
            _lameFacadeType = facadeAsm.GetType(typeof(Mp3AudioEncoderLame).Namespace + ".Runtime.LameFacadeImpl");
        }

        private static Assembly GenerateLameFacadeAssembly(string lameDllName)
        {
            var thisAsm = typeof(Mp3AudioEncoderLame).Assembly;
            var compiler = new Microsoft.CSharp.CSharpCodeProvider();
            var compilerOptions = new System.CodeDom.Compiler.CompilerParameters()
            {
                 GenerateInMemory = true,
                 GenerateExecutable = false,
                 IncludeDebugInformation = false,
                 CompilerOptions = "/optimize",
                 ReferencedAssemblies = {"mscorlib.dll", thisAsm.Location}
            };
            var source = GetLameFacadeAssemblySource(lameDllName, thisAsm);
            var compilerResult = compiler.CompileAssemblyFromSource(compilerOptions, source);
            if (compilerResult.Errors.HasErrors)
            {
                throw new Exception("Could not generate LAME facade assembly.");
            }
            return compilerResult.CompiledAssembly;
        }

        private static string GetLameFacadeAssemblySource(string lameDllName, Assembly resourceAsm)
        {
            string source;
            using (var sourceStream = resourceAsm.GetManifestResourceStream("SharpAvi.Codecs.LameFacadeImpl.cs"))
            using (var sourceReader = new StreamReader(sourceStream))
            {
                source = sourceReader.ReadToEnd();
                sourceReader.Close();
            }

            var lameDllNameLiteral = $"\"{lameDllName}\"";
            source = source.Replace("\"lame_enc.dll\"", lameDllNameLiteral);

            return source;
        }

        private static bool IsLibraryLoaded(string libraryName)
        {
            var process = Process.GetCurrentProcess();
            return process.Modules.Cast<ProcessModule>().
                Any(m => string.Compare(m.ModuleName, libraryName, StringComparison.InvariantCultureIgnoreCase) == 0);
        }

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string fileName);

        #endregion


        private const int SAMPLE_BYTE_SIZE = 2;

        private readonly ILameFacade _lame;
        private readonly byte[] _formatData;

        /// <summary>
        /// Creates a new instance of <see cref="Mp3AudioEncoderLame"/>.
        /// </summary>
        /// <param name="channelCount">Channel count.</param>
        /// <param name="sampleRate">Sample rate (in samples per second).</param>
        /// <param name="outputBitRateKbps">Output bit rate (in kilobits per second).</param>
        /// <remarks>
        /// Encoder expects audio data in 16-bit samples.
        /// Stereo data should be interleaved: left sample first, right sample second.
        /// </remarks>
        public Mp3AudioEncoderLame(int channelCount, int sampleRate, int outputBitRateKbps)
        {
            Contract.Requires(channelCount == 1 || channelCount == 2);
            Contract.Requires(sampleRate > 0);
            Contract.Requires(SupportedBitRates.Contains(outputBitRateKbps));

            if (_lameFacadeType == null)
            {
                throw new InvalidOperationException("LAME DLL is not loaded. Call SetLameDllLocation first.");
            }

            _lame = (ILameFacade)Activator.CreateInstance(_lameFacadeType);
            _lame.ChannelCount = channelCount;
            _lame.InputSampleRate = sampleRate;
            _lame.OutputBitRate = outputBitRateKbps;

            _lame.PrepareEncoding();

            _formatData = FillFormatData();
        }

        /// <summary>
        /// Releases resources.
        /// </summary>
        public void Dispose()
        {
            if (_lame is IDisposable lameDisposable)
            {
                lameDisposable.Dispose();
            }
        }

        /// <summary>
        /// Encodes block of audio data.
        /// </summary>
        public int EncodeBlock(byte[] source, int sourceOffset, int sourceCount, byte[] destination, int destinationOffset)
        {
            return _lame.Encode(source, sourceOffset, sourceCount / SAMPLE_BYTE_SIZE, destination, destinationOffset);
        }

        /// <summary>
        /// Flushes internal encoder's buffers.
        /// </summary>
        public int Flush(byte[] destination, int destinationOffset)
        {
            return _lame.FinishEncoding(destination, destinationOffset);
        }

        /// <summary>
        /// Gets maximum length of encoded data.
        /// </summary>
        public int GetMaxEncodedLength(int sourceCount)
        {
            // Estimate taken from the description of 'lame_encode_buffer' method in 'lame.h'
            var numberOfSamples = sourceCount / SAMPLE_BYTE_SIZE;
            return (int)Math.Ceiling(1.25 * numberOfSamples + 7200);
        }


        /// <summary>
        /// Number of audio channels.
        /// </summary>
        public int ChannelCount => _lame.ChannelCount;

        /// <summary>
        /// Sample rate.
        /// </summary>
        public int SamplesPerSecond => _lame.OutputSampleRate;

        /// <summary>
        /// Bits per sample per single channel.
        /// </summary>
        public int BitsPerSample => SAMPLE_BYTE_SIZE * 8;

        /// <summary>
        /// Audio format.
        /// </summary>
        public short Format => AudioFormats.Mp3;

        /// <summary>
        /// Byte rate of the stream.
        /// </summary>
        public int BytesPerSecond => _lame.OutputBitRate * 1000 / 8;

        /// <summary>
        /// Minimum amount of data.
        /// </summary>
        public int Granularity => 1;

        /// <summary>
        /// Format-specific data.
        /// </summary>
        public byte[] FormatSpecificData => _formatData;


        private byte[] FillFormatData()
        {
            // See MPEGLAYER3WAVEFORMAT structure
            var mp3Data = new MemoryStream(4 * sizeof(ushort) + sizeof(uint));
            using (var writer = new BinaryWriter(mp3Data))
            {
                writer.Write((ushort)1); // MPEGLAYER3_ID_MPEG
                writer.Write(0x00000002U); // MPEGLAYER3_FLAG_PADDING_OFF
                writer.Write((ushort)_lame.FrameSize); // nBlockSize
                writer.Write((ushort)1); // nFramesPerBlock
                writer.Write((ushort)_lame.EncoderDelay);
            }
            return mp3Data.ToArray();
        }
    }
}
