using System;
using System.Windows;
using Samba.Infrastructure.Settings;

namespace Samba.Presentation.Common.Services
{
    public static class ThemeManager
    {
        public const string LightTheme = "Light";
        public const string DarkTheme = "Dark";

        public static string CurrentTheme { get; private set; }

        public static void ApplySavedTheme()
        {
            Apply(ReadSavedTheme());
        }

        public static string ReadSavedTheme()
        {
            return LocalSettings.ReadSetting("UITheme") == DarkTheme ? DarkTheme : LightTheme;
        }

        public static void ToggleTheme()
        {
            Apply(CurrentTheme == LightTheme ? DarkTheme : LightTheme);
        }

        public static void Apply(string theme)
        {
            if (theme != DarkTheme) theme = LightTheme;
            CurrentTheme = theme;
            LocalSettings.UpdateSetting("UITheme", theme);

            var app = Application.Current;
            if (app == null) return;

            var dictionaries = app.Resources.MergedDictionaries;
            for (var i = 0; i < dictionaries.Count; i++)
            {
                var source = dictionaries[i].Source;
                if (source == null || !source.OriginalString.Contains("PosLiteBrushes.")) continue;
                dictionaries[i] = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Samba.Presentation;component/Themes/PosLite/PosLiteBrushes." + theme + ".xaml")
                };
                return;
            }
        }
    }
}