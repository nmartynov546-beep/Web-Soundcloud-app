using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SoundCloudDesktop
{
    internal static class Program
    {
        private const string MutexName = "Local\\SoundCloudDesktop_SingleInstance_Mutex";
        internal const string PipeName = "SoundCloudDesktop_Pipe";

        [STAThread]
        private static void Main(string[] args)
        {
            NativeMethods.EnableDpiAwareness();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string initialUrl = ExtractSoundCloudUrl(args);

            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    NotifyExistingInstance(initialUrl);
                    return;
                }

                Application.Run(new MainForm(initialUrl));
                GC.KeepAlive(mutex);
            }
        }

        internal static string ExtractSoundCloudUrl(string[] args)
        {
            if (args == null || args.Length == 0) return null;
            for (int i = 0; i < args.Length; i++)
            {
                string candidate = args[i];
                if (IsValidSoundCloudUrl(candidate))
                {
                    return candidate.Trim();
                }
            }
            return null;
        }

        internal static bool IsValidSoundCloudUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            Uri uri;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri)) return false;
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
            string host = uri.Host;
            return string.Equals(host, "soundcloud.com", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".soundcloud.com", StringComparison.OrdinalIgnoreCase);
        }

        private static void NotifyExistingInstance(string initialUrl)
        {
            string payload = !string.IsNullOrEmpty(initialUrl) ? initialUrl : "FOCUS";
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    client.Connect(1500);
                    using (StreamWriter writer = new StreamWriter(client, Encoding.UTF8))
                    {
                        writer.WriteLine(payload);
                        writer.Flush();
                    }
                }
            }
            catch
            {
                // Existing instance may be starting up or shutting down
            }
        }
    }
}
