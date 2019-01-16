using System;
using System.IO;
using System.Reflection;
using System.Windows;
using SharpAvi.Codecs.Lame;

namespace SharpAvi.Sample
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set LAME DLL path for MP3 encoder
            var asmDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
#if NET471
            var is64BitProcess = Environment.Is64BitProcess;
#else
            var is64BitProcess = IntPtr.Size * 8 == 64;
#endif
            var dllName = $"lameenc{(is64BitProcess ? "64" : "32")}.dll";
            Mp3AudioEncoderLame.SetLameDllLocation(Path.Combine(asmDir, dllName));
        }
    }
}
