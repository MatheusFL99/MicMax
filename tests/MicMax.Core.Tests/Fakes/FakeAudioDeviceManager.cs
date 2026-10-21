using System;
using System.Collections.Generic;
using System.Linq;
using MicMax.Core.Audio;
using MicMax.Core.Models;

namespace MicMax.Core.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IAudioEndpoint"/> that records operations and lets
/// tests simulate external volume/mute changes.
/// </summary>
public sealed class FakeAudioEndpoint : IAudioEndpoint
{
    private float _volume = 0.5f;
    private bool _muted;

    public FakeAudioEndpoint(Guid deviceGuid, string displayName, float initialVolume = 0.5f, bool initialMuted = false)
    {
        DeviceGuid = deviceGuid;
        DisplayName = displayName;
        _volume = initialVolume;
        _muted = initialMuted;
    }

    public Guid DeviceGuid { get; }

    public string DisplayName { get; }

    public bool Monitoring { get; private set; }

    public int SetVolumeCalls { get; private set; }

    public int SetMuteCalls { get; private set; }

    public bool Disposed { get; private set; }

    public event EventHandler<VolumeChangeEventArgs>? VolumeChanged;

    public float GetCurrentVolume() => _volume;

    public bool GetCurrentMuteState() => _muted;

    public void SetVolume(float level)
    {
        SetVolumeCalls++;
        _volume = Math.Clamp(level, 0f, 1f);
    }

    public void SetMute(bool muted)
    {
        SetMuteCalls++;
        _muted = muted;
    }

    public void MonitorVolumeChanges() => Monitoring = true;

    public void StopMonitoring() => Monitoring = false;

    /// <summary>
    /// Simulates another application or the OS changing level/mute, raising
    /// the event the way Windows would when monitoring is active.
    /// </summary>
    public void SimulateExternalChange(float volume, bool muted)
    {
        _volume = volume;
        _muted = muted;

        if (Monitoring)
        {
            VolumeChanged?.Invoke(this, new VolumeChangeEventArgs(volume, muted));
        }
    }

    public void Dispose() => Disposed = true;
}

/// <summary>
/// In-memory <see cref="IAudioDeviceManager"/> with a settable device list and
/// default communication device, used to drive fallback scenarios.
/// </summary>
public sealed class FakeAudioDeviceManager : IAudioDeviceManager
{
    private readonly Dictionary<Guid, FakeAudioEndpoint> _openEndpoints = new();

    public List<AudioInputDevice> Devices { get; } = new();

    public Guid? DefaultCommunicationGuid { get; set; }

    public bool Listening { get; private set; }

    public bool Disposed { get; private set; }

    public event EventHandler<DeviceChangeEventArgs>? DevicesChanged;

    public IReadOnlyList<AudioInputDevice> GetInputDevices() =>
        Devices.Where(d => d.IsAvailable).ToList();

    public AudioInputDevice? GetDefaultCommunicationDevice() =>
        Devices.FirstOrDefault(d => d.GUID == DefaultCommunicationGuid && d.IsAvailable)
        ?? Devices.FirstOrDefault(d => d.IsAvailable);

    public bool IsDeviceAvailable(Guid guid) =>
        Devices.Any(d => d.GUID == guid && d.IsAvailable);

    public IAudioEndpoint? OpenEndpoint(Guid guid)
    {
        if (!IsDeviceAvailable(guid))
        {
            return null;
        }

        if (_openEndpoints.TryGetValue(guid, out var existing))
        {
            return existing;
        }

        var model = Devices.First(d => d.GUID == guid);
        var endpoint = new FakeAudioEndpoint(guid, model.DisplayName);
        _openEndpoints[guid] = endpoint;
        return endpoint;
    }

    public void StartListening() => Listening = true;

    public void StopListening() => Listening = false;

    public void Dispose() => Disposed = true;

    /// <summary>Adds a device and raises an Added event.</summary>
    public AudioInputDevice AddDevice(Guid guid, string name, bool isDefault = false)
    {
        var device = new AudioInputDevice
        {
            GUID = guid,
            DisplayName = name,
            IsAvailable = true,
            InputLevel = 50,
            MuteState = false,
            IsDefaultCommunication = isDefault,
            DeviceCategory = isDefault ? DeviceCategory.DefaultCommunication : DeviceCategory.ExplicitlySelected
        };

        Devices.Add(device);
        if (isDefault || DefaultCommunicationGuid == null)
        {
            DefaultCommunicationGuid = guid;
        }

        RaiseChanged(DeviceChangeKind.Added, guid);
        return device;
    }

    /// <summary>Removes a device and raises a Removed event.</summary>
    public void RemoveDevice(Guid guid)
    {
        var device = Devices.FirstOrDefault(d => d.GUID == guid);
        if (device != null)
        {
            Devices.Remove(device);
        }

        _openEndpoints.Remove(guid);

        if (DefaultCommunicationGuid == guid)
        {
            DefaultCommunicationGuid = Devices.FirstOrDefault(d => d.IsAvailable)?.GUID;
        }

        RaiseChanged(DeviceChangeKind.Removed, guid);
    }

    /// <summary>Raises a default-device change event.</summary>
    public void SetDefaultCommunication(Guid guid)
    {
        DefaultCommunicationGuid = guid;
        foreach (var d in Devices)
        {
            d.IsDefaultCommunication = d.GUID == guid;
        }

        RaiseChanged(DeviceChangeKind.DefaultChanged, guid);
    }

    /// <summary>Raises a raw device change event for churn testing.</summary>
    public void RaiseChanged(DeviceChangeKind kind, Guid? guid) =>
        DevicesChanged?.Invoke(this, new DeviceChangeEventArgs(kind, guid));

    /// <summary>Returns an endpoint previously handed out, for assertions.</summary>
    public FakeAudioEndpoint? GetOpenEndpoint(Guid guid) =>
        _openEndpoints.TryGetValue(guid, out var ep) ? ep : null;
}
