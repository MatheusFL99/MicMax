using System;
using System.Drawing;
using System.Windows.Forms;
using MicMax.Core.Services;

namespace MicMax.Tray;

/// <summary>
/// System tray host (FR-005). Owns the <see cref="NotifyIcon"/> and a context
/// menu exposing the Tray Menu Command contract (contracts/README.md §2):
/// Lock, Pause, Resume, Auto-Start, Select Microphone, Open Window, Exit.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _lockItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _resumeItem;
    private readonly ToolStripMenuItem _autoStartItem;
    private readonly ToolStripMenuItem _selectMicItem;
    private readonly ToolStripMenuItem _openWindowItem;
    private readonly ToolStripMenuItem _exitItem;

    private bool _disposed;

    /// <summary>Creates the tray icon and wires menu commands to handlers.</summary>
    public TrayIcon(
        Action onLock,
        Action onPause,
        Action onResume,
        Action onToggleAutoStart,
        Action onSelectMicrophone,
        Action onOpenWindow,
        Action onExit,
        Func<bool> isAutoStartEnabled,
        Func<LockStateStatus> getLockStatus)
    {
        _menu = new ContextMenuStrip();

        _lockItem = new ToolStripMenuItem("Lock", null, (_, _) => onLock());
        _pauseItem = new ToolStripMenuItem("Pause", null, (_, _) => onPause());
        _resumeItem = new ToolStripMenuItem("Resume", null, (_, _) => onResume());
        _autoStartItem = new ToolStripMenuItem("Auto-Start", null, (_, _) => onToggleAutoStart());
        _selectMicItem = new ToolStripMenuItem("Select Microphone", null, (_, _) => onSelectMicrophone());
        _openWindowItem = new ToolStripMenuItem("Open Window", null, (_, _) => onOpenWindow());
        _exitItem = new ToolStripMenuItem("Exit", null, (_, _) => onExit());

        _menu.Items.Add(_lockItem);
        _menu.Items.Add(_pauseItem);
        _menu.Items.Add(_resumeItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_autoStartItem);
        _menu.Items.Add(_selectMicItem);
        _menu.Items.Add(_openWindowItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_exitItem);

        _menu.Opening += (_, _) =>
        {
            _autoStartItem.Checked = isAutoStartEnabled();
            UpdateForStatus(getLockStatus());
        };

        _notifyIcon = new NotifyIcon
        {
            Icon = AppIcon.Icon,
            Text = "MicMax — Microphone Volume Lock",
            ContextMenuStrip = _menu,
            Visible = true
        };

        // Single left-click opens the main window (US2 acceptance 4).
        _notifyIcon.Click += (_, e) =>
        {
            if (e is MouseEventArgs { Button: MouseButtons.Left })
            {
                onOpenWindow();
            }
        };
    }

    /// <summary>The underlying NotifyIcon, shared with the notification sink.</summary>
    public NotifyIcon NotifyIcon => _notifyIcon;

    /// <summary>
    /// The command labels exposed by the context menu, in display order.
    /// Used to assert the Tray Menu Command contract (contracts/README.md §2).
    /// </summary>
    public IReadOnlyList<string> CommandLabels => _menu.Items
        .OfType<ToolStripMenuItem>()
        .Select(i => i.Text ?? string.Empty)
        .ToList();

    /// <summary>Updates menu item visibility/enabled state for the lock status.</summary>
    public void UpdateForStatus(LockStateStatus status)
    {
        if (_disposed)
        {
            return;
        }

        var active = status == LockStateStatus.Active;
        var paused = status == LockStateStatus.Paused;

        _lockItem.Enabled = status is LockStateStatus.Inactive or LockStateStatus.Pending;
        _pauseItem.Enabled = active;
        _resumeItem.Enabled = paused;
    }

    /// <summary>Sets the tooltip text.</summary>
    public void SetTooltip(string text)
    {
        if (!_disposed)
        {
            _notifyIcon.Text = text.Length <= 63 ? text : text[..63];
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
    }
}
