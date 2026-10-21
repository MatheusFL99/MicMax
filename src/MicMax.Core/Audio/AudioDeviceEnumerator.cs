using System;
using System.Collections.Generic;
using MicMax.Core.Models;

namespace MicMax.Core.Audio;

/// <summary>
/// Convenience facade over <see cref="CoreAudioDeviceManager"/> for callers
/// that need a one-shot lookup (default communication device, device list,
/// availability) without managing a long-lived manager instance.
/// </summary>
public static class AudioDeviceEnumerator
{
    /// <summary>
    /// Gets the Windows default communication recording device.
    /// </summary>
    /// <returns>The default communication device, or null when none exists.</returns>
    public static AudioInputDevice? GetDefaultCommunicationDevice()
    {
        using var manager = new CoreAudioDeviceManager();
        return manager.GetDefaultCommunicationDevice();
    }

    /// <summary>
    /// Gets all active audio input devices.
    /// </summary>
    public static IReadOnlyList<AudioInputDevice> GetAllAudioInputDevices()
    {
        using var manager = new CoreAudioDeviceManager();
        return manager.GetInputDevices();
    }

    /// <summary>
    /// Checks whether an endpoint GUID corresponds to an available input device.
    /// </summary>
    public static bool IsDeviceAvailable(Guid guid)
    {
        using var manager = new CoreAudioDeviceManager();
        return manager.IsDeviceAvailable(guid);
    }
}
