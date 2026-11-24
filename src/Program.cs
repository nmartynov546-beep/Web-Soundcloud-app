using System;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.CompilerServices;
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
            SetupEmbeddedDependencies();
            Run(args);
        }

        private static void SetupEmbeddedDependencies()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;

            try
            {
                string localApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundCloudDesktop");
                string nativeDir = Path.Combine(localApp, "runtimes", "win-x64", "native");
                Directory.CreateDirectory(nativeDir);
                string nativeLoader = Path.Combine(nativeDir, "WebView2Loader.dll");
                string rootLoader = Path.Combine(localApp, "WebView2Loader.dll");

                ExtractResource("WebView2Loader.dll", nativeLoader);
                ExtractResource("WebView2Loader.dll", rootLoader);

                NativeMethods.SetDllDirectory(nativeDir);
                NativeMethods.SetDllDirectory(localApp);
                NativeMethods.LoadLibrary(nativeLoader);
                NativeMethods.LoadLibrary(rootLoader);
            }
            catch
            {
            }
        }

        private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            string shortName = new AssemblyName(args.Name).Name;
            if (string.Equals(shortName, "Microsoft.Web.WebView2.WinForms", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shortName, "Microsoft.Web.WebView2.Core", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = LoadResourceBytes(shortName + ".dll");
                if (bytes != null)
                {
                    return Assembly.Load(bytes);
                }
            }
            return null;
        }

        private static byte[] LoadResourceBytes(string name)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(name))
            {
                if (s == null) return null;
                byte[] b = new byte[s.Length];
                s.Read(b, 0, b.Length);
                return b;
            }
        }

        private static void ExtractResource(string resName, string targetPath)
        {
            byte[] bytes = LoadResourceBytes(resName);
            if (bytes == null) return;
            if (File.Exists(targetPath) && new FileInfo(targetPath).Length == bytes.Length) return;
            try
            {
                File.WriteAllBytes(targetPath, bytes);
            }
            catch
            {
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Run(string[] args)
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
