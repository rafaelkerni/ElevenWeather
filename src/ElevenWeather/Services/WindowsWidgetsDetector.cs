using Microsoft.Win32;

namespace ElevenWeather.Services;

/// <summary>
/// Detecta se o botão Widgets do Windows está visível na barra de tarefas — ele ocupa o mesmo
/// lugar do ElevenWeather. Só lê: o Windows bloqueia a escrita dessa configuração por apps de terceiros.
/// </summary>
public static class WindowsWidgetsDetector
{
    public const string TaskbarSettingsUri = "ms-settings:taskbar";

    private const string AdvancedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string PolicyKeyPath = @"SOFTWARE\Policies\Microsoft\Dsh";

    public static bool IsWidgetsButtonEnabled() =>
        !IsDisabledByPolicy(Registry.LocalMachine, PolicyKeyPath) && IsWidgetsButtonEnabled(AdvancedKeyPath);

    /// <summary>Sobrecarga com a subchave de HKCU configurável, usada pelos testes.</summary>
    internal static bool IsWidgetsButtonEnabled(string keyPath)
    {
        // Valor ausente = padrão de fábrica do Windows 11, que mostra o botão.
        var value = ReadValue(Registry.CurrentUser, keyPath, "TaskbarDa");
        return value is not int i || i != 0;
    }

    /// <summary>Política de grupo que desliga os Widgets no sistema inteiro, independente da preferência do usuário.</summary>
    internal static bool IsDisabledByPolicy(RegistryKey hive, string keyPath) =>
        ReadValue(hive, keyPath, "AllowNewsAndInterests") is int i && i == 0;

    private static object? ReadValue(RegistryKey hive, string keyPath, string valueName)
    {
        try
        {
            using var key = hive.OpenSubKey(keyPath);
            return key?.GetValue(valueName);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
