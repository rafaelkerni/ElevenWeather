using System.Diagnostics;
using ElevenWeather.Models;

namespace ElevenWeather.Services;

/// <summary>
/// Central weather state: configured location, latest reading, and refresh scheduling.
/// Shared between the taskbar widget and the flyout so network calls aren't duplicated.
/// </summary>
public sealed class WeatherStore
{
    private static readonly TimeSpan NormalInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(30);

    private readonly IWeatherService _weatherService;
    private readonly ILocationService _locationService;
    private readonly ISettingsService _settingsService;
    private readonly IAutoStartService _autoStartService;
    private readonly IUiTimer _refreshTimer;

    private TimeSpan _currentBackoff = MinBackoff;
    private CancellationTokenSource? _refreshCts;

    public AppSettings Settings { get; private set; }
    public WeatherSnapshot? Current { get; private set; }
    public bool IsLoading { get; private set; }

    /// <summary>Fired after any refresh attempt, whether it succeeded or not.</summary>
    public event Action? Updated;

    public WeatherStore(
        IWeatherService weatherService,
        ILocationService locationService,
        ISettingsService settingsService,
        IAutoStartService autoStartService,
        IUiTimer refreshTimer)
    {
        _weatherService = weatherService;
        _locationService = locationService;
        _settingsService = settingsService;
        _autoStartService = autoStartService;
        _refreshTimer = refreshTimer;
        Settings = settingsService.Load();
        if (Settings.Language is { } savedLanguage)
            AppLocalization.Instance.Current = savedLanguage;
        TemperaturePreference.Instance.Current = Settings.TemperatureUnit;
        IconSetPreference.Instance.Current = Settings.IconSet;
        TimeFormatPreference.Instance.Current = Settings.TimeFormat;

        _refreshTimer.Tick += async () =>
        {
            try
            {
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                // A timer tick must never crash the whole app; the next tick simply tries again.
                Debug.WriteLine($"Scheduled refresh failed: {ex}");
            }
        };
    }

    public async Task InitializeAsync() => await RefreshAsync();

    /// <summary>
    /// Cancels any refresh already in flight before starting a new one, so a stale response
    /// (e.g. from before the user switched city) can never overwrite a newer one.
    /// </summary>
    public async Task RefreshAsync()
    {
        // Registra a nova antes de cancelar a antiga, para que a antiga já se veja como obsoleta
        var previous = _refreshCts;
        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        previous?.Cancel();
        var ct = cts.Token;

        IsLoading = true;
        try
        {
            if (!Settings.HasLocation)
            {
                var detected = await _locationService.DetectByIpAsync(ct);
                if (detected is { } loc)
                {
                    Settings.Latitude = loc.Lat;
                    Settings.Longitude = loc.Lon;
                    Settings.CityName = string.IsNullOrWhiteSpace(loc.City) ? AppLocalization.Get("ui.currentLocation") : loc.City;
                    _settingsService.Save(Settings);
                }
                else
                {
                    BackoffAndReschedule();
                    return;
                }
            }

            var snapshot = await _weatherService.FetchAsync(
                Settings.Latitude!.Value, Settings.Longitude!.Value, Settings.CityName ?? AppLocalization.Get("ui.currentLocation"), ct);

            if (snapshot is not null)
            {
                Current = snapshot;
                _currentBackoff = MinBackoff;
                ScheduleNext(NormalInterval);
            }
            else
            {
                BackoffAndReschedule();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Superseded by a newer refresh; that one owns reporting IsLoading/Updated below.
        }
        finally
        {
            if (ReferenceEquals(_refreshCts, cts))
            {
                IsLoading = false;
                Updated?.Invoke();
            }
        }
    }

    public bool IsAutoStartEnabled => _autoStartService.IsEnabled();

    /// <summary>Reaplica a escolha salva na inicialização, caso o executável tenha mudado de lugar.</summary>
    public void SyncAutoStart()
    {
        if (Settings.AutoStartPromptShown)
            _autoStartService.SetEnabled(Settings.AutoStart);
    }

    public void SetAutoStart(bool enabled)
    {
        Settings.AutoStart = enabled;
        _settingsService.Save(Settings);
        _autoStartService.SetEnabled(enabled);
    }

    /// <summary>Records the user's answer to the one-time "start with Windows?" prompt shown on first run.</summary>
    public void HandleFirstRunAutoStartChoice(bool enabled)
    {
        Settings.AutoStart = enabled;
        Settings.AutoStartPromptShown = true;
        _settingsService.Save(Settings);
        _autoStartService.SetEnabled(enabled);
    }

    /// <summary>Records that the user declined the "turn off Windows Widgets" prompt, so it isn't shown again.</summary>
    public void DismissWidgetsPrompt()
    {
        Settings.WidgetsPromptDismissed = true;
        _settingsService.Save(Settings);
    }

    public void SetLanguage(AppLanguage language)
    {
        Settings.Language = language;
        _settingsService.Save(Settings);
        AppLocalization.Instance.Current = language;
    }

    public void SetTemperatureUnit(TemperatureUnit unit)
    {
        Settings.TemperatureUnit = unit;
        _settingsService.Save(Settings);
        TemperaturePreference.Instance.Current = unit;
        Updated?.Invoke();
    }

    public void SetIconSet(IconSet iconSet)
    {
        Settings.IconSet = iconSet;
        _settingsService.Save(Settings);
        IconSetPreference.Instance.Current = iconSet;
        Updated?.Invoke();
    }

    public void SetTimeFormat(TimeFormat format)
    {
        Settings.TimeFormat = format;
        _settingsService.Save(Settings);
        TimeFormatPreference.Instance.Current = format;
        Updated?.Invoke();
    }

    public async Task SetLocationAsync(GeocodingResult location)
    {
        Settings.Latitude = location.Latitude;
        Settings.Longitude = location.Longitude;
        Settings.CityName = location.Name;
        _settingsService.Save(Settings);
        await RefreshAsync();
    }

    private void BackoffAndReschedule()
    {
        _currentBackoff = TimeSpan.FromSeconds(Math.Min(_currentBackoff.TotalSeconds * 2, NormalInterval.TotalSeconds));
        ScheduleNext(_currentBackoff);
    }

    private void ScheduleNext(TimeSpan interval)
    {
        _refreshTimer.Stop();
        _refreshTimer.Interval = interval;
        _refreshTimer.Start();
    }
}
