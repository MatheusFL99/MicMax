using System;
using System.Windows.Forms;
using MicMax.Core.Notifications;

namespace MicMax.Tray;

/// <summary>
/// Presents notifications as standard Windows balloon/toast messages through
/// the tray's <see cref="NotifyIcon"/>. Because it uses the standard path,
/// Focus Assist / Do Not Disturb is honored automatically (no bypass).
/// </summary>
public sealed class TrayNotificationSink : INotificationSink
{
    private readonly NotifyIcon _notifyIcon;

    /// <summary>Creates the sink bound to the shared tray NotifyIcon.</summary>
    public TrayNotificationSink(NotifyIcon notifyIcon)
    {
        _notifyIcon = notifyIcon ?? throw new ArgumentNullException(nameof(notifyIcon));
    }

    /// <inheritdoc />
    public void Show(string title, string message)
    {
        try
        {
            if (!_notifyIcon.Visible)
            {
                _notifyIcon.Visible = true;
            }

            _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
        }
        catch
        {
            // Notifications are best-effort; never fail the lock over them.
        }
    }
}
