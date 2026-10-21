using System;
using MicMax.Core.Logging;
using MicMax.Core.Models;

namespace MicMax.Core.Settings;

/// <summary>
/// Minimal key/value abstraction over the Windows Registry so persistence can
/// be unit-tested with an in-memory implementation while production writes to
/// the per-user hive (FR-009, VR-006).
/// </summary>
public interface IRegistryAccessor
{
    /// <summary>Reads a string value, or null when absent.</summary>
    string? Read(string name);

    /// <summary>Writes a string value.</summary>
    void Write(string name, string value);

    /// <summary>Deletes a value when present.</summary>
    void Delete(string name);
}

/// <summary>
/// Per-user Registry implementation backed by HKCU (FR-016 — no admin needed).
/// </summary>
public sealed class UserRegistryAccessor : IRegistryAccessor
{
    private readonly string _subKey;

    /// <summary>
    /// Opens (creating as needed) the given per-user subkey path.
    /// </summary>
    /// <param name="subKey">Registry path under HKCU (default Software\MicMax).</param>
    public UserRegistryAccessor(string subKey = @"Software\MicMax")
    {
        _subKey = subKey;
    }

    /// <inheritdoc />
    public string? Read(string name)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(_subKey, writable: false);
            return key?.GetValue(name) as string;
        }
        catch (Exception ex)
        {
            EventLogger.LogWarning($"Registry read failed for {name}: {ex.Message}");
            return null;
        }
    }

    /// <inheritdoc />
    public void Write(string name, string value)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(_subKey, writable: true);
        if (key == null)
        {
            throw new InvalidOperationException($"Unable to open HKCU\\{_subKey} for writing.");
        }

        // Registry.SetValue is atomic per value; ordering keeps corruption risk minimal.
        key.SetValue(name, value, Microsoft.Win32.RegistryValueKind.String);
    }

    /// <inheritdoc />
    public void Delete(string name)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(_subKey, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            EventLogger.LogWarning($"Registry delete failed for {name}: {ex.Message}");
        }
    }
}
