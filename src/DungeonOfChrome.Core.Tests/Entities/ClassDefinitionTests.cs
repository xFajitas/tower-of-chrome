using DungeonOfChrome.Core.Tests.TestUtil;

namespace DungeonOfChrome.Core.Tests.Entities;

public class ClassDefinitionTests
{
    [Fact]
    public void StatAtLevel_TruncatesGrowth_DoesNotCompound()
    {
        var knight = TestGameData.NewClassRegistry().Get("knight");
        // Verified from data/classes.json: hp base=130, growth=20.0 (exact multiple, no truncation edge case).
        Assert.Equal(130, knight.StatAtLevel("hp", 1));
        Assert.Equal(150, knight.StatAtLevel("hp", 2));
        Assert.Equal(130 + 20 * 19, knight.StatAtLevel("hp", 20));
    }

    [Fact]
    public void StatAtLevel_TruncatesFractionalGrowth()
    {
        var knight = TestGameData.NewClassRegistry().Get("knight");
        // dex growth = 0.5 -> at level 3 (2 level-ups): int(0.5 * 2) = 1, not rounded to 1 via banker's rounding.
        Assert.Equal(8 + 1, knight.StatAtLevel("dex", 3));
        // At level 2 (1 level-up): int(0.5 * 1) = 0 (truncated toward zero, not rounded up).
        Assert.Equal(8, knight.StatAtLevel("dex", 2));
    }

    [Fact]
    public void AllStatsAtLevel_ReturnsAllEightKeys()
    {
        var mage = TestGameData.NewClassRegistry().Get("mage");
        var stats = mage.AllStatsAtLevel(5);
        Assert.Equal(8, stats.Count);
        Assert.Contains("hp", stats.Keys);
        Assert.Contains("luck", stats.Keys);
    }

    [Fact]
    public void Registry_UnknownId_Throws()
    {
        var reg = TestGameData.NewClassRegistry();
        Assert.Throws<KeyNotFoundException>(() => reg.Get("not_a_class"));
    }

    [Fact]
    public void Registry_ByRole_FiltersCorrectly()
    {
        var reg = TestGameData.NewClassRegistry();
        var tanks = reg.ByRole("tank");
        Assert.Contains(tanks, c => c.Id == "knight");
        Assert.Contains(tanks, c => c.Id == "guardian");
        Assert.DoesNotContain(tanks, c => c.Id == "mage");
    }
}
