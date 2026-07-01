using DungeonOfChrome.Core.Combat;
using DungeonOfChrome.Core.Tests.TestUtil;

namespace DungeonOfChrome.Core.Tests.Combat;

public class CooldownTrackerTests
{
    [Fact]
    public void NewAbility_IsReadyByDefault()
    {
        var tracker = new CooldownTracker();
        Assert.True(tracker.IsReady("fireball"));
        Assert.Equal(0, tracker.Get("fireball"));
    }

    [Fact]
    public void Set_ZeroOrNegativeTurns_IsNoOp()
    {
        var tracker = new CooldownTracker();
        tracker.Set("fireball", 0);
        Assert.True(tracker.IsReady("fireball"));
    }

    [Fact]
    public void Set_PositiveTurns_MakesAbilityNotReady()
    {
        var tracker = new CooldownTracker();
        tracker.Set("fireball", 3);
        Assert.False(tracker.IsReady("fireball"));
        Assert.Equal(3, tracker.Get("fireball"));
    }

    [Fact]
    public void Tick_DecrementsAndExpires()
    {
        var tracker = new CooldownTracker();
        tracker.Set("fireball", 2);

        tracker.Tick();
        Assert.False(tracker.IsReady("fireball"));
        Assert.Equal(1, tracker.Get("fireball"));

        tracker.Tick();
        Assert.True(tracker.IsReady("fireball"));
        Assert.Equal(0, tracker.Get("fireball"));
    }

    [Fact]
    public void ResetAll_ClearsEverything()
    {
        var tracker = new CooldownTracker();
        tracker.Set("fireball", 3);
        tracker.Set("heal", 2);
        tracker.ResetAll();
        Assert.True(tracker.IsReady("fireball"));
        Assert.True(tracker.IsReady("heal"));
    }
}

public class CooldownRegistryTests
{
    [Fact]
    public void DifferentCombatants_HaveIndependentCooldowns()
    {
        var registry = new CooldownRegistry();
        var a = new FakeCombatant("A");
        var b = new FakeCombatant("B");

        registry.SetCooldown(a, "fireball", 3);

        Assert.False(registry.IsReady(a, "fireball"));
        Assert.True(registry.IsReady(b, "fireball"));
    }

    [Fact]
    public void Tick_OnlyAffectsThatCombatant()
    {
        var registry = new CooldownRegistry();
        var a = new FakeCombatant("A");
        var b = new FakeCombatant("B");
        registry.SetCooldown(a, "fireball", 1);
        registry.SetCooldown(b, "fireball", 1);

        registry.Tick(a);

        Assert.True(registry.IsReady(a, "fireball"));
        Assert.False(registry.IsReady(b, "fireball"));
    }
}
