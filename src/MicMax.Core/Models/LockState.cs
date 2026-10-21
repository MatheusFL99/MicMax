namespace MicMax.Core.Models;

/// <summary>
/// Represents the user's intent regarding the microphone lock for a session.
/// </summary>
public class LockState
{
    /// <summary>
    /// Whether the lock is currently active. true = locking enabled, false = locking disabled/paused.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Saves the active state before a pause, so resume restores to 100% unmuted (per Assumptions in spec.md).
    /// </summary>
    public bool WasActiveBeforePause { get; set; }

    /// <summary>
    /// Timestamp of when the lock was last activated.
    /// </summary>
    public DateTime LastActivationTime { get; set; }

    /// <summary>
    /// Timestamp of when the lock was last deactivated or paused.
    /// </summary>
    public DateTime LastDeactivationTime { get; set; }
}