using System;
using System.IO;
using System.Windows.Forms;
using CadDevLoader.Shared.Settings;

namespace CadDevLoader.Shared.Watching
{
    public static class BuildWatcher
    {
        private static Timer _watchTimer;
        public static DateTime ObservedWriteUtc { get; private set; }

        public static event Action<DateTime> NewBuildDetected;

        public static void Start(Func<string> getSourcePath)
        {
            if (_watchTimer != null) return;
            _watchTimer = new Timer { Interval = 1200 };
            _watchTimer.Tick += (s, e) => CheckForNewBuild(getSourcePath());
            _watchTimer.Start();
        }

        public static void Stop()
        {
            if (_watchTimer != null)
            {
                _watchTimer.Stop();
                _watchTimer.Dispose();
                _watchTimer = null;
            }
        }

        public static void ResetObservedTime(string path)
        {
            if (File.Exists(path))
            {
                ObservedWriteUtc = File.GetLastWriteTimeUtc(path);
            }
        }

        private static void CheckForNewBuild(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            DateTime writeUtc = File.GetLastWriteTimeUtc(path);
            if (ObservedWriteUtc == DateTime.MinValue) ObservedWriteUtc = writeUtc;
            if (writeUtc <= ObservedWriteUtc) return;
            
            if (SettingsStore.AutoReload)
            {
                ObservedWriteUtc = writeUtc;
            }
            
            NewBuildDetected?.Invoke(writeUtc);
        }
    }
}
