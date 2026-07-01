using TowerOfChrome.Core.Rng;

namespace TowerOfChrome.Core.Tests.Rng;

public class SystemRandomSourceTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new SystemRandomSource(seed: 42);
        var b = new SystemRandomSource(seed: 42);

        for (int i = 0; i < 20; i++)
            Assert.Equal(a.NextDouble(), b.NextDouble());
    }

    [Fact]
    public void NextInt_RespectsBounds()
    {
        var rng = new SystemRandomSource(seed: 1);
        for (int i = 0; i < 1000; i++)
        {
            var v = rng.NextInt(1, 11); // Python randint(1,10) equivalent: inclusive 1..10
            Assert.InRange(v, 1, 10);
        }
    }

    [Fact]
    public void NextDoubleRange_RespectsBounds()
    {
        var rng = new SystemRandomSource(seed: 2);
        for (int i = 0; i < 1000; i++)
        {
            var v = rng.NextDouble(0.88, 1.12);
            Assert.InRange(v, 0.88, 1.12);
        }
    }

    [Fact]
    public void Shuffle_PreservesAllElements()
    {
        var rng = new SystemRandomSource(seed: 3);
        var list = Enumerable.Range(0, 10).ToList();
        rng.Shuffle(list);
        Assert.Equal(Enumerable.Range(0, 10).OrderBy(x => x), list.OrderBy(x => x));
    }

    [Fact]
    public void Sample_ReturnsRequestedCount_WithoutDuplicates_AllFromSource()
    {
        var rng = new SystemRandomSource(seed: 4);
        var source = Enumerable.Range(0, 12).ToList();
        var sample = rng.Sample(source, 5);

        Assert.Equal(5, sample.Count);
        Assert.Equal(5, sample.Distinct().Count());
        Assert.All(sample, x => Assert.Contains(x, source));
    }

    [Fact]
    public void Sample_FullCount_ReturnsAllElementsInSomeOrder()
    {
        var rng = new SystemRandomSource(seed: 5);
        var source = Enumerable.Range(0, 6).ToList();
        var sample = rng.Sample(source, 6);
        Assert.Equal(source.OrderBy(x => x), sample.OrderBy(x => x));
    }
}
