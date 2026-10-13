using System.ComponentModel;

namespace ElevenWeather.Models;

/// <summary>
/// Currently selected temperature unit — same singleton pattern as AppLocalization,
/// notifying whoever is listening when the user switches units from the menu.
/// </summary>
public sealed class TemperaturePreference : INotifyPropertyChanged
{
    public static TemperaturePreference Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Fired after any unit switch — for C# (non-XAML) code to recalculate text.</summary>
    public event Action? UnitChanged;

    private TemperatureUnit _current = TemperatureUnit.Celsius;

    private TemperaturePreference() { }

    public TemperatureUnit Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
            UnitChanged?.Invoke();
        }
    }

    public string Symbol => _current == TemperatureUnit.Fahrenheit ? "°F" : "°C";

    public double Convert(double celsius) =>
        _current == TemperatureUnit.Fahrenheit ? celsius * 9.0 / 5.0 + 32.0 : celsius;

    public int Round(double celsius) => (int)Math.Round(Convert(celsius), MidpointRounding.AwayFromZero);
}
