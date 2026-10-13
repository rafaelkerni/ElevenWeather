using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using ElevenWeather.Models;

namespace ElevenWeather.Services;

public sealed class WeatherService : IWeatherService
{
    private readonly HttpClient _http;
    private readonly MetarService _metarService;

    public WeatherService(HttpClient http, MetarService metarService)
    {
        _http = http;
        _metarService = metarService;
    }

    /// <summary>Last successful result; returned on network failure so the widget never ends up empty.</summary>
    public WeatherSnapshot? LastKnownGood { get; private set; }

    public async Task<WeatherSnapshot?> FetchAsync(double lat, double lon, string cityName, CancellationToken ct = default)
    {
        try
        {
            var weatherTask = FetchWeatherJsonAsync(lat, lon, ct);
            var aqiTask = FetchAqiAsync(lat, lon, ct);
            var metarTask = _metarService.FetchNearbyAsync(lat, lon, ct);
            await Task.WhenAll(weatherTask, aqiTask, metarTask);

            var weatherJson = await weatherTask;
            if (weatherJson is null)
                return LastKnownGood;

            var root = weatherJson.Value;
            var current = root.GetProperty("current");

            double temp = current.GetProperty("temperature_2m").GetDouble();
            double feelsLike = current.TryGetProperty("apparent_temperature", out var apEl) ? apEl.GetDouble() : temp;
            int code = current.GetProperty("weather_code").GetInt32();
            bool isDay = current.GetProperty("is_day").GetInt32() == 1;

            var daily = new List<DailyForecastItem>();
            if (root.TryGetProperty("daily", out var dailyEl))
            {
                var dates = dailyEl.GetProperty("time");
                var codes = dailyEl.GetProperty("weather_code");
                var maxT = dailyEl.GetProperty("temperature_2m_max");
                var minT = dailyEl.GetProperty("temperature_2m_min");
                int count = dates.GetArrayLength();
                for (int i = 0; i < count; i++)
                {
                    daily.Add(new DailyForecastItem(
                        DateOnly.Parse(dates[i].GetString()!, CultureInfo.InvariantCulture),
                        codes[i].GetInt32(),
                        maxT[i].GetDouble(),
                        minT[i].GetDouble()));
                }
            }

            var hourly = new List<HourlyForecastItem>();
            if (root.TryGetProperty("hourly", out var hourlyEl))
            {
                var times = hourlyEl.GetProperty("time");
                var codes = hourlyEl.GetProperty("weather_code");
                var temps = hourlyEl.GetProperty("temperature_2m");
                var precip = hourlyEl.GetProperty("precipitation_probability");
                var isDayArr = hourlyEl.GetProperty("is_day");
                int count = times.GetArrayLength();
                var currentHour = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0);

                for (int i = 0; i < count; i++)
                {
                    var time = DateTime.Parse(times[i].GetString()!, CultureInfo.InvariantCulture);
                    if (time < currentHour) continue;

                    hourly.Add(new HourlyForecastItem(
                        time,
                        codes[i].GetInt32(),
                        temps[i].GetDouble(),
                        precip[i].GetInt32(),
                        isDayArr[i].GetInt32() == 1));

                    if (hourly.Count >= 24) break;
                }
            }

            int? aqi = await aqiTask;

            // Prefer a real station reading over the model, keeping the model's wind/humidity
            // offset by shifting "feels like" along with the air temperature.
            string? station = null;
            double siteElevation = root.TryGetProperty("elevation", out var elevEl) && elevEl.ValueKind == JsonValueKind.Number
                ? elevEl.GetDouble()
                : double.NaN;
            if (MetarService.SelectBest(await metarTask, siteElevation) is { } observed)
            {
                feelsLike += observed.TemperatureC - temp;
                temp = observed.TemperatureC;
                station = observed.StationName;
            }

            var snapshot = new WeatherSnapshot(cityName, temp, feelsLike, code, isDay, aqi, daily, hourly, DateTime.UtcNow, station);
            LastKnownGood = snapshot;
            return snapshot;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // A caller-requested cancellation (e.g. the user switched city) isn't a fetch failure — let it propagate.
            throw;
        }
        catch (Exception ex) when (FetchErrors.IsRecoverable(ex))
        {
            return LastKnownGood;
        }
    }

    private async Task<JsonElement?> FetchWeatherJsonAsync(double lat, double lon, CancellationToken ct)
    {
        string latStr = lat.ToString(CultureInfo.InvariantCulture);
        string lonStr = lon.ToString(CultureInfo.InvariantCulture);
        var url = "https://api.open-meteo.com/v1/forecast" +
                   $"?latitude={latStr}&longitude={lonStr}" +
                   "&current=temperature_2m,weather_code,is_day,apparent_temperature" +
                   "&daily=weather_code,temperature_2m_max,temperature_2m_min" +
                   "&hourly=temperature_2m,weather_code,precipitation_probability,is_day" +
                   "&timezone=auto&forecast_days=10";

        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    private async Task<int?> FetchAqiAsync(double lat, double lon, CancellationToken ct)
    {
        try
        {
            string latStr = lat.ToString(CultureInfo.InvariantCulture);
            string lonStr = lon.ToString(CultureInfo.InvariantCulture);
            var url = $"https://air-quality-api.open-meteo.com/v1/air-quality?latitude={latStr}&longitude={lonStr}&current=us_aqi";

            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var current = doc.RootElement.GetProperty("current");
            return current.TryGetProperty("us_aqi", out var aqiEl) && aqiEl.ValueKind == JsonValueKind.Number
                ? aqiEl.GetInt32()
                : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (FetchErrors.IsRecoverable(ex))
        {
            return null;
        }
    }
}
