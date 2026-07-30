using System;
using System.IO;
using CadDevLoader.Shared.Settings;

namespace CadDevLoader.Shared.Logging
{
    public static class DevLogger
    {
        private static string _lastError = "";
        public static string LastError
        {
            get { return _lastError; }
            set
            {
                _lastError = value;
                if (!string.IsNullOrEmpty(value)) WriteLog(value);
                ErrorUpdated?.Invoke();
            }
        }

        public static event Action ErrorUpdated;

        public static void WriteLog(string message)
        {
            try
            {
                string logDir = SettingsStore.LogDirectory;
                Directory.CreateDirectory(logDir);
                string file = Path.Combine(logDir, "log-" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
                File.AppendAllText(file, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
            }
            catch { }
        }

        public static void OpenLogFolder()
        {
            try { 
                string logDir = SettingsStore.LogDirectory;
                Directory.CreateDirectory(logDir); 
                System.Diagnostics.Process.Start("explorer.exe", logDir); 
            }
            catch { }
        }

        public static void OpenLogFile()
        {
            try { 
                string logDir = SettingsStore.LogDirectory;
                Directory.CreateDirectory(logDir); 
                string file = Path.Combine(logDir, "log-" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
                if (File.Exists(file))
                    System.Diagnostics.Process.Start("notepad.exe", file); 
                else
                    System.Diagnostics.Process.Start("explorer.exe", logDir); 
            }
            catch { }
        }
    }
}
