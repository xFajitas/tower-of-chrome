using TowerOfChrome.Core.Data;

namespace TowerOfChrome.Core.Tests.Data;

/// <summary>
/// Loads the real data/*.json files (copied verbatim into TestData/) and asserts exact
/// counts against the verified inventory, as a cheap regression guard that the JSON
/// schema translation didn't drop or mis-key anything.
/// </summary>
public class GameDataFixtureTests
{
    private static readonly string TestDataDir = System.IO.Path.Combine(AppContext.BaseDirectory, "TestData");

    private static FileSystemGameDataSource MakeSource() => new(TestDataDir);

    [Fact]
    public void Classes_LoadsExactly16()
    {
        var classes = MakeSource().LoadClasses().Classes;
        Assert.Equal(16, classes.Count);
        Assert.Equal(16, classes.Select(c => c.Id).Distinct().Count()); // all IDs unique
        Assert.Contains(classes, c => c.Id == "knight");
        Assert.Contains(classes, c => c.Id == "spellblade");
    }

    [Fact]
    public void Classes_KnightHasExpectedFields()
    {
        var knight = MakeSource().LoadClasses().Classes.Single(c => c.Id == "knight");
        Assert.Equal("Knight", knight.Name);
        Assert.Equal("tank", knight.Role);
        Assert.Equal(130, knight.BaseStats["hp"]);
        Assert.Equal(20.0, knight.StatGrowth["hp"]);
        Assert.Equal(new[] { "sword", "axe", "mace" }, knight.WeaponTypes);
        Assert.Equal(new[] { "slash", "shield_bash", "taunt", "guard_stance" }, knight.Abilities);
        Assert.Equal("iron_sword", knight.StartingWeapon);
    }

    [Fact]
    public void Items_LoadsExactly70()
    {
        var items = MakeSource().LoadItems().Items;
        Assert.Equal(70, items.Count);
        Assert.Equal(70, items.Select(i => i.Id).Distinct().Count());
    }

    [Fact]
    public void Items_ConsumableEffectParsesCorrectly()
    {
        var potion = MakeSource().LoadItems().Items.Single(i => i.Id == "health_potion_small");
        Assert.True(potion.Consumable);
        Assert.NotNull(potion.Effect);
        Assert.Equal("heal_hp", potion.Effect!.Type);
        Assert.Equal(50, potion.Effect.Value);
    }

    [Fact]
    public void Enemies_LoadsExactly12()
    {
        var enemies = MakeSource().LoadEnemies().Enemies;
        Assert.Equal(12, enemies.Count);
        Assert.Equal(12, enemies.Select(e => e.Id).Distinct().Count());
        Assert.Contains(enemies, e => e.Id == "circuit_mage" && e.Tier == "boss");
        Assert.Contains(enemies, e => e.Id == "nexus_core" && e.Tier == "mini_boss");
    }

    [Fact]
    public void Abilities_LoadsExactly89()
    {
        var abilities = MakeSource().LoadAbilities().Abilities;
        Assert.Equal(89, abilities.Count);
        Assert.True(abilities.ContainsKey("basic_attack"));
    }

    [Fact]
    public void Abilities_FlagsParseCorrectly()
    {
        var reckless = MakeSource().LoadAbilities().Abilities["reckless_strike"];
        Assert.Equal(0.10, reckless.Flags["self_damage_ratio"], precision: 5);
    }

    [Fact]
    public void LootTables_LoadsExactly4_AndSkipsUnderscoreKeys()
    {
        var tables = MakeSource().LoadLootTables();
        Assert.Equal(4, tables.Count);
        Assert.Equal(new[] { "boss", "common_floor1", "elite_floor1", "mini_boss" },
            tables.Keys.OrderBy(k => k));
        Assert.DoesNotContain("_description", tables.Keys);
    }

    [Fact]
    public void LootTables_BossTableMatchesSource()
    {
        var boss = MakeSource().LoadLootTables()["boss"];
        Assert.Equal(1.0, boss.DropChance);
        Assert.Equal(3, boss.Rolls);
        Assert.Equal(new[] { "EPIC", "LEGENDARY" }, boss.ItemRarities);
        Assert.Equal(new[] { 70, 30 }, boss.RarityWeights);
    }

    [Fact]
    public void CombatConfig_MagicalCritBaseChance_IsAbsent()
    {
        // Verified quirk: magical.json config block has no crit_base_chance key.
        // DamageCalculator must fall back to Physical.CritBaseChance + Magical.LuckCritFactor.
        var cfg = MakeSource().LoadCombatConfig();
        Assert.Null(cfg.Magical.CritBaseChance);
        Assert.True(cfg.Physical.CritBaseChance > 0);
    }

    [Fact]
    public void CombatConfig_KnownValuesMatchSource()
    {
        var cfg = MakeSource().LoadCombatConfig();
        Assert.Equal(1.8, cfg.Physical.BaseMultiplier);
        Assert.Equal(0.05, cfg.Physical.CritBaseChance);
        Assert.Equal(2.0, cfg.Magical.BaseMultiplier);
        Assert.Equal(0.40, cfg.Flee.BaseChance);
    }

    [Fact]
    public void Leveling_MatchesSource()
    {
        var leveling = MakeSource().LoadLeveling();
        Assert.Equal(20, leveling.MaxLevel);
        Assert.Equal(50, leveling.XpBase);
        Assert.Equal(1.5, leveling.XpExponent);
    }

    [Fact]
    public void Settings_MatchesSource()
    {
        var settings = MakeSource().LoadSettings();
        Assert.Equal(1280, settings.Window.Width);
        Assert.Equal(720, settings.Window.Height);
        Assert.Equal("Tower of Chrome", settings.Window.Title);
        Assert.Equal(60, settings.Fps);
    }
}
