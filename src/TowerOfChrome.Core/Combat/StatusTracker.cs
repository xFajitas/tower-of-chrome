using TowerOfChrome.Core.Entities;

namespace TowerOfChrome.Core.Combat;

/// <summary>
/// Tracks remaining duration for all active status effects on all combatants. Port of
/// combat/status_effects.py. Modifiers that affect combat formulas (weakened, blessed, etc.)
/// are read directly from the combatant's status list inside DamageCalculator; this class
/// handles the time-based updates: DoT ticks, regen, duration countdown, and expiry.
/// </summary>
public sealed class StatusTracker
{
    // Default durations (turns). 0 would mean permanent until cleansed (none currently use 0).
    private static readonly IReadOnlyDictionary<string, int> DefaultDurations = new Dictionary<string, int>
    {
        ["poison"] = 3,
        ["burn"] = 3,
        ["stun"] = 1,
        ["weakened"] = 3,
        ["cursed"] = 4,
        ["marked"] = 3,
        ["silenced"] = 2,
        ["blessed"] = 3,
        ["guarding"] = 1, // lasts only the turn it's applied
        ["haste"] = 2,
        ["regenerating"] = 3,
        ["berserk"] = 3,
        ["thorns"] = 2,
        ["shielded"] = 2,
        ["taunted"] = 2,
    };

    private const int FallbackDuration = 2; // Python: _DEFAULT_DURATIONS.get(effect, 2)

    // key: (combatant_id, effect_name) -> turns remaining
    private readonly Dictionary<(string CombatantId, string Effect), int> _durations = new();

    private readonly DamageCalculator _damage;

    public StatusTracker(DamageCalculator damage) => _damage = damage;

    /// <summary>Apply `effect` to `combatant`. Returns a log string. If `turns` is -1, use the
    /// default duration (or 2 if the effect has no configured default).</summary>
    public string Apply(Combatant combatant, string effect, int turns = -1)
    {
        var duration = turns >= 0 ? turns : DefaultDurations.GetValueOrDefault(effect, FallbackDuration);
        combatant.AddStatus(effect);
        _durations[(combatant.CombatantId(), effect)] = duration;
        return $"{combatant.Name} is now {effect}!";
    }

    /// <summary>
    /// Called at the END of a combatant's turn:
    ///   1. Apply DoT damage / regen healing.
    ///   2. Decrement durations.
    ///   3. Remove expired effects.
    /// Returns a list of log lines.
    /// </summary>
    public List<string> Tick(Combatant combatant)
    {
        var lines = new List<string>();
        var cid = combatant.CombatantId();

        var dot = _damage.CalcDot(combatant);
        if (dot > 0 && combatant.IsAlive)
        {
            var taken = combatant.TakeDamage(dot);
            var effects = new[] { "poison", "burn" }.Where(combatant.HasStatus);
            lines.Add($"{combatant.Name} suffers {taken} dmg from {string.Join("/", effects)}.");
        }

        var regen = _damage.CalcRegen(combatant);
        if (regen > 0 && combatant.IsAlive)
        {
            var healed = combatant.Heal(regen);
            if (healed > 0)
                lines.Add($"{combatant.Name} regenerates {healed} HP.");
        }

        var toRemove = new List<string>();
        foreach (var key in _durations.Keys.ToList())
        {
            if (key.CombatantId != cid)
                continue;
            var remaining = _durations[key];
            if (remaining <= 0)
                continue; // permanent / already gone
            var newRemaining = remaining - 1;
            _durations[key] = newRemaining;
            if (newRemaining == 0)
                toRemove.Add(key.Effect);
        }

        foreach (var effect in toRemove)
        {
            combatant.RemoveStatus(effect);
            _durations.Remove((cid, effect));
            lines.Add($"{combatant.Name}'s {effect} wore off.");
        }

        return lines;
    }

    /// <summary>Remove all tracked durations for this combatant (e.g. on KO).</summary>
    public void Clear(Combatant combatant)
    {
        var cid = combatant.CombatantId();
        foreach (var key in _durations.Keys.Where(k => k.CombatantId == cid).ToList())
            _durations.Remove(key);
        combatant.StatusEffects.Clear();
    }
}
