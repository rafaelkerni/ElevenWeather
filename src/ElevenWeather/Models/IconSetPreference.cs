using System.ComponentModel;

namespace ElevenWeather.Models;

/// <summary>
/// Currently selected weather icon set — same singleton pattern as <see cref="TemperaturePreference"/>,
/// so both the taskbar pill and the flyout pick up a menu change without recreating any window.
/// </summary>
public sealed class IconSetPreference : INotifyPropertyChanged
{
    public static IconSetPreference Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Fired after any icon set switch — for C# (non-XAML) code to recalculate images.</summary>
    public event Action? Changed;

    private IconSet _current = IconSet.Meteocons;

    private IconSetPreference() { }

    public IconSet Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
            Changed?.Invoke();
        }
    }
}
