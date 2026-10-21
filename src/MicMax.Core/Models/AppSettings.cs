namespace MicMax.Core.Models;

/// <summary>
/// The combination of device selection, lock state, and auto-start preference; persisted locally per user via the Windows Registry.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Which device the lock applies to.
    /// </summary>
    public DeviceSelection SelectedDevice { get; set; } = new();

    /// <summary>
    /// The user's current lock intent.
    /// </summary>
    public LockState LockState { get; set; } = new();

    /// <summary>
    /// Whether MicMax should launch automatically at Windows sign-in.
    /// </summary>
    public bool AutoStartEnabled { get; set; }

    /// <summary>
    /// Timestamp of when settings were last persisted.
    /// </summary>
    public DateTime LastSavedTime { get; set; }
}