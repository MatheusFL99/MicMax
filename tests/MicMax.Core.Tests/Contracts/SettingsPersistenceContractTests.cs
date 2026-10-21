using System;
using MicMax.Core.Models;
using MicMax.Core.Settings;
using MicMax.Core.Tests.Fakes;
using Xunit;
using SelectionMode = MicMax.Core.Models.SelectionMode;

namespace MicMax.Core.Tests.Contracts;

/// <summary>
/// Contract test for the Settings Persistence contract (contracts/README.md §3):
/// round-trip of DeviceGUID, DeviceMode, LockActive, AutoStart (task T024).
/// Uses an in-memory registry accessor to avoid touching the real HKCU hive.
/// </summary>
public class SettingsPersistenceContractTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsAllContractValues()
    {
        var registry = new FakeRegistryAccessor();
        var store = new RegistrySettingsStore(registry);

        var original = new AppSettings
        {
            SelectedDevice = new DeviceSelection
            {
                SelectionMode = SelectionMode.ExplicitDevice,
                SelectedDeviceGUID = Guid.NewGuid()
            },
            LockState = new LockState { IsActive = true },
            AutoStartEnabled = true
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(original.SelectedDevice.SelectedDeviceGUID, loaded.SelectedDevice.SelectedDeviceGUID);
        Assert.Equal(SelectionMode.ExplicitDevice, loaded.SelectedDevice.SelectionMode);
        Assert.True(loaded.LockState.IsActive);
        Assert.True(loaded.AutoStartEnabled);
    }

    [Fact]
    public void Save_WritesTheFourContractValueNames()
    {
        var registry = new FakeRegistryAccessor();
        var store = new RegistrySettingsStore(registry);

        store.Save(new AppSettings
        {
            SelectedDevice = new DeviceSelection
            {
                SelectionMode = SelectionMode.FollowDefaultCommunication,
                SelectedDeviceGUID = null
            },
            LockState = new LockState { IsActive = false },
            AutoStartEnabled = false
        });

        Assert.True(registry.Values.ContainsKey(RegistrySettingsStore.DeviceGuidValue));
        Assert.True(registry.Values.ContainsKey(RegistrySettingsStore.DeviceModeValue));
        Assert.True(registry.Values.ContainsKey(RegistrySettingsStore.LockActiveValue));
        Assert.True(registry.Values.ContainsKey(RegistrySettingsStore.AutoStartValue));
    }

    [Fact]
    public void Load_WhenNothingPersisted_ReturnsDefaults()
    {
        var registry = new FakeRegistryAccessor();
        var store = new RegistrySettingsStore(registry);

        var loaded = store.Load();

        Assert.Equal(SelectionMode.FollowDefaultCommunication, loaded.SelectedDevice.SelectionMode);
        Assert.Null(loaded.SelectedDevice.SelectedDeviceGUID);
        Assert.False(loaded.LockState.IsActive);
        Assert.False(loaded.AutoStartEnabled);
    }

    [Fact]
    public void RoundTrip_LockInactiveAndAutoStartDisabled()
    {
        var registry = new FakeRegistryAccessor();
        var store = new RegistrySettingsStore(registry);

        store.Save(new AppSettings
        {
            LockState = new LockState { IsActive = false },
            AutoStartEnabled = false
        });

        var loaded = store.Load();

        Assert.False(loaded.LockState.IsActive);
        Assert.False(loaded.AutoStartEnabled);
    }

    [Fact]
    public void RoundTrip_FollowDefaultMode_PersistsMode()
    {
        var registry = new FakeRegistryAccessor();
        var store = new RegistrySettingsStore(registry);

        store.Save(new AppSettings
        {
            SelectedDevice = new DeviceSelection
            {
                SelectionMode = SelectionMode.FollowDefaultCommunication,
                SelectedDeviceGUID = Guid.NewGuid()
            }
        });

        var loaded = store.Load();

        Assert.Equal(SelectionMode.FollowDefaultCommunication, loaded.SelectedDevice.SelectionMode);
    }

    [Fact]
    public void Save_MultipleTimes_LastValueWins()
    {
        var registry = new FakeRegistryAccessor();
        var store = new RegistrySettingsStore(registry);

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        store.Save(new AppSettings
        {
            SelectedDevice = new DeviceSelection
            {
                SelectionMode = SelectionMode.ExplicitDevice,
                SelectedDeviceGUID = first
            }
        });
        store.Save(new AppSettings
        {
            SelectedDevice = new DeviceSelection
            {
                SelectionMode = SelectionMode.ExplicitDevice,
                SelectedDeviceGUID = second
            }
        });

        Assert.Equal(second, store.Load().SelectedDevice.SelectedDeviceGUID);
    }
}
