// Deep link launcher for EWova.DeepLink.Win. Replaces the wscript/VBS agent, which antivirus heuristics tend to flag.
//
// Copied next to the app exe at build time and registered (HKCU by WindowsDeepLinkingCore, or HKLM by an installer) as:
//   "<dir>\DeepLinkLauncher.exe" "<scheme>" "<exe file name>" "%1"
//   app running     -> write HKCU\Software\Classes\<scheme>\uri and bring the app to the front (it reads the value on focus)
//   app not running -> start <dir>\<exe file name> with the URI as its argument (it reads it from the command line)
//
// Only starts an exe in its own folder, so a registry entry can't point it at anything else.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

using Microsoft.Win32;

[assembly: AssemblyTitle("Deep Link Launcher")]
[assembly: AssemblyDescription("Opens deep links in the application installed alongside it.")]
[assembly: AssemblyCompany("EWova")]
[assembly: AssemblyProduct("EWova.DeepLink.Win")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace EWova.DeepLink.Win
{
    internal static class DeepLinkLauncher
    {
        private const string RegistryValueName = "uri";
        private const int MaxUriLength = 2048;
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private static int Main(string[] args)
        {
            if (args.Length != 3)
                return 1;

            string scheme = args[0];
            string exeFileName = args[1];
            string uri = args[2];

            if (!IsValidScheme(scheme))
                return 1;
            if (exeFileName != Path.GetFileName(exeFileName) || !exeFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (uri.Length > MaxUriLength || !uri.StartsWith(scheme + ":", StringComparison.OrdinalIgnoreCase))
                return 1;

            // A quote would end the argument early when the URI is passed on the command line
            uri = uri.Replace("\"", "%22");

            string directory = AppDomain.CurrentDomain.BaseDirectory;
            string exePath = Path.Combine(directory, exeFileName);
            if (!File.Exists(exePath))
                return 2;

            Process app = FindRunningApp(Path.GetFileNameWithoutExtension(exeFileName));
            if (app != null)
            {
                // Warm start
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + scheme))
                {
                    key.SetValue(RegistryValueName, uri, RegistryValueKind.String);
                }
                BringToFront(app);
            }
            else
            {
                // Cold start
                var startInfo = new ProcessStartInfo(exePath, "\"" + uri + "\"")
                {
                    UseShellExecute = false,
                    WorkingDirectory = directory,
                };
                Process.Start(startInfo);
            }
            return 0;
        }

        // RFC 3986: ALPHA *( ALPHA / DIGIT / "+" / "-" / "." )
        private static bool IsValidScheme(string scheme)
        {
            if (string.IsNullOrEmpty(scheme) || !IsAsciiLetter(scheme[0]))
                return false;

            foreach (char c in scheme)
            {
                if (!(IsAsciiLetter(c) || (c >= '0' && c <= '9') || c == '+' || c == '-' || c == '.'))
                    return false;
            }
            return true;
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        // Prefer the instance that has a window (the player), like the VBS agent matched by process name
        private static Process FindRunningApp(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
                return null;

            foreach (Process process in processes)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                    return process;
            }
            return processes[0];
        }

        // Allowed because the launcher was started by the foreground process (the browser)
        private static void BringToFront(Process app)
        {
            IntPtr hWnd = app.MainWindowHandle;
            if (hWnd == IntPtr.Zero)
                return;

            if (IsIconic(hWnd))
                ShowWindow(hWnd, SW_RESTORE);
            SetForegroundWindow(hWnd);
        }
    }
}
