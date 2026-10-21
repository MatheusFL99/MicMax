using System;
using MicMax.Core.Logging;

namespace MicMax.Core.Notifications;

/// <summary>
/// Contract for presenting Windows notifications (contracts/README.md §4).
/// The production implementation posts balloon/toast notifications via the
/// tray icon; tests substitute a recording sink.
/// </summary>
public interface INotificationSink
{
    /// <summary>
    /// Shows a standard Windows notification. Implementations must use the
    /// standard notification path so Focus Assist / Do Not Disturb is honored
    /// (no bypass or priority override).
    /// </summary>
    void Show(string title, string message);
}

/// <summary>
/// Central builder + dispatcher for the app's notifications. Enforces the
/// exact message formats from the Notification contract and throttles repeats
/// to avoid spam (FR-017). Messages respect Focus Assist by using the
/// standard notification path.
/// </summary>
public sealed class NotificationService
{
    /// <summary>Title used on every notification.</summary>
    public const string Title = "MicMax";

    private readonly INotificationSink _sink;
    private readonly TimeSpan _minInterval;
    private readonly object _gate = new();

    private DateTime _lastShownUtc = DateTime.MinValue;
    private string? _lastMessage;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="sink">Notification presentation backend.</param>
    /// <param name="minInterval">
    /// Minimum interval between identical consecutive notifications
    /// (default 2 seconds). Non-identical messages are also rate-limited.
    /// </param>
    public NotificationService(INotificationSink sink, TimeSpan? minInterval = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _minInterval = minInterval ?? TimeSpan.FromSeconds(2);
    }

    /// <summary>Message for lock activation.</summary>
    public static string LockActivatedMessage() => "MicMax: Microphone locked at 100%";

    /// <summary>Message for lock deactivation.</summary>
    public static string LockDeactivatedMessage() => "MicMax: Microphone unlocked";

    /// <summary>Message for pause.</summary>
    public static string LockPausedMessage() => "MicMax: Microphone lock paused";

    /// <summary>Message for resume.</summary>
    public static string LockResumedMessage() => "MicMax: Microphone lock resumed";

    /// <summary>Message for a fallback device switch.</summary>
    /// <param name="deviceName">Name of the newly locked device.</param>
    public static string DeviceSwitchedMessage(string deviceName) =>
        $"MicMax: Switched to {deviceName} — microphone locked at 100%";

    /// <summary>Message for restoring the lock to the original device.</summary>
    /// <param name="deviceName">Name of the restored device.</param>
    public static string DeviceRestoredMessage(string deviceName) =>
        $"MicMax: Restored lock to {deviceName}";

    /// <summary>Notifies that the lock is active at 100%.</summary>
    public void NotifyLockActivated(string? deviceName = null)
    {
        var message = deviceName != null
            ? $"{LockActivatedMessage()} ({deviceName})"
            : LockActivatedMessage();
        Show(message);
    }

    /// <summary>Notifies that the lock is off.</summary>
    public void NotifyLockDeactivated() => Show(LockDeactivatedMessage());

    /// <summary>Notifies that the lock is paused.</summary>
    public void NotifyLockPaused() => Show(LockPausedMessage());

    /// <summary>Notifies that the lock resumed.</summary>
    public void NotifyLockResumed() => Show(LockResumedMessage());

    /// <summary>Notifies that the lock moved to a different device.</summary>
    public void NotifyDeviceSwitched(string deviceName) => Show(DeviceSwitchedMessage(deviceName));

    /// <summary>Notifies that the lock returned to the original device.</summary>
    public void NotifyDeviceRestored(string deviceName) => Show(DeviceRestoredMessage(deviceName));

    private void Show(string message)
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            var throttled = message == _lastMessage && (now - _lastShownUtc) < _minInterval;
            if (throttled)
            {
                EventLogger.LogVerbose($"Notification throttled: {message}");
                return;
            }

            _lastMessage = message;
            _lastShownUtc = now;
        }

        try
        {
            _sink.Show(Title, message);
        }
        catch (Exception ex)
        {
            EventLogger.LogWarning($"Failed to show notification: {ex.Message}");
        }
    }
}
