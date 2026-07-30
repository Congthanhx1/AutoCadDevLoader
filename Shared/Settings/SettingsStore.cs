using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CadDevLoader.Shared.Settings
{
    public static class SettingsStore
    {
        public static readonly HashSet<string> Favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public static readonly List<string> RecentCommands = new List<string>();
        
        public static string SettingsDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CadDevLoader"); } }
        private static string LastDllFile { get { return Path.Combine(SettingsDirectory, "last-dll.txt"); } }
        private static string FavoritesFile { get { return Path.Combine(SettingsDirectory, "favorites.txt"); } }
        private static string LanguageFile { get { return Path.Combine(SettingsDirectory, "language.txt"); } }
        private static string AutoReloadFile { get { return Path.Combine(SettingsDirectory, "auto-reload.txt"); } }
        private static string PositionFile { get { return Path.Combine(SettingsDirectory, "panel-position.txt"); } }
        public static string LogDirectory { get { return Path.Combine(SettingsDirectory, "logs"); } }

        public static bool UseEnglish { get; set; }
        public static bool AutoReload { get; set; }

        public static void LoadPreferences()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                Favorites.Clear();
                if (File.Exists(FavoritesFile))
                    foreach (string item in File.ReadAllLines(FavoritesFile))
                        if (!String.IsNullOrWhiteSpace(item)) Favorites.Add(item.Trim());
                UseEnglish = File.Exists(LanguageFile)
                    && String.Equals(File.ReadAllText(LanguageFile).Trim(), "en", StringComparison.OrdinalIgnoreCase);
                AutoReload = File.Exists(AutoReloadFile)
                    && String.Equals(File.ReadAllText(AutoReloadFile).Trim(), "1", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
        }

        public static string ReadLastDll()
        {
            try { return File.Exists(LastDllFile) ? File.ReadAllText(LastDllFile).Trim() : null; }
            catch { return null; }
        }

        public static void SaveLastDll(string path)
        {
            try { Directory.CreateDirectory(SettingsDirectory); File.WriteAllText(LastDllFile, path ?? ""); }
            catch { }
        }

        public static void SaveFavorites()
        {
            try { Directory.CreateDirectory(SettingsDirectory); File.WriteAllLines(FavoritesFile, Favorites.OrderBy(x => x).ToArray()); }
            catch { }
        }

        public static void SaveLanguage()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllText(LanguageFile, UseEnglish ? "en" : "vi");
            }
            catch { }
        }

        public static void SaveAutoReload()
        {
            try { Directory.CreateDirectory(SettingsDirectory); File.WriteAllText(AutoReloadFile, AutoReload ? "1" : "0"); }
            catch { }
        }

        public static Point LoadPanelPosition()
        {
            try
            {
                if (!File.Exists(PositionFile)) return Point.Empty;
                string[] parts = File.ReadAllText(PositionFile).Trim().Split(',');
                if (parts.Length < 2) return Point.Empty;
                Point candidate = new Point(int.Parse(parts[0].Trim()), int.Parse(parts[1].Trim()));
                Rectangle panelRect = new Rectangle(candidate, new Size(300, 620));
                foreach (Screen s in Screen.AllScreens)
                    if (s.WorkingArea.IntersectsWith(panelRect)) return candidate;
                return Point.Empty;
            }
            catch { return Point.Empty; }
        }

        public static void SavePanelPosition(Point location)
        {
            try { Directory.CreateDirectory(SettingsDirectory); File.WriteAllText(PositionFile, location.X + "," + location.Y); }
            catch { }
        }

        public static Screen GetAcadScreen()
        {
            try 
            { 
                return Screen.FromHandle(Autodesk.AutoCAD.ApplicationServices.Application.MainWindow.Handle); 
            }
            catch { return Screen.PrimaryScreen; }
        }

        public static void RememberRecentCommand(string commandName)
        {
            RecentCommands.RemoveAll(name => name.Equals(commandName, StringComparison.OrdinalIgnoreCase));
            RecentCommands.Insert(0, commandName);
            if (RecentCommands.Count > 4) RecentCommands.RemoveRange(4, RecentCommands.Count - 4);
        }
    }
}
