using ElevenWeather.Models;
using ElevenWeather.Services;
using ElevenWeather.Tests.Helpers;
using ElevenWeather.ViewModels;

namespace ElevenWeather.Tests.ViewModels;

public sealed class FlyoutViewModelTests : PreferenceScope
{
    private readonly WeatherStore _store;
    private readonly StoreFakes _fakes;
    private readonly FakeLocationService _searchService = new();
    private readonly FakeUiTimer _debounce = new();
    private readonly FakeMsnIconInstaller _msnIcons = new();
    private readonly FlyoutViewModel _vm;
    private readonly List<string?> _changed = new();

    public FlyoutViewModelTests()
    {
        (_store, _fakes) = TestData.CreateStore(TestData.WithLocation("Campinas"));
        _vm = new FlyoutViewModel(_store, _searchService, _debounce, _msnIcons);
        _vm.PropertyChanged += (_, e) => _changed.Add(e.PropertyName);
    }

    private async Task LoadAsync(WeatherSnapshot snapshot)
    {
        _fakes.Weather.Handler = (_, _, _, _) => Task.FromResult<WeatherSnapshot?>(snapshot);
        await _store.RefreshAsync();
    }

    [Fact]
    public void BeforeFirstLoad_ShowsPlaceholders()
    {
        Assert.Equal("Campinas", _vm.CityName);
        Assert.Equal("--°", _vm.TemperatureText);
        Assert.Equal("Loading...", _vm.DescriptionText);
        Assert.Equal("ClearDay", _vm.IconKey);
        Assert.False(_vm.HasAqi);
        Assert.Equal("", _vm.AqiCategoryText);
        Assert.Empty(_vm.VisiblePageRows);
        Assert.False(_vm.CanGoToNextPage);
    }

    [Fact]
    public void CityName_FallsBackToLocalizedPlaceholder()
    {
        var (store, _) = TestData.CreateStore();
        var vm = new FlyoutViewModel(store, _searchService, new FakeUiTimer(), _msnIcons);

        Assert.Equal("Current location", vm.CityName);
    }

    [Fact]
    public async Task AfterLoad_ShowsCurrentConditions()
    {
        await LoadAsync(TestData.Snapshot(temp: 20.6, weatherCode: 61, isDay: false, aqi: 120));

        Assert.Equal("21°", _vm.TemperatureText);
        Assert.Equal("Rain", _vm.DescriptionText);
        Assert.Equal("Rain", _vm.IconKey);
        Assert.True(_vm.HasAqi);
        Assert.Equal("Unhealthy for sensitive groups", _vm.AqiCategoryText);
        Assert.Contains(nameof(FlyoutViewModel.TemperatureText), _changed);
        Assert.Contains(nameof(FlyoutViewModel.VisiblePageRows), _changed);
    }

    [Fact]
    public async Task HourlyRows_AreFormattedWithCurrentPreferences()
    {
        await LoadAsync(TestData.Snapshot(hourlyCount: 3, firstHour: new DateTime(2026, 10, 13, 14, 0, 0)));

        Assert.Equal(new ForecastRow("14:00", "Rain", "10°", "0%"), _vm.VisiblePageRows[0]);

        _store.SetTimeFormat(TimeFormat.TwelveHour);
        _store.SetTemperatureUnit(TemperatureUnit.Fahrenheit);

        Assert.Equal(new ForecastRow("2:00 PM", "Rain", "50°", "0%"), _vm.VisiblePageRows[0]);
        Assert.Equal("52°", _vm.VisiblePageRows[1].PrimaryText);
        Assert.Equal("10%", _vm.VisiblePageRows[1].SecondaryText);
    }

    [Fact]
    public async Task Paging_WalksThroughFiveRowPages()
    {
        await LoadAsync(TestData.Snapshot(hourlyCount: 24));

        Assert.Equal(5, _vm.VisiblePageRows.Count);
        Assert.False(_vm.CanGoToPreviousPage);
        Assert.True(_vm.CanGoToNextPage);

        for (int i = 0; i < 4; i++) _vm.GoToNextPage();

        Assert.Equal(4, _vm.VisiblePageRows.Count);
        Assert.Equal("05:00", _vm.VisiblePageRows[0].TopLabel); // 20ª hora a partir das 09:00
        Assert.False(_vm.CanGoToNextPage);
        Assert.True(_vm.CanGoToPreviousPage);

        _vm.GoToNextPage(); // além da última página: ignorado
        Assert.Equal(4, _vm.VisiblePageRows.Count);

        for (int i = 0; i < 10; i++) _vm.GoToPreviousPage();
        Assert.False(_vm.CanGoToPreviousPage);
        Assert.Equal("09:00", _vm.VisiblePageRows[0].TopLabel);
    }

    [Fact]
    public async Task SwitchingTab_ResetsPageAndShowsDailyRows()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        await LoadAsync(TestData.Snapshot(hourlyCount: 24, dailyCount: 10, firstDay: today));
        _vm.GoToNextPage();
        _changed.Clear();

        _vm.SelectedTab = ForecastTab.Daily;

        Assert.True(_vm.IsDailyTabActive);
        Assert.False(_vm.IsHourlyTabActive);
        Assert.False(_vm.CanGoToPreviousPage);
        Assert.Contains(nameof(FlyoutViewModel.IsDailyTabActive), _changed);

        var rows = _vm.VisiblePageRows;
        Assert.Equal(new ForecastRow("Today", "Cloudy", "25°", "15°"), rows[0]);
        var tomorrow = today.AddDays(1).DayOfWeek.ToString()[..3];
        Assert.Equal(tomorrow, rows[1].TopLabel);
    }

    [Fact]
    public void SelectingSameTab_DoesNotNotify()
    {
        _vm.SelectedTab = ForecastTab.Hourly;

        Assert.Empty(_changed);
    }

    [Fact]
    public async Task StoreUpdate_ClampsPageWhenForecastShrinks()
    {
        await LoadAsync(TestData.Snapshot(hourlyCount: 24));
        for (int i = 0; i < 4; i++) _vm.GoToNextPage();

        await LoadAsync(TestData.Snapshot(hourlyCount: 7));

        Assert.Equal(2, _vm.VisiblePageRows.Count);
        Assert.False(_vm.CanGoToNextPage);
        Assert.True(_vm.CanGoToPreviousPage);
    }

    [Fact]
    public void TypingCityQuery_DebouncesSearch()
    {
        _vm.CitySearchQuery = "camp";

        Assert.True(_debounce.IsRunning);
        Assert.Equal(TimeSpan.FromMilliseconds(400), _debounce.Interval);
        Assert.Empty(_searchService.SearchQueries);
    }

    [Fact]
    public void DebounceTick_RunsSearchAndShowsResults()
    {
        var campinas = new GeocodingResult("Campinas", "São Paulo", "Brasil", -22.9, -47.06);
        _searchService.SearchHandler = _ => Task.FromResult<IReadOnlyList<GeocodingResult>>(new[] { campinas });
        _vm.CitySearchQuery = "camp";

        _debounce.Fire();

        Assert.False(_debounce.IsRunning);
        Assert.Equal(new[] { "camp" }, _searchService.SearchQueries);
        Assert.Equal(new[] { campinas }, _vm.CitySearchResults);
        Assert.False(_vm.IsSearchingCity);
        Assert.False(_vm.HasNoCityResults);
    }

    [Fact]
    public void SearchWithoutResults_ShowsEmptyStateOnlyAfterCompletion()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<GeocodingResult>>();
        _searchService.SearchHandler = _ => pending.Task;
        _vm.CitySearchQuery = "zzzz";

        _debounce.Fire();
        Assert.True(_vm.IsSearchingCity);
        Assert.False(_vm.HasNoCityResults);

        pending.SetResult(Array.Empty<GeocodingResult>());
        Assert.False(_vm.IsSearchingCity);
        Assert.True(_vm.HasNoCityResults);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ClearingQuery_StopsDebounceAndClearsResults(string blank)
    {
        _searchService.SearchHandler = _ => Task.FromResult<IReadOnlyList<GeocodingResult>>(
            new[] { new GeocodingResult("X", null, "Y", 0, 0) });
        _vm.CitySearchQuery = "x";
        _debounce.Fire();

        _vm.CitySearchQuery = "xy";
        _vm.CitySearchQuery = blank;

        Assert.False(_debounce.IsRunning);
        Assert.Empty(_vm.CitySearchResults);
        Assert.False(_vm.HasNoCityResults);
    }

    [Fact]
    public async Task SelectCity_ChangesLocationAndResetsPanel()
    {
        _vm.IsLocationPanelOpen = true;
        _vm.CitySearchQuery = "lis";

        await _vm.SelectCityAsync(new GeocodingResult("Lisboa", null, "Portugal", 38.72, -9.14));

        Assert.Equal("Lisboa", _store.Settings.CityName);
        Assert.Equal((38.72, -9.14, "Lisboa"), _fakes.Weather.Calls.Last());
        Assert.False(_vm.IsLocationPanelOpen);
        Assert.Equal("", _vm.CitySearchQuery);
        Assert.Empty(_vm.CitySearchResults);
        Assert.False(_debounce.IsRunning);
        Assert.Equal("Lisboa", _vm.CityName);
    }

    [Fact]
    public void Commands_ToggleLocationPanelAndRefresh()
    {
        _vm.ToggleLocationPanelCommand.Execute(null);
        Assert.True(_vm.IsLocationPanelOpen);
        _vm.ToggleLocationPanelCommand.Execute(null);
        Assert.False(_vm.IsLocationPanelOpen);

        _vm.RefreshCommand.Execute(null);
        Assert.Single(_fakes.Weather.Calls);
    }

    [Fact]
    public void SearchCityCommand_SearchesImmediately()
    {
        _vm.CitySearchQuery = "rio";

        _vm.SearchCityCommand.Execute(null);

        Assert.Equal(new[] { "rio" }, _searchService.SearchQueries);
    }

    [Fact]
    public void LanguageMenu_SwitchesAndReflectsLanguage()
    {
        _vm.IsPortuguese = true;
        Assert.Equal(AppLanguage.Portuguese, _store.Settings.Language);
        Assert.True(_vm.IsPortuguese);
        Assert.False(_vm.IsEnglish);
        Assert.Contains(nameof(FlyoutViewModel.IsEnglish), _changed);

        _vm.IsSpanish = true;
        Assert.True(_vm.IsSpanish);

        _vm.IsEnglish = true;
        Assert.True(_vm.IsEnglish);
    }

    [Fact]
    public void MenuSetters_IgnoreUncheck()
    {
        _vm.IsEnglish = false;
        _vm.IsCelsius = false;
        _vm.IsTimeFormat24 = false;
        _vm.IsMeteoconsIconSet = false;

        Assert.Equal(0, _fakes.Settings.SaveCount);
    }

    [Fact]
    public void UnitTimeAndIconMenus_UpdateStore()
    {
        _vm.IsFahrenheit = true;
        Assert.True(_vm.IsFahrenheit);
        Assert.False(_vm.IsCelsius);

        _vm.IsTimeFormat12 = true;
        Assert.True(_vm.IsTimeFormat12);
        Assert.False(_vm.IsTimeFormat24);

        _vm.IsFluentEmojiIconSet = true;
        Assert.True(_vm.IsFluentEmojiIconSet);
        Assert.False(_vm.IsMeteoconsIconSet);
        Assert.False(_vm.IsMsnIconSet);

        _vm.IsCelsius = true;
        _vm.IsTimeFormat24 = true;
        _vm.IsMeteoconsIconSet = true;
        Assert.True(_vm.IsCelsius && _vm.IsTimeFormat24 && _vm.IsMeteoconsIconSet);
        Assert.Equal(6, _fakes.Settings.SaveCount);
    }

    [Fact]
    public void AutoStartMenu_ReadsAndWritesThroughStore()
    {
        Assert.False(_vm.AutoStartEnabled);

        _vm.AutoStartEnabled = true;

        Assert.True(_vm.AutoStartEnabled);
        Assert.Equal(new[] { true }, _fakes.AutoStart.SetCalls);
        Assert.Contains(nameof(FlyoutViewModel.AutoStartEnabled), _changed);
    }

    [Fact]
    public void MsnTooltip_OnlyWhenIconsAreNotInstalled()
    {
        Assert.NotNull(_vm.MsnIconSetTooltip);

        _msnIcons.IsInstalled = true;

        Assert.Null(_vm.MsnIconSetTooltip);
    }

    [Fact]
    public async Task SelectingMsn_CopiesIconsOnFirstUseThenApplies()
    {
        _vm.IsMsnIconSet = true;
        await Task.Yield();

        Assert.Equal(1, _msnIcons.InstallCalls);
        Assert.True(_vm.IsMsnIconSet);
        Assert.Equal(IconSet.Msn, IconSetPreference.Instance.Current);
        Assert.Contains(nameof(FlyoutViewModel.MsnIconSetTooltip), _changed);
    }

    [Fact]
    public async Task SelectingMsn_SkipsCopyWhenAlreadyInstalled()
    {
        _msnIcons.IsInstalled = true;

        _vm.IsMsnIconSet = true;
        await Task.Yield();

        Assert.Equal(0, _msnIcons.InstallCalls);
        Assert.True(_vm.IsMsnIconSet);
    }

    [Theory]
    [InlineData(MsnIconInstallStatus.NotInstalled, "isn't installed")]
    [InlineData(MsnIconInstallStatus.UnexpectedLayout, "version 4.54.1.0")]
    [InlineData(MsnIconInstallStatus.CopyFailed, "version 4.54.1.0")]
    public async Task SelectingMsn_WhenCopyFails_KeepsCurrentSetAndExplainsWhy(MsnIconInstallStatus status, string expectedText)
    {
        _msnIcons.Result = new MsnIconInstallResult(status, "4.54.1.0");
        var messages = new List<string>();
        _vm.MsnIconInstallFailed += messages.Add;

        _vm.IsMsnIconSet = true;
        await Task.Yield();

        var message = Assert.Single(messages);
        Assert.Contains(expectedText, message);
        Assert.False(_vm.IsMsnIconSet);
        Assert.NotEqual(IconSet.Msn, IconSetPreference.Instance.Current);
        Assert.Contains(nameof(FlyoutViewModel.IsMsnIconSet), _changed);
    }

    [Fact]
    public void UnexpectedLayoutMessage_PointsToTheIssueTracker()
    {
        var message = FlyoutViewModel.BuildMsnInstallFailureMessage(
            new MsnIconInstallResult(MsnIconInstallStatus.UnexpectedLayout, "4.54.1.0"));

        Assert.Contains("github.com/rafaelkerni/ElevenWeather/issues", message);
    }

    [Fact]
    public void FailureMessage_UsesPlaceholderWhenVersionIsUnknown()
    {
        var message = FlyoutViewModel.BuildMsnInstallFailureMessage(
            new MsnIconInstallResult(MsnIconInstallStatus.CopyFailed));

        Assert.Contains("version ?", message);
    }

    [Fact]
    public async Task LanguageSwitch_RefreshesTranslatedTexts()
    {
        await LoadAsync(TestData.Snapshot(weatherCode: 0, dailyCount: 1));
        _changed.Clear();

        AppLocalization.Instance.Current = AppLanguage.Portuguese;

        Assert.Equal("Limpo", _vm.DescriptionText);
        Assert.Contains(nameof(FlyoutViewModel.DescriptionText), _changed);
        Assert.Contains(nameof(FlyoutViewModel.VisiblePageRows), _changed);
        _vm.SelectedTab = ForecastTab.Daily;
        Assert.Equal("Hoje", _vm.VisiblePageRows[0].TopLabel);
    }

    [Theory]
    [InlineData(AppLanguage.Portuguese, "https://www.msn.com/pt-br/weather/forecast")]
    [InlineData(AppLanguage.English, "https://www.msn.com/en-us/weather/forecast")]
    [InlineData(AppLanguage.Spanish, "https://www.msn.com/es-es/weather/forecast")]
    public void ForecastUrl_MatchesLanguage(AppLanguage language, string expected)
    {
        Assert.Equal(expected, FlyoutViewModel.BuildForecastUrl(language));
    }
}
