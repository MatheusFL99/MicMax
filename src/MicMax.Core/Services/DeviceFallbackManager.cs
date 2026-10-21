using System;
using MicMax.Core.Audio;
using MicMax.Core.Logging;
using MicMax.Core.Services;

namespace MicMax.Core.Services;

/// <summary>
/// Keeps the lock following the right device when the topology changes
/// (User Story 4): switches to the default communication device when the
/// locked device disconnects (FR-012), re-locks the original device when it
/// returns (FR-013), holds a pending lock when no device exists (FR-014), and
/// settles on the current default during rapid churn without crashing or
/// notification spam (FR-017).
/// </summary>
public sealed class DeviceFallbackManager : IDisposable
{
    private readonly IAudioDeviceManager _deviceManager;
    private readonly VolumeLockService _lockService;
    private readonly object _gate = new();

    private Guid? _originalDeviceGuid;
    private bool _listening;
    private bool _disposed;

    /// <summary>
    /// Creates the manager and subscribes to device topology changes.
    /// </summary>
    public DeviceFallbackManager(IAudioDeviceManager deviceManager, VolumeLockService lockService)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));

        _deviceManager.DevicesChanged += OnDevicesChanged;
    }

    /// <summary>
    /// Raised when the lock moved to a different device. <see cref="DeviceSwitchedEventArgs.IsRestored"/>
    /// is true when the originally selected device was re-locked (FR-013).
    /// </summary>
    public event EventHandler<DeviceSwitchedEventArgs>? DeviceSwitched;

    /// <summary>Starts listening for topology changes.</summary>
    public void Start()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_listening)
            {
                return;
            }

            _deviceManager.StartListening();
            _listening = true;
        }
    }

    /// <summary>Stops listening for topology changes.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (_disposed || !_listening)
            {
                return;
            }

            _deviceManager.StopListening();
            _listening = false;
        }
    }

    /// <summary>
    /// Records the user's explicitly chosen device so it can be identified as a
    /// restoration when it returns. Pass null when following the default.
    /// </summary>
    public void SetOriginalDevice(Guid? deviceGuid)
    {
        lock (_gate)
        {
            _originalDeviceGuid = deviceGuid;
        }
    }

    private void OnDevicesChanged(object? sender, DeviceChangeEventArgs e)
    {
        try
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                ProcessChange(e);
            }
        }
        catch (Exception ex)
        {
            // Never crash on rapid churn (FR-017); log and continue.
            EventLogger.LogError($"Device change handling failed: {ex.Message}");
        }
    }

    private void ProcessChange(DeviceChangeEventArgs e)
    {
        if (!_lockService.IsActive && _lockService.State.Status != LockStateStatus.Pending)
        {
            // Lock not engaged — nothing to follow.
            return;
        }

        var previousGuid = _lockService.LockedDeviceGuid;
        var changed = _lockService.HandleDeviceChange(e.DeviceGuid, e.Kind, out var newGuid);

        if (!changed || newGuid == null || newGuid == previousGuid)
        {
            return;
        }

        var isRestored = _originalDeviceGuid.HasValue && newGuid == _originalDeviceGuid.Value;

        if (isRestored)
        {
            EventLogger.LogInfo($"Original device {_originalDeviceGuid} reconnected; lock restored.");
        }

        DeviceSwitched?.Invoke(this, new DeviceSwitchedEventArgs(
            newGuid.Value,
            _lockService.LockedDeviceName,
            isRestored));
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

            if (_listening)
            {
                _deviceManager.StopListening();
                _listening = false;
            }

            _deviceManager.DevicesChanged -= OnDevicesChanged;
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DeviceFallbackManager));
        }
    }
}

/// <summary>
/// Payload for a device switch event: which device is now locked and whether
/// this was a restoration of the original device.
/// </summary>
public sealed class DeviceSwitchedEventArgs : EventArgs
{
    public DeviceSwitchedEventArgs(Guid deviceGuid, string? deviceName, bool isRestored)
    {
        DeviceGuid = deviceGuid;
        DeviceName = deviceName;
        IsRestored = isRestored;
    }

    public Guid DeviceGuid { get; }

    public string? DeviceName { get; }

    public bool IsRestored { get; }
}
