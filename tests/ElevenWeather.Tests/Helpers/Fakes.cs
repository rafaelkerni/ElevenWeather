using ElevenWeather.Models;
using ElevenWeather.Services;

namespace ElevenWeather.Tests.Helpers;

/// <summary>Timer manual: o teste decide quando ele "dispara".</summary>
public sealed class FakeUiTimer : IUiTimer
{
    public TimeSpan Interval { get; set; }
    public bool IsRunning { get; private set; }

    public event Action? Tick;

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    public void Fire() => Tick?.Invoke();
}

public sealed class FakeWeatherService : IWeatherService
{
    public Func<double, double, string, CancellationToken, Task<WeatherSnapshot?>> Handler { get; set; } =
        (_, _, city, _) => Task.FromResult<WeatherSnapshot?>(TestData.Snapshot(city));

    public List<(double Lat, double Lon, string City)> Calls { get; } = new();

    public WeatherSnapshot? LastKnownGood { get; set; }

    public Task<WeatherSnapshot?> FetchAsync(double lat, double lon, string cityName, CancellationToken ct = default)
    {
        Calls.Add((lat, lon, cityName));
        return Handler(lat, lon, cityName, ct);
    }
}

public sealed class FakeLocationService : ILocationService
{
    public (double Lat, double Lon, string City)? DetectResult { get; set; }
    public int DetectCalls { get; private set; }

    public Func<string, Task<IReadOnlyList<GeocodingResult>>> SearchHandler { get; set; } =
        _ => Task.FromResult<IReadOnlyList<GeocodingResult>>(Array.Empty<GeocodingResult>());

    public List<string> SearchQueries { get; } = new();

    public Task<(double Lat, double Lon, string City)?> DetectByIpAsync(CancellationToken ct = default)
    {
        DetectCalls++;
        return Task.FromResult(DetectResult);
    }

    public Task<IReadOnlyList<GeocodingResult>> SearchCityAsync(string query, CancellationToken ct = default)
    {
        SearchQueries.Add(query);
        return SearchHandler(query);
    }
}

public sealed class InMemorySettingsService : ISettingsService
{
    public InMemorySettingsService(AppSettings? initial = null) => Stored = initial ?? new AppSettings();

    public AppSettings Stored { get; }
    public int SaveCount { get; private set; }

    public AppSettings Load() => Stored;

    public void Save(AppSettings settings) => SaveCount++;
}

public sealed class FakeAutoStartService : IAutoStartService
{
    public bool Enabled { get; private set; }
    public List<bool> SetCalls { get; } = new();

    public bool IsEnabled() => Enabled;

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        SetCalls.Add(enabled);
    }
}

public static class TestData
{
    public static WeatherSnapshot Snapshot(
        string city = "Test City",
        double temp = 20,
        int weatherCode = 0,
        bool isDay = true,
        int? aqi = null,
        int hourlyCount = 0,
        int dailyCount = 0,
        DateTime? firstHour = null,
        DateOnly? firstDay = null)
    {
        var startHour = firstHour ?? new DateTime(2026, 10, 13, 9, 0, 0);
        var startDay = firstDay ?? DateOnly.FromDateTime(DateTime.Now);

        var hourly = Enumerable.Range(0, hourlyCount)
            .Select(i => new HourlyForecastItem(startHour.AddHours(i), 61, 10 + i, i * 10 % 100, true))
            .ToList();
        var daily = Enumerable.Range(0, dailyCount)
            .Select(i => new DailyForecastItem(startDay.AddDays(i), 3, 25 + i, 15 + i))
            .ToList();

        return new WeatherSnapshot(city, temp, temp - 2, weatherCode, isDay, aqi, daily, hourly, DateTime.UtcNow);
    }

    /// <summary>Monta um WeatherStore com todas as dependências falsas.</summary>
    public static (WeatherStore Store, StoreFakes Fakes) CreateStore(AppSettings? settings = null)
    {
        var fakes = new StoreFakes(
            new FakeWeatherService(),
            new FakeLocationService(),
            new InMemorySettingsService(settings),
            new FakeAutoStartService(),
            new FakeUiTimer());
        var store = new WeatherStore(fakes.Weather, fakes.Location, fakes.Settings, fakes.AutoStart, fakes.Timer);
        return (store, fakes);
    }

    public static AppSettings WithLocation(string city = "Campinas") =>
        new() { Latitude = -22.9, Longitude = -47.06, CityName = city };
}

public sealed record StoreFakes(
    FakeWeatherService Weather,
    FakeLocationService Location,
    InMemorySettingsService Settings,
    FakeAutoStartService AutoStart,
    FakeUiTimer Timer);

public sealed class FakeMsnIconInstaller : IMsnIconInstaller
{
    public bool IsInstalled { get; set; }
    public MsnIconInstallResult Result { get; set; } = new(MsnIconInstallStatus.Success, "1.0.0.0");
    public int InstallCalls { get; private set; }

    public Task<MsnIconInstallResult> InstallAsync()
    {
        InstallCalls++;
        if (Result.Succeeded) IsInstalled = true;
        return Task.FromResult(Result);
    }
}

public sealed class FakeUpdateService : IUpdateService
{
    public UpdateResult Result { get; set; } = UpdateResult.UpToDate;
    public TaskCompletionSource? Gate { get; set; }
    public int Calls { get; private set; }

    public async Task<UpdateResult> TryUpdateAsync(CancellationToken ct = default)
    {
        Calls++;
        if (Gate is not null) await Gate.Task;
        return Result;
    }
}
