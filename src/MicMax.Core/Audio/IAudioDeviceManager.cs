using System;
using MicMax.Core.Models;

namespace MicMax.Core.Audio;

/// <summary>
/// Event args carrying the new scalar volume (0..1) and mute state after an
/// endpoint volume/mute change notification.
/// </summary>
public sealed class VolumeChangeEventArgs : EventArgs
{
    public VolumeChangeEventArgs(float volume, bool muted)
    {
        Volume = volume;
        Muted = muted;
    }

    /// <summary>New scalar volume level (0.0 to 1.0).</summary>
    public float Volume { get; }

    /// <summary>New mute state.</summary>
    public bool Muted { get; }
}

/// <summary>
/// Describes how the set of audio devices changed.
/// </summary>
public enum DeviceChangeKind
{
    Added,
    Removed,
    StateChanged,
    DefaultChanged
}

/// <summary>
/// Event args for a device topology change. <see cref="DeviceGuid"/> is the
/// affected endpoint when known, otherwise null (for example a default-device
/// change that only carries the new default endpoint id).
/// </summary>
public sealed class DeviceChangeEventArgs : EventArgs
{
    public DeviceChangeEventArgs(DeviceChangeKind kind, Guid? deviceGuid)
    {
        Kind = kind;
        DeviceGuid = deviceGuid;
    }

    public DeviceChangeKind Kind { get; }

    public Guid? DeviceGuid { get; }
}

/// <summary>
/// Abstraction over a single audio endpoint's volume/mute controls.
/// The production implementation wraps IAudioEndpointVolume; tests substitute
/// an in-memory fake. Contract: Core Audio Control (contracts/README.md §1).
/// </summary>
public interface IAudioEndpoint : IDisposable
{
    /// <summary>Persistent endpoint GUID (Windows audio endpoint GUID).</summary>
    Guid DeviceGuid { get; }

    /// <summary>Friendly display name.</summary>
    string DisplayName { get; }

    /// <summary>Reads the current scalar volume (0.0 to 1.0).</summary>
    float GetCurrentVolume();

    /// <summary>Sets the scalar volume (0.0 to 1.0).</summary>
    void SetVolume(float level);

    /// <summary>Reads the current mute state.</summary>
    bool GetCurrentMuteState();

    /// <summary>Sets the mute state.</summary>
    void SetMute(bool muted);

    /// <summary>Starts event-driven change monitoring (no polling).</summary>
    void MonitorVolumeChanges();

    /// <summary>Stops event-driven change monitoring.</summary>
    void StopMonitoring();

    /// <summary>Raised when Windows reports a volume/mute change on this endpoint.</summary>
    event EventHandler<VolumeChangeEventArgs>? VolumeChanged;
}

/// <summary>
/// Abstraction over audio device enumeration and endpoint access. The
/// production implementation wraps the MMDevice API; tests substitute a fake.
/// </summary>
public interface IAudioDeviceManager : IDisposable
{
    /// <summary>All currently active audio input devices.</summary>
    IReadOnlyList<AudioInputDevice> GetInputDevices();

    /// <summary>The Windows default communication capture device, or null.</summary>
    AudioInputDevice? GetDefaultCommunicationDevice();

    /// <summary>Whether the endpoint identified by <paramref name="guid"/> is available.</summary>
    bool IsDeviceAvailable(Guid guid);

    /// <summary>
    /// Opens the endpoint identified by <paramref name="guid"/> for volume
    /// control, or returns null when it is unavailable.
    /// </summary>
    IAudioEndpoint? OpenEndpoint(Guid guid);

    /// <summary>Starts listening for device topology changes.</summary>
    void StartListening();

    /// <summary>Stops listening for device topology changes.</summary>
    void StopListening();

    /// <summary>Raised when devices are added, removed, changed, or the default changes.</summary>
    event EventHandler<DeviceChangeEventArgs>? DevicesChanged;
}
