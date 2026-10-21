using System;
using System.IO;
using MicMax.Core.Logging;

namespace MicMax.Core.Settings;

/// <summary>
/// Enables/disables launching MicMax at Windows sign-in by toggling a value
/// under the per-user HKCU Run key (FR-008, FR-010, FR-016 — current user
/// only, no admin, not a service).
/// </summary>
public sealed class AutoStartManager : IAutoStartManager
{
    /// <summary>Value name written under the Run key.</summary>
    public const string RunValueName = "MicMax";

    /// <summary>Per-user Run key path.</summary>
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly IRegistryAccessor _runKey;
    private readonly string _executablePath;

    /// <summary>
    /// Creates the manager.
    /// </summary>
    /// <param name="runKey">Accessor for the Run key (real or fake in tests).</param>
    /// <param name="executablePath">Full path of the executable to launch.</param>
    public AutoStartManager(IRegistryAccessor runKey, string executablePath)
    {
        _runKey = runKey ?? throw new ArgumentNullException(nameof(runKey));
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        _executablePath = executablePath;
    }

    /// <summary>
    /// Creates the manager against the real HKCU Run key using the current
    /// process executable.
    /// </summary>
    public AutoStartManager()
        : this(new UserRegistryAccessor(RunKeyPath), ResolveExecutablePath())
    {
    }

    /// <inheritdoc />
    public bool IsEnabled()
    {
        var value = _runKey.Read(RunValueName);
        return !string.IsNullOrEmpty(value);
    }

    /// <inheritdoc />
    public void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            // Quote the path so paths with spaces launch correctly.
            _runKey.Write(RunValueName, $"\"{_executablePath}\"");
            EventLogger.LogInfo("Auto-start enabled.");
        }
        else
        {
            _runKey.Delete(RunValueName);
            EventLogger.LogInfo("Auto-start disabled.");
        }
    }

    private static string ResolveExecutablePath()
    {
        var path = Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "MicMax.exe");
        return path;
    }
}
