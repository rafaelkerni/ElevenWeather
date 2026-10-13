namespace ElevenWeather.Models;

public sealed record DailyForecastItem(
    DateOnly Date,
    int WeatherCode,
    double TempMaxC,
    double TempMinC);

public sealed record HourlyForecastItem(
    DateTime Time,
    int WeatherCode,
    double TemperatureC,
    int PrecipitationProbability,
    bool IsDay);

public sealed record WeatherSnapshot(
    string CityName,
    double TemperatureC,
    double ApparentTemperatureC,
    int WeatherCode,
    bool IsDay,
    int? UsAqi,
    IReadOnlyList<DailyForecastItem> Daily,
    IReadOnlyList<HourlyForecastItem> Hourly,
    DateTime FetchedAtUtc,
    string? ObservationStation = null)
{
    public string Description => WeatherCondition.Describe(WeatherCode);

    public string IconKey => WeatherCondition.IconKey(WeatherCode, IsDay);

    public string? AqiCategory => UsAqi.HasValue ? WeatherCondition.UsAqiCategory(UsAqi.Value) : null;

    public int TemperatureRounded => TemperaturePreference.Instance.Round(TemperatureC);
}
