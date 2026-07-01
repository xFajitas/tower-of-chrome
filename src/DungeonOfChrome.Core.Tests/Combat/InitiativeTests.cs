using DungeonOfChrome.Core.Combat;
using DungeonOfChrome.Core.Tests.TestUtil;

namespace DungeonOfChrome.Core.Tests.Combat;

public class InitiativeTests
{
    [Fact]
    public void HigherScore_GoesFirst()
    {
        var fast = new FakeCombatant("Fast", speed: 10);
        var slow = new FakeCombatant("Slow", speed: 5);
        // d10 rolls: fast gets +1 (score 11), slow gets +1 (score 6) -> fast wins outright, no tie.
        var rng = new ScriptedRandomSource(ints: new[] { 1, 1 }, doubles: new[] { 0.1, 0.1 });

        var order = Initiative.RollInitiative(new[] { slow, fast }, rng);

        Assert.Equal(new[] { fast, slow }, order);
    }

    [Fact]
    public void TiedScore_BrokenBySpeed()
    {
        var fast = new FakeCombatant("Fast", speed: 8);
        var slow = new FakeCombatant("Slow", speed: 5);
        // fast: 8+2=10, slow: 5+5=10 -> tied score, speed breaks the tie (fast wins).
        var rng = new ScriptedRandomSource(ints: new[] { 2, 5 }, doubles: new[] { 0.5, 0.5 });

        var order = Initiative.RollInitiative(new[] { slow, fast }, rng);

        Assert.Equal(new[] { fast, slow }, order);
    }

    [Fact]
    public void TiedScoreAndSpeed_BrokenByPrecomputedRandomTiebreak()
    {
        var a = new FakeCombatant("A", speed: 5);
        var b = new FakeCombatant("B", speed: 5);
        // Both: 5+3=8, same speed -> tiebreak decides. A's precomputed tiebreak (0.9) beats B's (0.1).
        var rng = new ScriptedRandomSource(ints: new[] { 3, 3 }, doubles: new[] { 0.9, 0.1 });

        var order = Initiative.RollInitiative(new[] { a, b }, rng);

        Assert.Equal(new[] { a, b }, order);
    }

    [Fact]
    public void DeadCombatants_AreExcluded()
    {
        var alive = new FakeCombatant("Alive", speed: 5);
        var dead = new FakeCombatant("Dead", speed: 100);
        dead.TakeDamage(dead.MaxHp);
        Assert.True(dead.IsKo);

        var rng = new ScriptedRandomSource(ints: new[] { 1 }, doubles: new[] { 0.1 });
        var order = Initiative.RollInitiative(new[] { alive, dead }, rng);

        Assert.Equal(new[] { alive }, order);
    }

    [Fact]
    public void EmptyOrAllDead_ReturnsEmptyList()
    {
        var dead = new FakeCombatant("Dead", speed: 5);
        dead.TakeDamage(dead.MaxHp);
        var rng = new ScriptedRandomSource();

        Assert.Empty(Initiative.RollInitiative(new[] { dead }, rng));
        Assert.Empty(Initiative.RollInitiative(Array.Empty<DungeonOfChrome.Core.Entities.Combatant>(), rng));
    }

    [Fact]
    public void InputList_IsNotMutated()
    {
        var a = new FakeCombatant("A", speed: 1);
        var b = new FakeCombatant("B", speed: 2);
        var input = new List<DungeonOfChrome.Core.Entities.Combatant> { a, b };
        var rng = new ScriptedRandomSource(ints: new[] { 1, 1 }, doubles: new[] { 0.1, 0.1 });

        Initiative.RollInitiative(input, rng);

        Assert.Equal(new DungeonOfChrome.Core.Entities.Combatant[] { a, b }, input); // original order untouched
    }
}
