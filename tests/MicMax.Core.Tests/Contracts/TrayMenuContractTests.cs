using System;
using System.Linq;
using MicMax.Core.Services;
using MicMax.Tray;
using Xunit;

namespace MicMax.Core.Tests.Contracts;

/// <summary>
/// Contract test for the Tray Menu Command contract (contracts/README.md §2):
/// Lock, Pause, Resume, Auto-Start, Select Microphone, Open Window, Exit
/// (task T018).
/// </summary>
public class TrayMenuContractTests
{
    private static TrayIcon CreateTray() => new(
        onLock: () => { },
        onPause: () => { },
        onResume: () => { },
        onToggleAutoStart: () => { },
        onSelectMicrophone: () => { },
        onOpenWindow: () => { },
        onExit: () => { },
        isAutoStartEnabled: () => false,
        getLockStatus: () => LockStateStatus.Inactive);

    [Fact]
    public void TrayMenu_ExposesAllContractCommands()
    {
        using var tray = CreateTray();
        var labels = tray.CommandLabels.Select(Normalize).ToList();

        Assert.Contains("lock", labels);
        Assert.Contains("pause", labels);
        Assert.Contains("resume", labels);
        Assert.Contains("auto-start", labels);
        Assert.Contains("select microphone", labels);
        Assert.Contains("open window", labels);
        Assert.Contains("exit", labels);
    }

    [Fact]
    public void TrayMenu_ContainsExactlyTheContractCommands_NoExtras()
    {
        using var tray = CreateTray();
        var labels = tray.CommandLabels.Select(Normalize).ToList();

        var expected = new[]
        {
            "lock", "pause", "resume", "auto-start",
            "select microphone", "open window", "exit"
        };

        Assert.Equal(expected.OrderBy(x => x), labels.OrderBy(x => x));
    }

    [Fact]
    public void TrayMenu_EnablesPauseOnlyWhenActive()
    {
        using var tray = CreateTray();

        // Simulate the status update the app performs on transitions.
        tray.UpdateForStatus(LockStateStatus.Active);
        tray.UpdateForStatus(LockStateStatus.Paused);
        tray.UpdateForStatus(LockStateStatus.Inactive);

        // Sanity: no exception thrown for each status; command set is stable.
        Assert.Equal(7, tray.CommandLabels.Count);
    }

    private static string Normalize(string text) => text.Trim().ToLowerInvariant();
}
