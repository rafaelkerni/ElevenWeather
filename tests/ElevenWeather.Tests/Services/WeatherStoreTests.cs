using ElevenWeather.Models;
using ElevenWeather.Tests.Helpers;

namespace ElevenWeather.Tests.Services;

public sealed class WeatherStoreTests : PreferenceScope
{
    private static readonly TimeSpan NormalInterval = TimeSpan.FromMinutes(15);

    [Fact]
    public void Constructor_AppliesSavedPreferences()
    {
        var settings = new AppSettings
        {
            Language = AppLanguage.Spanish,
            TemperatureUnit = TemperatureUnit.Fahrenheit,
            IconSet = IconSet.FluentEmoji,
            TimeFormat = TimeFormat.TwelveHour,
        };

        TestData.CreateStore(settings);

        Assert.Equal(AppLanguage.Spanish, AppLocalization.Instance.Current);
        Assert.Equal(TemperatureUnit.Fahrenheit, TemperaturePreference.Instance.Current);
        Assert.Equal(IconSet.FluentEmoji, IconSetPreference.Instance.Current);
        Assert.Equal(TimeFormat.TwelveHour, TimeFormatPreference.Instance.Current);
    }

    [Fact]
    public void Constructor_KeepsSystemLanguageWhenNoneSaved()
    {
        AppLocalization.Instance.Current = AppLanguage.Portuguese;

        TestData.CreateStore(new AppSettings { Language = null });

        Assert.Equal(AppLanguage.Portuguese, AppLocalization.Instance.Current);
    }

    [Fact]
    public async Task Refresh_WithSavedLocation_FetchesAndSchedulesNormalInterval()
    {
        var (store, fakes) = TestData.CreateStore(TestData.WithLocation("Campinas"));
        int updated = 0;
        store.Updated += () => updated++;

        await store.InitializeAsync();

        Assert.Equal((-22.9, -47.06, "Campinas"), fakes.Weather.Calls.Single());
        Assert.Equal(0, fakes.Location.DetectCalls);
        Assert.Equal("Campinas", store.Current?.CityName);
        Assert.False(store.IsLoading);
        Assert.Equal(1, updated);
        Assert.True(fakes.Timer.IsRunning);
        Assert.Equal(NormalInterval, fakes.Timer.Interval);
    }

    [Fact]
    public async Task Refresh_WithoutLocation_DetectsByIpAndPersists()
    {
        var (store, fakes) = TestData.CreateStore();
        fakes.Location.DetectResult = (-23.5, -46.6, "São Paulo");

        await store.RefreshAsync();

        Assert.Equal(-23.5, store.Settings.Latitude);
        Assert.Equal(-46.6, store.Settings.Longitude);
        Assert.Equal("São Paulo", store.Settings.CityName);
        Assert.Equal(1, fakes.Settings.SaveCount);
        Assert.Equal((-23.5, -46.6, "São Paulo"), fakes.Weather.Calls.Single());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Refresh_DetectedWithoutCity_UsesLocalizedPlaceholder(string city)
    {
        var (store, fakes) = TestData.CreateStore();
        fakes.Location.DetectResult = (1, 2, city);

        await store.RefreshAsync();

        Assert.Equal("Current location", store.Settings.CityName);
    }

    [Fact]
    public async Task Refresh_IpDetectionFails_BacksOffWithoutFetching()
    {
        var (store, fakes) = TestData.CreateStore();
        int updated = 0;
        store.Updated += () => updated++;

        await store.RefreshAsync();

        Assert.Empty(fakes.Weather.Calls);
        Assert.Null(store.Current);
        Assert.Equal(TimeSpan.FromSeconds(60), fakes.Timer.Interval);
        Assert.True(fakes.Timer.IsRunning);
        Assert.Equal(1, updated);
        Assert.False(store.IsLoading);
    }

    [Fact]
    public async Task Refresh_RepeatedFailures_DoubleBackoffUpToNormalInterval()
    {
        var (store, fakes) = TestData.CreateStore(TestData.WithLocation());
        fakes.Weather.Handler = (_, _, _, _) => Task.FromResult<WeatherSnapshot?>(null);

        var intervals = new List<double>();
        for (int i = 0; i < 7; i++)
        {
            await store.RefreshAsync();
            intervals.Add(fakes.Timer.Interval.TotalSeconds);
        }

        Assert.Equal(new double[] { 60, 120, 240, 480, 900, 900, 900 }, intervals);
    }

    [Fact]
    public async Task Refresh_SuccessResetsBackoff()
    {
        var (store, fakes) = TestData.CreateStore(TestData.WithLocation());
        bool fail = true;
        fakes.Weather.Handler = (_, _, city, _) =>
            Task.FromResult(fail ? null : TestData.Snapshot(city));

        await store.RefreshAsync();
        await store.RefreshAsync();
        Assert.Equal(TimeSpan.FromSeconds(120), fakes.Timer.Interval);

        fail = false;
        await store.RefreshAsync();
        Assert.Equal(NormalInterval, fakes.Timer.Interval);

        fail = true;
        await store.RefreshAsync();
        Assert.Equal(TimeSpan.FromSeconds(60), fakes.Timer.Interval);
    }

    [Fact]
    public async Task Refresh_FailureKeepsPreviousSnapshot()
    {
        var (store, fakes) = TestData.CreateStore(TestData.WithLocation());
        await store.RefreshAsync();
        var first = store.Current;

        fakes.Weather.Handler = (_, _, _, _) => Task.FromResult<WeatherSnapshot?>(null);
        await store.RefreshAsync();

        Assert.NotNull(first);
        Assert.Same(first, store.Current);
    }

    [Fact]
    public async Task Refresh_NewerRequestCancelsAndWinsOverStaleOne()
    {
        var (store, fakes) = TestData.CreateStore(TestData.WithLocation("Old"));
        var pendingOld = new TaskCompletionSource<WeatherSnapshot?>();
        CancellationToken oldToken = default;
        fakes.Weather.Handler = (_, _, city, ct) =>
        {
            if (city == "Old")
            {
                oldToken = ct;
                ct.Register(() => pendingOld.TrySetCanceled(ct));
                return pendingOld.Task;
            }
            return Task.FromResult<WeatherSnapshot?>(TestData.Snapshot(city));
        };
        int updated = 0;
        store.Updated += () => updated++;

        var oldRefresh = store.RefreshAsync();
        Assert.True(store.IsLoading);

        await store.SetLocationAsync(new GeocodingResult("New", null, "BR", 1, 2));
        await oldRefresh;

        Assert.True(oldToken.IsCancellationRequested);
        Assert.Equal("New", store.Current?.CityName);
        Assert.False(store.IsLoading);
        // Só a requisição vigente notifica; a cancelada fica em silêncio
        Assert.Equal(1, updated);
    }

    [Fact]
    public async Task TimerTick_TriggersRefresh()
    {
        var (store, fakes) = TestData.CreateStore(TestData.WithLocation());
        await store.InitializeAsync();

        fakes.Timer.Fire();

        Assert.Equal(2, fakes.Weather.Calls.Count);
    }

    [Fact]
    public void TimerTick_SwallowsUnexpectedErrors()
    {
        var (_, fakes) = TestData.CreateStore(TestData.WithLocation());
        fakes.Weather.Handler = (_, _, _, _) => throw new InvalidOperationException("boom");

        var ex = Record.Exception(fakes.Timer.Fire);

        Assert.Null(ex);
    }

    [Fact]
    public async Task SetLocation_PersistsAndRefreshes()
    {
        var (store, fakes) = TestData.CreateStore(TestData.WithLocation());

        await store.SetLocationAsync(new GeocodingResult("Lisboa", null, "Portugal", 38.72, -9.14));

        Assert.Equal("Lisboa", store.Settings.CityName);
        Assert.Equal(38.72, store.Settings.Latitude);
        Assert.Equal(1, fakes.Settings.SaveCount);
        Assert.Equal((38.72, -9.14, "Lisboa"), fakes.Weather.Calls.Last());
    }

    [Fact]
    public void SetTemperatureUnit_PersistsAppliesAndNotifies()
    {
        var (store, fakes) = TestData.CreateStore();
        int updated = 0;
        store.Updated += () => updated++;

        store.SetTemperatureUnit(TemperatureUnit.Fahrenheit);

        Assert.Equal(TemperatureUnit.Fahrenheit, store.Settings.TemperatureUnit);
        Assert.Equal(TemperatureUnit.Fahrenheit, TemperaturePreference.Instance.Current);
        Assert.Equal(1, fakes.Settings.SaveCount);
        Assert.Equal(1, updated);
    }

    [Fact]
    public void SetIconSet_PersistsAppliesAndNotifies()
    {
        var (store, fakes) = TestData.CreateStore();
        int updated = 0;
        store.Updated += () => updated++;

        store.SetIconSet(IconSet.FluentEmoji);

        Assert.Equal(IconSet.FluentEmoji, store.Settings.IconSet);
        Assert.Equal(IconSet.FluentEmoji, IconSetPreference.Instance.Current);
        Assert.Equal(1, fakes.Settings.SaveCount);
        Assert.Equal(1, updated);
    }

    [Fact]
    public void SetTimeFormat_PersistsAppliesAndNotifies()
    {
        var (store, fakes) = TestData.CreateStore();
        int updated = 0;
        store.Updated += () => updated++;

        store.SetTimeFormat(TimeFormat.TwelveHour);

        Assert.Equal(TimeFormat.TwelveHour, store.Settings.TimeFormat);
        Assert.Equal(TimeFormat.TwelveHour, TimeFormatPreference.Instance.Current);
        Assert.Equal(1, fakes.Settings.SaveCount);
        Assert.Equal(1, updated);
    }

    [Fact]
    public void SetLanguage_PersistsAndApplies()
    {
        var (store, fakes) = TestData.CreateStore();

        store.SetLanguage(AppLanguage.Portuguese);

        Assert.Equal(AppLanguage.Portuguese, store.Settings.Language);
        Assert.Equal(AppLanguage.Portuguese, AppLocalization.Instance.Current);
        Assert.Equal(1, fakes.Settings.SaveCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetAutoStart_PersistsAndRegisters(bool enabled)
    {
        var (store, fakes) = TestData.CreateStore();

        store.SetAutoStart(enabled);

        Assert.Equal(enabled, store.Settings.AutoStart);
        Assert.Equal(new[] { enabled }, fakes.AutoStart.SetCalls);
        Assert.Equal(enabled, store.IsAutoStartEnabled);
        Assert.False(store.Settings.AutoStartPromptShown);
    }

    [Fact]
    public void HandleFirstRunAutoStartChoice_MarksPromptAsShown()
    {
        var (store, fakes) = TestData.CreateStore();

        store.HandleFirstRunAutoStartChoice(true);

        Assert.True(store.Settings.AutoStart);
        Assert.True(store.Settings.AutoStartPromptShown);
        Assert.Equal(new[] { true }, fakes.AutoStart.SetCalls);
        Assert.Equal(1, fakes.Settings.SaveCount);
    }

    [Fact]
    public void SyncAutoStart_ReappliesOnlyAfterUserAnswered()
    {
        var (notAsked, notAskedFakes) = TestData.CreateStore(new AppSettings { AutoStart = true, AutoStartPromptShown = false });
        notAsked.SyncAutoStart();
        Assert.Empty(notAskedFakes.AutoStart.SetCalls);

        var (answered, answeredFakes) = TestData.CreateStore(new AppSettings { AutoStart = true, AutoStartPromptShown = true });
        answered.SyncAutoStart();
        Assert.Equal(new[] { true }, answeredFakes.AutoStart.SetCalls);
    }
}
