using System;
using System.Runtime.InteropServices;

namespace MicMax.Core.Audio;

/// <summary>
/// Managed bridge for IAudioEndpointVolumeCallback. Windows invokes
/// <see cref="OnNotify"/> when the endpoint's master volume or mute state
/// changes, allowing correction without any polling.
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("657804FA-D6AD-4496-8A60-352752AF4F89")]
public sealed class AudioVolumeCallback : CoreAudioInterop.IAudioEndpointVolumeCallback
{
    // AUDIO_VOLUME_NOTIFICATION_DATA layout (x86/x64):
    //   offset 0 : GUID  guidEventContext   (16 bytes)
    //   offset 16: BOOL  bMuted             (4 bytes)
    //   offset 20: float fVolumeLevel       (4 bytes)
    //   offset 24: UINT  cChannels          (4 bytes)
    //   offset 28: float afChannelVolumes[8]
    private const int OffsetMuted = 16;
    private const int OffsetVolume = 20;

    private readonly object _gate = new();
    private EventHandler<VolumeChangeEventArgs>? _handlers;

    /// <summary>Raised with the new volume/mute values reported by Windows.</summary>
    public event EventHandler<VolumeChangeEventArgs>? VolumeChanged
    {
        add { lock (_gate) { _handlers += value; } }
        remove { lock (_gate) { _handlers -= value; } }
    }

    /// <summary>
    /// COM callback entry point. Parses the notification struct and raises
    /// <see cref="VolumeChanged"/>. Always returns S_OK.
    /// </summary>
    public int OnNotify(IntPtr pNotify)
    {
        if (pNotify == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            bool muted = Marshal.ReadInt32(pNotify, OffsetMuted) != 0;
            float volume = Marshal.PtrToStructure<float>(IntPtr.Add(pNotify, OffsetVolume));

            EventHandler<VolumeChangeEventArgs>? handlers;
            lock (_gate)
            {
                handlers = _handlers;
            }

            handlers?.Invoke(this, new VolumeChangeEventArgs(volume, muted));
        }
        catch (Exception ex)
        {
            Logging.EventLogger.LogWarning($"Volume change callback failed: {ex.Message}");
        }

        return 0;
    }
}
