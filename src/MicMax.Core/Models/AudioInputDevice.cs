namespace MicMax.Core.Models;

/// <summary>
/// Represents a microphone input device available to the Windows audio system.
/// </summary>
public class AudioInputDevice
{
    /// <summary>
    /// Unique identifier for the audio endpoint. Provides stable identification across renames, driver updates, and dock/undock scenarios.
    /// </summary>
    public Guid GUID { get; set; }

    /// <summary>
    /// Human-readable name of the microphone device as exposed by Windows.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Current input level percentage (0-100). Set to 100 when lock is active.
    /// </summary>
    public int InputLevel { get; set; }

    /// <summary>
    /// Current mute state. false = unmuted, true = muted. Set to false when lock is active.
    /// </summary>
    public bool MuteState { get; set; }

    /// <summary>
    /// Whether the device is currently available/active on the system.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Whether this device is the Windows default communication recording device.
    /// </summary>
    public bool IsDefaultCommunication { get; set; }

    /// <summary>
    /// Tracks how the device was selected.
    /// </summary>
    public DeviceCategory DeviceCategory { get; set; }
}

/// <summary>
/// Enum for device category.
/// </summary>
public enum DeviceCategory
{
    /// <summary>
    /// Default communication device.
    /// </summary>
    DefaultCommunication,

    /// <summary>
    /// Explicitly selected device.
    /// </summary>
    ExplicitlySelected,

    /// <summary>
    /// Fallback device.
    /// </summary>
    Fallback
}