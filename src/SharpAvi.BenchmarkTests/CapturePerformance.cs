using System.IO;
using BenchmarkDotNet.Attributes;
using SharpAvi.Enums;
using SharpAvi.Output;

namespace SharpAvi.BenchmarkTests
{
    [MinColumn, MaxColumn, MemoryDiagnoser]
    public class CapturePerformance
    {
        private AviWriter _aviWriter;
        private IAviVideoStream _aviVideoStream;
        private const int Width = 10;
        private const int Height = 10;
        private readonly byte[] _imageData = new byte[Height * Width];

        [GlobalSetup]
        public void Setup()
        {
            var aviFile = Path.Combine(Path.GetTempPath(), @"test.avi");
            _aviWriter = new AviWriter(aviFile)
            {
                FramesPerSecond = 30,
                // Emitting AVI v1 index in addition to OpenDML index (AVI v2)
                // improves compatibility with some software, including 
                // standard Windows programs like Media Player and File Explorer
                EmitIndex1 = true
            };
            _aviVideoStream = _aviWriter.AddVideoStream(Width, Height, BitsPerPixel.Bpp24);
        }

        /// <summary>
        /// Capture the screen and write a frame
        /// </summary>
        [Benchmark]
        public void CaptureBuffered()
        {
            _aviVideoStream.WriteFrame(false, _imageData, 0, Height * Width);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _aviWriter.Close();
        }
    }
}
