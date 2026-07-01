using System.Text.Json.Serialization;

namespace TowerOfChrome.Core.Data.DataModels;

public sealed class CombatConfigData
{
    [JsonPropertyName("physical")] public PhysicalConfigData Physical { get; set; } = new();
    [JsonPropertyName("magical")] public MagicalConfigData Magical { get; set; } = new();
    [JsonPropertyName("healing")] public HealingConfigData Healing { get; set; } = new();
    [JsonPropertyName("status_effects")] public StatusEffectsConfigData StatusEffects { get; set; } = new();
    [JsonPropertyName("flee")] public FleeConfigData Flee { get; set; } = new();
    [JsonPropertyName("defend")] public DefendConfigData Defend { get; set; } = new();
}

public sealed class PhysicalConfigData
{
    [JsonPropertyName("base_multiplier")] public double BaseMultiplier { get; set; }
    [JsonPropertyName("defense_factor")] public double DefenseFactor { get; set; }
    [JsonPropertyName("variance")] public double Variance { get; set; }
    [JsonPropertyName("crit_base_chance")] public double CritBaseChance { get; set; }
    [JsonPropertyName("crit_multiplier")] public double CritMultiplier { get; set; }
    [JsonPropertyName("luck_crit_factor")] public double LuckCritFactor { get; set; }
}

public sealed class MagicalConfigData
{
    [JsonPropertyName("base_multiplier")] public double BaseMultiplier { get; set; }
    [JsonPropertyName("resistance_factor")] public double ResistanceFactor { get; set; }
    [JsonPropertyName("variance")] public double Variance { get; set; }
    /// <summary>Intentionally absent from combat_config.json today (verified) — magical crit
    /// chance falls back to Physical.CritBaseChance combined with THIS class's LuckCritFactor.
    /// Null, not defaulted to 0, so callers can tell "absent" from "explicitly zero".</summary>
    [JsonPropertyName("crit_base_chance")] public double? CritBaseChance { get; set; }
    [JsonPropertyName("luck_crit_factor")] public double LuckCritFactor { get; set; }
}

public sealed class HealingConfigData
{
    [JsonPropertyName("base_multiplier")] public double BaseMultiplier { get; set; }
    [JsonPropertyName("variance")] public double Variance { get; set; }
}

public sealed class StatusEffectsConfigData
{
    [JsonPropertyName("poison_dot_percent")] public double PoisonDotPercent { get; set; }
    [JsonPropertyName("burn_flat_damage")] public double BurnFlatDamage { get; set; }
    [JsonPropertyName("regen_heal_percent")] public double RegenHealPercent { get; set; }
    [JsonPropertyName("weakened_def_factor")] public double WeakenedDefFactor { get; set; }
    [JsonPropertyName("blessed_atk_factor")] public double BlessedAtkFactor { get; set; }
    [JsonPropertyName("haste_speed_bonus")] public double HasteSpeedBonus { get; set; } // unused, kept for parity
    [JsonPropertyName("berserk_atk_factor")] public double BerserkAtkFactor { get; set; }
    [JsonPropertyName("berserk_def_factor")] public double BerserkDefFactor { get; set; }
    [JsonPropertyName("guarding_dmg_factor")] public double GuardingDmgFactor { get; set; }
    [JsonPropertyName("thorns_reflect_pct")] public double ThornsReflectPct { get; set; } // unused, kept for parity
}

public sealed class FleeConfigData
{
    [JsonPropertyName("base_chance")] public double BaseChance { get; set; }
    [JsonPropertyName("speed_factor")] public double SpeedFactor { get; set; }
}

public sealed class DefendConfigData
{
    [JsonPropertyName("damage_factor")] public double DamageFactor { get; set; } // unused, kept for parity
}
