using System.Reflection;
using Autodesk.AutoCAD.Runtime;

namespace CadDevLoader.Shared.Data
{
    public sealed class PluginCommand
    {
        public PluginCommand(string name, MethodInfo method, CommandFlags flags)
        {
            Name = name;
            Method = method;
            Flags = flags;
        }

        public string Name { get; private set; }
        public MethodInfo Method { get; private set; }
        public CommandFlags Flags { get; private set; }

        public bool IsSession { get { return (Flags & CommandFlags.Session) != 0; } }
        public bool IsAsync { get { return typeof(System.Threading.Tasks.Task).IsAssignableFrom(Method.ReturnType); } }
    }
}
