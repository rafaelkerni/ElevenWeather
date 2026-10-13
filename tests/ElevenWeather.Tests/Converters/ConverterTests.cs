using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ElevenWeather.Converters;
using ElevenWeather.Models;
using ElevenWeather.Tests.Helpers;

namespace ElevenWeather.Tests.Converters;

public sealed class ConverterTests : PreferenceScope
{
    private static readonly string[] AllIconKeys =
    {
        "ClearDay", "ClearNight", "PartlyCloudyDay", "Cloudy", "Fog", "Rain", "Snow", "Thunderstorm",
    };

    [Theory]
    [InlineData(null, Visibility.Visible)]
    [InlineData("", Visibility.Visible)]
    [InlineData("abc", Visibility.Collapsed)]
    [InlineData(42, Visibility.Visible)]
    public void EmptyStringToVisibility_ShowsPlaceholderOnlyWhenEmpty(object? value, Visibility expected)
    {
        var converter = new EmptyStringToVisibilityConverter();

        Assert.Equal(expected, converter.Convert(value, typeof(Visibility), null, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(expected, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("ClearDay", "clear_day")]
    [InlineData("PartlyCloudyDay", "partly_cloudy_day")]
    [InlineData("Fog", "fog")]
    [InlineData("rain", "rain")]
    public void ToSnakeCase_MatchesIconFileNames(string key, string expected)
    {
        Assert.Equal(expected, IconKeyToImageSourceConverter.ToSnakeCase(key));
    }

    /// <summary>Todo ícone que o WeatherCondition pode pedir precisa existir nos conjuntos embutidos.</summary>
    [Theory]
    [InlineData(IconSet.Meteocons)]
    [InlineData(IconSet.FluentEmoji)]
    public void BundledIconSets_ContainEveryIconKey(IconSet set)
    {
        IconSetPreference.Instance.Current = set;
        var converter = new IconKeyToImageSourceConverter();

        Assert.True(IconKeyToImageSourceConverter.IsIconSetAvailable(set));
        foreach (var key in AllIconKeys)
        {
            var image = converter.Convert(key, typeof(ImageSource), null, CultureInfo.InvariantCulture);
            var bitmap = Assert.IsAssignableFrom<ImageSource>(image);
            Assert.True(bitmap.IsFrozen, key);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(3)] [InlineData(45)] [InlineData(61)]
    [InlineData(71)] [InlineData(95)] [InlineData(999)]
    public void EveryWeatherCodeIcon_IsKnown(int code)
    {
        Assert.Contains(WeatherCondition.IconKey(code, isDay: true), AllIconKeys);
        Assert.Contains(WeatherCondition.IconKey(code, isDay: false), AllIconKeys);
    }

    [Fact]
    public void IconConverter_ReturnsNullForUnknownOrNonStringKeys()
    {
        var converter = new IconKeyToImageSourceConverter();

        Assert.Null(converter.Convert("Tornado", typeof(ImageSource), null, CultureInfo.InvariantCulture));
        Assert.Null(converter.Convert(null, typeof(ImageSource), null, CultureInfo.InvariantCulture));
        Assert.Null(converter.Convert(3, typeof(ImageSource), null, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => converter.ConvertBack(null, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void IconConverter_NeverFailsForMsnSet()
    {
        // Com ou sem os ícones MSN baixados localmente, sempre há imagem (fallback para Meteocons)
        IconSetPreference.Instance.Current = IconSet.Msn;

        var image = new IconKeyToImageSourceConverter().Convert("Rain", typeof(ImageSource), null, CultureInfo.InvariantCulture);

        Assert.NotNull(image);
    }
}
