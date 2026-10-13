using System.IO;
using System.Text.Json;
using ElevenWeather.Models;

namespace ElevenWeather.Services;

public sealed class SettingsService : ISettingsService
{
    public static readonly string DefaultSettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ElevenWeather");

    private readonly string _settingsDir;
    private readonly string _settingsPath;

    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    /// <param name="settingsDir">Pasta do settings.json; configurável para os testes usarem uma pasta temporária.</param>
    public SettingsService(string? settingsDir = null)
    {
        _settingsDir = settingsDir ?? DefaultSettingsDir;
        _settingsPath = Path.Combine(_settingsDir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                    return settings;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupted or inaccessible config: fall back to defaults.
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_settingsDir);
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A persistence failure shouldn't crash the app; the next session tries again.
        }
    }
}
