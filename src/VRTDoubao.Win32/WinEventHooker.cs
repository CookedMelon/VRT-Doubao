using System;
using System.Runtime.InteropServices;

namespace VRTDoubao.Win32;

public sealed class WinEventHooker : IDisposable
{
    private readonly nint _hook;
    private readonly WinEventDelegate _callback;
    private readonly nint _target;
    private readonly Action _onChanged;

    private const uint WINEVENT_OUTOFCONTEXT = 0;
    private const uint EVENT_MIN = 0x00000001;
    private const uint EVENT_MAX = 0x7FFFFFFF;
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint EVENT_OBJECT_HIDE = 0x8003;
    private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    private const uint EVENT_OBJECT_DESTROY = 0x8001;
    private const uint EVENT_OBJECT_CREATE = 0x8000;
    private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    private const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;

    public WinEventHooker(nint targetHwnd, Action onChanged)
    {
        _target = targetHwnd;
        _onChanged = onChanged;
        _callback = Callback;
        _hook = SetWinEventHook(EVENT_OBJECT_CREATE, EVENT_OBJECT_LOCATIONCHANGE, nint.Zero, _callback,
            0, 0, WINEVENT_OUTOFCONTEXT);
    }

    private void Callback(nint hWinEventHook, uint @event, nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == _target && (@event == EVENT_OBJECT_SHOW || @event == EVENT_OBJECT_HIDE ||
                                 @event == EVENT_OBJECT_LOCATIONCHANGE || @event == EVENT_OBJECT_DESTROY ||
                                 @event == EVENT_OBJECT_CREATE || @event == EVENT_SYSTEM_MINIMIZEEND ||
                                 @event == EVENT_SYSTEM_MINIMIZESTART))
        {
            try { _onChanged(); } catch { }
        }
    }

    public void Dispose()
    {
        if (_hook != nint.Zero) UnhookWinEvent(_hook);
    }

    private delegate void WinEventDelegate(nint hWinEventHook, uint @event, nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(nint hWinEventHook);
}


