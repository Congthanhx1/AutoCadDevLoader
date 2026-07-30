using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CadDevLoader.Shared.Localization;
using CadDevLoader.Shared.Logging;

namespace CadDevLoader.Shared.Loader
{
    public static class DependencyResolver
    {
        public static Assembly Resolve(ResolveEventArgs args, string cacheDirectory)
        {
            try
            {
                AssemblyName requested = new AssemblyName(args.Name);
                string simpleName = requested.Name;

                Assembly loaded = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(item => String.Equals(item.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
                if (loaded != null)
                {
                    Version loadedVersion = loaded.GetName().Version;
                    Version requestedVersion = requested.Version;
                    if (requestedVersion != null && loadedVersion != null && loadedVersion != requestedVersion)
                    {
                        DevLogger.LastError = L10n.T("Xung đột dependency: ", "Dependency conflict: ")
                            + simpleName
                            + L10n.T(" — yêu cầu v", " — requested v") + requestedVersion
                            + L10n.T(", đang dùng v", ", using v") + loadedVersion
                            + L10n.T(". Khởi động lại AutoCAD nếu có lỗi.", ". Restart AutoCAD if issues occur.");
                    }
                    return loaded;
                }

                string candidate = Path.Combine(cacheDirectory, simpleName + ".dll");
                if (!File.Exists(candidate)) return null;
                return Assembly.Load(File.ReadAllBytes(candidate));
            }
            catch (Exception exception)
            {
                DevLogger.LastError = L10n.T("Nạp dependency thất bại: ", "Dependency load failed: ") + exception.Message;
                return null;
            }
        }
    }
}
