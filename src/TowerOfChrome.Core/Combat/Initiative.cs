using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Rng;

namespace TowerOfChrome.Core.Combat;

/// <summary>
/// Initiative system. Each combatant's initiative = speed + d10 roll. Higher value goes first.
/// Ties broken by speed, then randomly. Port of combat/initiative.py.
/// </summary>
public static class Initiative
{
    /// <summary>Return a new list of living combatants sorted by initiative (descending).
    /// The input list is not modified.</summary>
    public static List<Combatant> RollInitiative(IReadOnlyList<Combatant> combatants, IRandomSource rng)
    {
        var living = combatants.Where(c => c.IsAlive).ToList();

        // Python's list.sort(key=...) computes each element's key exactly once (including the
        // random.random() tiebreak) before any comparisons happen — so the tiebreak must be
        // precomputed per element here too, never re-rolled inside the comparator, or repeated
        // comparisons during the sort would see different tiebreak values for the same element.
        var scored = living
            .Select(c => (Combatant: c, Score: c.Speed + rng.NextInt(1, 11), Tiebreak: rng.NextDouble()))
            .ToList();

        scored.Sort((a, b) =>
        {
            var cmp = b.Score.CompareTo(a.Score); // descending by score
            if (cmp != 0) return cmp;
            cmp = b.Combatant.Speed.CompareTo(a.Combatant.Speed); // descending by speed
            if (cmp != 0) return cmp;
            return b.Tiebreak.CompareTo(a.Tiebreak); // descending by precomputed random tiebreak
        });

        return scored.Select(x => x.Combatant).ToList();
    }
}
