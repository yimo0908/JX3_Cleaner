using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace JX3Cleaner.App.Interop;

/// <summary>
/// Win11 窗口圆角。DWM 在 Win11 上才会认 DWMWA_WINDOW_CORNER_PREFERENCE，
/// 在 Win10 及更早系统上调用会返回错误码，直接忽略即可（保持原外观）。
/// </summary>
public static class WindowChrome
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;
    private const int DwmwcpDoNotRound = 1;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>给窗口套上系统级圆角（含标题栏）。请在 SourceInitialized 之后调用。</summary>
    public static void EnableRoundedCorners(Window window)
    {
        if (window is null) return;

        if (window.IsInitialized && PresentationSource.FromVisual(window) is HwndSource source)
        {
            Apply(source.Handle);
            return;
        }

        window.SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(window) is HwndSource s) Apply(s.Handle);
        };
    }

    /// <summary>禁用系统圆角（内容自身已有圆角时用，避免出现双层边角）。</summary>
    public static void DisableRoundedCorners(Window window)
    {
        if (window is null) return;
        if (PresentationSource.FromVisual(window) is HwndSource s) Apply(s.Handle, DwmwcpDoNotRound);
    }

    private static void Apply(IntPtr hwnd, int preference = DwmwcpRound)
    {
        if (hwnd == IntPtr.Zero) return;

        try
        {
            var value = preference;
            _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref value, sizeof(int));
        }
        catch (DllNotFoundException) { /* 极老系统没有 dwmapi，忽略 */ }
        catch (EntryPointNotFoundException) { /* 同上 */ }
    }
}
