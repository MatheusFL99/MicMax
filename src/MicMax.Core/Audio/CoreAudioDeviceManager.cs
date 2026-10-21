using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MicMax.Core.Logging;
using MicMax.Core.Models;
using static MicMax.Core.Audio.CoreAudioInterop;

namespace MicMax.Core.Audio;

/// <summary>
/// Production <see cref="IAudioDeviceManager"/> built on the MMDevice API.
/// Enumerates capture endpoints, resolves the default communication device,
/// opens endpoints for volume control, and forwards topology change events
/// from <see cref="DeviceNotificationListener"/>.
/// </summary>
public sealed class CoreAudioDeviceManager : IAudioDeviceManager, IDisposable
{
    private readonly IMMDeviceEnumerator _enumerator;
    private readonly DeviceNotificationListener _listener = new();
    private readonly object _gate = new();
    private bool _listening;
    private bool _disposed;

    public CoreAudioDeviceManager()
    {
        _enumerator = CreateDeviceEnumerator();
        _listener.DevicesChanged += OnListenerDevicesChanged;
    }

    /// <inheritdoc />
    public event EventHandler<DeviceChangeEventArgs>? DevicesChanged;

    /// <inheritdoc />
    public IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var results = new List<AudioInputDevice>();

            var hr = _enumerator.EnumAudioEndpoints(EDataFlow.Capture, DeviceStateActive, out var collection);
            if (hr < 0 || collection == null)
            {
                return results;
            }

            try
            {
                CoreAudioInterop.ThrowIfFailed(collection.GetCount(out var count), "IMMDeviceCollection.GetCount");

                var defaultId = TryGetDefaultCommunicationEndpointId();

                for (uint i = 0; i < count; i++)
                {
                    if (collection.Item(i, out var device) < 0 || device == null)
                    {
                        continue;
                    }

                    try
                    {
                        var model = BuildDeviceModel(device, defaultId);
                        if (model != null)
                        {
                            results.Add(model);
                        }
                    }
                    finally
                    {
                        if (Marshal.IsComObject(device))
                        {
                            Marshal.ReleaseComObject(device);
                        }
                    }
                }
            }
            finally
            {
                if (Marshal.IsComObject(collection))
                {
                    Marshal.ReleaseComObject(collection);
                }
            }

            return results;
        }
    }

    /// <inheritdoc />
    public AudioInputDevice? GetDefaultCommunicationDevice()
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            var hr = _enumerator.GetDefaultAudioEndpoint(EDataFlow.Capture, ERole.Communications, out var device);
            if (hr < 0 || device == null)
            {
                return null;
            }

            try
            {
                return BuildDeviceModel(device, null, isDefault: true);
            }
            finally
            {
                if (Marshal.IsComObject(device))
                {
                    Marshal.ReleaseComObject(device);
                }
            }
        }
    }

    /// <inheritdoc />
    public bool IsDeviceAvailable(Guid guid)
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            foreach (var device in GetInputDevicesUnlocked())
            {
                if (device.GUID == guid)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <inheritdoc />
    public IAudioEndpoint? OpenEndpoint(Guid guid)
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            // Fast path: prefer the default communication device when it matches.
            var endpointId = FindEndpointIdByGuid(guid);
            if (endpointId == null)
            {
                return null;
            }

            var hr = _enumerator.GetDevice(endpointId, out var device);
            if (hr < 0 || device == null)
            {
                return null;
            }

            try
            {
                var displayName = TryReadProperty(device, DeviceFriendlyNameKey)
                    ?? TryReadProperty(device, DeviceDescKey)
                    ?? guid.ToString();

                return new CoreAudioEndpoint(device, guid, displayName);
            }
            catch (Exception ex)
            {
                EventLogger.LogError($"Failed to open endpoint {guid}: {ex.Message}");
                if (Marshal.IsComObject(device))
                {
                    Marshal.ReleaseComObject(device);
                }

                return null;
            }
        }
    }

    /// <inheritdoc />
    public void StartListening()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_listening)
            {
                return;
            }

            CoreAudioInterop.ThrowIfFailed(
                _enumerator.RegisterEndpointNotificationCallback(_listener),
                "IMMDeviceEnumerator.RegisterEndpointNotificationCallback");
            _listening = true;
        }
    }

    /// <inheritdoc />
    public void StopListening()
    {
        lock (_gate)
        {
            if (_disposed || !_listening)
            {
                return;
            }

            try
            {
                CoreAudioInterop.ThrowIfFailed(
                    _enumerator.UnregisterEndpointNotificationCallback(_listener),
                    "IMMDeviceEnumerator.UnregisterEndpointNotificationCallback");
            }
            catch (Exception ex)
            {
                EventLogger.LogWarning($"UnregisterEndpointNotificationCallback failed: {ex.Message}");
            }

            _listening = false;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            StopListening();
            _listener.DevicesChanged -= OnListenerDevicesChanged;

            if (Marshal.IsComObject(_enumerator))
            {
                Marshal.ReleaseComObject(_enumerator);
            }

            _disposed = true;
        }
    }

    private void OnListenerDevicesChanged(object? sender, DeviceChangeEventArgs e)
    {
        DevicesChanged?.Invoke(this, e);
    }

    private List<AudioInputDevice> GetInputDevicesUnlocked()
    {
        var results = new List<AudioInputDevice>();

        var hr = _enumerator.EnumAudioEndpoints(EDataFlow.Capture, DeviceStateActive, out var collection);
        if (hr < 0 || collection == null)
        {
            return results;
        }

        try
        {
            CoreAudioInterop.ThrowIfFailed(collection.GetCount(out var count), "IMMDeviceCollection.GetCount");
            for (uint i = 0; i < count; i++)
            {
                if (collection.Item(i, out var device) < 0 || device == null)
                {
                    continue;
                }

                try
                {
                    var model = BuildDeviceModel(device, null);
                    if (model != null)
                    {
                        results.Add(model);
                    }
                }
                finally
                {
                    if (Marshal.IsComObject(device))
                    {
                        Marshal.ReleaseComObject(device);
                    }
                }
            }
        }
        finally
        {
            if (Marshal.IsComObject(collection))
            {
                Marshal.ReleaseComObject(collection);
            }
        }

        return results;
    }

    private string? TryGetDefaultCommunicationEndpointId()
    {
        var hr = _enumerator.GetDefaultAudioEndpoint(EDataFlow.Capture, ERole.Communications, out var device);
        if (hr < 0 || device == null)
        {
            return null;
        }

        try
        {
            return TryGetEndpointId(device);
        }
        finally
        {
            if (Marshal.IsComObject(device))
            {
                Marshal.ReleaseComObject(device);
            }
        }
    }

    private string? FindEndpointIdByGuid(Guid guid)
    {
        // Enumerate active capture devices and match the trailing GUID.
        var hr = _enumerator.EnumAudioEndpoints(EDataFlow.Capture, DeviceStateActive, out var collection);
        if (hr < 0 || collection == null)
        {
            return null;
        }

        try
        {
            CoreAudioInterop.ThrowIfFailed(collection.GetCount(out var count), "IMMDeviceCollection.GetCount");
            for (uint i = 0; i < count; i++)
            {
                if (collection.Item(i, out var device) < 0 || device == null)
                {
                    continue;
                }

                try
                {
                    var id = TryGetEndpointId(device);
                    if (id != null && DeviceNotificationListener.TryParseEndpointGuid(id) == guid)
                    {
                        return id;
                    }
                }
                finally
                {
                    if (Marshal.IsComObject(device))
                    {
                        Marshal.ReleaseComObject(device);
                    }
                }
            }
        }
        finally
        {
            if (Marshal.IsComObject(collection))
            {
                Marshal.ReleaseComObject(collection);
            }
        }

        return null;
    }

    private static string? TryGetEndpointId(IMMDevice device)
    {
        var hr = device.GetId(out var idPtr);
        if (hr < 0 || idPtr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(idPtr);
        }
        finally
        {
            FreeDeviceId(idPtr);
        }
    }

    private static AudioInputDevice? BuildDeviceModel(IMMDevice device, string? defaultCommunicationId, bool isDefault = false)
    {
        var id = TryGetEndpointId(device);
        var guid = DeviceNotificationListener.TryParseEndpointGuid(id);
        if (guid == null)
        {
            return null;
        }

        device.GetState(out var state);
        var displayName = TryReadProperty(device, DeviceFriendlyNameKey)
            ?? TryReadProperty(device, DeviceDescKey)
            ?? guid.Value.ToString();

        var isDefaultComm = isDefault || (defaultCommunicationId != null && defaultCommunicationId == id);

        // Read current level/mute for display purposes only.
        var level = 0;
        var muted = false;
        try
        {
            var iid = IAudioEndpointVolumeIid;
            if (device.Activate(ref iid, ClsCtxInprocServer, IntPtr.Zero, out var obj) >= 0 && obj != null)
            {
                var volume = (IAudioEndpointVolume)obj;
                if (volume.GetMasterVolumeLevelScalar(out var scalar) >= 0)
                {
                    level = (int)Math.Round(scalar * 100);
                }

                if (volume.GetMute(out var isMuted) >= 0)
                {
                    muted = isMuted;
                }

                if (Marshal.IsComObject(volume))
                {
                    Marshal.ReleaseComObject(volume);
                }
            }
        }
        catch
        {
            // Display values are best-effort; the lock does not depend on them.
        }

        return new AudioInputDevice
        {
            GUID = guid.Value,
            DisplayName = displayName,
            InputLevel = level,
            MuteState = muted,
            IsAvailable = (state & DeviceStateActive) != 0,
            IsDefaultCommunication = isDefaultComm,
            DeviceCategory = isDefaultComm ? DeviceCategory.DefaultCommunication : DeviceCategory.ExplicitlySelected
        };
    }

    private static string? TryReadProperty(IMMDevice device, PropertyKey key)
    {
        var hr = device.OpenPropertyStore(StgmRead, out var store);
        if (hr < 0 || store == null)
        {
            return null;
        }

        try
        {
            var localKey = key;
            hr = store.GetValue(ref localKey, out var value);
            if (hr < 0)
            {
                return null;
            }

            try
            {
                return value.AsString();
            }
            finally
            {
                ClearPropVariant(ref value);
            }
        }
        finally
        {
            if (Marshal.IsComObject(store))
            {
                Marshal.ReleaseComObject(store);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CoreAudioDeviceManager));
        }
    }
}
