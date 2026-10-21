using System;
using System.Linq;
using MicMax.Core.Audio;
using MicMax.Core.Services;
using MicMax.Core.Tests.Fakes;
using Xunit;

namespace MicMax.Core.Tests;

/// <summary>
/// Unit tests for device fallback (task T030): fallback switch on disconnect,
/// original-device re-lock on return, and pending-lock behavior when no device
/// is available (US4 / FR-012, FR-013, FR-014).
/// </summary>
public class DeviceFallbackTests : IDisposable
{
    private static readonly Guid HeadsetGuid = Guid.NewGuid();
    private static readonly Guid LaptopMicGuid = Guid.NewGuid();
    private static readonly Guid NewMicGuid = Guid.NewGuid();

    private readonly FakeAudioDeviceManager _manager = new();
    private readonly VolumeLockService _lockService;
    private readonly DeviceFallbackManager _fallback;

    public DeviceFallbackTests()
    {
        _manager.AddDevice(HeadsetGuid, "Headset", isDefault: false);
        _manager.AddDevice(LaptopMicGuid, "Laptop Mic", isDefault: true);

        _lockService = new VolumeLockService(_manager);
        _fallback = new DeviceFallbackManager(_manager, _lockService);
    }

    [Fact]
    public void LockedDeviceDisconnected_SwitchesToDefaultCommunicationDevice()
    {
        _lockService.SelectDevice(HeadsetGuid);
        _fallback.SetOriginalDevice(HeadsetGuid);
        Assert.True(_lockService.ActivateLock());

        DeviceSwitchedEventArgs? switched = null;
        _fallback.DeviceSwitched += (_, e) => switched = e;

        // Disconnect the locked headset.
        _manager.RemoveDevice(HeadsetGuid);

        // Lock follows to the default (Laptop Mic) and stays active (FR-012).
        Assert.True(_lockService.IsActive);
        Assert.Equal(LaptopMicGuid, _lockService.LockedDeviceGuid);

        Assert.NotNull(switched);
        Assert.Equal(LaptopMicGuid, switched!.DeviceGuid);
        Assert.False(switched.IsRestored);

        var endpoint = _manager.GetOpenEndpoint(LaptopMicGuid);
        Assert.NotNull(endpoint);
        Assert.Equal(1.0f, endpoint!.GetCurrentVolume());
        Assert.False(endpoint.GetCurrentMuteState());
    }

    [Fact]
    public void OriginalDeviceReturns_ReLocksOriginal_AndFlagsRestored()
    {
        _lockService.SelectDevice(HeadsetGuid);
        _fallback.SetOriginalDevice(HeadsetGuid);
        Assert.True(_lockService.ActivateLock());

        _manager.RemoveDevice(HeadsetGuid);
        Assert.Equal(LaptopMicGuid, _lockService.LockedDeviceGuid);

        DeviceSwitchedEventArgs? switched = null;
        _fallback.DeviceSwitched += (_, e) => switched = e;

        // Reconnect the original headset (FR-013).
        _manager.AddDevice(HeadsetGuid, "Headset", isDefault: false);

        Assert.True(_lockService.IsActive);
        Assert.Equal(HeadsetGuid, _lockService.LockedDeviceGuid);

        Assert.NotNull(switched);
        Assert.Equal(HeadsetGuid, switched!.DeviceGuid);
        Assert.True(switched.IsRestored);

        var endpoint = _manager.GetOpenEndpoint(HeadsetGuid);
        Assert.NotNull(endpoint);
        Assert.Equal(1.0f, endpoint!.GetCurrentVolume());
        Assert.False(endpoint.GetCurrentMuteState());
    }

    [Fact]
    public void NoDeviceAvailable_HoldsPending_ThenLocksFirstDeviceThatAppears()
    {
        // Follow the default communication device.
        Assert.True(_lockService.ActivateLock());

        // Disconnect everything (FR-014: keep lock state pending).
        _manager.RemoveDevice(HeadsetGuid);
        _manager.RemoveDevice(LaptopMicGuid);

        Assert.Equal(LockStateStatus.Pending, _lockService.State.Status);
        Assert.Null(_lockService.LockedDeviceGuid);

        // A new device appears: lock it as soon as it is available.
        _manager.AddDevice(NewMicGuid, "New Mic", isDefault: true);

        Assert.Equal(LockStateStatus.Active, _lockService.State.Status);
        Assert.True(_lockService.IsActive);
        Assert.Equal(NewMicGuid, _lockService.LockedDeviceGuid);

        var endpoint = _manager.GetOpenEndpoint(NewMicGuid);
        Assert.NotNull(endpoint);
        Assert.Equal(1.0f, endpoint!.GetCurrentVolume());
    }

    [Fact]
    public void RapidDeviceChurn_SettlesOnCurrentDefault_WithoutCrashingOrSpam()
    {
        _lockService.SelectDevice(HeadsetGuid);
        _fallback.SetOriginalDevice(HeadsetGuid);
        Assert.True(_lockService.ActivateLock());

        var switchEvents = 0;
        _fallback.DeviceSwitched += (_, _) => switchEvents++;

        // Simulate a docking station churning devices rapidly (edge case).
        var churn1 = Guid.NewGuid();
        var churn2 = Guid.NewGuid();
        for (var i = 0; i < 3; i++)
        {
            _manager.AddDevice(churn1, "Dock Mic", isDefault: false);
            _manager.RemoveDevice(churn1);
            _manager.AddDevice(churn2, "Dock Mic 2", isDefault: false);
            _manager.RemoveDevice(churn2);
        }

        // The explicit Headset target remains connected, so the lock must stay
        // on it — churn of unrelated devices must not thrash the target (FR-017).
        Assert.True(_lockService.IsActive);
        Assert.Equal(HeadsetGuid, _lockService.LockedDeviceGuid);

        // Churn must not produce an excessive number of switch notifications.
        Assert.True(switchEvents < 10, $"Expected bounded switch events, got {switchEvents}");
    }

    [Fact]
    public void Fallback_WhenLockInactive_DoesNothing()
    {
        // Lock is not active; topology changes must be ignored.
        _manager.RemoveDevice(HeadsetGuid);

        Assert.False(_lockService.IsActive);
        Assert.Null(_lockService.LockedDeviceGuid);
        Assert.Equal(LockStateStatus.Inactive, _lockService.State.Status);
    }

    [Fact]
    public void ManagerStart_RegistersListening_AndStopUnregisters()
    {
        _fallback.Start();
        Assert.True(_manager.Listening);

        _fallback.Stop();
        Assert.False(_manager.Listening);
    }

    public void Dispose()
    {
        _fallback.Dispose();
        _lockService.Dispose();
        _manager.Dispose();
    }
}
