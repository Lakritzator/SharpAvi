using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SharpAvi.Vfw.Structs
{
    /// <summary>
    /// Corresponds to the <c>ICINFO</c> structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
    public unsafe struct CompressorInfo
    {
        private const int VIDCF_QUALITY = 0x0001;
        private const int VIDCF_COMPRESSFRAMES = 0x0008;
        private const int VIDCF_FASTTEMPORALC = 0x0020;

        private uint sizeOfStruct;
        private uint fccType;
        private uint fccHandler;
        private uint flags;
        private uint version;
        private uint versionIcm;
        private fixed char szName[16];
        private fixed char szDescription[128];
        private fixed char szDriver[128];

        public bool SupportsQuality => (flags & VIDCF_QUALITY) == VIDCF_QUALITY;

        public bool SupportsFastTemporalCompression => (flags & VIDCF_FASTTEMPORALC) == VIDCF_FASTTEMPORALC;

        public bool RequestsCompressFrames => (flags & VIDCF_COMPRESSFRAMES) == VIDCF_COMPRESSFRAMES;

        public string Name
        {
            get
            {
                fixed (char* name = szName)
                {
                    return new string(name);
                }
            }
        }

        public string Description
        {
            get
            {
                fixed (char* desc = szDescription)
                {
                    return new string(desc);
                }
            }
        }
    }
}
