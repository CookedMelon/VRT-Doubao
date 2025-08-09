using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;

namespace VRTDoubao.Audio;

public static class DeviceSelector
{
    public static IReadOnlyList<MMDevice> ListRenderDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .OrderBy(d => d.FriendlyName)
            .ToList();
    }

    public static MMDevice? GetDefaultRender()
    {
        using var enumerator = new MMDeviceEnumerator();
        try { return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); }
        catch { return null; }
    }

    public static IReadOnlyList<MMDevice> ListCaptureDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .OrderBy(d => d.FriendlyName)
            .ToList();
    }

    public static MMDevice? GetDefaultCapture()
    {
        using var enumerator = new MMDeviceEnumerator();
        try { return enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia); }
        catch { return null; }
    }
}


