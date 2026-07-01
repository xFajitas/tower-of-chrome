using System.Collections.Immutable;

namespace DungeonOfChrome.Core.Combat;

/// <summary>Immutable ability definition loaded from abilities.json. Port of combat/abilities.py.</summary>
public sealed record AbilityDefinition(
    string Id,
    string Name,
    string Description,
    string Type, // physical | magical | physical_magical | healing | buff | debuff | utility
    string Targeting, // SINGLE_ENEMY | ALL_ENEMIES | SINGLE_ALLY | ALL_ALLIES | SELF
    int MpCost,
    int Cooldown,
    double Power,
    ImmutableArray<string> StatusEffects,
    int StatusChance,
    IReadOnlyDictionary<string, double> Flags)
{
    public bool IsOffensive => Type is "physical" or "magical" or "physical_magical";
    public bool IsHeal => Type == "healing";
    public bool TargetsAllies => Targeting is "SINGLE_ALLY" or "ALL_ALLIES" or "SELF";
    public bool TargetsEnemies => Targeting is "SINGLE_ENEMY" or "ALL_ENEMIES";
    public bool IsAoe => Targeting is "ALL_ENEMIES" or "ALL_ALLIES";

    private static readonly IReadOnlyDictionary<string, string> TypeTags = new Dictionary<string, string>
    {
        ["physical"] = "Physical",
        ["magical"] = "Magical",
        ["physical_magical"] = "Phys+Magic",
        ["healing"] = "Heal",
        ["buff"] = "Buff",
        ["debuff"] = "Debuff",
        ["utility"] = "Utility",
    };

    private static readonly IReadOnlyDictionary<string, string> TargetingTags = new Dictionary<string, string>
    {
        ["SINGLE_ENEMY"] = "Single Target",
        ["ALL_ENEMIES"] = "AOE",
        ["SINGLE_ALLY"] = "Single Ally",
        ["ALL_ALLIES"] = "AOE Ally",
        ["SELF"] = "Self",
    };

    /// <summary>Short descriptive labels for the ability UI, e.g. ("Debuff", "AOE").</summary>
    public IReadOnlyList<string> Tags
    {
        get
        {
            var tags = new List<string>();
            if (TypeTags.TryGetValue(Type, out var typeTag))
                tags.Add(typeTag);
            if (TargetingTags.TryGetValue(Targeting, out var targetingTag))
                tags.Add(targetingTag);
            return tags;
        }
    }
}
