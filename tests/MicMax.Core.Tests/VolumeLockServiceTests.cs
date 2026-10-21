using System;
using MicMax.Core.Models;
using MicMax.Core.Services;
using MicMax.Core.Tests.Fakes;
using Xunit;

namespace MicMax.Core.Tests;

/// <summary>
/// Unit tests for <see cref="VolumeLockService"/> (task T014): reverting
/// external level/mute changes promptly, no-op when already 100% unmuted, and
/// leaving changes alone while paused (acceptance scenarios 1–5).
/// </summary>
public class VolumeLockServiceTests : IDisposable
{
    private static readonly Guid MicGuid = Guid.NewGuid();

    private readonly FakeAudioDeviceManager _manager = new();
    private readonly VolumeLockService _service;

    public VolumeLockServiceTests()
    {
        _manager.AddDevice(MicGuid, "Test Mic", isDefault: true);
        _service = new VolumeLockService(_manager);
    }

    [Fact]
    public void ActivateLock_WhenDeviceAvailable_Sets100PercentUnmuted()
    {
        var ok = _service.ActivateLock();

        Assert.True(ok);
        Assert.True(_service.IsActive);

        var endpoint = _manager.GetOpenEndpoint(MicGuid);
        Assert.NotNull(endpoint);
        Assert.Equal(1.0f, endpoint!.GetCurrentVolume());
        Assert.False(endpoint.GetCurrentMuteState());
        Assert.True(endpoint.Monitoring);
    }

    [Fact]
    public void ExternalChange_LoweringLevel_IsRevertedTo100WithinOneSecond()
    {
        _service.ActivateLock();
        var endpoint = _manager.GetOpenEndpoint(MicGuid)!;

        var start = DateTime.UtcNow;
        endpoint.SimulateExternalChange(0.5f, muted: false);
        var elapsed = DateTime.UtcNow - start;

        Assert.Equal(1.0f, endpoint.GetCurrentVolume());
        Assert.True(elapsed < TimeSpan.FromSeconds(1), $"Correction took {elapsed.TotalMilliseconds} ms");
    }

    [Fact]
    public void ExternalChange_Muting_IsUnmutedWithinOneSecond()
    {
        _service.ActivateLock();
        var endpoint = _manager.GetOpenEndpoint(MicGuid)!;

        var start = DateTime.UtcNow;
        endpoint.SimulateExternalChange(1.0f, muted: true);
        var elapsed = DateTime.UtcNow - start;

        Assert.False(endpoint.GetCurrentMuteState());
        Assert.True(elapsed < TimeSpan.FromSeconds(1), $"Correction took {elapsed.TotalMilliseconds} ms");
    }

    [Fact]
    public void ExternalChange_WhenAlready100Unmuted_IsNoOp_NoRedundantWrites()
    {
        _service.ActivateLock();
        var endpoint = _manager.GetOpenEndpoint(MicGuid)!;

        // Baseline writes performed during activation.
        var volumeWritesAfterActivate = endpoint.SetVolumeCalls;
        var muteWritesAfterActivate = endpoint.SetMuteCalls;

        // An external event reporting an already-correct state must not write.
        endpoint.SimulateExternalChange(1.0f, muted: false);

        Assert.Equal(volumeWritesAfterActivate, endpoint.SetVolumeCalls);
        Assert.Equal(muteWritesAfterActivate, endpoint.SetMuteCalls);
    }

    [Fact]
    public void RepeatedExternalChanges_AreAllReverted()
    {
        _service.ActivateLock();
        var endpoint = _manager.GetOpenEndpoint(MicGuid)!;

        for (var i = 0; i < 5; i++)
        {
            endpoint.SimulateExternalChange(0.2f, muted: i % 2 == 0);
            Assert.Equal(1.0f, endpoint.GetCurrentVolume());
            Assert.False(endpoint.GetCurrentMuteState());
        }
    }

    [Fact]
    public void Pause_StopsReverting_Resume_ReappliesImmediately()
    {
        _service.ActivateLock();
        var endpoint = _manager.GetOpenEndpoint(MicGuid)!;

        _service.Pause();
        Assert.Equal(LockStateStatus.Paused, _service.State.Status);

        // While paused, an external change must NOT be reverted.
        endpoint.SimulateExternalChange(0.3f, muted: false);
        Assert.Equal(0.3f, endpoint.GetCurrentVolume());

        _service.Resume();

        // On resume the microphone is immediately back to 100% unmuted.
        Assert.Equal(LockStateStatus.Active, _service.State.Status);
        Assert.Equal(1.0f, endpoint.GetCurrentVolume());
        Assert.False(endpoint.GetCurrentMuteState());
    }

    [Fact]
    public void DeactivateLock_StopsMonitoring_AndDisablesCorrection()
    {
        _service.ActivateLock();
        var endpoint = _manager.GetOpenEndpoint(MicGuid)!;

        _service.DeactivateLock();

        Assert.False(_service.IsActive);
        Assert.False(endpoint.Monitoring);
        Assert.Null(_service.LockedDeviceGuid);
    }

    [Fact]
    public void ActivateLock_WhenNoDeviceAvailable_ReturnsFalse_AndPending()
    {
        var empty = new FakeAudioDeviceManager();
        using var service = new VolumeLockService(empty);

        var ok = service.ActivateLock();

        Assert.False(ok);
        Assert.False(service.IsActive);
        Assert.Equal(LockStateStatus.Pending, service.State.Status);
    }

    public void Dispose()
    {
        _service.Dispose();
        _manager.Dispose();
    }
}
