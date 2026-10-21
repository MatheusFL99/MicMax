using System;
using MicMax.Core.Settings;
using MicMax.Core.Tests.Fakes;
using Xunit;

namespace MicMax.Core.Tests;

/// <summary>
/// Unit tests for the auto-start enable/disable toggle (task T025), backed by
/// an in-memory registry accessor so no real Run key is modified.
/// </summary>
public class AutoStartManagerTests
{
    private static AutoStartManager CreateManager(FakeRegistryAccessor registry) =>
        new(registry, @"C:\Apps\MicMax.exe");

    [Fact]
    public void IsEnabled_WhenRunValueAbsent_ReturnsFalse()
    {
        var registry = new FakeRegistryAccessor();
        var manager = CreateManager(registry);

        Assert.False(manager.IsEnabled());
    }

    [Fact]
    public void SetEnabled_True_WritesQuotedRunValue_AndReportsEnabled()
    {
        var registry = new FakeRegistryAccessor();
        var manager = CreateManager(registry);

        manager.SetEnabled(true);

        Assert.True(manager.IsEnabled());
        var value = registry.Values[AutoStartManager.RunValueName];
        Assert.Equal("\"C:\\Apps\\MicMax.exe\"", value);
    }

    [Fact]
    public void SetEnabled_False_RemovesRunValue_AndReportsDisabled()
    {
        var registry = new FakeRegistryAccessor();
        var manager = CreateManager(registry);

        manager.SetEnabled(true);
        Assert.True(manager.IsEnabled());

        manager.SetEnabled(false);

        Assert.False(manager.IsEnabled());
        Assert.False(registry.Values.ContainsKey(AutoStartManager.RunValueName));
    }

    [Fact]
    public void SetEnabled_IsIdempotent()
    {
        var registry = new FakeRegistryAccessor();
        var manager = CreateManager(registry);

        manager.SetEnabled(true);
        manager.SetEnabled(true);
        Assert.True(manager.IsEnabled());

        manager.SetEnabled(false);
        manager.SetEnabled(false);
        Assert.False(manager.IsEnabled());
    }

    [Fact]
    public void RunValueName_And_KeyPath_MatchContract()
    {
        // The contract specifies the per-user Run key (FR-008, FR-010).
        Assert.Equal("MicMax", AutoStartManager.RunValueName);
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", AutoStartManager.RunKeyPath);
    }
}
