using DungeonOfChrome.Core.Entities;

namespace DungeonOfChrome.Core.Combat;

/// <summary>Tracks all cooldowns for a single combatant. Port of combat/cooldowns.py.</summary>
public sealed class CooldownTracker
{
    private readonly Dictionary<string, int> _cds = new(); // ability_id -> turns remaining

    public void Set(string abilityId, int turns)
    {
        if (turns > 0)
            _cds[abilityId] = turns;
    }

    public int Get(string abilityId) => _cds.GetValueOrDefault(abilityId, 0);

    public bool IsReady(string abilityId) => Get(abilityId) == 0;

    /// <summary>Call once per turn for this combatant (after they act).</summary>
    public void Tick()
    {
        var keys = _cds.Keys.ToList();
        foreach (var k in keys)
        {
            var v = Math.Max(0, _cds[k] - 1);
            if (v > 0)
                _cds[k] = v;
            else
                _cds.Remove(k);
        }
    }

    public void ResetAll() => _cds.Clear();
}

/// <summary>Maps combatant IDs to their CooldownTracker instances. Port of CooldownRegistry.</summary>
public sealed class CooldownRegistry
{
    private readonly Dictionary<string, CooldownTracker> _map = new();

    public CooldownTracker ForCombatant(Combatant combatant)
    {
        var cid = combatant.CombatantId();
        if (!_map.TryGetValue(cid, out var tracker))
        {
            tracker = new CooldownTracker();
            _map[cid] = tracker;
        }
        return tracker;
    }

    public void Tick(Combatant combatant) => ForCombatant(combatant).Tick();

    public bool IsReady(Combatant combatant, string abilityId) => ForCombatant(combatant).IsReady(abilityId);

    public void SetCooldown(Combatant combatant, string abilityId, int turns) => ForCombatant(combatant).Set(abilityId, turns);

    public int GetCooldown(Combatant combatant, string abilityId) => ForCombatant(combatant).Get(abilityId);
}
