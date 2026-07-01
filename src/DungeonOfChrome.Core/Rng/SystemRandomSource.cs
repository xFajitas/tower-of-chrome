namespace DungeonOfChrome.Core.Rng;

/// <summary>Default IRandomSource backed by System.Random. Seedable for deterministic tests.</summary>
public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _rng;

    public SystemRandomSource(int? seed = null) => _rng = seed.HasValue ? new Random(seed.Value) : new Random();

    public int NextInt(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

    public double NextDouble() => _rng.NextDouble();

    public double NextDouble(double min, double max) => min + _rng.NextDouble() * (max - min);

    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = NextInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public List<T> Sample<T>(IReadOnlyList<T> source, int count)
    {
        if (count < 0 || count > source.Count)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be between 0 and source.Count.");

        var copy = new List<T>(source);
        // Partial Fisher-Yates: only need the first `count` positions to be a uniform
        // random sample without replacement, so stop shuffling once we have enough.
        int n = copy.Count;
        for (int i = 0; i < count; i++)
        {
            int j = NextInt(i, n);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy.GetRange(0, count);
    }
}
