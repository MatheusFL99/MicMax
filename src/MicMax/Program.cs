using System;
using System.Windows.Forms;
using MicMax.Core.Audio;
using MicMax.Core.Logging;
using MicMax.Core.Models;
using MicMax.Core.Services;
using MicMax.Core.Settings;

namespace MicMax;

/// <summary>
/// Application entry point. Composes the Core services, restores persisted
/// settings, restores the previous lock state, and runs a tray-centric
/// message loop (FR-005, FR-010).
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        EventLogger.LogInfo("MicMax starting.");

        // Core services.
        var deviceManager = new CoreAudioDeviceManager();
        var stateManager = new LockStateManager();
        var lockService = new VolumeLockService(deviceManager, stateManager);
        var fallbackManager = new DeviceFallbackManager(deviceManager, lockService);

        // Persistence.
        var settingsStore = new RegistrySettingsStore();
        var autoStartManager = new AutoStartManager();
        var settings = settingsStore.Load();

        // Trust the actual Run key as the source of truth for auto-start (FR-009).
        settings.AutoStartEnabled = autoStartManager.IsEnabled();

        // Restore device selection intent (FR-001, FR-009).
        if (settings.SelectedDevice.SelectionMode == MicMax.Core.Models.SelectionMode.ExplicitDevice &&
            settings.SelectedDevice.SelectedDeviceGUID.HasValue)
        {
            lockService.SelectDevice(settings.SelectedDevice.SelectedDeviceGUID);
            fallbackManager.SetOriginalDevice(settings.SelectedDevice.SelectedDeviceGUID);
        }

        using var context = new MicMaxApplicationContext(
            deviceManager,
            lockService,
            fallbackManager,
            stateManager,
            settingsStore,
            autoStartManager,
            settings);

        // Restore the previous lock state (FR-010). Notifications flow through
        // the tray sink owned by the context.
        if (settings.LockState.IsActive)
        {
            lockService.ActivateLock();
        }

        fallbackManager.Start();

        Application.Run(context);

        // Persist final state on exit (FR-009).
        try
        {
            var final = new AppSettings
            {
                SelectedDevice = new DeviceSelection
                {
                    SelectionMode = settings.SelectedDevice.SelectionMode,
                    SelectedDeviceGUID = settings.SelectedDevice.SelectedDeviceGUID
                },
                LockState = new LockState
                {
                    IsActive = lockService.IsActive || stateManager.Status == LockStateStatus.Pending
                },
                AutoStartEnabled = autoStartManager.IsEnabled()
            };
            settingsStore.Save(final);
        }
        catch (Exception ex)
        {
            EventLogger.LogError($"Failed to persist settings on exit: {ex.Message}");
        }

        EventLogger.LogInfo("MicMax exited.");
    }
}
