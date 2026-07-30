using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using CadDevLoader.Shared.Data;
using CadDevLoader.Shared.Localization;
using CadDevLoader.Shared.Logging;

namespace CadDevLoader.Shared.Loader
{
    public static class PluginLoader
    {
        public static List<PluginCommand> DiscoverCommands(Assembly assembly)
        {
            var commands = new List<PluginCommand>();
            foreach (Type type in GetLoadableTypes(assembly))
            {
                const BindingFlags flags =
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static;

                foreach (MethodInfo method in type.GetMethods(flags))
                {
                    if (method.GetParameters().Length != 0)
                        continue;

                    object[] attributes =
                        method.GetCustomAttributes(typeof(CommandMethodAttribute), false);
                    foreach (CommandMethodAttribute attribute in attributes)
                    {
                        if (!String.IsNullOrWhiteSpace(attribute.GlobalName))
                            commands.Add(new PluginCommand(attribute.GlobalName, method, attribute.Flags));
                    }
                }
            }
            return commands;
        }

        public static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                foreach (string warning in exception.LoaderExceptions
                    .Where(item => item != null)
                    .Select(item => item.Message)
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    DevLogger.LastError = L10n.T("Cảnh báo dependency: ", "Dependency warning: ") + warning;
                }
                return exception.Types.Where(x => x != null);
            }
        }

        public static List<IExtensionApplication> InitializeExtensions(Assembly assembly, Action<string> writeLine)
        {
            var extensions = new List<IExtensionApplication>();
            foreach (Type type in GetLoadableTypes(assembly))
            {
                if (type.IsAbstract || !typeof(IExtensionApplication).IsAssignableFrom(type))
                    continue;

                try
                {
                    var extension = (IExtensionApplication)Activator.CreateInstance(type, true);
                    extension.Initialize();
                    extensions.Add(extension);
                }
                catch (System.Exception exception)
                {
                    DevLogger.LastError = L10n.T("Khởi tạo extension ", "Initialize extension ") + type.FullName + ": " + exception.Message;
                    writeLine?.Invoke("\n" + DevLogger.LastError);
                }
            }
            return extensions;
        }

        public static void TerminateExtensions(LoadedPlugin plugin, Action<string> writeLine)
        {
            if (plugin == null || plugin.Extensions == null) return;
            for (int index = plugin.Extensions.Count - 1; index >= 0; index--)
            {
                try
                {
                    plugin.Extensions[index].Terminate();
                }
                catch (System.Exception exception)
                {
                    DevLogger.LastError = L10n.T("Kết thúc extension cũ: ", "Terminate previous extension: ") + exception.Message;
                    writeLine?.Invoke("\n" + DevLogger.LastError);
                }
            }
        }
        
        public static void TryCleanupAllPluginUi(string targetAssemblyName, Action<string> writeLine)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == typeof(PluginLoader).Assembly) continue;
                string assemblyName = assembly.GetName().Name ?? "";
                if (!assemblyName.Equals(targetAssemblyName, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    MethodInfo cleanup = GetLoadableTypes(assembly)
                        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                        .FirstOrDefault(method =>
                            method.GetParameters().Length == 0 &&
                            (String.Equals(method.Name, "CloseAllPalettes", StringComparison.OrdinalIgnoreCase) ||
                             String.Equals(method.Name, "DevCleanup", StringComparison.OrdinalIgnoreCase)));

                    if (cleanup == null) continue;
                    cleanup.Invoke(null, null);
                    writeLine?.Invoke(L10n.T("\nĐã đóng giao diện plugin cũ: ", "\nClosed old plug-in UI: ") + assembly.GetName().Name);
                }
                catch (System.Exception exception)
                {
                    DevLogger.LastError = L10n.T("Dọn giao diện ", "Cleanup ") + assembly.GetName().Name + ": " + exception.Message;
                }
            }
        }
    }
}
