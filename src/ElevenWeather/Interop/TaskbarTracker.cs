using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ElevenWeather.Interop;

/// <summary>
/// Queries the state of the real taskbars (one per monitor, when "show taskbar on all
/// displays" is enabled): handles, position, full screen, auto-hide.
/// Decides nothing about widget windows — it just exposes the facts to whoever decides.
/// Full-screen detection technique inspired by ElevenClock: the taskbar loses WS_EX_TOPMOST
/// when the shell yields the top to a full-screen app, so it's enough to watch its own ex-style.
/// </summary>
public sealed class TaskbarTracker : IDisposable
{
    private const string PrimaryTrayClass = "Shell_TrayWnd";
    private const string SecondaryTrayClass = "Shell_SecondaryTrayWnd";

    public static readonly uint WM_TASKBARCREATED = NativeMethods.RegisterWindowMessage("TaskbarCreated");

    private static readonly TimeSpan HandleCacheLifetime = TimeSpan.FromSeconds(1);

    private readonly DispatcherTimer _pollTimer;
    private IntPtr _winEventHook;
    private NativeMethods.WinEventDelegate? _winEventProc;

    private IReadOnlyList<IntPtr> _cachedHandles = Array.Empty<IntPtr>();
    private DateTime _handlesCachedAt = DateTime.MinValue;

    /// <summary>Fired on every polling tick or relevant external event. The consumer should re-query the state.</summary>
    public event Action? Changed;

    public TaskbarTracker()
    {
        _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _pollTimer.Tick += (_, _) => Changed?.Invoke();
    }

    public void Start()
    {
        _pollTimer.Start();

        _winEventProc = OnWinEvent;
        _winEventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProc, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);
    }

    public void Stop()
    {
        _pollTimer.Stop();
        if (_winEventHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
    }

    /// <summary>Call when the host window receives the "TaskbarCreated" broadcast message (explorer restart).</summary>
    public void HandleTaskbarCreatedMessage()
    {
        _handlesCachedAt = DateTime.MinValue;
        Changed?.Invoke();
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        => Changed?.Invoke();

    /// <summary>
    /// Handle of the primary taskbar (Shell_TrayWnd) plus one per secondary monitor (Shell_SecondaryTrayWnd), if
    /// shown. Monitor/taskbar topology barely ever changes, but this is re-resolved on every poll tick (up to
    /// 4x/second) and each resolution walks every top-level window and opens a Process handle per taskbar found —
    /// so the result is cached for a second at a time instead of paying that cost on every tick.
    /// </summary>
    public IReadOnlyList<IntPtr> GetTaskbarHandles()
    {
        var now = DateTime.UtcNow;
        if (now - _handlesCachedAt < HandleCacheLifetime)
            return _cachedHandles;

        var result = new List<IntPtr>();

        var primary = NativeMethods.FindWindow(PrimaryTrayClass, null);
        if (primary != IntPtr.Zero && OwnedByExplorer(primary))
            result.Add(primary);

        IntPtr secondary = IntPtr.Zero;
        while (true)
        {
            secondary = NativeMethods.FindWindowEx(IntPtr.Zero, secondary, SecondaryTrayClass, null);
            if (secondary == IntPtr.Zero) break;
            if (OwnedByExplorer(secondary))
                result.Add(secondary);
        }

        _cachedHandles = result;
        _handlesCachedAt = now;
        return result;
    }

    private static bool OwnedByExplorer(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return false;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return string.Equals(proc.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public bool TryGetRect(IntPtr taskbarHandle, out NativeMethods.RECT rect)
    {
        rect = default;
        return taskbarHandle != IntPtr.Zero
            && NativeMethods.IsWindow(taskbarHandle)
            && NativeMethods.GetWindowRect(taskbarHandle, out rect);
    }

    public bool IsFullScreenActive(IntPtr taskbarHandle)
    {
        if (taskbarHandle == IntPtr.Zero || !NativeMethods.IsWindow(taskbarHandle)) return false;
        long exStyle = NativeMethods.GetWindowLongPtrSafe(taskbarHandle, NativeMethods.GWL_EXSTYLE).ToInt64();
        return (exStyle & NativeMethods.WS_EX_TOPMOST) == 0;
    }

    /// <summary>Auto-hide is a global Windows setting, applying to all taskbars.</summary>
    public bool IsAutoHide()
    {
        var data = new NativeMethods.APPBARDATA { cbSize = Marshal.SizeOf<NativeMethods.APPBARDATA>() };
        var result = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref data);
        return ((uint)result.ToInt64() & NativeMethods.ABS_AUTOHIDE) != 0;
    }

    public bool CursorNearBottomEdge(in NativeMethods.RECT taskbarRect)
    {
        if (!NativeMethods.GetCursorPos(out var pt)) return false;
        return pt.Y >= taskbarRect.Bottom - 2
            && pt.X >= taskbarRect.Left
            && pt.X <= taskbarRect.Right;
    }

    public void Dispose() => Stop();
}
