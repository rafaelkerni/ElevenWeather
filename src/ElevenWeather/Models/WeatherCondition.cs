namespace ElevenWeather.Models;

/// <summary>
/// Translates WMO codes (returned by Open-Meteo) to text (via AppLocalization, in the current
/// language) and to the matching icon key under Resources/Icons/{set}/.
/// </summary>
public static class WeatherCondition
{
    public static string Describe(int wmoCode) => AppLocalization.Get(DescribeKey(wmoCode));

    private static string DescribeKey(int wmoCode) => wmoCode switch
    {
        0 => "weather.clear",
        1 => "weather.mostlyClear",
        2 => "weather.partlyCloudy",
        3 => "weather.cloudy",
        45 or 48 => "weather.fog",
        51 or 53 or 55 => "weather.drizzle",
        56 or 57 => "weather.freezingDrizzle",
        61 or 63 => "weather.rain",
        65 => "weather.heavyRain",
        66 or 67 => "weather.freezingRain",
        71 or 73 => "weather.snow",
        75 => "weather.heavySnow",
        77 => "weather.snowGrains",
        80 or 81 => "weather.rainShowers",
        82 => "weather.heavyRainShowers",
        85 or 86 => "weather.snowShowers",
        95 => "weather.thunderstorm",
        96 or 99 => "weather.thunderstormHail",
        _ => "weather.unavailable",
    };

    /// <summary>
    /// Icon key shared by every bundled icon set (Resources/Icons/{set}/{key in snake_case}.png).
    /// Variants without their own icon (heavy rain, drizzle, heavy snow, partly cloudy at night)
    /// reuse the icon from the closest family — the same simplification the original makes.
    /// </summary>
    public static string IconKey(int wmoCode, bool isDay) => wmoCode switch
    {
        0 or 1 => isDay ? "ClearDay" : "ClearNight",
        2 => isDay ? "PartlyCloudyDay" : "Cloudy",
        3 => "Cloudy",
        45 or 48 => "Fog",
        51 or 53 or 55 or 56 or 57 => "Rain",
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "Rain",
        71 or 73 or 75 or 77 or 85 or 86 => "Snow",
        95 or 96 or 99 => "Thunderstorm",
        _ => isDay ? "ClearDay" : "ClearNight",
    };

    public static string UsAqiCategory(int aqi) => AppLocalization.Get(aqi switch
    {
        <= 50 => "aqi.good",
        <= 100 => "aqi.moderate",
        <= 150 => "aqi.unhealthySensitive",
        <= 200 => "aqi.unhealthy",
        <= 300 => "aqi.veryUnhealthy",
        _ => "aqi.hazardous",
    });
}
