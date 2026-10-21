using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MicMax.Core.Audio;
using MicMax.Core.Models;
using MicMax.Core.Services;

namespace MicMax.Windows;

/// <summary>
/// Main window (FR-001, FR-006, FR-008). Shows the available microphones,
/// a lock toggle, a "follow default communication device" option, and the
/// auto-start toggle. Closing the window hides it and keeps the app running
/// in the tray (FR-005).
/// </summary>
public sealed class MainWindow : Form
{
    private readonly IAudioDeviceManager _deviceManager;
    private readonly VolumeLockService _lockService;

    private readonly ComboBox _deviceList = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 };
    private readonly CheckBox _followDefault = new() { Text = "Follow Windows default communication device", AutoSize = true };
    private readonly Button _lockButton = new() { Text = "Lock Microphone", Width = 160, Height = 32 };
    private readonly CheckBox _autoStart = new() { Text = "Start automatically with Windows", AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, MaximumSize = new Size(360, 0) };

    private IReadOnlyList<AudioInputDevice> _devices = Array.Empty<AudioInputDevice>();
    private bool _suspendEvents;
    private bool _forceClose;

    /// <summary>Creates and lays out the window.</summary>
    public MainWindow(IAudioDeviceManager deviceManager, VolumeLockService lockService, Func<bool> isAutoStartEnabled)
    {
        _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
        _lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));

        Text = "MicMax — Microphone Volume Lock";
        Icon = AppIcon.Icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(400, 240);
        ShowInTaskbar = true;

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(16),
            AutoScroll = true
        };

        layout.Controls.Add(new Label { Text = "Microphone:", AutoSize = true });
        layout.Controls.Add(_deviceList);
        layout.Controls.Add(_followDefault);
        layout.Controls.Add(_lockButton);
        layout.Controls.Add(_autoStart);
        layout.Controls.Add(_status);

        Controls.Add(layout);

        _autoStart.Checked = isAutoStartEnabled();

        _deviceList.SelectedIndexChanged += OnDeviceSelectionChanged;
        _followDefault.CheckedChanged += OnFollowDefaultChanged;
        _lockButton.Click += OnLockButtonClicked;
        _autoStart.CheckedChanged += OnAutoStartChanged;

        // Close-to-tray: hide instead of exiting (FR-005, US2 acceptance 1).
        FormClosing += OnFormClosing;
        Shown += (_, _) => RefreshDevices();

        _lockService.LockStateChanged += OnLockStateChanged;
    }

    /// <summary>Callback invoked when the auto-start toggle changes.</summary>
    public Action<bool>? AutoStartChanged { get; set; }

    /// <summary>Callback invoked when the user picks a device or toggles follow-default.</summary>
    public Action? SelectionChanged { get; set; }

    /// <summary>Refreshes the device list and status text.</summary>
    public void RefreshDevices()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke((Action)RefreshDevices);
            return;
        }

        _suspendEvents = true;
        try
        {
            _devices = _deviceManager.GetInputDevices();

            var currentGuid = _lockService.LockedDeviceGuid;
            _deviceList.Items.Clear();

            foreach (var device in _devices)
            {
                _deviceList.Items.Add(new DeviceListItem(device));
            }

            // Select the locked device, else the default communication device.
            var selectedIndex = -1;
            for (var i = 0; i < _devices.Count; i++)
            {
                if (currentGuid.HasValue && _devices[i].GUID == currentGuid.Value)
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex < 0)
            {
                for (var i = 0; i < _devices.Count; i++)
                {
                    if (_devices[i].IsDefaultCommunication)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            _followDefault.Checked = currentGuid == null;
            if (selectedIndex >= 0)
            {
                _deviceList.SelectedIndex = selectedIndex;
            }

            UpdateStatusText();
        }
        finally
        {
            _suspendEvents = false;
        }
    }

    private void OnDeviceSelectionChanged(object? sender, EventArgs e)
    {
        if (_suspendEvents || _followDefault.Checked)
        {
            return;
        }

        if (_deviceList.SelectedItem is DeviceListItem item)
        {
            _lockService.SelectDevice(item.Device.GUID);
            SelectionChanged?.Invoke();
        }
    }

    private void OnFollowDefaultChanged(object? sender, EventArgs e)
    {
        if (_suspendEvents)
        {
            return;
        }

        if (_followDefault.Checked)
        {
            _lockService.SelectDevice(null);
            SelectionChanged?.Invoke();
        }
    }

    private void OnLockButtonClicked(object? sender, EventArgs e)
    {
        if (_lockService.IsActive)
        {
            _lockService.DeactivateLock();
        }
        else
        {
            _lockService.ActivateLock();
        }

        UpdateStatusText();
    }

    private void OnAutoStartChanged(object? sender, EventArgs e)
    {
        if (_suspendEvents)
        {
            return;
        }

        AutoStartChanged?.Invoke(_autoStart.Checked);
    }

    private void OnLockStateChanged(object? sender, LockStateChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke((Action)(() => OnLockStateChanged(sender, e)));
            return;
        }

        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        var status = _lockService.State.Status;
        string text = status switch
        {
            LockStateStatus.Active => $"Status: LOCKED at 100% ({_lockService.LockedDeviceName ?? "default device"})",
            LockStateStatus.Paused => "Status: Paused",
            LockStateStatus.Pending => "Status: Waiting for a microphone…",
            _ => "Status: Unlocked"
        };

        _status.Text = text;
        _lockButton.Text = _lockService.IsActive ? "Unlock Microphone" : "Lock Microphone";
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_forceClose && e.CloseReason == CloseReason.UserClosing)
        {
            // Hide to tray; keep the lock running (FR-005).
            e.Cancel = true;
            Hide();
            return;
        }

        _lockService.LockStateChanged -= OnLockStateChanged;
    }

    /// <summary>Forces the window to close (app exit).</summary>
    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lockService.LockStateChanged -= OnLockStateChanged;
        }

        base.Dispose(disposing);
    }

    private sealed class DeviceListItem
    {
        public DeviceListItem(AudioInputDevice device) => Device = device;

        public AudioInputDevice Device { get; }

        public override string ToString() =>
            Device.IsDefaultCommunication
                ? $"{Device.DisplayName} (default)"
                : Device.DisplayName;
    }
}
