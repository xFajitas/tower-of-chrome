using TowerOfChrome.Core.Combat;
using TowerOfChrome.Core.Tests.TestUtil;

namespace TowerOfChrome.Core.Tests.Combat;

public class StatusTrackerTests
{
    private static StatusTracker NewTracker(params double[] doubles) =>
        new(new DamageCalculator(TestGameData.NewCombatConfig(), new ScriptedRandomSource(doubles: doubles)));

    [Fact]
    public void Apply_AddsStatusToCombatant_AndReturnsLogLine()
    {
        var c = new FakeCombatant("Hero");
        var tracker = NewTracker();

        var line = tracker.Apply(c, "poison");

        Assert.True(c.HasStatus("poison"));
        Assert.Contains("poison", line);
        Assert.Contains(c.Name, line);
    }

    [Fact]
    public void Apply_UnknownEffect_UsesFallbackDurationOfTwo()
    {
        var c = new FakeCombatant("Hero");
        var tracker = NewTracker();
        tracker.Apply(c, "totally_made_up_effect");

        // Tick twice: should still be present after 1 tick (2->1), gone after 2nd tick (1->0).
        tracker.Tick(c);
        Assert.True(c.HasStatus("totally_made_up_effect"));
        tracker.Tick(c);
        Assert.False(c.HasStatus("totally_made_up_effect"));
    }

    [Fact]
    public void Apply_KnownEffect_UsesConfiguredDefaultDuration()
    {
        var c = new FakeCombatant("Hero");
        var tracker = NewTracker();
        tracker.Apply(c, "stun"); // default duration = 1

        var lines = tracker.Tick(c);

        Assert.False(c.HasStatus("stun"));
        Assert.Contains(lines, l => l.Contains("wore off"));
    }

    [Fact]
    public void Tick_AppliesPoisonDot_BeforeDecrementingDuration()
    {
        var c = new FakeCombatant("Hero", hp: 200);
        var tracker = NewTracker();
        tracker.Apply(c, "poison"); // 5% of 200 = 10 dmg/turn, duration 3

        var lines = tracker.Tick(c);

        Assert.Equal(190, c.CurrentHp);
        Assert.Contains(lines, l => l.Contains("suffers 10 dmg"));
    }

    [Fact]
    public void Tick_AppliesRegenHeal()
    {
        var c = new FakeCombatant("Hero", hp: 200);
        c.TakeDamage(50);
        var tracker = NewTracker();
        tracker.Apply(c, "regenerating"); // 6% of 200 = 12

        var lines = tracker.Tick(c);

        Assert.Equal(200 - 50 + 12, c.CurrentHp);
        Assert.Contains(lines, l => l.Contains("regenerates 12 HP"));
    }

    [Fact]
    public void Tick_DoesNotDamageOrHeal_DeadCombatant()
    {
        var c = new FakeCombatant("Hero", hp: 10);
        c.TakeDamage(10); // KO
        var tracker = NewTracker();
        tracker.Apply(c, "poison");

        var lines = tracker.Tick(c);

        Assert.DoesNotContain(lines, l => l.Contains("suffers"));
    }

    [Fact]
    public void Clear_RemovesAllStatuses_AndDurations()
    {
        var c = new FakeCombatant("Hero");
        var tracker = NewTracker();
        tracker.Apply(c, "poison");
        tracker.Apply(c, "blessed");

        tracker.Clear(c);

        Assert.Empty(c.StatusEffects);
        // Duration entries are gone too: ticking again should produce no DoT/expiry lines.
        var lines = tracker.Tick(c);
        Assert.Empty(lines);
    }

    [Fact]
    public void Tick_OnlyAffectsRequestedCombatant()
    {
        var a = new FakeCombatant("A", hp: 200);
        var b = new FakeCombatant("B", hp: 200);
        var tracker = NewTracker();
        tracker.Apply(a, "poison");
        tracker.Apply(b, "poison");

        tracker.Tick(a);

        Assert.Equal(190, a.CurrentHp);
        Assert.Equal(200, b.CurrentHp); // untouched
    }
}
