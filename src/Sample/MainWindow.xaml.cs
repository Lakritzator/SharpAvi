using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;
using SharpAvi.Codecs.Lame;

namespace SharpAvi.Sample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _recordingTimer.Tick += recordingTimer_Tick;
            DataContext = this;

            InitDefaultSettings();

            WindowMoveBehavior.Attach(this);
        }


        #region Recording

        private readonly DispatcherTimer _recordingTimer;
        private readonly Stopwatch _recordingStopwatch = new Stopwatch();
        private Recorder _recorder;
        private string _lastFileName;

        private static readonly DependencyPropertyKey IsRecordingPropertyKey =
            DependencyProperty.RegisterReadOnly("IsRecording", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
        public static readonly DependencyProperty IsRecordingProperty = IsRecordingPropertyKey.DependencyProperty;

        public bool IsRecording
        {
            get { return (bool)GetValue(IsRecordingProperty); }
            private set { SetValue(IsRecordingPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey ElapsedPropertyKey =
            DependencyProperty.RegisterReadOnly("Elapsed", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty ElapsedProperty = ElapsedPropertyKey.DependencyProperty;

        public string Elapsed
        {
            get { return (string)GetValue(ElapsedProperty); }
            private set { SetValue(ElapsedPropertyKey, value); }
        }

        private static readonly DependencyPropertyKey HasLastScreencastPropertyKey =
            DependencyProperty.RegisterReadOnly("HasLastScreencast", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));
        public static readonly DependencyProperty HasLastScreencastProperty = HasLastScreencastPropertyKey.DependencyProperty;

        public bool HasLastScreencast
        {
            get { return (bool)GetValue(HasLastScreencastProperty); }
            private set { SetValue(HasLastScreencastPropertyKey, value); }
        }

        private void StartRecording()
        {
            if (IsRecording)
            {
                throw new InvalidOperationException("Already recording.");
            }

            if (_minimizeOnStart)
            {
                WindowState = WindowState.Minimized;
            }

            Elapsed = "00:00";
            HasLastScreencast = false;
            IsRecording = true;

            _recordingStopwatch.Reset();
            _recordingTimer.Start();

            _lastFileName = System.IO.Path.Combine(_outputFolder, DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".avi");
            var bitRate = Mp3AudioEncoderLame.SupportedBitRates.OrderBy(br => br).ElementAt(_audioQuality);
            _recorder = new Recorder(_lastFileName, 
                _encoder, _encodingQuality, 
                _audioSourceIndex, _audioWaveFormat, _encodeAudio, bitRate);

            _recordingStopwatch.Start();
        }

        private void StopRecording()
        {
            if (!IsRecording)
            {
                throw new InvalidOperationException("Not recording.");
            }

            try
            {
                if (_recorder == null)
                {
                    return;
                }

                _recorder.Dispose();
                _recorder = null;
            }
            finally
            {
                _recordingTimer.Stop();
                _recordingStopwatch.Stop();

                IsRecording = false;
                HasLastScreencast = true;

                WindowState = WindowState.Normal;
            }
        }

        private void recordingTimer_Tick(object sender, EventArgs e)
        {
            var elapsed = _recordingStopwatch.Elapsed;
            Elapsed = $"{Math.Floor(elapsed.TotalMinutes):00}:{elapsed.Seconds:00}";
        }

        #endregion

        #region Settings

        private string _outputFolder;
        private FourCC _encoder;
        private int _encodingQuality;
        private int _audioSourceIndex;
        private SupportedWaveFormat _audioWaveFormat;
        private bool _encodeAudio;
        private int _audioQuality;
        private bool _minimizeOnStart;

        private void InitDefaultSettings()
        {
            var exePath = new Uri(System.Reflection.Assembly.GetEntryAssembly().Location).LocalPath;
            _outputFolder = System.IO.Path.GetDirectoryName(exePath);

            _encoder = KnownFourCCs.Codecs.MotionJpeg;
            _encodingQuality = 70;

            _audioSourceIndex = -1;
            _audioWaveFormat = SupportedWaveFormat.WAVE_FORMAT_44M16;
            _encodeAudio = true;
            _audioQuality = (Mp3AudioEncoderLame.SupportedBitRates.Length + 1) / 2;

            _minimizeOnStart = true;
        }

        private void ShowSettingsDialog()
        {
            var dlg = new SettingsWindow()
            {
                Owner = this,
                Folder = _outputFolder,
                Encoder = _encoder,
                Quality = _encodingQuality,
                SelectedAudioSourceIndex = _audioSourceIndex,
                AudioWaveFormat = _audioWaveFormat,
                EncodeAudio = _encodeAudio,
                AudioQuality = _audioQuality,
                MinimizeOnStart = _minimizeOnStart
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            _outputFolder = dlg.Folder;
            _encoder = dlg.Encoder;
            _encodingQuality = dlg.Quality;
            _audioSourceIndex = dlg.SelectedAudioSourceIndex;
            _audioWaveFormat = dlg.AudioWaveFormat;
            _encodeAudio = dlg.EncodeAudio;
            _audioQuality = dlg.AudioQuality;
            _minimizeOnStart = dlg.MinimizeOnStart;
        }

        #endregion


        private void StartRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartRecording();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error starting recording\r\n" + ex);
                StopRecording();
            }
        }

        private void StopRecording_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StopRecording();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error stopping recording\r\n" + ex);
            }
        }

        private void GoToLastScreencast_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", $"/select, \"{_lastFileName}\"");
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsDialog();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            if (IsRecording)
            {
                StopRecording();
            }

            Close();
        }
    }
}
