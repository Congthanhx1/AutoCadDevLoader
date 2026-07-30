using CadDevLoader.Shared.Settings;

namespace CadDevLoader.Shared.Localization
{
    public static class L10n
    {
        public static string T(string vietnamese, string english)
        {
            return SettingsStore.UseEnglish ? english : vietnamese;
        }
    }
}
