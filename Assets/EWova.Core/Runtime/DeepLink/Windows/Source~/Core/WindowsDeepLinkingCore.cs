using System;
using System.IO;
using System.Text;

using Microsoft.Win32;

namespace EWova.DeepLink.Win
{
    public enum WinDeepLinkingLoadMode
    {
        /// <summary>
        /// The application will be launched with the deep link URI as a command line argument.
        /// </summary>
        CommandLineArgument,
        /// <summary>
        /// The application will be launched without any command line arguments, and the deep link URI will be stored in the registry.
        /// </summary>
        RegistryValue
    }
    public readonly struct WinActivatedDeepLink
    {
        public readonly string Uri;
        public readonly WinDeepLinkingLoadMode LoadMode;

        public WinActivatedDeepLink(string uri, WinDeepLinkingLoadMode loadMode)
        {
            Uri = uri;
            LoadMode = loadMode;
        }
    }
    public enum WinDeepLinkRegistration
    {
        /// <summary>
        /// The scheme was not registered by this process (launcher missing and agent fallback not allowed).
        /// </summary>
        None,
        /// <summary>
        /// The scheme is handled by the launcher next to the target exe.
        /// </summary>
        Launcher,
        /// <summary>
        /// The scheme is handled by a VBS agent written to the persistent data folder (wscript).
        /// </summary>
        Agent
    }
    public static class WindowsDeepLinkingCore
    {
        public static WinActivatedDeepLink? CurrentWinActivatedDeepLink { get; set; }

        public static event Action<WinActivatedDeepLink> OnDeepLinkActivated;

        public static bool IsInitialized { get; private set; }
        private static string s_uriScheme;
        private static string s_productName;
        private static string s_targetExePath;
        private static string s_persistentDataPath;
        private const string RegistryValueName = "uri";

        /// <summary>
        /// Launcher placed next to the target exe (copied there at build time). When present it handles the
        /// scheme instead of the VBS agent, which antivirus heuristics tend to flag.
        /// </summary>
        public const string LauncherFileName = "DeepLinkLauncher.exe";

        public static Func<string> OverrideTargetExecutablePath;
        public static Func<string> OverrideWmiQuery;

        /// <summary>
        /// How the scheme was registered by <see cref="Initialize(string, string, string, string, bool)"/>.
        /// </summary>
        public static WinDeepLinkRegistration Registration { get; private set; }

        public static void Initialize(
            string uriScheme,
            string productName,
            string targetExePath,
            string persistentDataPath)
        {
            Initialize(uriScheme, productName, targetExePath, persistentDataPath, allowAgentFallback: true);
        }

        /// <param name="allowAgentFallback">
        /// Register the VBS agent when the launcher is missing. Pass true only where no launcher can exist (Unity Editor):
        /// an app that writes a script and registers it as a protocol handler looks like malware to antivirus.
        /// </param>
        public static void Initialize(
            string uriScheme,
            string productName,
            string targetExePath,
            string persistentDataPath,
            bool allowAgentFallback)
        {
            if (IsInitialized)
                throw new InvalidOperationException("WindowsDeepLinking is already initialized.");

            IsInitialized = true;

            s_uriScheme = uriScheme;
            s_productName = productName;
            s_targetExePath = targetExePath;
            s_persistentDataPath = persistentDataPath;

            string launcherPath = FindLauncher();
            if (launcherPath != null)
            {
                string exeFileName = Path.GetFileName(GetTargetExecutablePath());
                RegisterCustomUriScheme(s_uriScheme, $"\"{launcherPath}\" \"{s_uriScheme}\" \"{exeFileName}\" \"%1\"");
                DeleteAgent();
                Registration = WinDeepLinkRegistration.Launcher;
            }
            else if (allowAgentFallback)
            {
                string agentPath = CreateAgent(s_uriScheme);
                RegisterCustomUriScheme(s_uriScheme, $"wscript.exe \"{agentPath}\" \"%1\"");
                Registration = WinDeepLinkRegistration.Agent;
            }
            else
            {
                // Don't leave a handler pointing at an agent this build no longer keeps
                UnregisterAgent();
                Registration = WinDeepLinkRegistration.None;
            }
        }

        public static void ResetState()
        {
            CurrentWinActivatedDeepLink = null;
            OnDeepLinkActivated = null;
            IsInitialized = false;
            Registration = WinDeepLinkRegistration.None;
            OverrideTargetExecutablePath = null;
            OverrideWmiQuery = null;
        }

        public static void ProcessCommandLineArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.StartsWith(s_uriScheme, StringComparison.OrdinalIgnoreCase))
                {
                    var callback = new WinActivatedDeepLink(arg, WinDeepLinkingLoadMode.CommandLineArgument);
                    CurrentWinActivatedDeepLink = callback;
                    OnDeepLinkActivated?.Invoke(callback);
                    break;
                }
            }
        }
        private static void ValidateInitialization()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("WindowsDeepLinking is not initialized. Call Initialize() first.");
        }

        public static void CheckRegistryForDeepLink()
        {
            ValidateInitialization();

            string registryKeyPath = $@"SOFTWARE\Classes\{s_uriScheme}";
            using (var key = Registry.CurrentUser.OpenSubKey(registryKeyPath, writable: true))
            {
                if (key == null)
                    return;

                var value = key.GetValue(RegistryValueName);
                if (value == null)
                    return;

                key.DeleteValue(RegistryValueName);

                string uri = value.ToString();
                if (!uri.StartsWith(s_uriScheme, StringComparison.OrdinalIgnoreCase))
                    return;

                var callback = new WinActivatedDeepLink(uri, WinDeepLinkingLoadMode.RegistryValue);
                CurrentWinActivatedDeepLink = callback;
                OnDeepLinkActivated?.Invoke(callback);
            }
        }

        public static void ClearRegistryDeepLinkValue()
        {
            ValidateInitialization();

            string registryKeyPath = $@"SOFTWARE\Classes\{s_uriScheme}";
            using (var key = Registry.CurrentUser.OpenSubKey(registryKeyPath, writable: true))
            {
                if (key == null)
                    return;
                if (key.GetValue(RegistryValueName) != null)
                {
                    key.DeleteValue(RegistryValueName);
                }
            }
        }

        private static string GetTargetExecutablePath()
        {
            if (OverrideTargetExecutablePath != null)
                return OverrideTargetExecutablePath.Invoke();

            return s_targetExePath;
        }

        private static string GetWmiQuery()
        {
            if (OverrideWmiQuery != null)
                return OverrideWmiQuery.Invoke();

            string targetProcessName = s_productName + ".exe";
            return $"Select * from Win32_Process Where Name = '{targetProcessName}'";
        }

        private static string CreateAgent(string uriScheme)
        {
            string targetExePath = GetTargetExecutablePath();
            string wmiQuery = GetWmiQuery();

            string vbsCode = $@"
Set objArgs = WScript.Arguments
If objArgs.Count = 0 Then WScript.Quit

uri = objArgs.Item(0)
Set objShell = CreateObject(""WScript.Shell"")
Set objWMIService = GetObject(""winmgmts:\\.\root\cimv2"")

q = Chr(34)
targetPath = ""{targetExePath}""
regPath = ""HKCU\SOFTWARE\Classes\{uriScheme}\{RegistryValueName}""

Call objShell.RegWrite(regPath, uri, ""REG_SZ"")

Set colProcesses = objWMIService.ExecQuery(""{wmiQuery}"")
If colProcesses.Count > 0 Then
    ' Warm Start
    For Each objProcess In colProcesses
        pid = CLng(objProcess.ProcessId)
        Call objShell.AppActivate(pid)
        Exit For
    Next
Else
    ' Cold Start
    cmd = q & targetPath & q & "" "" & q & uri & q
    Call objShell.Run(cmd, 1, False)
End If
";
            string agentPath = GetAgentPath();

            File.WriteAllText(agentPath, vbsCode, Encoding.Unicode);
            return agentPath;
        }

        private static string GetAgentPath()
        {
            string persistentPath = s_persistentDataPath.Replace("/", @"\");
            return Path.Combine(persistentPath, $"{s_productName}.vbs");
        }

        private static string FindLauncher()
        {
            string exeDirectory = Path.GetDirectoryName(GetTargetExecutablePath());
            if (string.IsNullOrEmpty(exeDirectory))
                return null;

            string launcherPath = Path.Combine(exeDirectory, LauncherFileName);
            return File.Exists(launcherPath) ? launcherPath : null;
        }

        // The agent left by earlier versions (or a launcher-less build) is no longer registered
        private static void DeleteAgent()
        {
            try
            {
                string agentPath = GetAgentPath();
                if (File.Exists(agentPath))
                    File.Delete(agentPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void UnregisterAgent()
        {
            string agentPath = GetAgentPath();
            string registryKeyPath = $@"SOFTWARE\Classes\{s_uriScheme}";
            using (var commandKey = Registry.CurrentUser.OpenSubKey(registryKeyPath + @"\shell\open\command"))
            {
                string command = commandKey?.GetValue("") as string;
                if (command == null || command.IndexOf(agentPath, StringComparison.OrdinalIgnoreCase) < 0)
                    return;
            }
            Registry.CurrentUser.DeleteSubKeyTree(registryKeyPath, throwOnMissingSubKey: false);
            DeleteAgent();
        }

        private static void RegisterCustomUriScheme(string uriScheme, string command)
        {
            string targetExePath = GetTargetExecutablePath();

            using (var key = Registry.CurrentUser.CreateSubKey($@"SOFTWARE\Classes\{uriScheme}"))
            {
                if (key == null)
                    throw new UnauthorizedAccessException("Unable to create registry key.");

                key.SetValue("", s_productName);
                key.SetValue("URL Protocol", "");

                using (var appKey = key.CreateSubKey("Application"))
                {
                    if (appKey != null)
                    {
                        appKey.SetValue("ApplicationName", s_productName);
                        appKey.SetValue("ApplicationIcon", $"\"{targetExePath}\",0");
                    }
                }

                using (var commandKey = key.CreateSubKey(@"shell\open\command"))
                {
                    if (commandKey == null)
                        throw new UnauthorizedAccessException("Unable to create registry sub key.");

                    commandKey.SetValue("", command);
                }
            }
        }
    }
}
