using System;
using System.Runtime.InteropServices;
using static MicMax.Core.Audio.CoreAudioInterop;

namespace MicMax.Core.Audio;

/// <summary>
/// Managed implementation of IMMNotificationClient. Windows invokes these
/// callbacks when devices are added, removed, change state, or when the
/// default device changes. Used for device fallback (User Story 4).
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
public sealed class DeviceNotificationListener : IMMNotificationClient
{
    private readonly object _gate = new();
    private EventHandler<DeviceChangeEventArgs>? _handlers;

    /// <summary>Raised for any device topology change.</summary>
    public event EventHandler<DeviceChangeEventArgs>? DevicesChanged
    {
        add { lock (_gate) { _handlers += value; } }
        remove { lock (_gate) { _handlers -= value; } }
    }

    /// <inheritdoc />
    public void OnDeviceStateChanged(string deviceId, uint newState)
    {
        Raise(DeviceChangeKind.StateChanged, TryParseEndpointGuid(deviceId));
    }

    /// <inheritdoc />
    public void OnDeviceAdded(string deviceId)
    {
        Raise(DeviceChangeKind.Added, TryParseEndpointGuid(deviceId));
    }

    /// <inheritdoc />
    public void OnDeviceRemoved(string deviceId)
    {
        Raise(DeviceChangeKind.Removed, TryParseEndpointGuid(deviceId));
    }

    /// <inheritdoc />
    public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, string defaultDeviceId)
    {
        if (flow != EDataFlow.Capture)
        {
            return;
        }

        Raise(DeviceChangeKind.DefaultChanged, TryParseEndpointGuid(defaultDeviceId));
    }

    /// <inheritdoc />
    public void OnPropertyValueChanged(string deviceId, PropertyKey key)
    {
        // Property changes (e.g. rename) do not affect the lock; ignore.
    }

    private void Raise(DeviceChangeKind kind, Guid? guid)
    {
        EventHandler<DeviceChangeEventArgs>? handlers;
        lock (_gate)
        {
            handlers = _handlers;
        }

        handlers?.Invoke(this, new DeviceChangeEventArgs(kind, guid));
    }

    /// <summary>
    /// Extracts the trailing endpoint GUID from a Windows device id such as
    /// {0.0.1.00000000}.{a3d4c8f2-...}. Returns null when unparseable.
    /// </summary>
    internal static Guid? TryParseEndpointGuid(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        var lastBrace = deviceId.LastIndexOf('{');
        if (lastBrace < 0)
        {
            return null;
        }

        var candidate = deviceId[lastBrace..].Trim('{', '}');
        return Guid.TryParse(candidate, out var guid) ? guid : null;
    }
}
