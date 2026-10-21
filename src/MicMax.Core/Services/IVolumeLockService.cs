using System;
using MicMax.Core.Audio;

namespace MicMax.Core.Services;

/// <summary>
/// Core Audio Control contract (contracts/README.md §1) plus lock lifecycle.
/// Sets the microphone to 100% unmuted and reverts external changes within
/// one second using event-driven monitoring (no polling).
/// </summary>
public interface IVolumeLockService
{
    /// <summary>Whether the lock is currently active (correcting changes).</summary>
    bool IsActive { get; }

    /// <summary>The endpoint currently under lock, when one is resolved.</summary>
    Guid? LockedDeviceGuid { get; }

    /// <summary>
    /// Resolves the target device, sets it to 100% unmuted, and begins
    /// monitoring. Returns false when no input device is available (pending).
    /// </summary>
    bool ActivateLock();

    /// <summary>Deactivates the lock and stops monitoring.</summary>
    void DeactivateLock();

    /// <summary>Suspends correction without changing the recorded lock intent.</summary>
    void Pause();

    /// <summary>Resumes correction, immediately restoring 100% unmuted.</summary>
    void Resume();

    /// <summary>Sets the endpoint's scalar volume to 100%.</summary>
    void SetVolumeTo100Percent();

    /// <summary>Returns the endpoint's current scalar volume (0.0 to 1.0).</summary>
    float GetCurrentVolume();

    /// <summary>Returns whether the endpoint is currently muted.</summary>
    bool GetCurrentMuteState();

    /// <summary>Clears the endpoint's mute state.</summary>
    void SetMuteToUnmuted();

    /// <summary>Starts event-driven monitoring of volume/mute changes.</summary>
    void MonitorVolumeChanges();

    /// <summary>Stops event-driven monitoring.</summary>
    void StopMonitoring();

    /// <summary>Raised when the lock is activated, deactivated, paused, or resumed.</summary>
    event EventHandler<LockStateChangedEventArgs>? LockStateChanged;

    /// <summary>Raised when an external change was detected and reverted.</summary>
    event EventHandler<VolumeChangeEventArgs>? CorrectionApplied;
}

/// <summary>Payload describing a lock lifecycle transition.</summary>
public sealed class LockStateChangedEventArgs : EventArgs
{
    public LockStateChangedEventArgs(LockTransition transition, Guid? deviceGuid, string? deviceName)
    {
        Transition = transition;
        DeviceGuid = deviceGuid;
        DeviceName = deviceName;
    }

    public LockTransition Transition { get; }

    public Guid? DeviceGuid { get; }

    public string? DeviceName { get; }
}

/// <summary>The kind of lock transition that occurred.</summary>
public enum LockTransition
{
    Activated,
    Deactivated,
    Paused,
    Resumed
}
