using System;
using System.Runtime.InteropServices;

namespace VRTDoubao.Win32;

public static class WindowEmbedder
{
    private const int GWL_STYLE = -16;
    private const long WS_CAPTION = 0x00C00000L;
    private const long WS_THICKFRAME = 0x00040000L;
    private const long WS_MINIMIZE = 0x20000000L;
    private const long WS_MAXIMIZE = 0x01000000L;
    private const long WS_SYSMENU = 0x00080000L;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOREDRAW = 0x0008;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOSENDCHANGING = 0x0400;

    public static bool Embed(nint childHwnd, nint hostHwnd)
    {
        if (childHwnd == nint.Zero || hostHwnd == nint.Zero) return false;

        // Disable DWM transitions to reduce flicker
        try
        {
            int disable = 1;
            DwmSetWindowAttribute(childHwnd, 20 /*DWMWA_TRANSITIONS_FORCEDISABLED*/, ref disable, sizeof(int));
        }
        catch { }

        // Avoid redundant style changes causing flicker by batching
        var style = GetWindowLongPtr(childHwnd, GWL_STYLE);
        var newStyle = (nint)(style.ToInt64() & ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZE | WS_MAXIMIZE | WS_SYSMENU));
        if (newStyle != style)
        {
            SetWindowLongPtr(childHwnd, GWL_STYLE, newStyle);
        }
        SetParent(childHwnd, hostHwnd);

        SetWindowPos(childHwnd, nint.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        return true;
    }

    public static bool ResizeToHost(nint childHwnd, int width, int height)
    {
        if (childHwnd == nint.Zero) return false;
        // Skip if size unchanged to avoid redundant repaints
        if (GetClientSize(childHwnd, out var cw, out var ch))
        {
            if (cw == width && ch == height) return true;
        }
        return SetWindowPos(childHwnd, nint.Zero, 0, 0, width, height,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOSENDCHANGING);
    }

    public static bool EnsureEmbedded(nint childHwnd, nint hostHwnd)
    {
        if (childHwnd == nint.Zero || hostHwnd == nint.Zero) return false;
        if (!IsWindow(childHwnd)) return false;
        var parent = GetParent(childHwnd);
        if (parent != hostHwnd)
        {
            Embed(childHwnd, hostHwnd);
        }
        return true;
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint GetParent(nint hWnd);

    // Intentionally no ShowWindow here to avoid flicker

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetClientRect(nint hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    private static bool GetClientSize(nint hWnd, out int width, out int height)
    {
        if (GetClientRect(hWnd, out var r))
        {
            width = r.Right - r.Left;
            height = r.Bottom - r.Top;
            return true;
        }
        width = height = 0;
        return false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetParent(nint hWndChild, nint hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);
}


