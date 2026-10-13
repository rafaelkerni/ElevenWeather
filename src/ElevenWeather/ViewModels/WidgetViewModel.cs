using ElevenWeather.Models;
using ElevenWeather.Services;

namespace ElevenWeather.ViewModels;

/// <summary>ViewModel for the compact pill that sits on the taskbar.</summary>
public sealed class WidgetViewModel : ObservableObject
{
    private readonly WeatherStore _store;

    public WidgetViewModel(WeatherStore store)
    {
        _store = store;
        _store.Updated += OnStoreUpdated;
        AppLocalization.Instance.LanguageChanged += OnLanguageChanged;
    }

    public string TemperatureText => _store.Current is { } s
        ? $"{s.TemperatureRounded}{TemperaturePreference.Instance.Symbol}"
        : "--°";

    public string DescriptionText => _store.Current?.Description ?? AppLocalization.Get("ui.loading");

    public string IconKey => _store.Current?.IconKey ?? "ClearDay";

    private void OnStoreUpdated()
    {
        OnPropertyChanged(nameof(TemperatureText));
        OnPropertyChanged(nameof(DescriptionText));
        OnPropertyChanged(nameof(IconKey));
    }

    private void OnLanguageChanged() => OnPropertyChanged(nameof(DescriptionText));
}
