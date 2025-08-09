using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace VRTDoubao.Win32;

public record WindowInfo(nint Hwnd, string Title, int ProcessId);

public static class ProcessFinder
{
    public static List<WindowInfo> ListTopLevelWindows()
    {
        var results = new List<WindowInfo>();
        EnumWindows((hwnd, lparam) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            var title = GetTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title)) return true;
            GetWindowThreadProcessId(hwnd, out var pid);
            results.Add(new WindowInfo(hwnd, title, pid));
            return true;
        }, nint.Zero);
        return results;
    }

    private static string GetTitle(nint hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length <= 0) return string.Empty;
        var sb = new StringBuilder(length + 1);
        _ = GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out int lpdwProcessId);
}


