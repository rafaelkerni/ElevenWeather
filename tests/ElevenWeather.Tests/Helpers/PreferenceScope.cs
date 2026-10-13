using System.Globalization;
using ElevenWeather.Models;

namespace ElevenWeather.Tests.Helpers;

/// <summary>Salva e restaura os singletons de preferência e a cultura, para cada teste começar de um estado conhecido.</summary>
public abstract class PreferenceScope : IDisposable
{
    private readonly AppLanguage _language = AppLocalization.Instance.Current;
    private readonly TemperatureUnit _unit = TemperaturePreference.Instance.Current;
    private readonly TimeFormat _timeFormat = TimeFormatPreference.Instance.Current;
    private readonly IconSet _iconSet = IconSetPreference.Instance.Current;
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    protected PreferenceScope()
    {
        AppLocalization.Instance.Current = AppLanguage.English;
        TemperaturePreference.Instance.Current = TemperatureUnit.Celsius;
        TimeFormatPreference.Instance.Current = TimeFormat.TwentyFourHour;
        IconSetPreference.Instance.Current = IconSet.Meteocons;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    public void Dispose()
    {
        AppLocalization.Instance.Current = _language;
        TemperaturePreference.Instance.Current = _unit;
        TimeFormatPreference.Instance.Current = _timeFormat;
        IconSetPreference.Instance.Current = _iconSet;
        CultureInfo.CurrentCulture = _culture;
        GC.SuppressFinalize(this);
    }
}
