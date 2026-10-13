using ElevenWeather.Models;
using ElevenWeather.Tests.Helpers;
using ElevenWeather.ViewModels;

namespace ElevenWeather.Tests.ViewModels;

public sealed class WidgetViewModelTests : PreferenceScope
{
    [Fact]
    public void BeforeFirstLoad_ShowsPlaceholders()
    {
        var (store, _) = TestData.CreateStore(TestData.WithLocation());
        var vm = new WidgetViewModel(store);

        Assert.Equal("--°", vm.TemperatureText);
        Assert.Equal("Loading...", vm.DescriptionText);
        Assert.Equal("ClearDay", vm.IconKey);
    }

    [Fact]
    public async Task AfterLoad_ShowsTemperatureWithUnitAndNotifies()
    {
        var (store, fakes) = TestData.CreateStore(TestData.WithLocation());
        fakes.Weather.Handler = (_, _, _, _) =>
            Task.FromResult<WeatherSnapshot?>(TestData.Snapshot(temp: -0.5, weatherCode: 71, isDay: false));
        var vm = new WidgetViewModel(store);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await store.RefreshAsync();

        Assert.Equal("-1°C", vm.TemperatureText);
        Assert.Equal("Snow", vm.DescriptionText);
        Assert.Equal("Snow", vm.IconKey);
        Assert.Equal(
            new[] { nameof(WidgetViewModel.TemperatureText), nameof(WidgetViewModel.DescriptionText), nameof(WidgetViewModel.IconKey) },
            changed);

        store.SetTemperatureUnit(TemperatureUnit.Fahrenheit);
        Assert.Equal("31°F", vm.TemperatureText);
    }

    [Fact]
    public async Task LanguageSwitch_NotifiesDescription()
    {
        var (store, _) = TestData.CreateStore(TestData.WithLocation());
        await store.RefreshAsync();
        var vm = new WidgetViewModel(store);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        AppLocalization.Instance.Current = AppLanguage.Spanish;

        Assert.Equal(new[] { nameof(WidgetViewModel.DescriptionText) }, changed);
        Assert.Equal("Despejado", vm.DescriptionText);
    }
}
