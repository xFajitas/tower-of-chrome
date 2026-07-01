using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Tests.TestUtil;

namespace TowerOfChrome.Core.Tests.Entities;

public class CharacterTests
{
    private static (Character character, TowerOfChrome.Core.Loot.ItemRegistry items) NewKnight(int level = 1, int currentXp = 0)
    {
        var classes = TestGameData.NewClassRegistry();
        var items = TestGameData.NewItemRegistry();
        var leveling = TestGameData.NewLeveling();
        var c = new Character("Test Knight", classes.Get("knight"), items, leveling, level: level, currentXp: currentXp);
        return (c, items);
    }

    [Fact]
    public void Level_ClampsToMaxLevelAtConstruction()
    {
        var classes = TestGameData.NewClassRegistry();
        var items = TestGameData.NewItemRegistry();
        var leveling = TestGameData.NewLeveling();
        var c = new Character("Overleveled", classes.Get("knight"), items, leveling, level: 999);
        Assert.Equal(leveling.MaxLevel, c.Level);
    }

    [Fact]
    public void Level_ClampsToMinimumOne()
    {
        var classes = TestGameData.NewClassRegistry();
        var items = TestGameData.NewItemRegistry();
        var leveling = TestGameData.NewLeveling();
        var c = new Character("Underleveled", classes.Get("knight"), items, leveling, level: 0);
        Assert.Equal(1, c.Level);
    }

    [Fact]
    public void CombatantId_IsStable_NotTiedToInstanceIdentity()
    {
        var (c1, _) = NewKnight();
        Assert.Equal("char_Test Knight", c1.CombatantId());
    }

    [Fact]
    public void EquippingItem_IncreasesStatViaBonus()
    {
        var (c, _) = NewKnight();
        var strBefore = c.Strength;

        c.Inventory.Add("iron_sword");
        c.Inventory.Equip("iron_sword");

        // iron_sword grants +4 str, +1 dex (verified from data/items.json).
        Assert.Equal(strBefore + 4, c.Strength);
    }

    [Fact]
    public void GainXp_SingleLevelUp_ReturnsTrue_AndIncrementsLevel()
    {
        var (c, _) = NewKnight();
        var leveling = TestGameData.NewLeveling();
        var xpForLevel1 = leveling.XpToNext(1);

        var leveled = c.GainXp(xpForLevel1);

        Assert.True(leveled);
        Assert.Equal(2, c.Level);
        Assert.Equal(0, c.CurrentXp); // consumed exactly
    }

    [Fact]
    public void GainXp_BelowThreshold_ReturnsFalse_NoLevelChange()
    {
        var (c, _) = NewKnight();
        var leveled = c.GainXp(1);
        Assert.False(leveled);
        Assert.Equal(1, c.Level);
        Assert.Equal(1, c.CurrentXp);
    }

    [Fact]
    public void GainXp_MassiveOverflow_LoopsThroughMultipleLevelUps()
    {
        var (c, _) = NewKnight();
        var leveling = TestGameData.NewLeveling();

        var leveled = c.GainXp(1_000_000);

        Assert.True(leveled);
        Assert.Equal(leveling.MaxLevel, c.Level); // capped at max level
    }

    [Fact]
    public void GainXp_AtMaxLevel_ReturnsFalse_DoesNotAccumulateXp()
    {
        var classes = TestGameData.NewClassRegistry();
        var items = TestGameData.NewItemRegistry();
        var leveling = TestGameData.NewLeveling();
        var c = new Character("MaxLevel", classes.Get("knight"), items, leveling, level: leveling.MaxLevel);

        var leveled = c.GainXp(999999);

        Assert.False(leveled);
        Assert.Equal(0, c.CurrentXp);
    }

    [Fact]
    public void XpToNextLevel_IsZero_AtMaxLevel()
    {
        var classes = TestGameData.NewClassRegistry();
        var items = TestGameData.NewItemRegistry();
        var leveling = TestGameData.NewLeveling();
        var c = new Character("MaxLevel", classes.Get("knight"), items, leveling, level: leveling.MaxLevel);
        Assert.Equal(0, c.XpToNextLevel);
        Assert.Equal(1.0, c.XpProgressFraction);
    }

    [Fact]
    public void LevelUp_PreservesHpMpDelta_NotFullHeal()
    {
        var (c, _) = NewKnight();
        c.TakeDamage(10); // knight now missing 10 HP
        var leveling = TestGameData.NewLeveling();

        c.GainXp(leveling.XpToNext(1));

        // HP should have increased by exactly the max_hp delta from leveling, not been fully healed.
        Assert.Equal(c.MaxHp - 10, c.CurrentHp);
    }

    [Fact]
    public void OnLevelUp_Fires_WithLevelInMessage()
    {
        var (c, _) = NewKnight();
        string? message = null;
        c.OnLevelUp = msg => message = msg;
        var leveling = TestGameData.NewLeveling();

        c.GainXp(leveling.XpToNext(1));

        Assert.NotNull(message);
        Assert.Contains("level 2", message);
    }
}
