using System.Runtime.InteropServices;

namespace SharpAvi.Vfw.Structs
{
    /// <summary>
    /// Corresponds to the <c>BITMAPINFOHEADER</c> structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BitmapInfoHeader
    {
        public uint SizeOfStruct;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint ImageSize;
        public int PixelsPerMeterX;
        public int PixelsPerMeterY;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }
}
