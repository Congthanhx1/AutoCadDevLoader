using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;

namespace CadDevLoader.Shared.Data
{
    public sealed class LoadedPlugin
    {
        public LoadedPlugin(
            string sourcePath,
            string loadedPath,
            DateTime loadedAt,
            Assembly assembly,
            List<PluginCommand> commands,
            List<IExtensionApplication> extensions)
        {
            SourcePath = sourcePath;
            LoadedPath = loadedPath;
            LoadedAt = loadedAt;
            Assembly = assembly;
            Commands = commands;
            Extensions = extensions;
        }

        public string SourcePath { get; private set; }
        public string LoadedPath { get; private set; }
        public DateTime LoadedAt { get; private set; }
        public Assembly Assembly { get; private set; }
        public List<PluginCommand> Commands { get; private set; }
        public List<IExtensionApplication> Extensions { get; private set; }
    }
}
