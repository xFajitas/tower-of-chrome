using DungeonOfChrome.Core.Entities;
using DungeonOfChrome.Core.Tests.TestUtil;

namespace DungeonOfChrome.Core.Tests.Entities;

/// <summary>Exercises Combatant's shared behavior via Character, its concrete subclass.</summary>
public class CombatantTests
{
    private static Character NewKnight()
    {
        var classes = TestGameData.NewClassRegistry();
        var items = TestGameData.NewItemRegistry();
        var leveling = TestGameData.NewLeveling();
        return new Character("Test Knight", classes.Get("knight"), items, leveling);
    }

    [Fact]
    public void NewCharacter_StartsAtFullHpAndMp_AndAlive()
    {
        var c = NewKnight();
        Assert.Equal(c.MaxHp, c.CurrentHp);
        Assert.Equal(c.MaxMp, c.CurrentMp);
        Assert.True(c.IsAlive);
        Assert.False(c.IsKo);
        Assert.Equal(1.0, c.HpFraction);
    }

    [Fact]
    public void TakeDamage_ClampsToZero_ReturnsActualLost()
    {
        var c = NewKnight();
        var actualLost = c.TakeDamage(c.MaxHp + 500);
        Assert.Equal(c.MaxHp, actualLost); // can't lose more HP than it had
        Assert.Equal(0, c.CurrentHp);
        Assert.True(c.IsKo);
    }

    [Fact]
    public void Heal_ClampsToMax_ReturnsActualRestored()
    {
        var c = NewKnight();
        c.TakeDamage(20);
        var healed = c.Heal(1000);
        Assert.Equal(20, healed); // only restored what was missing
        Assert.Equal(c.MaxHp, c.CurrentHp);
    }

    [Fact]
    public void SpendMp_FailsSilently_WhenInsufficient()
    {
        var c = NewKnight();
        var startingMp = c.CurrentMp;
        var ok = c.SpendMp(startingMp + 1000);
        Assert.False(ok);
        Assert.Equal(startingMp, c.CurrentMp); // unchanged on failure
    }

    [Fact]
    public void SpendMp_Succeeds_WhenSufficient()
    {
        var c = NewKnight();
        var startingMp = c.CurrentMp;
        Assert.True(startingMp >= 5);
        var ok = c.SpendMp(5);
        Assert.True(ok);
        Assert.Equal(startingMp - 5, c.CurrentMp);
    }

    [Fact]
    public void StatusEffects_AddIsIdempotent_RemoveIsExact()
    {
        var c = NewKnight();
        c.AddStatus("poison");
        c.AddStatus("poison"); // no duplicate
        Assert.Single(c.StatusEffects);
        Assert.True(c.HasStatus("poison"));

        c.RemoveStatus("poison");
        Assert.False(c.HasStatus("poison"));
    }

    [Fact]
    public void ClearDebuffs_RemovesOnlyDebuffSet_ReturnsCount()
    {
        var c = NewKnight();
        c.AddStatus("poison");
        c.AddStatus("stun");
        c.AddStatus("blessed"); // a buff, should survive

        var removed = c.ClearDebuffs();

        Assert.Equal(2, removed);
        Assert.False(c.HasStatus("poison"));
        Assert.False(c.HasStatus("stun"));
        Assert.True(c.HasStatus("blessed"));
    }

    [Fact]
    public void ClearBuffs_RemovesOnlyBuffSet_ReturnsCount()
    {
        var c = NewKnight();
        c.AddStatus("guarding");
        c.AddStatus("berserk");
        c.AddStatus("cursed"); // a debuff, should survive

        var removed = c.ClearBuffs();

        Assert.Equal(2, removed);
        Assert.False(c.HasStatus("guarding"));
        Assert.False(c.HasStatus("berserk"));
        Assert.True(c.HasStatus("cursed"));
    }

    [Fact]
    public void DerivedStatShorthands_DelegateToGetStat()
    {
        var c = NewKnight();
        Assert.Equal(c.GetStat("spd"), c.Speed);
        Assert.Equal(c.GetStat("str"), c.Strength);
        Assert.Equal(c.GetStat("int"), c.Intelligence);
        Assert.Equal(c.GetStat("vit"), c.Vitality);
        Assert.Equal(c.GetStat("dex"), c.Dexterity);
        Assert.Equal(c.GetStat("luck"), c.Luck);
    }
}
