using TowerOfChrome.Core.Tests.TestUtil;

namespace TowerOfChrome.Core.Tests.Entities;

public class LevelingTests
{
    [Fact]
    public void MaxLevel_MatchesSource()
    {
        Assert.Equal(20, TestGameData.NewLeveling().MaxLevel);
    }

    [Theory]
    [InlineData(1, 50)]    // floor(50 * 1^1.5) = 50
    [InlineData(2, 141)]   // floor(50 * 2^1.5) = floor(141.42) = 141
    [InlineData(5, 559)]   // floor(50 * 5^1.5) = floor(559.01...) = 559
    public void XpToNext_MatchesDocumentedExamples(int level, int expected)
    {
        Assert.Equal(expected, TestGameData.NewLeveling().XpToNext(level));
    }

    [Fact]
    public void XpToNext_IsCached_ReturnsSameValueOnRepeatedCalls()
    {
        var leveling = TestGameData.NewLeveling();
        var first = leveling.XpToNext(7);
        var second = leveling.XpToNext(7);
        Assert.Equal(first, second);
    }

    [Fact]
    public void TotalXpForLevel_IsCumulativeSum()
    {
        var leveling = TestGameData.NewLeveling();
        var expected = leveling.XpToNext(1) + leveling.XpToNext(2) + leveling.XpToNext(3);
        Assert.Equal(expected, leveling.TotalXpForLevel(4));
    }
}
