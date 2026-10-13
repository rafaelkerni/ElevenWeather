using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using ElevenWeather.Interop;
using ElevenWeather.ViewModels;

namespace ElevenWeather.Views;

/// <summary>
/// The pill that sits on the taskbar, in the exact spot of the native weather widget.
/// All the "where" and "whether it should appear" logic lives in <see cref="TaskbarTracker"/>;
/// this class only reacts to its events and keeps the HWND physically pinned to the taskbar.
/// </summary>
public partial class TaskbarWidgetWindow : Window
{
    private const int DesignWidth = 152;
    private const int DesignHeight = 48;
    private const int DesignLeftOffset = 0;

    private readonly TaskbarTracker _tracker;
    private readonly IntPtr _taskbarHandle;

    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private NativeMethods.RECT _lastRect;
    private bool _havePosition;
    private bool _isFlyoutOpen;

    /// <summary>Fired when the widget is clicked — the listener decides whether to open/close/reposition the flyout.</summary>
    public event Action? Clicked;

    public TaskbarWidgetWindow(TaskbarTracker tracker, IntPtr taskbarHandle, WidgetViewModel viewModel)
    {
        InitializeComponent();
        _tracker = tracker;
        _taskbarHandle = taskbarHandle;
        DataContext = viewModel;

        RootGrid.MouseEnter += (_, _) => AnimateOpacity(HoverLayer, 1);
        RootGrid.MouseLeave += (_, _) =>
        {
            AnimateOpacity(HoverLayer, 0);
            AnimateOpacity(PressedLayer, 0);
        };
        RootGrid.PreviewMouseLeftButtonDown += (_, _) => AnimateOpacity(PressedLayer, 1);
        RootGrid.MouseLeftButtonUp += (_, _) =>
        {
            AnimateOpacity(PressedLayer, 0);
            Clicked?.Invoke();
        };

        _tracker.Changed += OnTrackerChanged;
    }

    /// <summary>While the flyout is open, the widget must stay visible even if the taskbar is in auto-hide mode.</summary>
    public void SetFlyoutOpenState(bool isOpen)
    {
        _isFlyoutOpen = isOpen;
        Reposition();
    }

    /// <summary>Current widget rectangle in physical screen coordinates — used to anchor the flyout.</summary>
    public bool TryGetScreenRect(out NativeMethods.RECT rect)
    {
        rect = default;
        return _hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(_hwnd, out rect);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;
        WindowChromeHelper.MakeToolWindowNoActivate(_hwnd);

        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);

        Reposition();
    }

    protected override void OnClosed(EventArgs e)
    {
        _tracker.Changed -= OnTrackerChanged;
        _hwndSource?.RemoveHook(WndProc);
        base.OnClosed(e);
    }

    private const int WM_SETTINGCHANGE = 0x001A;

    /// <summary>Fired when Windows notifies a system setting change (e.g. theme switch).</summary>
    public event Action? SystemSettingChanged;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)TaskbarTracker.WM_TASKBARCREATED)
            _tracker.HandleTaskbarCreatedMessage();
        else if (msg == WM_SETTINGCHANGE)
            SystemSettingChanged?.Invoke();

        return IntPtr.Zero;
    }

    private void OnTrackerChanged() => Reposition();

    private void Reposition()
    {
        if (_hwnd == IntPtr.Zero) return;

        if (_tracker.IsFullScreenActive(_taskbarHandle))
        {
            SetHidden();
            return;
        }

        if (!_tracker.TryGetRect(_taskbarHandle, out var rect))
        {
            SetHidden();
            return;
        }

        bool shouldShow = !_tracker.IsAutoHide() || _isFlyoutOpen || _tracker.CursorNearBottomEdge(rect);
        if (!shouldShow)
        {
            SetHidden();
            return;
        }

        if (Visibility != Visibility.Visible)
            Visibility = Visibility.Visible;

        bool rectChanged = !_havePosition || !RectsEqual(rect, _lastRect);
        if (rectChanged)
        {
            double scale = rect.Height / (double)DesignHeight;
            int x = rect.Left + (int)Math.Round(DesignLeftOffset * scale);
            int width = (int)Math.Round(DesignWidth * scale);

            WindowChromeHelper.MoveTopMost(_hwnd, x, rect.Top, width, rect.Height);
            _lastRect = rect;
            _havePosition = true;
        }
        else if (!RootGrid.IsMouseOver)
        {
            // Skip the topmost reinforcement while the mouse is over the widget: SetWindowPos
            // reorders the Z-order and that interrupts Windows' native mouse enter/leave
            // tracking, leaving the hover "stuck" until the next actual cursor movement.
            WindowChromeHelper.ForceTopMost(_hwnd);
        }
    }

    private void SetHidden()
    {
        if (Visibility != Visibility.Hidden)
            Visibility = Visibility.Hidden;
        _havePosition = false;
    }

    private static bool RectsEqual(in NativeMethods.RECT a, in NativeMethods.RECT b) =>
        a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;

    private static void AnimateOpacity(UIElement element, double to)
    {
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        element.BeginAnimation(OpacityProperty, animation);
    }
}
