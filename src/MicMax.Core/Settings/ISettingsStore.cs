using System;
using MicMax.Core.Models;

namespace MicMax.Core.Settings;

/// <summary>
/// Settings Persistence contract (contracts/README.md §3).
/// </summary>
public interface ISettingsStore
{
    /// <summary>Loads persisted settings, or defaults when none exist.</summary>
    AppSettings Load();

    /// <summary>Persists the given settings atomically.</summary>
    void Save(AppSettings settings);
}

/// <summary>
/// Auto-start contract: toggles launch-at-sign-in for the current user (FR-008).
/// </summary>
public interface IAutoStartManager
{
    /// <summary>Whether auto-start is currently enabled.</summary>
    bool IsEnabled();

    /// <summary>Enables or disables launch at Windows sign-in.</summary>
    void SetEnabled(bool enabled);
}
