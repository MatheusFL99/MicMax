using MicMax.Core.Services;
using Xunit;

namespace MicMax.Core.Tests;

/// <summary>
/// Unit tests for the LockState transition table (data-model.md) with
/// WasActiveBeforePause semantics (task T019).
/// </summary>
public class LockStateManagerTests
{
    [Fact]
    public void InitialStatus_IsInactive()
    {
        var manager = new LockStateManager();
        Assert.Equal(LockStateStatus.Inactive, manager.Status);
        Assert.False(manager.IsActive);
    }

    [Fact]
    public void Inactive_To_Active_ViaActivate()
    {
        var manager = new LockStateManager();

        Assert.True(manager.Activate());

        Assert.Equal(LockStateStatus.Active, manager.Status);
        Assert.True(manager.IsActive);
    }

    [Fact]
    public void Active_To_Paused_ViaPause_SetsWasActiveBeforePause()
    {
        var manager = new LockStateManager();
        manager.Activate();

        Assert.True(manager.Pause());

        Assert.Equal(LockStateStatus.Paused, manager.Status);
        Assert.False(manager.IsActive);
        Assert.True(manager.WasActiveBeforePause);
    }

    [Fact]
    public void Paused_To_Active_ViaResume_ClearsWasActiveBeforePause()
    {
        var manager = new LockStateManager();
        manager.Activate();
        manager.Pause();

        Assert.True(manager.Resume());

        Assert.Equal(LockStateStatus.Active, manager.Status);
        Assert.True(manager.IsActive);
        Assert.False(manager.WasActiveBeforePause);
    }

    [Fact]
    public void Active_To_Inactive_ViaDeactivate()
    {
        var manager = new LockStateManager();
        manager.Activate();

        Assert.True(manager.Deactivate());

        Assert.Equal(LockStateStatus.Inactive, manager.Status);
        Assert.False(manager.IsActive);
        Assert.False(manager.WasActiveBeforePause);
    }

    [Fact]
    public void Paused_To_Inactive_ViaDeactivate()
    {
        var manager = new LockStateManager();
        manager.Activate();
        manager.Pause();

        Assert.True(manager.Deactivate());

        Assert.Equal(LockStateStatus.Inactive, manager.Status);
        Assert.False(manager.WasActiveBeforePause);
    }

    [Fact]
    public void InvalidTransitions_ReturnFalse_AndDoNotChangeState()
    {
        var manager = new LockStateManager();

        // Cannot pause or resume when inactive.
        Assert.False(manager.Pause());
        Assert.False(manager.Resume());

        manager.Activate();

        // Cannot activate again while active; cannot resume while active.
        Assert.False(manager.Activate());
        Assert.False(manager.Resume());

        manager.Deactivate();

        // Cannot deactivate when already inactive.
        Assert.False(manager.Deactivate());
    }

    [Fact]
    public void Pause_From_Inactive_IsRejected()
    {
        var manager = new LockStateManager();

        Assert.False(manager.Pause());
        Assert.Equal(LockStateStatus.Inactive, manager.Status);
    }

    [Fact]
    public void MarkPending_FromActive_ToPending_ToActive()
    {
        var manager = new LockStateManager();
        manager.Activate();

        Assert.True(manager.MarkPending());
        Assert.Equal(LockStateStatus.Pending, manager.Status);

        Assert.True(manager.CompletePending());
        Assert.Equal(LockStateStatus.Active, manager.Status);
    }

    [Fact]
    public void Restore_WhenWasActive_MapsToPending()
    {
        var manager = new LockStateManager();

        manager.Restore(wasActive: true);
        Assert.Equal(LockStateStatus.Pending, manager.Status);

        manager.Restore(wasActive: false);
        Assert.Equal(LockStateStatus.Inactive, manager.Status);
    }
}
