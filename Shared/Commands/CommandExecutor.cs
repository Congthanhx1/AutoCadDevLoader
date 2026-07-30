using System;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using CadDevLoader.Shared.Data;
using CadDevLoader.Shared.Localization;
using CadDevLoader.Shared.Logging;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadDevLoader.Shared.Commands
{
    public static class CommandExecutor
    {
        public static void Invoke(PluginCommand command, Action<string> writeLine, Action<string> rememberCommand)
        {
            try
            {
                if (command.IsSession)
                    writeLine?.Invoke(L10n.T("\n⚠ Lệnh có flag Session — có thể hoạt động khác khi chạy từ panel.",
                                             "\n⚠ Session-flagged command — may behave differently when run from panel."));
                if (command.IsAsync)
                    writeLine?.Invoke(L10n.T("\n⚠ Lệnh async (Task) — ngoại lệ có thể không được bắt từ panel.",
                                             "\n⚠ Async command (Task) — exceptions may not be caught from panel."));

                object instance = null;
                if (!command.Method.IsStatic)
                    instance = Activator.CreateInstance(command.Method.DeclaringType, true);

                object returnValue = command.Method.Invoke(instance, null);
                
                rememberCommand?.Invoke(command.Name);
                
                writeLine?.Invoke(L10n.T("\nĐã chạy lệnh development: ", "\nExecuted development command: ") + command.Name);

                if (returnValue != null && !command.IsAsync)
                    writeLine?.Invoke(L10n.T("\nGiá trị trả về: ", "\nReturn value: ") + returnValue);
            }
            catch (TargetInvocationException exception)
            {
                Exception actualException = exception.InnerException ?? exception;
                DevLogger.LastError = L10n.T("Lệnh ", "Command ") + command.Name + L10n.T(" thất bại: ", " failed: ") + actualException.ToString();
                DevLogger.WriteLog(DevLogger.LastError);
                writeLine?.Invoke("\n" + L10n.T("Lệnh ", "Command ") + command.Name + L10n.T(" thất bại: ", " failed: ") + actualException.Message);
            }
            catch (Exception exception)
            {
                DevLogger.LastError = L10n.T("Lệnh ", "Command ") + command.Name + L10n.T(" thất bại: ", " failed: ") + exception.ToString();
                DevLogger.WriteLog(DevLogger.LastError);
                writeLine?.Invoke("\n" + L10n.T("Lệnh ", "Command ") + command.Name + L10n.T(" thất bại: ", " failed: ") + exception.Message);
            }
        }

        public static void QueueCommand(string command)
        {
            Document document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document != null)
                document.SendStringToExecute(command + " ", true, false, false);
        }

        public static void QueueDevRun(string command)
        {
            Document document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document != null)
                document.SendStringToExecute("DEVRUN " + command + " ", true, false, false);
        }
        
        public static string GetCommandDisplayName(string command)
        {
            return command.Replace("CMD_", "").Replace('_', ' ').Trim();
        }
    }
}
