namespace DungeonOfChrome.Core.Combat;

public enum BattlePhase
{
    Setup,
    Ongoing,
    Victory,
    Defeat,
    Fled,
}

public enum ActionType
{
    Attack,
    Ability,
    Defend,
    Flee,
    /// <summary>Never wired into resolution today — no in-battle item-use path exists.
    /// Kept for parity with Python's ActionType.ITEM.</summary>
    Item,
}

/// <summary>Redundant with AbilityDefinition.Targeting (a plain string) — kept for parity
/// with Python's TargetMode enum, which is likewise unused beyond its declaration.</summary>
public enum TargetMode
{
    SingleEnemy,
    AllEnemies,
    SingleAlly,
    AllAllies,
    Self,
}
