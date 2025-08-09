using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VRTDoubao.Audio;

public sealed class AudioRouter : IDisposable
{
    private readonly object _stateLock = new();
    private WasapiLoopbackCapture? _loopbackCapture;
    private WasapiOut? _cableAOut;
    private WasapiCapture? _micCapture;
    private BufferedWaveProvider? _buffered;
    private string? _renderDeviceId;
    private string? _cableADeviceId;
    private string? _micDeviceId;
    private string? _micTargetRenderId;

    public void SetRenderDevice(MMDevice device) => _renderDeviceId = device.ID;
    public void SetRenderDeviceId(string deviceId) => _renderDeviceId = deviceId;
    public void SetCableADevice(MMDevice device) => _cableADeviceId = device.ID;
    public void SetCableADeviceId(string deviceId) => _cableADeviceId = deviceId;
    public void SetMicDevice(MMDevice device) => _micDeviceId = device.ID;
    public void SetMicDeviceId(string deviceId) => _micDeviceId = deviceId;
    public void SetMicTargetRenderDevice(MMDevice device) => _micTargetRenderId = device.ID;
    public void SetMicTargetRenderDeviceId(string deviceId) => _micTargetRenderId = deviceId;

    public bool IsSpeakerDuplicationRunning { get; private set; }

    public async Task StartSpeakerDuplicationAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            // ensure exclusive mode
            if (IsMicForwardingRunning)
            {
                // stop mic path synchronously
                try { _micCapture?.StopRecording(); } catch { }
                try { _cableAOut?.Stop(); } catch { }
                _micCapture?.Dispose(); _micCapture = null;
                _cableAOut?.Dispose(); _cableAOut = null;
                _buffered = null;
                IsMicForwardingRunning = false;
            }
        }
        if (IsSpeakerDuplicationRunning) return;
        await Task.Run(() =>
        {
            using var enumerator = new MMDeviceEnumerator();
            var render = _renderDeviceId != null
                ? enumerator.GetDevice(_renderDeviceId)
                : enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (render is null) throw new InvalidOperationException("No render device");
            if (_cableADeviceId is null) throw new InvalidOperationException("No cable A device selected");
            var cable = enumerator.GetDevice(_cableADeviceId);
            if (cable is null) throw new InvalidOperationException("Cable A device not found");
            if (string.Equals(cable.ID, render.ID, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Target device equals source speaker. Please select 'CABLE Input A' as target, not the speaker.");
            }

            _loopbackCapture = new WasapiLoopbackCapture(render);
            _buffered = new BufferedWaveProvider(_loopbackCapture.WaveFormat)
            {
                DiscardOnBufferOverflow = true
            };
            _loopbackCapture.DataAvailable += (_, e) =>
            {
                _buffered?.AddSamples(e.Buffer, 0, e.BytesRecorded);
            };

            _cableAOut = new WasapiOut(cable, AudioClientShareMode.Shared, false, 50);
            _cableAOut.Init(_buffered);

            _cableAOut.Play();
            _loopbackCapture.StartRecording();
            IsSpeakerDuplicationRunning = true;
        }, ct);
    }

    public async Task StopSpeakerDuplicationAsync(CancellationToken ct = default)
    {
        if (!IsSpeakerDuplicationRunning) return;
        await Task.Run(() =>
        {
            try { _loopbackCapture?.StopRecording(); } catch { }
            try { _cableAOut?.Stop(); } catch { }
            _loopbackCapture?.Dispose(); _loopbackCapture = null;
            _cableAOut?.Dispose(); _cableAOut = null;
            _buffered = null;
            IsSpeakerDuplicationRunning = false;
        }, ct);
    }

    public bool IsMicForwardingRunning { get; private set; }

    public async Task StartMicForwardingAsync(CancellationToken ct = default)
    {
        // 麦克风只供豆包2：将麦克风采集转发到指定的渲染目标（建议选择 CABLE Input B 或豆包2可用的虚拟输入）。
        lock (_stateLock)
        {
            if (IsSpeakerDuplicationRunning)
            {
                try { _loopbackCapture?.StopRecording(); } catch { }
                try { _cableAOut?.Stop(); } catch { }
                _loopbackCapture?.Dispose(); _loopbackCapture = null;
                _cableAOut?.Dispose(); _cableAOut = null;
                _buffered = null;
                IsSpeakerDuplicationRunning = false;
            }
            if (IsMicForwardingRunning) return;
        }
        await Task.Run(() =>
        {
            using var enumerator = new MMDeviceEnumerator();
            if (_micDeviceId is null) throw new InvalidOperationException("No microphone selected");
            if (_micTargetRenderId is null) throw new InvalidOperationException("No microphone target render selected");
            var mic = enumerator.GetDevice(_micDeviceId);
            var target = enumerator.GetDevice(_micTargetRenderId);

            _micCapture = new WasapiCapture(mic);
            _buffered = new BufferedWaveProvider(_micCapture.WaveFormat) { DiscardOnBufferOverflow = true };
            _micCapture.DataAvailable += (_, e) => _buffered?.AddSamples(e.Buffer, 0, e.BytesRecorded);

            _cableAOut = new WasapiOut(target, AudioClientShareMode.Shared, false, 50);
            _cableAOut.Init(_buffered);
            _cableAOut.Play();
            _micCapture.StartRecording();
            IsMicForwardingRunning = true;
        }, ct);
    }

    public async Task StopMicForwardingAsync(CancellationToken ct = default)
    {
        if (!IsMicForwardingRunning) return;
        await Task.Run(() =>
        {
            try { _micCapture?.StopRecording(); } catch { }
            try { _cableAOut?.Stop(); } catch { }
            _micCapture?.Dispose(); _micCapture = null;
            _cableAOut?.Dispose(); _cableAOut = null;
            _buffered = null;
            IsMicForwardingRunning = false;
        }, ct);
    }

    public void Dispose()
    {
        _loopbackCapture?.Dispose();
        _cableAOut?.Dispose();
    }
}


