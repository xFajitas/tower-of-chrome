using System.Collections.Immutable;
using DungeonOfChrome.Core.Data;

namespace DungeonOfChrome.Core.Entities;

/// <summary>Port of entities/class_definition.py's ClassRegistry. Constructor-injected, not a
/// Python-style global singleton, so tests get a fresh instance from fixture data.</summary>
public sealed class ClassRegistry
{
    private readonly Dictionary<string, ClassDefinition> _classes = new();

    public ClassRegistry(IGameDataSource dataSource)
    {
        foreach (var raw in dataSource.LoadClasses().Classes)
        {
            _classes[raw.Id] = new ClassDefinition(
                Id: raw.Id,
                Name: raw.Name,
                Description: raw.Description,
                Role: raw.Role,
                Resource: raw.Resource,
                ResourceName: raw.ResourceName,
                BaseStats: raw.BaseStats,
                StatGrowth: raw.StatGrowth,
                WeaponTypes: raw.WeaponTypes.ToImmutableArray(),
                ArmorTypes: raw.ArmorTypes.ToImmutableArray(),
                EquipmentSlots: raw.EquipmentSlots.ToImmutableArray(),
                Abilities: raw.Abilities.ToImmutableArray(),
                StartingWeapon: raw.StartingWeapon);
        }
    }

    public ClassDefinition Get(string classId) =>
        _classes.TryGetValue(classId, out var def)
            ? def
            : throw new KeyNotFoundException($"Unknown class id: '{classId}'. Available: {string.Join(", ", _classes.Keys)}");

    public IReadOnlyCollection<ClassDefinition> All() => _classes.Values;

    public IReadOnlyList<ClassDefinition> ByRole(string role) => _classes.Values.Where(c => c.Role == role).ToList();

    public IReadOnlyList<string> Ids() => _classes.Keys.ToList();
}
