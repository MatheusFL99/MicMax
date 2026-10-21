using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using MicMax.Core.Audio;
using MicMax.Core.Services;
using Xunit;

namespace MicMax.Core.Tests;

/// <summary>
/// Integration tests against the real Windows Core Audio (MMDevice) API — no
/// fakes. They are what catch stale COM IIDs and wrong vtable declarations in
/// <see cref="CoreAudioInterop"/>, which unit tests with fakes cannot see.
///
/// Tests that change the level/mute always restore the original values.
/// Every test returns early when the machine has no active capture endpoint,
/// so the suite still passes on microphone-less machines.
/// </summary>
public sealed class CoreAudioIntegrationTests : IDisposable
{
    private const int TimeoutMs = 3000;

    private readonly CoreAudioDeviceManager _manager = new();

    public void Dispose() => _manager.Dispose();

    [Fact]
    public void CreateDeviceEnumerator_EnumeratesCaptureEndpoints()
    {
        var enumerator = CoreAudioInterop.CreateDeviceEnumerator();
        try
        {
            Assert.NotNull(enumerator);

            var hr = enumerator.EnumAudioEndpoints(
                CoreAudioInterop.EDataFlow.Capture,
                CoreAudioInterop.DeviceStateActive,
                out var collection);

            CoreAudioInterop.ThrowIfFailed(hr, "IMMDeviceEnumerator.EnumAudioEndpoints");
            Assert.NotNull(collection);

            try
            {
                CoreAudioInterop.ThrowIfFailed(
                    collection!.GetCount(out var count),
                    "IMMDeviceCollection.GetCount");
            }
            finally
            {
                ReleaseIfCom(collection);
            }
        }
        finally
        {
            ReleaseIfCom(enumerator);
        }
    }

    [Fact]
    public void GetDefaultCommunicationDevice_ReturnsActiveCaptureDevice()
    {
        var device = _manager.GetDefaultCommunicationDevice();
        if (device == null)
        {
            return; // No capture device on this machine.
        }

        Assert.NotEqual(Guid.Empty, device.GUID);
        Assert.False(string.IsNullOrWhiteSpace(device.DisplayName), "friendly name should come from IPropertyStore");

        // The default communication endpoint can be inactive; the active-only
        // enumeration must agree with the reported availability.
        Assert.Equal(device.IsAvailable, _manager.GetInputDevices().Any(d => d.GUID == device.GUID));
        Assert.Equal(device.IsAvailable, _manager.IsDeviceAvailable(device.GUID));
    }

    [Fact]
    public void Endpoint_SetVolumeAndMute_RoundTripsAndRaisesNativeCallback()
    {
        var device = _manager.GetDefaultCommunicationDevice();
        if (device == null || !device.IsAvailable)
        {
            // No active capture endpoint on this machine.
            return;
        }

        using var endpoint = _manager.OpenEndpoint(device.GUID);
        Assert.NotNull(endpoint);

        var originalVolume = endpoint!.GetCurrentVolume();
        var originalMuted = endpoint.GetCurrentMuteState();

        using var changed = new SemaphoreSlim(0);
        VolumeChangeEventArgs? seen = null;
        void Handler(object? sender, VolumeChangeEventArgs e)
        {
            seen = e;
            changed.Release();
        }

        try
        {
            endpoint.VolumeChanged += Handler;
            endpoint.MonitorVolumeChanges();

            var target = originalVolume > 0.5f ? 0.25f : 0.75f;
            endpoint.SetVolume(target);

            Assert.True(
                changed.Wait(TimeoutMs),
                "Windows did not deliver IAudioEndpointVolumeCallback.OnNotify for a level change.");

            Assert.NotNull(seen);
            Assert.Equal((double)target, (double)seen!.Volume, 2);
            Assert.Equal((double)target, (double)endpoint.GetCurrentVolume(), 2);

            endpoint.SetMute(!originalMuted);
            Assert.Equal(!originalMuted, endpoint.GetCurrentMuteState());
        }
        finally
        {
            endpoint.VolumeChanged -= Handler;
            endpoint.SetMute(originalMuted);
            endpoint.SetVolume(originalVolume);
        }
    }

    [Fact]
    public void VolumeLockService_KeepsRealDeviceAt100Unmuted_AndRevertsExternalChange()
    {
        var device = _manager.GetDefaultCommunicationDevice();
        if (device == null)
        {
            return;
        }

        // A second endpoint handle plays the role of an external application
        // (e.g. Discord or the Windows mixer) moving the level.
        using var external = _manager.OpenEndpoint(device.GUID);
        Assert.NotNull(external);

        var originalVolume = external!.GetCurrentVolume();
        var originalMuted = external.GetCurrentMuteState();

        using var service = new VolumeLockService(_manager);
        try
        {
            Assert.True(
                service.ActivateLock(),
                "ActivateLock should succeed while a real capture endpoint is available.");
            Assert.Equal(1.0f, service.GetCurrentVolume());
            Assert.False(service.GetCurrentMuteState());

            external.SetVolume(0.4f);

            Assert.True(
                WaitUntil(() => service.GetCurrentVolume() >= 0.999f, TimeoutMs),
                "The lock did not revert an external level change within one second.");
        }
        finally
        {
            service.Dispose();
            external.SetMute(originalMuted);
            external.SetVolume(originalVolume);
        }
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(25);
        }

        return condition();
    }

    private static void ReleaseIfCom(object? comObject)
    {
        if (comObject != null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }
}
