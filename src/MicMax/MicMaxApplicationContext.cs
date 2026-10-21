using System;
using System.Windows.Forms;
using MicMax.Core.Audio;
using MicMax.Core.Logging;
using MicMax.Core.Models;
using MicMax.Core.Notifications;
using MicMax.Core.Services;
using MicMax.Core.Settings;
using MicMax.Tray;
using MicMax.Windows;

namespace MicMax;

/// <summary>
/// Tray-centric application context: owns the tray icon, the main window, and
/// coordinates lock/pause/resume, auto-start, device selection, and
/// notifications. Closing the window keeps the app alive in the tray (FR-005).
/// </summary>
internal sealed class MicMaxApplicationContext : ApplicationContext
{
    private readonly IAudioDeviceManager _deviceManager;
    private readonly VolumeLockService _lockService;
    private readonly DeviceFallbackManager _fallbackManager;
    private readonly LockStateManager _stateManager;
    private readonly ISettingsStore _settingsStore;
    private readonly IAutoStartManager _autoStart;
    private readonly NotificationService _notifications;
    private readonly AppSettings _settings;

    private readonly TrayNotificationSink _sink;
    private readonly TrayIcon _trayIcon;
    private readonly MainWindow _mainWindow;

    private bool _exiting;

    public MicMaxApplicationContext(
        IAudioDeviceManager deviceManager,
        VolumeLockService lockService,
        DeviceFallbackManager fallbackManager,
        LockStateManager stateManager,
        ISettingsStore settingsStore,
        IAutoStartManager autoStart,
        AppSettings settings)
    {
        _deviceManager = deviceManager;
        _lockService = lockService;
        _fallbackManager = fallbackManager;
        _stateManager = stateManager;
        _settingsStore = settingsStore;
        _autoStart = autoStart;
        _settings = settings;

        // Build tray first so its NotifyIcon can back the notification sink.
        _trayIcon = new TrayIcon(
            onLock: OnLock,
            onPause: OnPause,
            onResume: OnResume,
            onToggleAutoStart: OnToggleAutoStart,
            onSelectMicrophone: ShowMainWindow,
            onOpenWindow: ShowMainWindow,
            onExit: OnExit,
            isAutoStartEnabled: () => _settings.AutoStartEnabled || _autoStart.IsEnabled(),
            getLockStatus: () => _stateManager.Status);

        _sink = new TrayNotificationSink(_trayIcon.NotifyIcon);
        _notifications = new NotificationService(_sink);

        _mainWindow = new MainWindow(_deviceManager, _lockService, () => _autoStart.IsEnabled())
        {
            AutoStartChanged = OnAutoStartChangedByUser,
            SelectionChanged = SaveSettings
        };

        _lockService.LockStateChanged += OnLockStateChanged;
        _fallbackManager.DeviceSwitched += OnDeviceSwitched;

        _trayIcon.UpdateForStatus(_stateManager.Status);
        Application.ApplicationExit += (_, _) => Cleanup();
    }

    private void OnLock()
    {
        if (_lockService.ActivateLock())
        {
            _notifications.NotifyLockActivated(_lockService.LockedDeviceName);
        }
        else
        {
            _notifications.NotifyLockActivated(null);
        }

        SaveSettings();
        _trayIcon.UpdateForStatus(_stateManager.Status);
        _mainWindow.RefreshDevices();
    }

    private void OnPause()
    {
        _lockService.Pause();
        _notifications.NotifyLockPaused();
        SaveSettings();
        _trayIcon.UpdateForStatus(_stateManager.Status);
    }

    private void OnResume()
    {
        _lockService.Resume();
        _notifications.NotifyLockResumed();
        SaveSettings();
        _trayIcon.UpdateForStatus(_stateManager.Status);
    }

    private void OnToggleAutoStart()
    {
        var enabled = !_autoStart.IsEnabled();
        _autoStart.SetEnabled(enabled);
        _settings.AutoStartEnabled = enabled;
        SaveSettings();
        EventLogger.LogInfo($"Auto-start {(enabled ? "enabled" : "disabled")} from tray menu.");
    }

    private void OnAutoStartChangedByUser(bool enabled)
    {
        _autoStart.SetEnabled(enabled);
        _settings.AutoStartEnabled = enabled;
        SaveSettings();
    }

    private void OnLockStateChanged(object? sender, LockStateChangedEventArgs e)
    {
        _trayIcon.UpdateForStatus(_stateManager.Status);
        SaveSettings();
    }

    private void OnDeviceSwitched(object? sender, DeviceSwitchedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.DeviceName))
        {
            return;
        }

        if (e.IsRestored)
        {
            _notifications.NotifyDeviceRestored(e.DeviceName);
        }
        else
        {
            _notifications.NotifyDeviceSwitched(e.DeviceName);
        }

        _mainWindow.RefreshDevices();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow.IsDisposed)
        {
            return;
        }

        if (_mainWindow.Visible)
        {
            _mainWindow.Activate();
        }
        else
        {
            _mainWindow.RefreshDevices();
            _mainWindow.Show();
            _mainWindow.Activate();
        }
    }

    private void OnExit()
    {
        _exiting = true;
        SaveSettings();
        Application.Exit();
    }

    private void SaveSettings()
    {
        if (_exiting)
        {
            return;
        }

        try
        {
            var settings = new AppSettings
            {
                SelectedDevice = new DeviceSelection
                {
                    SelectionMode = _settings.SelectedDevice.SelectionMode,
                    SelectedDeviceGUID = _lockService.LockedDeviceGuid ?? _settings.SelectedDevice.SelectedDeviceGUID
                },
                LockState = new LockState
                {
                    IsActive = _lockService.IsActive || _stateManager.Status == LockStateStatus.Pending
                },
                AutoStartEnabled = _autoStart.IsEnabled()
            };

            _settingsStore.Save(settings);
            _settings.AutoStartEnabled = settings.AutoStartEnabled;
            _settings.SelectedDevice = settings.SelectedDevice;
            _settings.LockState = settings.LockState;
        }
        catch (Exception ex)
        {
            EventLogger.LogError($"Failed to save settings: {ex.Message}");
        }
    }

    private void Cleanup()
    {
        _lockService.LockStateChanged -= OnLockStateChanged;
        _fallbackManager.DeviceSwitched -= OnDeviceSwitched;

        try
        {
            _fallbackManager.Dispose();
        }
        catch
        {
            // Best effort.
        }

        try
        {
            _lockService.Dispose();
        }
        catch
        {
            // Best effort.
        }

        try
        {
            _deviceManager.Dispose();
        }
        catch
        {
            // Best effort.
        }

        _trayIcon.Dispose();
        _mainWindow.Dispose();
    }
}
