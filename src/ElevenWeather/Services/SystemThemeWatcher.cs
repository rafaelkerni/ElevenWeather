using Microsoft.Win32;

namespace ElevenWeather.Services;

/// <summary>
/// Reads the system theme (the one that governs the taskbar color, not the apps theme).
/// Real-time changes arrive via WM_SETTINGCHANGE, handled in the host window.
/// </summary>
public static class SystemThemeWatcher
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ValueName = "SystemUsesLightTheme";

    public static bool IsLightTheme() => IsLightTheme(PersonalizeKeyPath);

    /// <summary>Sobrecarga com a subchave de HKCU configurável, usada pelos testes.</summary>
    internal static bool IsLightTheme(string keyPath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath);
            var value = key?.GetValue(ValueName);
            return value is int i ? i != 0 : true; // Windows 11's factory default is the light theme
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
