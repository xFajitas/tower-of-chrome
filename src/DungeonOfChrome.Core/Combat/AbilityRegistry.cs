using System.Collections.Immutable;
using DungeonOfChrome.Core.Data;

namespace DungeonOfChrome.Core.Combat;

/// <summary>Port of combat/abilities.py's AbilityRegistry. Constructor-injected, not a
/// Python-style global singleton, so tests get a fresh instance from fixture data.</summary>
public sealed class AbilityRegistry
{
    private readonly Dictionary<string, AbilityDefinition> _abilities = new();

    public AbilityRegistry(IGameDataSource dataSource)
    {
        foreach (var (id, raw) in dataSource.LoadAbilities().Abilities)
        {
            _abilities[id] = new AbilityDefinition(
                Id: id,
                Name: raw.Name,
                Description: raw.Description,
                Type: raw.Type,
                Targeting: raw.Targeting,
                MpCost: raw.MpCost,
                Cooldown: raw.Cooldown,
                Power: raw.Power,
                StatusEffects: raw.StatusEffects.ToImmutableArray(),
                StatusChance: raw.StatusChance,
                Flags: raw.Flags);
        }
    }

    public AbilityDefinition Get(string abilityId) =>
        _abilities.TryGetValue(abilityId, out var ab) ? ab : throw new KeyNotFoundException($"Unknown ability: '{abilityId}'");

    public IReadOnlyCollection<AbilityDefinition> All() => _abilities.Values;
}
