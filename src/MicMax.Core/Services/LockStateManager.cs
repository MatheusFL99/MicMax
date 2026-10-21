using System;
using MicMax.Core.Models;

namespace MicMax.Core.Services;

/// <summary>
/// Lock state values enforced by <see cref="LockStateManager"/>.
/// </summary>
public enum LockStateStatus
{
    /// <summary>Lock is not active.</summary>
    Inactive,

    /// <summary>Lock intent is recorded but no device is available yet (FR-014).</summary>
    Pending,

    /// <summary>Lock is active and correcting external changes.</summary>
    Active,

    /// <summary>Lock is paused — external changes are not reverted.</summary>
    Paused
}

/// <summary>
/// Enforces the data-model.md LockState transition table with
/// WasActiveBeforePause semantics. Pure in-memory state machine (no I/O).
/// </summary>
public sealed class LockStateManager
{
    private LockStateStatus _status = LockStateStatus.Inactive;
    private bool _wasActiveBeforePause;

    /// <summary>Current status.</summary>
    public LockStateStatus Status => _status;

    /// <summary>True when the lock is correcting changes.</summary>
    public bool IsActive => _status == LockStateStatus.Active;

    /// <summary>True when the lock was active before being paused.</summary>
    public bool WasActiveBeforePause => _wasActiveBeforePause;

    /// <summary>
    /// Snapshot of the current state for persistence.
    /// </summary>
    public LockState ToLockState() => new()
    {
        IsActive = _status is LockStateStatus.Active or LockStateStatus.Pending or LockStateStatus.Paused,
        WasActiveBeforePause = _wasActiveBeforePause,
        LastActivationTime = DateTime.UtcNow
    };

    /// <summary>
    /// Inactive → Active. Records that the lock is now correcting.
    /// </summary>
    /// <returns>True when the transition occurred.</returns>
    public bool Activate()
    {
        if (_status == LockStateStatus.Active)
        {
            return false;
        }

        _wasActiveBeforePause = false;
        _status = LockStateStatus.Active;
        return true;
    }

    /// <summary>
    /// Active → Paused. Saves WasActiveBeforePause so Resume restores the lock.
    /// </summary>
    /// <returns>True when the transition occurred.</returns>
    public bool Pause()
    {
        if (_status != LockStateStatus.Active)
        {
            return false;
        }

        _wasActiveBeforePause = true;
        _status = LockStateStatus.Paused;
        return true;
    }

    /// <summary>
    /// Paused → Active. Clears the pause flag and resumes correction.
    /// </summary>
    /// <returns>True when the transition occurred.</returns>
    public bool Resume()
    {
        if (_status != LockStateStatus.Paused)
        {
            return false;
        }

        _wasActiveBeforePause = false;
        _status = LockStateStatus.Active;
        return true;
    }

    /// <summary>
    /// Any → Inactive. Clears the pause memory.
    /// </summary>
    /// <returns>True when the transition occurred.</returns>
    public bool Deactivate()
    {
        if (_status == LockStateStatus.Inactive)
        {
            return false;
        }

        _wasActiveBeforePause = false;
        _status = LockStateStatus.Inactive;
        return true;
    }

    /// <summary>
    /// Any non-pending status → Pending: the user wants the lock but no device
    /// is available (FR-014), or the locked device vanished. Returns to Active
    /// when a device appears.
    /// </summary>
    /// <returns>True when the transition occurred.</returns>
    public bool MarkPending()
    {
        if (_status == LockStateStatus.Pending)
        {
            return false;
        }

        _status = LockStateStatus.Pending;
        return true;
    }

    /// <summary>
    /// Pending → Active: a device became available and was locked.
    /// </summary>
    /// <returns>True when the transition occurred.</returns>
    public bool CompletePending()
    {
        if (_status != LockStateStatus.Pending)
        {
            return false;
        }

        _status = LockStateStatus.Active;
        return true;
    }

    /// <summary>
    /// Restores state from a persisted snapshot (app startup).
    /// </summary>
    /// <param name="wasActive">Whether the lock was active when last saved.</param>
    public void Restore(bool wasActive)
    {
        _wasActiveBeforePause = false;
        _status = wasActive ? LockStateStatus.Pending : LockStateStatus.Inactive;
    }
}
