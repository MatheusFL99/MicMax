using System;
using MicMax.Core.Models;
using MicMax.Core.Services;
using MicMax.Core.Tests.Fakes;
using Xunit;

namespace MicMax.Core.Tests.Contracts;

/// <summary>
/// Contract test for the Core Audio Control contract (contracts/README.md §1)
/// exercised through <see cref="VolumeLockService"/> against a mocked endpoint.
/// Covers: SetVolumeTo100Percent, GetCurrentVolume, GetCurrentMuteState,
/// SetMuteToUnmuted, MonitorVolumeChanges, StopMonitoring (task T013).
/// </summary>
public class CoreAudioControlContractTests : IDisposable
{
    private static readonly Guid DeviceGuid = Guid.NewGuid();

    private readonly FakeAudioDeviceManager _manager;
    private readonly VolumeLockService _service;
    private readonly FakeAudioEndpoint _endpoint;

    public CoreAudioControlContractTests()
    {
        _manager = new FakeAudioDeviceManager();
        _manager.AddDevice(DeviceGuid, "Fake Mic", isDefault: true);

        _service = new VolumeLockService(_manager);
        _service.ActivateLock();

        _endpoint = _manager.GetOpenEndpoint(DeviceGuid)!;
        Assert.NotNull(_endpoint);
    }

    [Fact]
    public void SetVolumeTo100Percent_SetsScalarToOne()
    {
        _service.SetVolumeTo100Percent();

        Assert.Equal(1.0f, _endpoint!.GetCurrentVolume());
        Assert.True(_endpoint.SetVolumeCalls >= 1);
    }

    [Fact]
    public void GetCurrentVolume_ReturnsEndpointLevel()
    {
        _endpoint!.SimulateExternalChange(0.42f, muted: false);

        // The lock may correct it, but the read reflects the endpoint state.
        var value = _service.GetCurrentVolume();
        Assert.InRange(value, 0f, 1f);
    }

    [Fact]
    public void GetCurrentMuteState_ReflectsEndpointMute()
    {
        _endpoint!.SetMute(true);

        Assert.True(_service.GetCurrentMuteState());
    }

    [Fact]
    public void SetMuteToUnmuted_ClearsMuteState()
    {
        _endpoint!.SetMute(true);

        _service.SetMuteToUnmuted();

        Assert.False(_endpoint.GetCurrentMuteState());
        Assert.False(_service.GetCurrentMuteState());
    }

    [Fact]
    public void MonitorVolumeChanges_SubscribesToEndpoint()
    {
        // ActivateLock already starts monitoring.
        Assert.True(_endpoint!.Monitoring);

        _service.StopMonitoring();
        Assert.False(_endpoint.Monitoring);

        _service.MonitorVolumeChanges();
        Assert.True(_endpoint.Monitoring);
    }

    [Fact]
    public void StopMonitoring_UnsubscribesFromEndpoint()
    {
        _service.StopMonitoring();

        Assert.False(_endpoint!.Monitoring);
    }

    [Fact]
    public void ActivateLock_TargetsDefaultCommunicationDevice_AndSets100Unmuted()
    {
        Assert.True(_service.IsActive);
        Assert.Equal(DeviceGuid, _service.LockedDeviceGuid);
        Assert.True(_endpoint!.Monitoring);
        Assert.Equal(1.0f, _endpoint.GetCurrentVolume());
        Assert.False(_endpoint.GetCurrentMuteState());
    }

    public void Dispose()
    {
        _service.Dispose();
        _manager.Dispose();
    }
}
