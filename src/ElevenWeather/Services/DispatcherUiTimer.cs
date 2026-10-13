using System.Windows.Threading;

namespace ElevenWeather.Services;

/// <summary>Implementação real de <see cref="IUiTimer"/> sobre o DispatcherTimer do WPF.</summary>
public sealed class DispatcherUiTimer : IUiTimer
{
    private readonly DispatcherTimer _timer;

    public DispatcherUiTimer(DispatcherPriority priority = DispatcherPriority.Background)
    {
        _timer = new DispatcherTimer(priority);
        _timer.Tick += (_, _) => Tick?.Invoke();
    }

    public event Action? Tick;

    public TimeSpan Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();
}
