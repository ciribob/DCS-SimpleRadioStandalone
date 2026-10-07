using System;
using System.Diagnostics;
using System.Windows;
using ControlzEx.Theming;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Utils;

public static class ThemeHelper
{
    public static void ApplyTheme(bool darkMode)
    {
        try
        {
            var theme = darkMode ? "Dark.Blue" : "Light.Blue";
            ThemeManager.Current.ChangeTheme(Application.Current, theme);
            Debug.WriteLine($"Applied theme {theme}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply theme: {ex}");
        }
    }
}