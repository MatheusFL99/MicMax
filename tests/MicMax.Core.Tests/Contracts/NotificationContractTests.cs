using System;
using System.Collections.Generic;
using MicMax.Core.Notifications;
using Xunit;

namespace MicMax.Core.Tests.Contracts;

/// <summary>
/// Contract test for the Notification contract (contracts/README.md §4),
/// asserting the exact message formats (task T036).
/// </summary>
public class NotificationContractTests
{
    private sealed class RecordingSink : INotificationSink
    {
        public List<(string Title, string Message)> Received { get; } = new();

        public void Show(string title, string message) => Received.Add((title, message));
    }

    [Fact]
    public void LockActivated_UsesExactMessage()
    {
        var sink = new RecordingSink();
        var service = new NotificationService(sink);

        service.NotifyLockActivated();

        Assert.Single(sink.Received);
        Assert.Equal("MicMax", sink.Received[0].Title);
        Assert.Equal("MicMax: Microphone locked at 100%", sink.Received[0].Message);
    }

    [Fact]
    public void LockDeactivated_UsesExactMessage()
    {
        Assert.Equal("MicMax: Microphone unlocked", NotificationService.LockDeactivatedMessage());
    }

    [Fact]
    public void LockPaused_UsesExactMessage()
    {
        Assert.Equal("MicMax: Microphone lock paused", NotificationService.LockPausedMessage());
    }

    [Fact]
    public void LockResumed_UsesExactMessage()
    {
        Assert.Equal("MicMax: Microphone lock resumed", NotificationService.LockResumedMessage());
    }

    [Fact]
    public void DeviceSwitched_InterpolatesDeviceName()
    {
        Assert.Equal(
            "MicMax: Switched to Headset — microphone locked at 100%",
            NotificationService.DeviceSwitchedMessage("Headset"));
    }

    [Fact]
    public void DeviceRestored_InterpolatesDeviceName()
    {
        Assert.Equal(
            "MicMax: Restored lock to Headset",
            NotificationService.DeviceRestoredMessage("Headset"));
    }

    [Fact]
    public void NotifyDeviceSwitched_SendsFormattedMessage()
    {
        var sink = new RecordingSink();
        var service = new NotificationService(sink);

        service.NotifyDeviceSwitched("USB Mic");

        Assert.Single(sink.Received);
        Assert.Equal("MicMax: Switched to USB Mic — microphone locked at 100%", sink.Received[0].Message);
    }

    [Fact]
    public void RapidRepeatedIdenticalNotifications_AreThrottled_NoSpam()
    {
        var sink = new RecordingSink();
        var service = new NotificationService(sink, TimeSpan.FromHours(1));

        for (var i = 0; i < 10; i++)
        {
            service.NotifyLockPaused();
        }

        // Throttling prevents spam (FR-017): only the first passes.
        Assert.Single(sink.Received);
    }

    [Fact]
    public void DifferentMessages_AreNotThrottledAgainstEachOther()
    {
        var sink = new RecordingSink();
        var service = new NotificationService(sink, TimeSpan.FromHours(1));

        service.NotifyLockActivated();
        service.NotifyLockPaused();

        Assert.Equal(2, sink.Received.Count);
    }
}
