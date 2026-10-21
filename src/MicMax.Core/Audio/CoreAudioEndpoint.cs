using System;
using System.Runtime.InteropServices;

namespace MicMax.Core.Audio;

/// <summary>
/// Production <see cref="IAudioEndpoint"/> that wraps an IMMDevice's
/// IAudioEndpointVolume. Reads and writes only scalar volume and mute state —
/// never audio content (FR-015). Works on standard user accounts (FR-016).
/// </summary>
public sealed class CoreAudioEndpoint : IAudioEndpoint
{
    private readonly CoreAudioInterop.IMMDevice _device;
    private readonly CoreAudioInterop.IAudioEndpointVolume _volume;
    private readonly AudioVolumeCallback _callback;
    private readonly object _gate = new();
    private bool _monitoring;
    private bool _disposed;

    /// <summary>
    /// Opens the volume interface for an already-resolved IMMDevice.
    /// </summary>
    /// <param name="device">The MMDevice.</param>
    /// <param name="deviceGuid">Persistent endpoint GUID.</param>
    /// <param name="displayName">Friendly name for notifications.</param>
    public CoreAudioEndpoint(CoreAudioInterop.IMMDevice device, Guid deviceGuid, string displayName)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        DeviceGuid = deviceGuid;
        DisplayName = displayName;

        var iid = CoreAudioInterop.IAudioEndpointVolumeIid;
        CoreAudioInterop.ThrowIfFailed(
            device.Activate(ref iid, CoreAudioInterop.ClsCtxInprocServer, IntPtr.Zero, out var obj),
            "IMMDevice.Activate(IAudioEndpointVolume)");

        _volume = (CoreAudioInterop.IAudioEndpointVolume)obj!;
        _callback = new AudioVolumeCallback();
        _callback.VolumeChanged += OnNativeVolumeChanged;
    }

    /// <inheritdoc />
    public Guid DeviceGuid { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public float GetCurrentVolume()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            CoreAudioInterop.ThrowIfFailed(
                _volume.GetMasterVolumeLevelScalar(out var level),
                "IAudioEndpointVolume.GetMasterVolumeLevelScalar");
            return level;
        }
    }

    /// <inheritdoc />
    public void SetVolume(float level)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var clamped = Math.Clamp(level, 0f, 1f);
            CoreAudioInterop.ThrowIfFailed(
                _volume.SetMasterVolumeLevelScalar(clamped, IntPtr.Zero),
                "IAudioEndpointVolume.SetMasterVolumeLevelScalar");
        }
    }

    /// <inheritdoc />
    public bool GetCurrentMuteState()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            CoreAudioInterop.ThrowIfFailed(
                _volume.GetMute(out var muted),
                "IAudioEndpointVolume.GetMute");
            return muted;
        }
    }

    /// <inheritdoc />
    public void SetMute(bool muted)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            CoreAudioInterop.ThrowIfFailed(
                _volume.SetMute(muted, IntPtr.Zero),
                "IAudioEndpointVolume.SetMute");
        }
    }

    /// <inheritdoc />
    public void MonitorVolumeChanges()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_monitoring)
            {
                return;
            }

            CoreAudioInterop.ThrowIfFailed(
                _volume.RegisterControlChangeNotify(_callback),
                "IAudioEndpointVolume.RegisterControlChangeNotify");
            _monitoring = true;
        }
    }

    /// <inheritdoc />
    public void StopMonitoring()
    {
        lock (_gate)
        {
            if (_disposed || !_monitoring)
            {
                return;
            }

            try
            {
                CoreAudioInterop.ThrowIfFailed(
                    _volume.UnregisterControlChangeNotify(_callback),
                    "IAudioEndpointVolume.UnregisterControlChangeNotify");
            }
            catch (Exception ex)
            {
                Logging.EventLogger.LogWarning(
                    $"UnregisterControlChangeNotify failed for endpoint {DeviceGuid}: {ex.Message}");
            }

            _monitoring = false;
        }
    }

    /// <inheritdoc />
    public event EventHandler<VolumeChangeEventArgs>? VolumeChanged;

    private void OnNativeVolumeChanged(object? sender, VolumeChangeEventArgs e)
    {
        VolumeChanged?.Invoke(this, e);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (_monitoring)
                {
                    _volume.UnregisterControlChangeNotify(_callback);
                    _monitoring = false;
                }
            }
            catch
            {
                // Best effort during teardown.
            }

            _callback.VolumeChanged -= OnNativeVolumeChanged;
            _disposed = true;

            if (Marshal.IsComObject(_volume))
            {
                Marshal.ReleaseComObject(_volume);
            }

            if (Marshal.IsComObject(_device))
            {
                Marshal.ReleaseComObject(_device);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CoreAudioEndpoint));
        }
    }
}
