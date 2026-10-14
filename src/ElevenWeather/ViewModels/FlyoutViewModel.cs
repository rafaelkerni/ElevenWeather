using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using ElevenWeather.Converters;
using ElevenWeather.Models;
using ElevenWeather.Services;

namespace ElevenWeather.ViewModels;

public enum ForecastTab
{
    Hourly,
    Daily,
}

/// <summary>ViewModel for the card that opens when the widget is clicked — mirrors the original weather flyout.</summary>
public sealed class FlyoutViewModel : ObservableObject
{
    private static readonly string[] WeekdayKeys =
    {
        "weekday.sun", "weekday.mon", "weekday.tue", "weekday.wed", "weekday.thu", "weekday.fri", "weekday.sat",
    };

    private readonly WeatherStore _store;
    private readonly ILocationService _locationService;
    private readonly IMsnIconInstaller _msnIcons;
    private readonly IUpdateService? _updates;
    private bool _isCheckingForUpdates;

    private const int RowsPerPage = 5;

    private bool _isLocationPanelOpen;
    private string _citySearchQuery = string.Empty;
    private IReadOnlyList<GeocodingResult> _citySearchResults = Array.Empty<GeocodingResult>();
    private bool _isSearchingCity;
    private readonly IUiTimer _citySearchDebounceTimer;
    private ForecastTab _selectedTab = ForecastTab.Hourly;
    private int _currentPage;

    public FlyoutViewModel(WeatherStore store, ILocationService locationService, IUiTimer citySearchDebounceTimer,
                           IMsnIconInstaller msnIcons, IUpdateService? updates = null)
    {
        _store = store;
        _locationService = locationService;
        _msnIcons = msnIcons;
        _updates = updates;
        _store.Updated += OnStoreUpdated;
        AppLocalization.Instance.LanguageChanged += OnLanguageChanged;

        _citySearchDebounceTimer = citySearchDebounceTimer;
        _citySearchDebounceTimer.Interval = TimeSpan.FromMilliseconds(400);
        _citySearchDebounceTimer.Tick += async () =>
        {
            _citySearchDebounceTimer.Stop();
            await SearchCityAsync();
        };

        RefreshCommand = new RelayCommand(async () => await _store.RefreshAsync());
        OpenForecastCommand = new RelayCommand(OpenForecastInBrowser);
        ToggleLocationPanelCommand = new RelayCommand(() => IsLocationPanelOpen = !IsLocationPanelOpen);
        SearchCityCommand = new RelayCommand(async () => await SearchCityAsync());
        CheckForUpdatesCommand = new RelayCommand(async () => await CheckForUpdatesAsync());
        ExitCommand = new RelayCommand(() => System.Windows.Application.Current.Shutdown());
    }

    public string CityName => _store.Settings.CityName ?? AppLocalization.Get("ui.currentLocation");

    public string TemperatureText => _store.Current is { } s ? $"{s.TemperatureRounded}°" : "--°";

    public string DescriptionText => _store.Current?.Description ?? AppLocalization.Get("ui.loading");

    public string IconKey => _store.Current?.IconKey ?? "ClearDay";

    public bool HasAqi => _store.Current?.UsAqi is not null;

    public string AqiCategoryText => _store.Current?.AqiCategory ?? "";

    public ForecastTab SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                _currentPage = 0;
                OnPropertyChanged(nameof(IsHourlyTabActive));
                OnPropertyChanged(nameof(IsDailyTabActive));
                NotifyForecastChanged();
            }
        }
    }

    public bool IsHourlyTabActive => SelectedTab == ForecastTab.Hourly;

    public bool IsDailyTabActive => SelectedTab == ForecastTab.Daily;

    private IReadOnlyList<ForecastRow> ForecastRows =>
        SelectedTab == ForecastTab.Hourly ? BuildHourlyRows() : BuildDailyRows();

    /// <summary>Always exactly 5 slots — items swap content, not position, when paging.</summary>
    public IReadOnlyList<ForecastRow> VisiblePageRows =>
        ForecastRows.Skip(_currentPage * RowsPerPage).Take(RowsPerPage).ToList();

    public bool CanGoToPreviousPage => _currentPage > 0;

    public bool CanGoToNextPage => (_currentPage + 1) * RowsPerPage < ForecastRows.Count;

    public void GoToPreviousPage()
    {
        if (!CanGoToPreviousPage) return;
        _currentPage--;
        NotifyForecastChanged();
    }

    public void GoToNextPage()
    {
        if (!CanGoToNextPage) return;
        _currentPage++;
        NotifyForecastChanged();
    }

    private void NotifyForecastChanged()
    {
        OnPropertyChanged(nameof(VisiblePageRows));
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
    }

    public bool IsLocationPanelOpen
    {
        get => _isLocationPanelOpen;
        set => SetProperty(ref _isLocationPanelOpen, value);
    }

    public string CitySearchQuery
    {
        get => _citySearchQuery;
        set
        {
            if (!SetProperty(ref _citySearchQuery, value)) return;
            OnPropertyChanged(nameof(HasNoCityResults));

            _citySearchDebounceTimer.Stop();
            if (string.IsNullOrWhiteSpace(value))
            {
                CitySearchResults = Array.Empty<GeocodingResult>();
                IsSearchingCity = false;
            }
            else
            {
                _citySearchDebounceTimer.Start();
            }
        }
    }

    public IReadOnlyList<GeocodingResult> CitySearchResults
    {
        get => _citySearchResults;
        private set
        {
            if (SetProperty(ref _citySearchResults, value))
                OnPropertyChanged(nameof(HasNoCityResults));
        }
    }

    public bool IsSearchingCity
    {
        get => _isSearchingCity;
        private set
        {
            if (SetProperty(ref _isSearchingCity, value))
                OnPropertyChanged(nameof(HasNoCityResults));
        }
    }

    /// <summary>Shows "no city found" only after a search has completed with no results.</summary>
    public bool HasNoCityResults =>
        !IsSearchingCity && CitySearchResults.Count == 0 && !string.IsNullOrWhiteSpace(CitySearchQuery);

    public bool AutoStartEnabled
    {
        get => _store.IsAutoStartEnabled;
        set
        {
            _store.SetAutoStart(value);
            OnPropertyChanged();
        }
    }

    // All three update together on every language switch (see OnLanguageChanged),
    // so they behave as an exclusive group even without a GroupName on the MenuItem.
    public bool IsPortuguese
    {
        get => AppLocalization.Instance.Current == AppLanguage.Portuguese;
        set { if (value) _store.SetLanguage(AppLanguage.Portuguese); }
    }

    public bool IsEnglish
    {
        get => AppLocalization.Instance.Current == AppLanguage.English;
        set { if (value) _store.SetLanguage(AppLanguage.English); }
    }

    public bool IsSpanish
    {
        get => AppLocalization.Instance.Current == AppLanguage.Spanish;
        set { if (value) _store.SetLanguage(AppLanguage.Spanish); }
    }

    public bool IsCelsius
    {
        get => TemperaturePreference.Instance.Current == TemperatureUnit.Celsius;
        set { if (value) _store.SetTemperatureUnit(TemperatureUnit.Celsius); }
    }

    public bool IsFahrenheit
    {
        get => TemperaturePreference.Instance.Current == TemperatureUnit.Fahrenheit;
        set { if (value) _store.SetTemperatureUnit(TemperatureUnit.Fahrenheit); }
    }

    public bool IsTimeFormat24
    {
        get => TimeFormatPreference.Instance.Current == TimeFormat.TwentyFourHour;
        set { if (value) _store.SetTimeFormat(TimeFormat.TwentyFourHour); }
    }

    public bool IsTimeFormat12
    {
        get => TimeFormatPreference.Instance.Current == TimeFormat.TwelveHour;
        set { if (value) _store.SetTimeFormat(TimeFormat.TwelveHour); }
    }

    public bool IsMeteoconsIconSet
    {
        get => IconSetPreference.Instance.Current == IconSet.Meteocons;
        set { if (value) _store.SetIconSet(IconSet.Meteocons); }
    }

    public bool IsFluentEmojiIconSet
    {
        get => IconSetPreference.Instance.Current == IconSet.FluentEmoji;
        set { if (value) _store.SetIconSet(IconSet.FluentEmoji); }
    }

    public bool IsMsnIconSet
    {
        get => IconSetPreference.Instance.Current == IconSet.Msn;
        set { if (value) _ = SelectMsnIconSetAsync(); }
    }

    /// <summary>Fired with a localized message when the MSN icons can't be copied from the Bing Weather app.</summary>
    public event Action<string>? MsnIconInstallFailed;

    public string VersionText
    {
        get
        {
            if (_isCheckingForUpdates) return AppLocalization.Get("menu.checkingForUpdates");
            var v = typeof(FlyoutViewModel).Assembly.GetName().Version;
            return string.Format(AppLocalization.Get("menu.version"), v is null ? "?" : $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}");
        }
    }

    public string? VersionTooltip => CanCheckForUpdates ? AppLocalization.Get("menu.checkForUpdates") : null;

    /// <summary>Sem serviço de atualização (build de debug) o item da versão fica só informativo.</summary>
    public bool CanCheckForUpdates => _updates is not null && !_isCheckingForUpdates;

    /// <summary>Fired with a localized message describing the result of a manual update check.</summary>
    public event Action<string, bool>? UpdateCheckCompleted;

    private async Task CheckForUpdatesAsync()
    {
        if (!CanCheckForUpdates) return;

        SetCheckingForUpdates(true);
        var result = await _updates!.TryUpdateAsync();
        SetCheckingForUpdates(false);

        var key = result switch
        {
            UpdateResult.UpToDate => "update.upToDate",
            UpdateResult.Installed => "update.installed",
            _ => "update.failed",
        };
        UpdateCheckCompleted?.Invoke(AppLocalization.Get(key), result == UpdateResult.Failed);
    }

    private void SetCheckingForUpdates(bool value)
    {
        _isCheckingForUpdates = value;
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(VersionTooltip));
        OnPropertyChanged(nameof(CanCheckForUpdates));
    }

    /// <summary>The MSN icons aren't bundled; the first selection copies them from the installed Bing Weather app.</summary>
    public string? MsnIconSetTooltip => _msnIcons.IsInstalled ? null : AppLocalization.Get("iconSet.msnUnavailableHint");

    private async Task SelectMsnIconSetAsync()
    {
        if (!_msnIcons.IsInstalled)
        {
            var result = await _msnIcons.InstallAsync();
            if (!result.Succeeded)
            {
                // Desfaz o check que o menu já aplicou
                OnPropertyChanged(nameof(IsMsnIconSet));
                MsnIconInstallFailed?.Invoke(BuildMsnInstallFailureMessage(result));
                return;
            }
        }

        OnPropertyChanged(nameof(MsnIconSetTooltip));
        _store.SetIconSet(IconSet.Msn);
    }

    internal static string BuildMsnInstallFailureMessage(MsnIconInstallResult result)
    {
        var key = result.Status switch
        {
            MsnIconInstallStatus.NotInstalled => "iconSet.msnNotInstalled",
            MsnIconInstallStatus.UnexpectedLayout => "iconSet.msnUnexpectedLayout",
            _ => "iconSet.msnCopyFailed",
        };
        return string.Format(AppLocalization.Get(key), result.PackageVersion ?? "?");
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenForecastCommand { get; }
    public ICommand ToggleLocationPanelCommand { get; }
    public ICommand SearchCityCommand { get; }
    public ICommand CheckForUpdatesCommand { get; }

    public ICommand ExitCommand { get; }

    public async Task SelectCityAsync(GeocodingResult city)
    {
        _citySearchDebounceTimer.Stop();
        await _store.SetLocationAsync(city);
        IsLocationPanelOpen = false;
        CitySearchQuery = string.Empty;
        CitySearchResults = Array.Empty<GeocodingResult>();
    }

    private async Task SearchCityAsync()
    {
        IsSearchingCity = true;
        try
        {
            CitySearchResults = await _locationService.SearchCityAsync(CitySearchQuery);
        }
        finally
        {
            IsSearchingCity = false;
        }
    }

    private List<ForecastRow> BuildHourlyRows()
    {
        var list = new List<ForecastRow>();
        if (_store.Current is null) return list;

        foreach (var hour in _store.Current.Hourly)
        {
            list.Add(new ForecastRow(
                TimeFormatPreference.Instance.Format(hour.Time),
                WeatherCondition.IconKey(hour.WeatherCode, hour.IsDay),
                $"{TemperaturePreference.Instance.Round(hour.TemperatureC)}°",
                $"{hour.PrecipitationProbability}%"));
        }
        return list;
    }

    private List<ForecastRow> BuildDailyRows()
    {
        var list = new List<ForecastRow>();
        if (_store.Current is null) return list;

        var today = DateOnly.FromDateTime(DateTime.Now);
        foreach (var day in _store.Current.Daily)
        {
            string label = day.Date == today
                ? AppLocalization.Get("ui.today")
                : AppLocalization.Get(WeekdayKeys[(int)day.Date.DayOfWeek]);
            list.Add(new ForecastRow(
                label,
                WeatherCondition.IconKey(day.WeatherCode, isDay: true),
                $"{TemperaturePreference.Instance.Round(day.TempMaxC)}°",
                $"{TemperaturePreference.Instance.Round(day.TempMinC)}°"));
        }
        return list;
    }

    internal static string BuildForecastUrl(AppLanguage language)
    {
        string locale = language switch
        {
            AppLanguage.Portuguese => "pt-br",
            AppLanguage.Spanish => "es-es",
            _ => "en-us",
        };
        return $"https://www.msn.com/{locale}/weather/forecast";
    }

    private static void OpenForecastInBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo(BuildForecastUrl(AppLocalization.Instance.Current)) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No default browser configured: silently ignore, not critical.
        }
    }

    private void OnStoreUpdated()
    {
        OnPropertyChanged(nameof(CityName));
        OnPropertyChanged(nameof(TemperatureText));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(IconKey));
        OnPropertyChanged(nameof(HasAqi));
        OnPropertyChanged(nameof(AqiCategoryText));
        OnPropertyChanged(nameof(IsCelsius));
        OnPropertyChanged(nameof(IsFahrenheit));
        OnPropertyChanged(nameof(IsMeteoconsIconSet));
        OnPropertyChanged(nameof(IsFluentEmojiIconSet));
        OnPropertyChanged(nameof(IsMsnIconSet));
        OnPropertyChanged(nameof(IsTimeFormat24));
        OnPropertyChanged(nameof(IsTimeFormat12));

        int maxPage = Math.Max(0, (ForecastRows.Count - 1) / RowsPerPage);
        if (_currentPage > maxPage)
            _currentPage = maxPage;
        NotifyForecastChanged();
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(CityName));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(AqiCategoryText));
        OnPropertyChanged(nameof(IsPortuguese));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(IsSpanish));
        OnPropertyChanged(nameof(MsnIconSetTooltip));
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(VersionTooltip));
        NotifyForecastChanged();
    }
}
