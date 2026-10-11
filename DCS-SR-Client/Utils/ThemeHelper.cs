using System;
using System.Diagnostics;
using System.Windows;
using ControlzEx.Theming;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;

public static class ThemeHelper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public static void ApplyTheme(bool darkMode)
    {
        var theme = darkMode ? "Dark.Blue" : "Light.Blue";
        try
        {
            ThemeManager.Current.ChangeTheme(Application.Current, theme);
            Logger.Info($"Applied theme {theme}");
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, $"Failed to apply theme {theme}");
        }
    }
}
