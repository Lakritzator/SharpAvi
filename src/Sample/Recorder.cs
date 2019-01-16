using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NAudio.Wave;
using SharpAvi.Codecs;
using SharpAvi.Output;
using System.Windows.Interop;
using System.Diagnostics;
using SharpAvi.Codecs.Lame;
using SharpAvi.Codecs.MotionJpeg;

namespace SharpAvi.Sample
{
    internal class Recorder : IDisposable
    {
        private readonly int _screenWidth;
        private readonly int _screenHeight;
        private readonly AviWriter _writer;
        private readonly IAviVideoStream _videoStream;
        private readonly IAviAudioStream _audioStream;
        private readonly WaveInEvent _audioSource;
        private readonly Thread _screenThread;
        private readonly ManualResetEvent _stopThread = new ManualResetEvent(false);
        private readonly AutoResetEvent _videoFrameWritten = new AutoResetEvent(false);
        private readonly AutoResetEvent _audioBlockWritten = new AutoResetEvent(false);

        public Recorder(string fileName, FourCC codec, int quality, int audioSourceIndex, SupportedWaveFormat audioWaveFormat, bool encodeAudio, int audioBitRate)
        {
            System.Windows.Media.Matrix toDevice;
            using (var source = new HwndSource(new HwndSourceParameters()))
            {
                toDevice = source.CompositionTarget.TransformToDevice;
            }

            _screenWidth = (int)Math.Round(SystemParameters.PrimaryScreenWidth * toDevice.M11);
            _screenHeight = (int)Math.Round(SystemParameters.PrimaryScreenHeight * toDevice.M22);

            // Create AVI writer and specify FPS
            _writer = new AviWriter(fileName)
            {
                FramesPerSecond = 10,
                EmitIndex1 = true,
            };

            // Create video stream
            _videoStream = CreateVideoStream(codec, quality);
            // Set only name. Other properties were when creating stream, 
            // either explicitly by arguments or implicitly by the encoder used
            _videoStream.Name = "Screencast";

            if (audioSourceIndex >= 0)
            {
                var waveFormat = ToWaveFormat(audioWaveFormat);

                _audioStream = CreateAudioStream(waveFormat, encodeAudio, audioBitRate);
                // Set only name. Other properties were when creating stream, 
                // either explicitly by arguments or implicitly by the encoder used
                _audioStream.Name = "Voice";

                _audioSource = new WaveInEvent
                {
                    DeviceNumber = audioSourceIndex,
                    WaveFormat = waveFormat,
                    // Buffer size to store duration of 1 frame
                    BufferMilliseconds = (int)Math.Ceiling(1000 / _writer.FramesPerSecond),
                    NumberOfBuffers = 3,
                };
                _audioSource.DataAvailable += AudioSource_DataAvailable;
            }

            _screenThread = new Thread(RecordScreen)
            {
                Name = typeof(Recorder).Name + ".RecordScreen",
                IsBackground = true
            };

            if (_audioSource != null)
            {
                _videoFrameWritten.Set();
                _audioBlockWritten.Reset();
                _audioSource.StartRecording();
            }
            _screenThread.Start();
        }

        private IAviVideoStream CreateVideoStream(FourCC codec, int quality)
        {
            // Select encoder type based on FOURCC of codec
            if (codec == KnownFourCCs.Codecs.Uncompressed)
            {
                return _writer.AddUncompressedVideoStream(_screenWidth, _screenHeight);
            }

            if (codec == KnownFourCCs.Codecs.MotionJpeg)
            {
                return _writer.AddMotionJpegVideoStream(_screenWidth, _screenHeight, quality);
            }

            return _writer.AddMpeg4VideoStream(_screenWidth, _screenHeight, (double)_writer.FramesPerSecond,
                // It seems that all tested MPEG-4 VfW codecs ignore the quality affecting parameters passed through VfW API
                // They only respect the settings from their own configuration dialogs, and Mpeg4VideoEncoder currently has no support for this
                quality: quality,
                codec: codec,
                // Most of VfW codecs expect single-threaded use, so we wrap this encoder to special wrapper
                // Thus all calls to the encoder (including its instantiation) will be invoked on a single thread although encoding (and writing) is performed asynchronously
                forceSingleThreadedAccess: true);
        }

        private IAviAudioStream CreateAudioStream(WaveFormat waveFormat, bool encode, int bitRate)
        {
            // Create encoding or simple stream based on settings
            if (encode)
            {
                // LAME DLL path is set in App.OnStartup()
                return _writer.AddMp3AudioStream(waveFormat.Channels, waveFormat.SampleRate, bitRate);
            }

            return _writer.AddAudioStream(
                waveFormat.Channels,
                waveFormat.SampleRate,
                waveFormat.BitsPerSample);
        }

        private static WaveFormat ToWaveFormat(SupportedWaveFormat waveFormat)
        {
            switch (waveFormat)
            {
                case SupportedWaveFormat.WAVE_FORMAT_44M16:
                    return new WaveFormat(44100, 16, 1);
                case SupportedWaveFormat.WAVE_FORMAT_44S16:
                    return new WaveFormat(44100, 16, 2);
                default:
                    throw new NotSupportedException("Wave formats other than '16-bit 44.1kHz' are not currently supported.");
            }
        }

        public void Dispose()
        {
            _stopThread.Set();
            _screenThread.Join();
            if (_audioSource != null)
            {
                _audioSource.StopRecording();
                _audioSource.DataAvailable -= AudioSource_DataAvailable;
            }

            // Close writer: the remaining data is written to a file and file is closed
            _writer.Close();

            _stopThread.Close();

            _audioBlockWritten.Dispose();
            _audioBlockWritten.Dispose();
        }

        private void RecordScreen()
        {
            var stopwatch = new Stopwatch();
            var buffer = new byte[_screenWidth * _screenHeight * 4];
            Task videoWriteTask = null;
            var isFirstFrame = true;
            var shotsTaken = 0;
            var timeTillNextFrame = TimeSpan.Zero;
            stopwatch.Start();

            while (!_stopThread.WaitOne(timeTillNextFrame))
            {
                GetScreenshot(buffer);
                shotsTaken++;

                // Wait for the previous frame is written
                if (!isFirstFrame)
                {
                    // TODO: await
                    videoWriteTask.Wait();
                    _videoFrameWritten.Set();
                }

                if (_audioStream != null)
                {
                    var signaled = WaitHandle.WaitAny(new WaitHandle[] {_audioBlockWritten, _stopThread});
                    if (signaled == 1)
                    {
                        break;
                    }
                }

                // Start asynchronous (encoding and) writing of the new frame
                videoWriteTask = _videoStream.WriteFrameAsync(true, buffer, 0, buffer.Length);

                timeTillNextFrame = TimeSpan.FromSeconds(shotsTaken / (double)_writer.FramesPerSecond - stopwatch.Elapsed.TotalSeconds);
                if (timeTillNextFrame < TimeSpan.Zero)
                {
                    timeTillNextFrame = TimeSpan.Zero;
                }

                isFirstFrame = false;
            }

            stopwatch.Stop();

            // Wait for the last frame is written
            if (!isFirstFrame)
            {
                // TODO: Await
                videoWriteTask.Wait();
            }
        }

        private void GetScreenshot(byte[] buffer)
        {
            using (var bitmap = new Bitmap(_screenWidth, _screenHeight))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(_screenWidth, _screenHeight));
                var bits = bitmap.LockBits(new Rectangle(0, 0, _screenWidth, _screenHeight), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
                Marshal.Copy(bits.Scan0, buffer, 0, buffer.Length);
                bitmap.UnlockBits(bits);

                // Should also capture the mouse cursor here, but skipping for simplicity
                // For those who are interested, look at http://www.codeproject.com/Articles/12850/Capturing-the-Desktop-Screen-with-the-Mouse-Cursor
            }
        }

        private void AudioSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            var signaled = WaitHandle.WaitAny(new WaitHandle[] { _videoFrameWritten, _stopThread });
            if (signaled != 0)
            {
                return;
            }

            _audioStream.WriteBlock(e.Buffer, 0, e.BytesRecorded);
            _audioBlockWritten.Set();
        }
    }
}
