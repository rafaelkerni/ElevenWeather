using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ElevenWeather.Interop;
using ElevenWeather.Models;
using ElevenWeather.Services;
using ElevenWeather.ViewModels;

namespace ElevenWeather.Views;

/// <summary>The card that opens when the widget is clicked, reproducing the original weather flyout.</summary>
public partial class WeatherFlyoutWindow : Window
{
    private readonly FlyoutViewModel _viewModel;
    private NativeMethods.RECT? _anchorRect;
    private IntPtr _hwnd;

    public event Action? FlyoutHidden;

    public WeatherFlyoutWindow(FlyoutViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.MsnIconInstallFailed += message => System.Windows.MessageBox.Show(
            message, "ElevenWeather", MessageBoxButton.OK, MessageBoxImage.Warning);
        _viewModel.UpdateCheckCompleted += (message, failed) => System.Windows.MessageBox.Show(
            message, "ElevenWeather", MessageBoxButton.OK, failed ? MessageBoxImage.Warning : MessageBoxImage.Information);

        Loaded += (_, _) => UpdatePosition();
        SizeChanged += (_, _) => UpdatePosition();
    }

    /// <summary>Sets the widget's physical rectangle on the taskbar, which the card anchors above.</summary>
    public void PositionAbove(NativeMethods.RECT widgetRect)
    {
        _anchorRect = widgetRect;
        UpdatePosition();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowChromeHelper.MakeToolWindow(_hwnd);
        WindowChromeHelper.RoundCorners(_hwnd);
        ApplyAcrylicForCurrentTheme();
    }

    /// <summary>Reapplies the acrylic with the right tone for the current theme — called on startup and after a theme switch.</summary>
    public void ApplyAcrylicForCurrentTheme()
    {
        if (_hwnd == IntPtr.Zero) return;

        bool light = SystemThemeWatcher.IsLightTheme();
        if (light)
            WindowChromeHelper.EnableAcrylic(_hwnd, a: 160, r: 0xF3, g: 0xF3, b: 0xF3);
        else
            WindowChromeHelper.EnableAcrylic(_hwnd, a: 160, r: 0x2C, g: 0x2C, b: 0x2E);
    }

    private void UpdatePosition()
    {
        if (_anchorRect is not { } rect) return;

        double dpiScale = rect.Height / 48.0;
        double gapDip = 10;

        Left = rect.Left / dpiScale;
        Top = (rect.Top / dpiScale) - ActualHeight - gapDip;
    }

    private void OnDeactivated(object sender, EventArgs e) => CloseFlyout();

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            CloseFlyout();
    }

    private void CloseFlyout()
    {
        if (Visibility == Visibility.Visible)
        {
            Hide();
            FlyoutHidden?.Invoke();
        }
    }

    private void OnMenuButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void OnCityHeaderClick(object sender, MouseButtonEventArgs e)
        => _viewModel.ToggleLocationPanelCommand.Execute(null);

    private void OnAirQualityClick(object sender, MouseButtonEventArgs e)
        => _viewModel.OpenForecastCommand.Execute(null);

    private void OnCitySearchKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _viewModel.SearchCityCommand.Execute(null);
    }

    private async void OnCityResultClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: GeocodingResult city })
            await _viewModel.SelectCityAsync(city);
    }

    private void OnHourlyTabClick(object sender, RoutedEventArgs e) => _viewModel.SelectedTab = ForecastTab.Hourly;

    private void OnDailyTabClick(object sender, RoutedEventArgs e) => _viewModel.SelectedTab = ForecastTab.Daily;

    private void OnScrollLeftClick(object sender, RoutedEventArgs e) => _viewModel.GoToPreviousPage();

    private void OnScrollRightClick(object sender, RoutedEventArgs e) => _viewModel.GoToNextPage();
}
