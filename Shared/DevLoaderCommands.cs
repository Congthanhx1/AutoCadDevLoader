using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CadDevLoader.Shared.Commands;
using CadDevLoader.Shared.Data;
using CadDevLoader.Shared.Loader;
using CadDevLoader.Shared.Localization;
using CadDevLoader.Shared.Logging;
using CadDevLoader.Shared.Settings;
using CadDevLoader.Shared.UI;
using CadDevLoader.Shared.Watching;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadDevLoader
{
    public class DevLoaderCommands : IExtensionApplication
    {
        private static readonly object SyncRoot = new object();
        private static LoadedPlugin _current;
        private static ResolveEventHandler _dependencyResolver;
        private static int _reloadCount;

        public void Initialize()
        {
            SettingsStore.LoadPreferences();
            BuildWatcher.NewBuildDetected += (utc) => ReloadOrChooseDll();
            BuildWatcher.Start(() => _current?.SourcePath ?? SettingsStore.ReadLastDll());
            
            QuickPanelForm.Initialize(
                () => { lock (SyncRoot) return _current; },
                QueueReloadOrChooseDll,
                CleanCache
            );

            WriteLine(L10n.T("\nĐã khởi động CAD DEV LOADER. Chạy lệnh DEVLOAD để nạp DLL.", "\nCAD DEV LOADER initialized. Run DEVLOAD to load a DLL."));
            
            if (SettingsStore.AutoReload)
            {
                string path = SettingsStore.ReadLastDll();
                if (!String.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    BuildWatcher.ResetObservedTime(path);
                }
            }
            
            // Tự động hiện giao diện khi nạp
            QuickPanelForm.ShowQuickBar();
        }

        public void Terminate()
        {
            BuildWatcher.Stop();
            QuickPanelForm.HideQuickBar();
            lock (SyncRoot)
            {
                if (_current != null)
                {
                    PluginLoader.TerminateExtensions(_current, WriteLine);
                    PluginLoader.TryCleanupAllPluginUi(_current.Assembly.GetName().Name, WriteLine);
                }
            }
        }

        [CommandMethod("DEVLOAD", CommandFlags.Modal)]
        public void LoadPluginCommand()
        {
            string sourcePath = null;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = L10n.T("AutoCAD Plug-ins (*.dll)|*.dll|Tất cả (*.*)|*.*", "AutoCAD Plug-ins (*.dll)|*.dll|All files (*.*)|*.*");
                dialog.Title = L10n.T("Chọn DLL plugin development", "Select development plug-in DLL");
                dialog.InitialDirectory = SettingsStore.ReadLastDll() != null 
                    ? Path.GetDirectoryName(SettingsStore.ReadLastDll()) 
                    : "";

                if (dialog.ShowDialog() == DialogResult.OK)
                    sourcePath = dialog.FileName;
            }

            if (String.IsNullOrWhiteSpace(sourcePath)) return;
            LoadPlugin(sourcePath);
        }

        [CommandMethod("DEVRELOAD", CommandFlags.Modal)]
        public void ReloadPluginCommand()
        {
            string sourcePath = null;
            lock (SyncRoot)
            {
                if (_current != null)
                    sourcePath = _current.SourcePath;
            }

            if (String.IsNullOrWhiteSpace(sourcePath)) sourcePath = SettingsStore.ReadLastDll();

            if (String.IsNullOrWhiteSpace(sourcePath))
            {
                WriteLine(L10n.T("\nChưa có DLL nào được ghi nhớ. Hãy chạy DEVLOAD trước.", "\nNo remembered plug-in DLL. Run DEVLOAD first."));
                return;
            }

            LoadPlugin(sourcePath);
        }

        [CommandMethod("DEVLIST", CommandFlags.Modal)]
        public void ListCommands()
        {
            LoadedPlugin plugin = GetCurrent();
            if (plugin == null)
                return;

            WriteLine(L10n.T("\nCác lệnh trong bản development mới nhất:", "\nCommands in the latest development build:"));
            foreach (PluginCommand command in plugin.Commands.OrderBy(x => x.Name))
                WriteLine("\n  " + command.Name + "  [" + command.Method.DeclaringType.FullName + "]");
        }

        [CommandMethod("DEVRUN", CommandFlags.Modal)]
        public void RunCommand()
        {
            LoadedPlugin plugin = GetCurrent();
            if (plugin == null)
                return;

            Editor editor = GetEditor();
            PromptStringOptions options =
                new PromptStringOptions(L10n.T("\nTên lệnh development:", "\nDevelopment command name:"));
            options.AllowSpaces = false;

            PromptResult result = editor.GetString(options);
            if (result.Status != PromptStatus.OK)
                return;

            PluginCommand command = plugin.Commands.FirstOrDefault(
                x => String.Equals(x.Name, result.StringResult, StringComparison.OrdinalIgnoreCase));

            if (command == null)
            {
                WriteLine(L10n.T("\nKhông tìm thấy lệnh. Chạy DEVLIST để xem các lệnh khả dụng.", "\nCommand not found. Run DEVLIST to see available commands."));
                return;
            }

            CommandExecutor.Invoke(command, WriteLine, SettingsStore.RememberRecentCommand);
            QuickPanelForm.QueueQuickCommandRefresh();
        }

        [CommandMethod("DEVSTATUS", CommandFlags.Modal)]
        public void Status()
        {
            LoadedPlugin plugin;
            lock (SyncRoot)
                plugin = _current;

            if (plugin == null)
            {
                WriteLine(L10n.T("\nChưa nạp plugin development nào.", "\nNo development plug-in is loaded."));
                return;
            }

            WriteLine(L10n.T("\nDLL nguồn: ", "\nSource: ") + plugin.SourcePath);
            WriteLine(L10n.T("\nBản sao đã nạp: ", "\nLoaded copy: ") + plugin.LoadedPath);
            WriteLine(L10n.T("\nThời điểm nạp: ", "\nLoaded at: ") + plugin.LoadedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            WriteLine(L10n.T("\nSố lệnh: ", "\nCommands: ") + plugin.Commands.Count);
        }

        [CommandMethod("DEVSHOW", CommandFlags.Modal)]
        public void ShowPanel()
        {
            QuickPanelForm.ShowQuickBar();
        }

        private static void LoadPlugin(string sourcePath)
        {
            LoadedPlugin previous = null;
            ResolveEventHandler previousResolver = null;
            ResolveEventHandler nextResolver = null;
            bool committed = false;
            try
            {
                string fullSourcePath = Path.GetFullPath(sourcePath);
                if (!File.Exists(fullSourcePath))
                    throw new FileNotFoundException(L10n.T("Không tìm thấy DLL plugin.", "Plug-in DLL was not found."), fullSourcePath);

                lock (SyncRoot)
                {
                    previous = _current;
                    previousResolver = _dependencyResolver;
                }

                if (previousResolver != null)
                {
                    AppDomain.CurrentDomain.AssemblyResolve -= previousResolver;
                    _dependencyResolver = null;
                }

                string sourceDirectory = Path.GetDirectoryName(fullSourcePath);
                string cacheDirectory = CacheManager.CreateCacheDirectory(fullSourcePath);
                CacheManager.CopyRuntimeFiles(sourceDirectory, cacheDirectory);

                nextResolver = delegate(object sender, ResolveEventArgs args)
                {
                    return DependencyResolver.Resolve(args, cacheDirectory);
                };
                AppDomain.CurrentDomain.AssemblyResolve += nextResolver;

                string loadedPath = Path.Combine(cacheDirectory, Path.GetFileName(fullSourcePath));
                AppDomain.CurrentDomain.SetData("CadDevLoader.SourcePath", fullSourcePath);
                AppDomain.CurrentDomain.SetData("CadDevLoader.CacheDirectory", cacheDirectory);
                byte[] assemblyBytes = File.ReadAllBytes(loadedPath);
                string pdbPath = Path.ChangeExtension(loadedPath, ".pdb");
                Assembly assembly = File.Exists(pdbPath)
                    ? Assembly.Load(assemblyBytes, File.ReadAllBytes(pdbPath))
                    : Assembly.Load(assemblyBytes);
                var commands = PluginLoader.DiscoverCommands(assembly);
                string cleanupAssemblyName = previous != null
                    ? previous.Assembly.GetName().Name
                    : assembly.GetName().Name;

                if (previous != null) PluginLoader.TerminateExtensions(previous, WriteLine);
                PluginLoader.TryCleanupAllPluginUi(cleanupAssemblyName, WriteLine);
                var extensions = PluginLoader.InitializeExtensions(assembly, WriteLine);

                bool isReload = previous != null;

                LoadedPlugin plugin = new LoadedPlugin(
                    fullSourcePath,
                    loadedPath,
                    DateTime.Now,
                    assembly,
                    commands,
                    extensions);

                lock (SyncRoot)
                {
                    _current = plugin;
                    _dependencyResolver = nextResolver;
                }
                committed = true;

                SettingsStore.SaveLastDll(fullSourcePath);
                BuildWatcher.ResetObservedTime(fullSourcePath);
                
                if (isReload) _reloadCount++;
                QuickPanelForm.SetReloadCount(_reloadCount);
                DevLogger.LastError = "";
                QuickPanelForm.QueueQuickCommandRefresh();

                WriteLine(L10n.T("\nĐã nạp bản sao development: ", "\nLoaded development copy: ") + loadedPath);
                WriteLine("\nAssembly MVID: " + assembly.ManifestModule.ModuleVersionId);
                WriteLine(L10n.T("\nTìm thấy ", "\nFound ") + commands.Count + L10n.T(" lệnh không tham số.", " parameterless command(s)."));
            }
            catch (System.Exception exception)
            {
                if (!committed)
                {
                    if (nextResolver != null)
                        AppDomain.CurrentDomain.AssemblyResolve -= nextResolver;
                    if (previousResolver != null)
                    {
                        AppDomain.CurrentDomain.AssemblyResolve += previousResolver;
                        _dependencyResolver = previousResolver;
                    }
                    lock (SyncRoot) _current = previous;
                }
                
                DevLogger.LastError = L10n.T("DEVLOAD thất bại: ", "DEVLOAD failed: ") + exception.ToString();
                DevLogger.WriteLog(DevLogger.LastError);
                WriteLine("\n" + L10n.T("DEVLOAD thất bại: ", "DEVLOAD failed: ") + exception.Message);
            }
        }

        private static LoadedPlugin GetCurrent()
        {
            lock (SyncRoot)
            {
                if (_current != null)
                    return _current;
            }

            WriteLine(L10n.T("\nChưa nạp plugin development. Hãy chạy DEVLOAD trước.", "\nNo development plug-in is loaded. Run DEVLOAD first."));
            return null;
        }

        private static Editor GetEditor()
        {
            Document document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
                throw new InvalidOperationException(L10n.T("Không có bản vẽ AutoCAD đang hoạt động.", "No active AutoCAD document."));

            return document.Editor;
        }

        private static void WriteLine(string message)
        {
            Document document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document != null)
                document.Editor.WriteMessage(message);
        }

        private static void QueueReloadOrChooseDll()
        {
            string path = null;
            lock (SyncRoot) path = _current?.SourcePath;
            if (String.IsNullOrWhiteSpace(path)) path = SettingsStore.ReadLastDll();

            if (!String.IsNullOrWhiteSpace(path) && File.Exists(path))
                CommandExecutor.QueueCommand("DEVRELOAD");
            else
                CommandExecutor.QueueCommand("DEVLOAD");
        }

        private static void ReloadOrChooseDll()
        {
            Document document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document != null)
            {
                string path = null;
                lock (SyncRoot) path = _current?.SourcePath;
                if (String.IsNullOrWhiteSpace(path)) path = SettingsStore.ReadLastDll();

                if (!String.IsNullOrWhiteSpace(path) && File.Exists(path))
                    document.SendStringToExecute("DEVRELOAD ", true, false, false);
                else
                    document.SendStringToExecute("DEVLOAD ", true, false, false);
            }
        }

        private static void CleanCache()
        {
            string activeDir = "";
            lock (SyncRoot)
            {
                if (_current != null)
                    activeDir = Path.GetDirectoryName(_current.LoadedPath);
            }
            CacheManager.CleanOldCaches(activeDir, WriteLine);
        }
    }
}
