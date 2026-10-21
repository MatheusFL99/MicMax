using System;
using MicMax.Core.Audio;
using MicMax.Core.Logging;

namespace MicMax.Core.Services;

/// <summary>
/// Implements the Core Audio Control contract. While active it keeps the
/// locked endpoint at 100% and unmuted, reverting any external change within
/// one second using event-driven callbacks (no polling, negligible idle cost
/// for SC-003). Never touches audio content (FR-015). Resolves the target
/// device by preferring an explicit selection and otherwise following the
/// Windows default communication device (FR-001, FR-012, FR-013, FR-014).
/// </summary>
public sealed class VolumeLockService : IVolumeLockService, IDisposable
{
    private readonly IAudioDeviceManager _deviceManager;
    private readonly LockStateManager _stateManager;
    private readonly object _gate = new();

    private IAudioEndpoint? _endpoint;
    private Guid? _selectedDeviceGuid;
    private bool _monitoring;
    private bool _disposed;

    /// <summary>
    /// Creates the service against a device manager. When no explicit device
    /// is selected, the Windows default communication device is targeted.
    /// </summary>
    public VolumeLockService(IAudioDeviceManager deviceManager, LockStateManager? stateManager = null)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _stateManager = stateManager ?? new LockStateManager();
    }

    /// <inheritdoc />
    public bool IsActive { get; private set; }

    /// <inheritdoc />
    public Guid? LockedDeviceGuid
    {
        get { lock (_gate) { return _endpoint?.DeviceGuid; } }
    }

    /// <summary>Display name of the currently locked endpoint, for notifications.</summary>
    public string? LockedDeviceName
    {
        get { lock (_gate) { return _endpoint?.DisplayName; } }
    }

    /// <summary>True when an explicit device (rather than follow-default) is chosen.</summary>
    public bool HasExplicitSelection
    {
        get { lock (_gate) { return _selectedDeviceGuid.HasValue; } }
    }

    /// <summary>The lock state machine shared with the tray/UI.</summary>
    public LockStateManager State => _stateManager;

    /// <inheritdoc />
    public event EventHandler<LockStateChangedEventArgs>? LockStateChanged;

    /// <inheritdoc />
    public event EventHandler<VolumeChangeEventArgs>? CorrectionApplied;

    /// <summary>
    /// Selects the target endpoint for subsequent binds. Null means "follow
    /// the Windows default communication device". Rebinds immediately when the
    /// lock is active.
    /// </summary>
    public void SelectDevice(Guid? deviceGuid)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _selectedDeviceGuid = deviceGuid;

            if (IsActive)
            {
                RebindAndApply();
            }
        }
    }

    /// <inheritdoc />
    public bool ActivateLock()
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            if (IsActive)
            {
                return true;
            }

            if (!RebindAndApply())
            {
                // No device available: keep the intent pending (FR-014).
                _stateManager.MarkPending();
                EventLogger.LogWarning("Lock activation pending: no audio input device available.");
                return false;
            }

            IsActive = true;
            if (_stateManager.Status == LockStateStatus.Pending)
            {
                _stateManager.CompletePending();
            }
            else
            {
                _stateManager.Activate();
            }

            RaiseLockStateChanged(LockTransition.Activated);
            EventLogger.LogInfo("Microphone lock activated at 100% unmuted.");
            return true;
        }
    }

    /// <inheritdoc />
    public void DeactivateLock()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (!IsActive && _endpoint == null && _stateManager.Status == LockStateStatus.Inactive)
            {
                return;
            }

            StopMonitoringUnlocked();
            ReleaseEndpointUnlocked();
            IsActive = false;
            _stateManager.Deactivate();
            RaiseLockStateChanged(LockTransition.Deactivated);
            EventLogger.LogInfo("Microphone lock deactivated.");
        }
    }

    /// <inheritdoc />
    public void Pause()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (!IsActive || _stateManager.Status != LockStateStatus.Active)
            {
                return;
            }

            StopMonitoringUnlocked();
            _stateManager.Pause();
            RaiseLockStateChanged(LockTransition.Paused);
            EventLogger.LogInfo("Microphone lock paused.");
        }
    }

    /// <inheritdoc />
    public void Resume()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_stateManager.Status != LockStateStatus.Paused)
            {
                return;
            }

            _stateManager.Resume();
            IsActive = true;

            // Immediately restore 100% unmuted on resume (US2 acceptance 3).
            RebindAndApply();

            RaiseLockStateChanged(LockTransition.Resumed);
            EventLogger.LogInfo("Microphone lock resumed.");
        }
    }

    /// <inheritdoc />
    public void SetVolumeTo100Percent()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _endpoint?.SetVolume(1.0f);
        }
    }

    /// <inheritdoc />
    public float GetCurrentVolume()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return _endpoint?.GetCurrentVolume() ?? 0f;
        }
    }

    /// <inheritdoc />
    public bool GetCurrentMuteState()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return _endpoint?.GetCurrentMuteState() ?? true;
        }
    }

    /// <inheritdoc />
    public void SetMuteToUnmuted()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _endpoint?.SetMute(false);
        }
    }

    /// <inheritdoc />
    public void MonitorVolumeChanges()
    {
        lock (_gate)
        {
            StartMonitoringUnlocked();
        }
    }

    /// <inheritdoc />
    public void StopMonitoring()
    {
        lock (_gate)
        {
            StopMonitoringUnlocked();
        }
    }

    /// <summary>
    /// Re-resolves the lock target after a device topology change. Prefers the
    /// explicit selection when it is available, otherwise follows the default
    /// communication device; holds a pending lock when none exists (FR-012,
    /// FR-013, FR-014).
    /// </summary>
    /// <param name="changedGuid">The affected endpoint, when known.</param>
    /// <param name="kind">The kind of change.</param>
    /// <param name="newDeviceGuid">The endpoint now locked, when the target changed.</param>
    /// <returns>True when the locked endpoint changed as a result.</returns>
    public bool HandleDeviceChange(Guid? changedGuid, DeviceChangeKind kind, out Guid? newDeviceGuid)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            newDeviceGuid = null;

            if (_stateManager.Status == LockStateStatus.Inactive)
            {
                return false;
            }

            var desired = ResolveTargetGuid();

            if (desired == null)
            {
                // No usable device at all: hold pending (FR-014).
                if (_endpoint != null)
                {
                    StopMonitoringUnlocked();
                    ReleaseEndpointUnlocked();
                    IsActive = false;
                    _stateManager.MarkPending();
                    EventLogger.LogWarning("No input device available; lock pending.");
                }

                return false;
            }

            var currentGuid = _endpoint?.DeviceGuid;
            if (_endpoint != null && currentGuid == desired)
            {
                // Already locked to the right device — settle with no churn.
                if (_stateManager.Status == LockStateStatus.Pending)
                {
                    _stateManager.CompletePending();
                }

                return false;
            }

            // Rebind to the newly resolved target.
            StopMonitoringUnlocked();
            ReleaseEndpointUnlocked();

            var endpoint = _deviceManager.OpenEndpoint(desired.Value);
            if (endpoint == null)
            {
                _stateManager.MarkPending();
                return false;
            }

            _endpoint = endpoint;
            _endpoint.VolumeChanged += OnEndpointVolumeChanged;

            if (_stateManager.Status == LockStateStatus.Pending)
            {
                _stateManager.CompletePending();
            }

            IsActive = true;
            ApplyTargetState();
            StartMonitoringUnlocked();

            newDeviceGuid = desired;
            EventLogger.LogInfo($"Lock moved to device {desired} ({endpoint.DisplayName}).");
            return true;
        }
    }

    /// <summary>
    /// Resolves the endpoint that should currently be locked: the explicit
    /// selection when it is available, otherwise the default communication
    /// device, otherwise null.
    /// </summary>
    private Guid? ResolveTargetGuid()
    {
        if (_selectedDeviceGuid.HasValue && _deviceManager.IsDeviceAvailable(_selectedDeviceGuid.Value))
        {
            return _selectedDeviceGuid;
        }

        var defaultDevice = _deviceManager.GetDefaultCommunicationDevice();
        return defaultDevice?.GUID;
    }

    /// <summary>
    /// Releases any bound endpoint, opens the resolved target, applies
    /// 100% unmuted, and starts monitoring. Returns false when no target exists.
    /// </summary>
    private bool RebindAndApply()
    {
        StopMonitoringUnlocked();
        ReleaseEndpointUnlocked();

        var target = ResolveTargetGuid();
        if (target == null)
        {
            return false;
        }

        var endpoint = _deviceManager.OpenEndpoint(target.Value);
        if (endpoint == null)
        {
            return false;
        }

        _endpoint = endpoint;
        _endpoint.VolumeChanged += OnEndpointVolumeChanged;

        ApplyTargetState();
        StartMonitoringUnlocked();
        return true;
    }

    private void StartMonitoringUnlocked()
    {
        if (_monitoring || _endpoint == null)
        {
            return;
        }

        _endpoint.MonitorVolumeChanges();
        _monitoring = true;
    }

    private void StopMonitoringUnlocked()
    {
        if (!_monitoring || _endpoint == null)
        {
            return;
        }

        _endpoint.StopMonitoring();
        _monitoring = false;
    }

    private void ReleaseEndpointUnlocked()
    {
        if (_endpoint == null)
        {
            return;
        }

        _endpoint.VolumeChanged -= OnEndpointVolumeChanged;
        _endpoint.Dispose();
        _endpoint = null;
    }

    /// <summary>
    /// Applies 100% unmuted to the bound endpoint; no-op when already correct.
    /// </summary>
    private void ApplyTargetState()
    {
        if (_endpoint == null)
        {
            return;
        }

        try
        {
            var level = _endpoint.GetCurrentVolume();
            var muted = _endpoint.GetCurrentMuteState();

            if (level < 0.999f || muted)
            {
                _endpoint.SetVolume(1.0f);
                if (muted)
                {
                    _endpoint.SetMute(false);
                }

                CorrectionApplied?.Invoke(this, new VolumeChangeEventArgs(1.0f, false));
            }
        }
        catch (Exception ex)
        {
            EventLogger.LogError($"Failed to apply target volume state: {ex.Message}");
        }
    }

    /// <summary>
    /// Event-driven correction: restore 100% unmuted whenever Windows reports a
    /// change (well within one second), unless paused.
    /// </summary>
    private void OnEndpointVolumeChanged(object? sender, VolumeChangeEventArgs e)
    {
        try
        {
            lock (_gate)
            {
                if (_disposed || !IsActive)
                {
                    return;
                }

                if (_stateManager.Status == LockStateStatus.Paused)
                {
                    return;
                }

                if (_endpoint == null)
                {
                    return;
                }

                if (e.Volume >= 0.999f && !e.Muted)
                {
                    // Already correct: negligible no-op (acceptance scenario 4).
                    return;
                }

                _endpoint.SetVolume(1.0f);
                if (e.Muted)
                {
                    _endpoint.SetMute(false);
                }

                EventLogger.LogVerbose(
                    $"Reverted external change (level={e.Volume:F2}, muted={e.Muted}) to 100% unmuted.");
            }

            CorrectionApplied?.Invoke(this, new VolumeChangeEventArgs(1.0f, false));
        }
        catch (Exception ex)
        {
            EventLogger.LogError($"Correction failed: {ex.Message}");
        }
    }

    private void RaiseLockStateChanged(LockTransition transition)
    {
        var args = new LockStateChangedEventArgs(transition, _endpoint?.DeviceGuid, _endpoint?.DisplayName);
        LockStateChanged?.Invoke(this, args);
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

            StopMonitoringUnlocked();
            ReleaseEndpointUnlocked();
            IsActive = false;
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VolumeLockService));
        }
    }
}
