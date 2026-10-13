using ElevenWeather.Models;
using ElevenWeather.Tests.Helpers;

namespace ElevenWeather.Tests.Models;

public sealed class PreferenceTests : PreferenceScope
{
    [Theory]
    [InlineData(0, 32)]
    [InlineData(100, 212)]
    [InlineData(-40, -40)]
    [InlineData(21.5, 70.7)]
    public void TemperaturePreference_ConvertsToFahrenheit(double celsius, double expected)
    {
        TemperaturePreference.Instance.Current = TemperatureUnit.Fahrenheit;
        Assert.Equal(expected, TemperaturePreference.Instance.Convert(celsius), precision: 6);
        Assert.Equal("°F", TemperaturePreference.Instance.Symbol);
    }

    [Fact]
    public void TemperaturePreference_KeepsCelsiusUnchanged()
    {
        Assert.Equal(21.5, TemperaturePreference.Instance.Convert(21.5));
        Assert.Equal("°C", TemperaturePreference.Instance.Symbol);
    }

    [Theory]
    [InlineData(22.5, 23)]
    [InlineData(-0.5, -1)]
    [InlineData(-2.4, -2)]
    [InlineData(22.49, 22)]
    public void TemperaturePreference_RoundsAwayFromZero(double celsius, int expected)
    {
        Assert.Equal(expected, TemperaturePreference.Instance.Round(celsius));
    }

    [Fact]
    public void TemperaturePreference_NotifiesOnlyOnActualChange()
    {
        int unitChanged = 0;
        var propertyNames = new List<string?>();
        void OnUnitChanged() => unitChanged++;
        void OnPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e) => propertyNames.Add(e.PropertyName);

        TemperaturePreference.Instance.UnitChanged += OnUnitChanged;
        TemperaturePreference.Instance.PropertyChanged += OnPropertyChanged;
        try
        {
            TemperaturePreference.Instance.Current = TemperatureUnit.Celsius;
            TemperaturePreference.Instance.Current = TemperatureUnit.Fahrenheit;
            TemperaturePreference.Instance.Current = TemperatureUnit.Fahrenheit;
        }
        finally
        {
            TemperaturePreference.Instance.UnitChanged -= OnUnitChanged;
            TemperaturePreference.Instance.PropertyChanged -= OnPropertyChanged;
        }

        Assert.Equal(1, unitChanged);
        Assert.Equal(new[] { nameof(TemperaturePreference.Current) }, propertyNames);
    }

    [Fact]
    public void TimeFormatPreference_FormatsBothStyles()
    {
        var time = new DateTime(2026, 1, 1, 15, 5, 0);
        Assert.Equal("15:05", TimeFormatPreference.Instance.Format(time));

        TimeFormatPreference.Instance.Current = TimeFormat.TwelveHour;
        Assert.Equal("3:05 PM", TimeFormatPreference.Instance.Format(time));
    }

    [Fact]
    public void AppLocalization_ReturnsKeyWhenMissing()
    {
        Assert.Equal("does.not.exist", AppLocalization.Get("does.not.exist"));
        Assert.Equal("does.not.exist", AppLocalization.Instance["does.not.exist"]);
    }

    [Fact]
    public void AppLocalization_RaisesIndexerChangeForXamlBindings()
    {
        var propertyNames = new List<string?>();
        int languageChanged = 0;
        void OnPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e) => propertyNames.Add(e.PropertyName);
        void OnLanguageChanged() => languageChanged++;

        AppLocalization.Instance.PropertyChanged += OnPropertyChanged;
        AppLocalization.Instance.LanguageChanged += OnLanguageChanged;
        try
        {
            AppLocalization.Instance.Current = AppLanguage.Spanish;
        }
        finally
        {
            AppLocalization.Instance.PropertyChanged -= OnPropertyChanged;
            AppLocalization.Instance.LanguageChanged -= OnLanguageChanged;
        }

        Assert.Equal(new[] { "Item[]" }, propertyNames);
        Assert.Equal(1, languageChanged);
        Assert.Equal("Hoy", AppLocalization.Instance["ui.today"]);
    }

    [Fact]
    public void AppSettings_HasLocationRequiresBothCoordinates()
    {
        Assert.False(new AppSettings().HasLocation);
        Assert.False(new AppSettings { Latitude = -23.5 }.HasLocation);
        Assert.True(new AppSettings { Latitude = -23.5, Longitude = -46.6 }.HasLocation);
    }

    [Fact]
    public void GeocodingResult_DisplayNameSkipsMissingRegion()
    {
        Assert.Equal("Lisbon, Portugal", new GeocodingResult("Lisbon", null, "Portugal", 0, 0).DisplayName);
        Assert.Equal("Campinas, São Paulo, Brazil", new GeocodingResult("Campinas", "São Paulo", "Brazil", 0, 0).DisplayName);
    }
}
