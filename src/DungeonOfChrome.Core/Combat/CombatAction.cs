using DungeonOfChrome.Core.Entities;

namespace DungeonOfChrome.Core.Combat;

/// <summary>Input to the battle engine. Mutable (Targets can be defaulted in place during
/// ability resolution, e.g. cleanse_debuffs), mirroring Python's mutable dataclass.</summary>
public sealed class CombatAction
{
    public Combatant Actor { get; }
    public ActionType ActionType { get; }
    public string? AbilityId { get; }
    public List<Combatant> Targets { get; set; }

    public CombatAction(Combatant actor, ActionType actionType, string? abilityId = null, List<Combatant>? targets = null)
    {
        Actor = actor;
        ActionType = actionType;
        AbilityId = abilityId;
        Targets = targets ?? new List<Combatant>();
    }
}

public sealed class HitResult
{
    public Combatant Target { get; }
    public int Damage { get; set; }
    public int Healing { get; set; }
    public int MpDrained { get; set; }
    public List<string> Statuses { get; } = new();
    public bool WasCrit { get; set; }

    /// <summary>Always false — no evasion/miss mechanic is implemented. Kept for parity with
    /// Python's HitResult.was_miss, which is likewise always false.</summary>
    public bool WasMiss { get; set; }

    /// <summary>HP restored to the actor via life drain.</summary>
    public int DrainHeal { get; set; }

    public HitResult(Combatant target) => Target = target;
}

public sealed class ActionResult
{
    public CombatAction Action { get; }
    public List<HitResult> Hits { get; } = new();
    public List<string> LogLines { get; } = new();
    public int MpSpent { get; set; }

    /// <summary>e.g. reckless_strike recoil.</summary>
    public int ActorSelfDmg { get; set; }

    /// <summary>False = couldn't afford MP / invalid.</summary>
    public bool Success { get; set; } = true;

    public ActionResult(CombatAction action) => Action = action;
}
