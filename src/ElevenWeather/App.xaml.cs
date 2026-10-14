using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows;
using ElevenWeather.Interop;
using ElevenWeather.Models;
using ElevenWeather.Services;
using ElevenWeather.ViewModels;
using ElevenWeather.Views;

namespace ElevenWeather;

public partial class App
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private HttpClient? _httpClient;
    private HttpClient? _updateHttpClient;
    private TaskbarTracker? _tracker;
    private WeatherStore? _weatherStore;
    private WidgetViewModel? _widgetViewModel;
    private WeatherFlyoutWindow? _flyoutWindow;
    private TaskbarWidgetWindow? _activeFlyoutOwner;

    // One widget window per physical taskbar (one per monitor, when "show on all displays" is enabled).
    private readonly Dictionary<IntPtr, TaskbarWidgetWindow> _widgetWindows = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, "ElevenWeather.SingleInstance", out bool createdNew);
        _ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        ApplyTheme();

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // aviationweather.gov filters traffic without a custom user agent.
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ElevenWeather/1.0 (+https://github.com/rafaelkerni/ElevenWeather)");
        var settingsService = new SettingsService();
        var weatherService = new WeatherService(_httpClient, new MetarService(_httpClient));
        var locationService = new LocationService(_httpClient);
        _weatherStore = new WeatherStore(weatherService, locationService, settingsService, new AutoStartService(), new DispatcherUiTimer());
        _weatherStore.SyncAutoStart();

        _widgetViewModel = new WidgetViewModel(_weatherStore);

        UpdateService? updateService = null;
#if !DEBUG
        updateService = StartAutoUpdate();
#endif

        var flyoutViewModel = new FlyoutViewModel(_weatherStore, locationService, new DispatcherUiTimer(), new MsnIconInstaller(), updateService);
        _flyoutWindow = new WeatherFlyoutWindow(flyoutViewModel);
        _flyoutWindow.FlyoutHidden += () =>
        {
            _activeFlyoutOwner?.SetFlyoutOpenState(false);
            _activeFlyoutOwner = null;
            MaybeShowAutoStartPrompt();
        };

        _tracker = new TaskbarTracker();
        _tracker.Changed += ReconcileWidgetWindows;
        _tracker.Start();
        ReconcileWidgetWindows();

        _ = _weatherStore.InitializeAsync();

        // Espera o widget aparecer na barra para o aviso fazer sentido visualmente.
        Dispatcher.BeginInvoke(new Action(MaybeShowWidgetsPrompt), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Checa por novas versões pouco depois de iniciar e periodicamente; a troca vale na próxima inicialização.</summary>
    private UpdateService? StartAutoUpdate()
    {
        var exePath = Environment.ProcessPath;
        var version = typeof(App).Assembly.GetName().Version;
        if (exePath is null || version is null) return null;

        // Cliente próprio: o download do exe não cabe no timeout curto usado pelas APIs de clima
        _updateHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _updateHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ElevenWeather/1.0 (+https://github.com/rafaelkerni/ElevenWeather)");
        var updateService = new UpdateService(_updateHttpClient, version, exePath);
        updateService.CleanupPreviousUpdate();

        var timer = new DispatcherUiTimer { Interval = TimeSpan.FromMinutes(1) };
        bool running = false;
        timer.Tick += async () =>
        {
            if (running) return;
            running = true;
            timer.Interval = TimeSpan.FromHours(12);
            if (await updateService.TryUpdateAsync() == UpdateResult.Installed)
                timer.Stop();
            running = false;
        };
        timer.Start();
        return updateService;
    }

    /// <summary>
    /// Avisa quando o botão Widgets do Windows está ativo, já que ele se sobrepõe ao ElevenWeather.
    /// O app não consegue desativá-lo sozinho, então só oferece abrir a tela de configurações.
    /// </summary>
    private void MaybeShowWidgetsPrompt()
    {
        if (_weatherStore is null || _weatherStore.Settings.WidgetsPromptDismissed) return;
        if (!WindowsWidgetsDetector.IsWidgetsButtonEnabled()) return;

        bool openSettings = System.Windows.MessageBox.Show(
            AppLocalization.Get("prompt.widgetsMessage"),
            AppLocalization.Get("prompt.widgetsTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Information) == MessageBoxResult.Yes;

        if (!openSettings)
        {
            _weatherStore.DismissWidgetsPrompt();
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(WindowsWidgetsDetector.TaskbarSettingsUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Sem como abrir as configurações: o aviso volta na próxima inicialização.
        }
    }

    /// <summary>
    /// Asks about auto-start the first time the user actually opens the flyout, instead of as an
    /// unsolicited popup right after boot — same one-time prompt, triggered by the user's own
    /// interaction instead of interrupting whatever else they were doing.
    /// </summary>
    private void MaybeShowAutoStartPrompt()
    {
        if (_weatherStore is null || _weatherStore.Settings.AutoStartPromptShown) return;

        bool enableAutoStart = System.Windows.MessageBox.Show(
            AppLocalization.Get("prompt.autoStartMessage"),
            AppLocalization.Get("prompt.autoStartTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
        _weatherStore.HandleFirstRunAutoStartChoice(enableAutoStart);
    }

    /// <summary>Creates/closes a widget window for each taskbar currently shown by Windows.</summary>
    private void ReconcileWidgetWindows()
    {
        if (_tracker is null || _widgetViewModel is null) return;

        var current = _tracker.GetTaskbarHandles();
        var currentSet = current.ToHashSet();

        foreach (var staleHandle in _widgetWindows.Keys.Where(h => !currentSet.Contains(h)).ToList())
        {
            var window = _widgetWindows[staleHandle];
            if (ReferenceEquals(_activeFlyoutOwner, window))
            {
                _flyoutWindow?.Hide();
                _activeFlyoutOwner = null;
            }
            _widgetWindows.Remove(staleHandle);
            window.Close();
        }

        foreach (var handle in current)
        {
            if (_widgetWindows.ContainsKey(handle)) continue;

            var window = new TaskbarWidgetWindow(_tracker, handle, _widgetViewModel);
            window.SystemSettingChanged += ApplyTheme;
            window.Clicked += () => OnWidgetClicked(window);
            window.Show();
            _widgetWindows[handle] = window;
        }
    }

    private void OnWidgetClicked(TaskbarWidgetWindow sourceWidget)
    {
        if (_flyoutWindow is null) return;

        bool alreadyOpenHere = _flyoutWindow.Visibility == Visibility.Visible && ReferenceEquals(_activeFlyoutOwner, sourceWidget);
        if (alreadyOpenHere)
        {
            _flyoutWindow.Hide();
            sourceWidget.SetFlyoutOpenState(false);
            _activeFlyoutOwner = null;
            return;
        }

        if (_activeFlyoutOwner is not null && !ReferenceEquals(_activeFlyoutOwner, sourceWidget))
            _activeFlyoutOwner.SetFlyoutOpenState(false);

        if (sourceWidget.TryGetScreenRect(out var rect))
            _flyoutWindow.PositionAbove(rect);

        _activeFlyoutOwner = sourceWidget;
        sourceWidget.SetFlyoutOpenState(true);
        _flyoutWindow.Show();
        _flyoutWindow.Activate();
    }

    private void ApplyTheme()
    {
        bool light = SystemThemeWatcher.IsLightTheme();
        var themeUri = new Uri(light ? "Resources/Theme.Light.xaml" : "Resources/Theme.Dark.xaml", UriKind.Relative);
        var themeDict = new ResourceDictionary { Source = themeUri };

        var previous = Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source is { } s &&
                (s.OriginalString.Contains("Theme.Light") || s.OriginalString.Contains("Theme.Dark")));

        if (previous is not null)
            Resources.MergedDictionaries.Remove(previous);

        Resources.MergedDictionaries.Add(themeDict);

        _flyoutWindow?.ApplyAcrylicForCurrentTheme();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tracker?.Dispose();
        _httpClient?.Dispose();
        _updateHttpClient?.Dispose();

        if (_singleInstanceMutex is not null)
        {
            if (_ownsSingleInstanceMutex)
                _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
    }
}
