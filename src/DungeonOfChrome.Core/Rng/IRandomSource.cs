namespace DungeonOfChrome.Core.Rng;

/// <summary>
/// Every Core system needing randomness takes this via constructor injection — never touches
/// System.Random/UnityEngine.Random directly. Gives deterministic, seeded unit tests everywhere.
///
/// Note: exact seed-for-seed reproducibility with the Python version is NOT a goal (Python's
/// random.sample/shuffle use different algorithms than any hand-rolled C# equivalent, even
/// sharing a seed). The bar is internal determinism for C# unit tests and formula/behavior
/// parity — not cross-language identical output streams.
/// </summary>
public interface IRandomSource
{
    /// <summary>Inclusive minInclusive, exclusive maxExclusive — matches System.Random.Next(min,max).</summary>
    int NextInt(int minInclusive, int maxExclusive);

    /// <summary>Uniform double in [0.0, 1.0).</summary>
    double NextDouble();

    /// <summary>Uniform double in [min, max) — replaces Python's random.uniform.</summary>
    double NextDouble(double min, double max);

    /// <summary>In-place Fisher-Yates shuffle — replaces Python's random.shuffle.</summary>
    void Shuffle<T>(IList<T> list);

    /// <summary>Without-replacement sample of `count` elements — replaces Python's random.sample.</summary>
    List<T> Sample<T>(IReadOnlyList<T> source, int count);
}
