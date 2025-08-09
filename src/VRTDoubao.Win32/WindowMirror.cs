using System;
using System.Runtime.InteropServices;

namespace VRTDoubao.Win32;

public static class WindowMirror
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx; public int cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_THUMBNAIL_PROPERTIES
    {
        public uint dwFlags;
        public RECT rcDestination;
        public RECT rcSource;
        public byte opacity;
        public bool fVisible;
        public bool fSourceClientAreaOnly;
    }

    private const uint DWM_TNP_RECTDESTINATION = 0x00000001;
    private const uint DWM_TNP_RECTSOURCE = 0x00000002;
    private const uint DWM_TNP_VISIBLE = 0x00000008;
    private const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

    public static bool MirrorToHost(nint sourceHwnd, nint hostHwnd, out nint thumbHandle)
    {
        thumbHandle = nint.Zero;
        if (sourceHwnd == nint.Zero || hostHwnd == nint.Zero) return false;
        var hr = DwmRegisterThumbnail(hostHwnd, sourceHwnd, out var hthumb);
        if (hr != 0 || hthumb == nint.Zero) return false;
        thumbHandle = hthumb;
        return true;
    }

    public static void UpdateLayout(nint thumbHandle, nint hostTopLevelHwnd, int x, int y, int width, int height)
    {
        if (thumbHandle == nint.Zero || hostTopLevelHwnd == nint.Zero) return;
        // Query source size
        if (DwmQueryThumbnailSourceSize(thumbHandle, out var src) != 0) return;
        int displayWidth = Math.Min(width, src.cx);
        int displayHeight = Math.Min(height, src.cy);
        int dx = x + (width - displayWidth) / 2;
        int dy = y + (height - displayHeight) / 2;

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_RECTSOURCE | DWM_TNP_VISIBLE | DWM_TNP_SOURCECLIENTAREAONLY,
            rcDestination = new RECT { left = dx, top = dy, right = dx + displayWidth, bottom = dy + displayHeight },
            rcSource = new RECT { left = 0, top = 0, right = displayWidth, bottom = displayHeight },
            fVisible = true,
            fSourceClientAreaOnly = true
        };
        DwmUpdateThumbnailProperties(thumbHandle, ref props);
    }

    public static void Unmirror(ref nint thumbHandle)
    {
        if (thumbHandle != nint.Zero)
        {
            DwmUnregisterThumbnail(thumbHandle);
            thumbHandle = nint.Zero;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmRegisterThumbnail(nint dest, nint src, out nint thumb);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUnregisterThumbnail(nint thumb);

    [DllImport("dwmapi.dll")]
    private static extern int DwmUpdateThumbnailProperties(nint hthumb, ref DWM_THUMBNAIL_PROPERTIES props);

    [DllImport("dwmapi.dll")]
    private static extern int DwmQueryThumbnailSourceSize(nint hthumb, out SIZE pSize);
}


