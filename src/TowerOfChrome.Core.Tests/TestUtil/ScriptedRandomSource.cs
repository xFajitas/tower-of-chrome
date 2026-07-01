using TowerOfChrome.Core.Rng;

namespace TowerOfChrome.Core.Tests.TestUtil;

/// <summary>
/// A fully deterministic IRandomSource that returns pre-scripted values in order, so tests
/// can assert exact formula outputs (e.g. "was this roll a crit") without depending on any
/// particular RNG algorithm's actual output stream.
/// </summary>
public sealed class ScriptedRandomSource : IRandomSource
{
    private readonly Queue<double> _doubles;
    private readonly Queue<int> _ints;

    public ScriptedRandomSource(IEnumerable<double>? doubles = null, IEnumerable<int>? ints = null)
    {
        _doubles = new Queue<double>(doubles ?? Array.Empty<double>());
        _ints = new Queue<int>(ints ?? Array.Empty<int>());
    }

    public int NextInt(int minInclusive, int maxExclusive) =>
        _ints.Count > 0 ? _ints.Dequeue() : throw new InvalidOperationException("ScriptedRandomSource: no more scripted ints.");

    public double NextDouble() =>
        _doubles.Count > 0 ? _doubles.Dequeue() : throw new InvalidOperationException("ScriptedRandomSource: no more scripted doubles.");

    public double NextDouble(double min, double max) => min + NextDouble() * (max - min);

    public void Shuffle<T>(IList<T> list) { /* no-op: tests using this stub don't care about shuffle order */ }

    public List<T> Sample<T>(IReadOnlyList<T> source, int count) => source.Take(count).ToList();
}
