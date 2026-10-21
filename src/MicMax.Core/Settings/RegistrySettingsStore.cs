using System;
using System.Globalization;
using MicMax.Core.Logging;
using MicMax.Core.Models;

namespace MicMax.Core.Settings;

/// <summary>
/// Persists <see cref="AppSettings"/> to the per-user Windows Registry under
/// HKCU\Software\MicMax (FR-009). Values: DeviceGUID, DeviceMode, LockActive,
/// AutoStart (Settings Persistence contract §3).
/// </summary>
public sealed class RegistrySettingsStore : ISettingsStore
{
    /// <summary>Value names persisted under the MicMax key.</summary>
    public const string DeviceGuidValue = "DeviceGUID";
    public const string DeviceModeValue = "DeviceMode";
    public const string LockActiveValue = "LockActive";
    public const string AutoStartValue = "AutoStart";

    private const string ModeExplicit = "ExplicitDevice";
    private const string ModeFollowDefault = "FollowDefaultCommunication";

    private readonly IRegistryAccessor _registry;

    /// <summary>Creates the store over the given registry accessor.</summary>
    public RegistrySettingsStore(IRegistryAccessor registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>Creates the store against the real HKCU\Software\MicMax key.</summary>
    public RegistrySettingsStore()
        : this(new UserRegistryAccessor())
    {
    }

    /// <inheritdoc />
    public AppSettings Load()
    {
        var settings = new AppSettings
        {
            SelectedDevice = new DeviceSelection
            {
                SelectionMode = ParseMode(_registry.Read(DeviceModeValue)),
                SelectedDeviceGUID = ParseGuid(_registry.Read(DeviceGuidValue))
            },
            LockState = new LockState
            {
                IsActive = ParseBool(_registry.Read(LockActiveValue))
            },
            AutoStartEnabled = ParseBool(_registry.Read(AutoStartValue)),
            LastSavedTime = DateTime.UtcNow
        };

        // Explicit GUID without an explicit mode implies explicit selection.
        if (settings.SelectedDevice.SelectedDeviceGUID.HasValue &&
            settings.SelectedDevice.SelectionMode == SelectionMode.FollowDefaultCommunication &&
            _registry.Read(DeviceModeValue) == null)
        {
            settings.SelectedDevice.SelectionMode = SelectionMode.ExplicitDevice;
        }

        return settings;
    }

    /// <inheritdoc />
    public void Save(AppSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        try
        {
            var selection = settings.SelectedDevice;

            _registry.Write(DeviceModeValue, selection.SelectionMode == SelectionMode.ExplicitDevice
                ? ModeExplicit
                : ModeFollowDefault);

            _registry.Write(DeviceGuidValue, selection.SelectedDeviceGUID?.ToString() ?? string.Empty);
            _registry.Write(LockActiveValue, settings.LockState.IsActive ? "true" : "false");
            _registry.Write(AutoStartValue, settings.AutoStartEnabled ? "true" : "false");

            settings.LastSavedTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            EventLogger.LogError($"Failed to save settings: {ex.Message}");
            throw;
        }
    }

    private static bool ParseBool(string? raw)
    {
        if (bool.TryParse(raw, out var value))
        {
            return value;
        }

        return raw == "1";
    }

    private static Guid? ParseGuid(string? raw) =>
        Guid.TryParse(raw, out var guid) ? guid : null;

    private static SelectionMode ParseMode(string? raw) =>
        string.Equals(raw, ModeExplicit, StringComparison.OrdinalIgnoreCase)
            ? SelectionMode.ExplicitDevice
            : SelectionMode.FollowDefaultCommunication;
}
