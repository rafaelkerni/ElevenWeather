using System.Diagnostics;
using Microsoft.Win32;

namespace ElevenWeather.Services;

/// <summary>Registers auto-start in HKCU\...\Run — doesn't require elevation.</summary>
public sealed class AutoStartService : IAutoStartService
{
    public const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ElevenWeather";

    private readonly string _runKeyPath;

    /// <param name="runKeyPath">Subchave de HKCU; configurável para os testes não tocarem na chave Run real.</param>
    public AutoStartService(string runKeyPath = DefaultRunKeyPath) => _runKeyPath = runKeyPath;

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath, writable: true);
        if (enabled)
        {
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (exePath is not null)
                key.SetValue(ValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
