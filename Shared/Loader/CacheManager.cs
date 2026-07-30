using System;
using System.IO;
using System.Linq;
using CadDevLoader.Shared.Localization;
using CadDevLoader.Shared.Logging;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadDevLoader.Shared.Loader
{
    public static class CacheManager
    {
        public static string CreateCacheDirectory(string sourcePath)
        {
            string name = Path.GetFileNameWithoutExtension(sourcePath);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string root = Path.Combine(
                Path.GetTempPath(),
                "CadDevLoader",
                GetAutoCadVersion(),
                name);
            string directory = Path.Combine(root, stamp);
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string GetAutoCadVersion()
        {
            try
            {
                return AcadApplication.Version.Major + "." + AcadApplication.Version.Minor;
            }
            catch
            {
                return "unknown";
            }
        }

        public static void CopyRuntimeFiles(string sourceDirectory, string targetDirectory)
        {
            string[] patterns = { "*.dll", "*.pdb", "*.json", "*.config" };
            string[] hostAssemblies = { "AcCoreMgd", "AcMgd", "AcDbMgd", "AcCui", "AcWindows", "AdWindows" };
            foreach (string pattern in patterns)
            {
                foreach (string file in Directory.GetFiles(sourceDirectory, pattern))
                {
                    string runtimeName = Path.GetFileNameWithoutExtension(file);
                    if (hostAssemblies.Any(name => String.Equals(name, runtimeName, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    string destination = Path.Combine(targetDirectory, Path.GetFileName(file));
                    File.Copy(file, destination, true);
                }
            }
        }

        public static void CleanOldCaches(string currentActiveDirectory, Action<string> writeLine)
        {
            try
            {
                string root = Path.Combine(Path.GetTempPath(), "CadDevLoader");
                int removed = 0;
                
                if (Directory.Exists(root))
                {
                    string[] directories = Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                        .OrderByDescending(path => path.Length)
                        .ToArray();
                    foreach (string directory in directories)
                    {
                        if (!String.IsNullOrWhiteSpace(currentActiveDirectory) &&
                            (String.Equals(directory, currentActiveDirectory, StringComparison.OrdinalIgnoreCase) ||
                             currentActiveDirectory.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        try
                        {
                            if (Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly).Length > 0)
                            {
                                Directory.Delete(directory, true);
                                removed++;
                            }
                            else if (Directory.GetFileSystemEntries(directory).Length == 0)
                            {
                                Directory.Delete(directory, false);
                            }
                        }
                        catch { }
                    }
                }
                DevLogger.LastError = "";
                writeLine?.Invoke(L10n.T("\nĐã dọn ", "\nCleaned ") + removed + L10n.T(" thư mục cache cũ.", " old cache folders."));
            }
            catch (Exception exception) { DevLogger.LastError = L10n.T("Dọn cache thất bại: ", "Cache cleanup failed: ") + exception.Message; }
        }
    }
}
