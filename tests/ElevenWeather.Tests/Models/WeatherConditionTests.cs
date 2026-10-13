using ElevenWeather.Models;
using ElevenWeather.Tests.Helpers;

namespace ElevenWeather.Tests.Models;

public sealed class WeatherConditionTests : PreferenceScope
{
    [Theory]
    [InlineData(0, true, "ClearDay")]
    [InlineData(0, false, "ClearNight")]
    [InlineData(1, false, "ClearNight")]
    [InlineData(2, true, "PartlyCloudyDay")]
    [InlineData(2, false, "Cloudy")]
    [InlineData(3, true, "Cloudy")]
    [InlineData(45, true, "Fog")]
    [InlineData(48, false, "Fog")]
    [InlineData(51, true, "Rain")]
    [InlineData(57, true, "Rain")]
    [InlineData(65, true, "Rain")]
    [InlineData(82, false, "Rain")]
    [InlineData(71, true, "Snow")]
    [InlineData(86, true, "Snow")]
    [InlineData(95, true, "Thunderstorm")]
    [InlineData(99, false, "Thunderstorm")]
    [InlineData(-1, true, "ClearDay")]
    [InlineData(1234, false, "ClearNight")]
    public void IconKey_MapsWmoCodeToIcon(int code, bool isDay, string expected)
    {
        Assert.Equal(expected, WeatherCondition.IconKey(code, isDay));
    }

    [Theory]
    [InlineData(0, "Clear")]
    [InlineData(2, "Partly cloudy")]
    [InlineData(48, "Fog")]
    [InlineData(65, "Heavy rain")]
    [InlineData(77, "Snow grains")]
    [InlineData(96, "Thunderstorm with hail")]
    [InlineData(42, "Unavailable")]
    public void Describe_ReturnsTextInCurrentLanguage(int code, string expected)
    {
        Assert.Equal(expected, WeatherCondition.Describe(code));
    }

    [Fact]
    public void Describe_FollowsLanguageSwitch()
    {
        AppLocalization.Instance.Current = AppLanguage.Portuguese;
        Assert.Equal("Chuva", WeatherCondition.Describe(61));

        AppLocalization.Instance.Current = AppLanguage.Spanish;
        Assert.Equal("Lluvia", WeatherCondition.Describe(61));
    }

    [Theory]
    [InlineData(0, "Good")]
    [InlineData(50, "Good")]
    [InlineData(51, "Moderate")]
    [InlineData(100, "Moderate")]
    [InlineData(150, "Unhealthy for sensitive groups")]
    [InlineData(200, "Unhealthy")]
    [InlineData(300, "Very unhealthy")]
    [InlineData(301, "Hazardous")]
    public void UsAqiCategory_UsesEpaBreakpoints(int aqi, string expected)
    {
        Assert.Equal(expected, WeatherCondition.UsAqiCategory(aqi));
    }

    /// <summary>Todo código WMO documentado deve ter descrição própria, não cair no "Indisponível".</summary>
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(45)] [InlineData(48)]
    [InlineData(51)] [InlineData(53)] [InlineData(55)] [InlineData(56)] [InlineData(57)]
    [InlineData(61)] [InlineData(63)] [InlineData(65)] [InlineData(66)] [InlineData(67)]
    [InlineData(71)] [InlineData(73)] [InlineData(75)] [InlineData(77)]
    [InlineData(80)] [InlineData(81)] [InlineData(82)] [InlineData(85)] [InlineData(86)]
    [InlineData(95)] [InlineData(96)] [InlineData(99)]
    public void Describe_CoversAllWmoCodes(int code)
    {
        Assert.NotEqual(AppLocalization.Get("weather.unavailable"), WeatherCondition.Describe(code));
    }
}
