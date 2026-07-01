using TowerOfChrome.Core.Rng;
using TowerOfChrome.Core.Tests.TestUtil;

namespace TowerOfChrome.Core.Tests.Entities;

public class EnemyTests
{
    [Fact]
    public void Spawn_FreezesStatsAtCreationTime_ScaledByFloor()
    {
        var reg = TestGameData.NewEnemyRegistry();
        var def = reg.Get("neon_grunt");

        // Verified from data/enemies.json: base_stats.hp=55, stat_growth.hp=8.0.
        var atFloor1 = reg.Spawn("neon_grunt", floor: 1);
        var atFloor4 = reg.Spawn("neon_grunt", floor: 4);

        Assert.Equal(55, atFloor1.MaxHp);
        Assert.Equal(55 + (int)(8.0 * 3), atFloor4.MaxHp); // (floor-1) multiplier
    }

    [Fact]
    public void Spawn_HpAndMp_StartAtFullFrozenStats()
    {
        var reg = TestGameData.NewEnemyRegistry();
        var e = reg.Spawn("neon_grunt", floor: 1);
        Assert.Equal(e.MaxHp, e.CurrentHp);
        Assert.Equal(e.MaxMp, e.CurrentMp);
    }

    [Fact]
    public void CombatantId_IsUniquePerInstance_EvenForSameDefinition()
    {
        var reg = TestGameData.NewEnemyRegistry();
        var e1 = reg.Spawn("neon_grunt", floor: 1);
        var e2 = reg.Spawn("neon_grunt", floor: 1);

        Assert.NotEqual(e1.CombatantId(), e2.CombatantId());
        Assert.StartsWith("enemy_neon_grunt_", e1.CombatantId());
    }

    [Fact]
    public void RandomEncounter_Floor10Plus_IsBossSolo()
    {
        var reg = TestGameData.NewEnemyRegistry();
        var rng = new SystemRandomSource(seed: 1);
        var encounter = reg.RandomEncounter(10, 3, rng);
        Assert.Single(encounter);
        Assert.Equal("circuit_mage", encounter[0].EnemyDef.Id);
    }

    [Fact]
    public void RandomEncounter_Floor5_IsMiniBossSolo()
    {
        var reg = TestGameData.NewEnemyRegistry();
        var rng = new SystemRandomSource(seed: 1);
        var encounter = reg.RandomEncounter(5, 3, rng);
        Assert.Single(encounter);
        Assert.Equal("nexus_core", encounter[0].EnemyDef.Id);
    }

    [Fact]
    public void RandomEncounter_EarlyFloor_OnlyDrawsFromCommons()
    {
        var reg = TestGameData.NewEnemyRegistry();
        var rng = new SystemRandomSource(seed: 7);
        var commons = new HashSet<string> { "neon_grunt", "chrome_hound", "glitch_witch", "wire_wraith", "nanobot_swarm", "pulse_drone" };

        for (var trial = 0; trial < 20; trial++)
        {
            var encounter = reg.RandomEncounter(1, 3, rng);
            Assert.All(encounter, e => Assert.Contains(e.EnemyDef.Id, commons));
        }
    }

    [Fact]
    public void RandomEncounter_CountIsCappedAtFour()
    {
        var reg = TestGameData.NewEnemyRegistry();
        var rng = new SystemRandomSource(seed: 3);
        var encounter = reg.RandomEncounter(1, 999, rng);
        Assert.Equal(4, encounter.Count);
    }
}
