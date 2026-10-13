using System.Runtime.InteropServices;

namespace ElevenWeather.Interop;

/// <summary>
/// Applies, via Win32, the window behavior that WPF doesn't expose:
/// staying out of Alt+Tab, not stealing focus when clicked, and staying on top of the z-order.
/// </summary>
public static class WindowChromeHelper
{
    public static void MakeToolWindowNoActivate(IntPtr hwnd)
    {
        long current = NativeMethods.GetWindowLongPtrSafe(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        current |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtrSafe(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(current));
    }

    public static void MakeToolWindow(IntPtr hwnd)
    {
        long current = NativeMethods.GetWindowLongPtrSafe(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        current |= NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtrSafe(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(current));
    }

    public static void ForceTopMost(IntPtr hwnd)
    {
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    public static void MoveTopMost(IntPtr hwnd, int x, int y, int width, int height)
    {
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, x, y, width, height,
            NativeMethods.SWP_NOACTIVATE);
    }

    public static void RoundCorners(IntPtr hwnd)
    {
        int preference = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }

    /// <summary>Turns on Windows' native acrylic behind the window (same visual effect as the original flyout).</summary>
    public static void EnableAcrylic(IntPtr hwnd, byte a, byte r, byte g, byte b)
    {
        int gradientColor = (a << 24) | (b << 16) | (g << 8) | r; // expected format: AABBGGRR

        var accent = new NativeMethods.ACCENT_POLICY
        {
            AccentState = NativeMethods.AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
            GradientColor = gradientColor,
        };

        int size = Marshal.SizeOf<NativeMethods.ACCENT_POLICY>();
        IntPtr accentPtr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new NativeMethods.WINDOWCOMPOSITIONATTRIBDATA
            {
                Attribute = NativeMethods.WCA_ACCENT_POLICY,
                Data = accentPtr,
                SizeOfData = size,
            };
            NativeMethods.SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }
}
