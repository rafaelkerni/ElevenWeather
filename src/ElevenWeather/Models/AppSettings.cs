namespace ElevenWeather.Models;

public sealed class AppSettings
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? CityName { get; set; }
    public bool AutoStart { get; set; }
    public bool AutoStartPromptShown { get; set; }
    public bool WidgetsPromptDismissed { get; set; }

    /// <summary>Null until the user picks one manually — in that case Windows' language decides.</summary>
    public AppLanguage? Language { get; set; }

    public TemperatureUnit TemperatureUnit { get; set; } = TemperatureUnit.Celsius;

    public IconSet IconSet { get; set; } = IconSet.Meteocons;

    public TimeFormat TimeFormat { get; set; } = TimeFormat.TwentyFourHour;

    public bool HasLocation => Latitude.HasValue && Longitude.HasValue;
}
