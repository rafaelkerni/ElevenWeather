using System.ComponentModel;

namespace ElevenWeather.Models;

/// <summary>
/// Currently selected time format — same singleton pattern as <see cref="TemperaturePreference"/>,
/// notifying whoever is listening when the user switches formats from the menu.
/// </summary>
public sealed class TimeFormatPreference : INotifyPropertyChanged
{
    public static TimeFormatPreference Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Fired after any format switch — for C# (non-XAML) code to recalculate text.</summary>
    public event Action? FormatChanged;

    private TimeFormat _current = TimeFormat.TwentyFourHour;

    private TimeFormatPreference() { }

    public TimeFormat Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
            FormatChanged?.Invoke();
        }
    }

    public string Format(DateTime time) =>
        _current == TimeFormat.TwelveHour ? time.ToString("h:mm tt") : time.ToString("HH:mm");
}
