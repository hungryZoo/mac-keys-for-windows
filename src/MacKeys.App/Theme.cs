using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace MacKeys;

internal static class Theme
{
    public static void Apply(Application application)
    {
        var light = IsLightTheme();
        var resources = application.Resources;
        resources["WindowBackground"] = Brush(light ? "#F4F5F7" : "#1C1E22");
        resources["CardBackground"] = Brush(light ? "#FFFFFF" : "#272B31");
        resources["CardBorder"] = Brush(light ? "#D8DCE3" : "#3A4048");
        resources["ListBackground"] = Brush(light ? "#FBFBFC" : "#1F2328");
        resources["TextPrimary"] = Brush(light ? "#1A1D21" : "#F2F4F7");
        resources["TextMuted"] = Brush(light ? "#5C6570" : "#B4BCC6");
        resources["Accent"] = Brush("#2F6FED");
        resources["ButtonBackground"] = Brush(light ? "#EEF1F5" : "#343A42");
        resources["ButtonBorder"] = Brush(light ? "#D0D5DD" : "#4A515B");
        resources["WarnBackground"] = Brush(light ? "#FFF6E5" : "#3A311C");
        resources["WarnForeground"] = Brush(light ? "#7A4E00" : "#F0D08A");
        resources["OkBrush"] = Brush(light ? "#0E7A32" : "#7DDB8A");
        resources["DangerBrush"] = Brush(light ? "#B42318" : "#F0A0A0");
    }

    private static bool IsLightTheme()
    {
        var value = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            1);
        return value is int mode && mode != 0;
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
