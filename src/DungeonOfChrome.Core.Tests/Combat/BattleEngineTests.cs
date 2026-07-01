using System.Collections.Immutable;
using DungeonOfChrome.Core.Combat;
using DungeonOfChrome.Core.Entities;
using DungeonOfChrome.Core.Rng;
using DungeonOfChrome.Core.Tests.TestUtil;

namespace DungeonOfChrome.Core.Tests.Combat;

public class BattleEngineTests
{
    private static BattleEngine NewBattleEngine(IRandomSource rng)
    {
        var abilities = TestGameData.NewAbilityRegistry();
        var damage = new DamageCalculator(TestGameData.NewCombatConfig(), rng);
        var lootTables = TestGameData.NewLootTables(rng);
        return new BattleEngine(abilities, damage, rng, lootTables);
    }

    private static Party NewFullParty() =>
        Party.DefaultParty(TestGameData.NewClassRegistry(), TestGameData.NewItemRegistry(), TestGameData.NewLeveling());

    private static Character NewCharacter(string name, string classId) =>
        new(name, TestGameData.NewClassRegistry().Get(classId), TestGameData.NewItemRegistry(), TestGameData.NewLeveling());

    /// <summary>A custom 1-HP enemy definition so a single attack guarantees a kill,
    /// isolating victory/XP logic from combat-formula randomness.</summary>
    private static Enemy NewOneHitEnemy(int xpReward = 90) =>
        new(new EnemyDefinition(
            "test_dummy", "Test Dummy", "", "common",
            new Dictionary<string, int> { ["hp"] = 1, ["mp"] = 0, ["str"] = 0, ["dex"] = 0, ["int"] = 0, ["vit"] = 0, ["spd"] = 0, ["luck"] = 0 },
            new Dictionary<string, double>(),
            ImmutableArray.Create("basic_attack"), "aggressive", xpReward, "common_floor1"), floor: 1);

    [Fact]
    public void Start_InitializesOngoingPhase_Round1_AndLogsInitiative()
    {
        var rng = new SystemRandomSource(seed: 1);
        var party = NewFullParty();
        var enemy = TestGameData.NewEnemyRegistry().Spawn("neon_grunt", floor: 1);
        var battle = NewBattleEngine(rng);

        battle.Start(party, new List<Enemy> { enemy });

        Assert.Equal(BattlePhase.Ongoing, battle.Phase);
        Assert.Equal(1, battle.Round);
        Assert.NotNull(battle.CurrentActor);
        Assert.Contains(battle.Log, l => l.Contains("Round 1"));
        Assert.Contains(battle.Log, l => l.StartsWith("Initiative:"));
    }

    [Fact]
    public void Round_Increments_AfterAllCombatantsHaveActedOnce()
    {
        // DEFEND never deals damage, so nobody dies during this loop — guarantees every
        // combatant gets exactly one turn before the round rolls over.
        var rng = new SystemRandomSource(seed: 42);
        var party = NewFullParty();
        var enemy = TestGameData.NewEnemyRegistry().Spawn("neon_grunt", floor: 1);
        var battle = NewBattleEngine(rng);
        battle.Start(party, new List<Enemy> { enemy });

        var totalCombatants = party.AllMembers.Count + 1;
        for (var i = 0; i < totalCombatants; i++)
        {
            var actor = battle.CurrentActor!;
            if (battle.IsPlayerTurn)
                battle.SubmitPlayerAction(new CombatAction(actor, ActionType.Defend));
            else
                battle.AdvanceEnemyTurn();
        }

        Assert.Equal(2, battle.Round);
        Assert.Contains(battle.Log, l => l.Contains("Round 2"));
    }

    [Fact]
    public void Victory_AwardsXpToLivingPartyOnly_SplitEvenly_RemainderDropped()
    {
        var rng = new SystemRandomSource(seed: 5);
        var party = NewFullParty();
        party.AllMembers[3].TakeDamage(9999); // KO the 4th member before battle starts
        var enemy = NewOneHitEnemy(xpReward: 91); // 91 / 3 = 30 remainder 1 (dropped)
        var battle = NewBattleEngine(rng);
        battle.Start(party, new List<Enemy> { enemy });

        for (var i = 0; i < 20 && battle.Phase == BattlePhase.Ongoing; i++)
        {
            var actor = battle.CurrentActor!;
            if (battle.IsPlayerTurn)
                battle.SubmitPlayerAction(new CombatAction(actor, ActionType.Attack, targets: new List<Combatant> { battle.LivingEnemies[0] }));
            else
                battle.AdvanceEnemyTurn();
        }

        Assert.Equal(BattlePhase.Victory, battle.Phase);
        Assert.Equal(3, battle.XpAwards.Count);
        Assert.DoesNotContain(battle.XpAwards, a => a.Name == party.AllMembers[3]!.Name);
        Assert.All(battle.XpAwards, a => Assert.Equal(30, a.Xp)); // max(1, 91/3) = 30, remainder dropped
    }

    [Fact]
    public void Defeat_WhenSolePartyMemberIsKoed()
    {
        var rng = new SystemRandomSource(seed: 3);
        var character = NewCharacter("Fragile", "knight");
        character.CurrentHp = 1;
        var party = new Party();
        party.AddMember(character);
        var enemy = TestGameData.NewEnemyRegistry().Spawn("neon_grunt", floor: 1);
        var battle = NewBattleEngine(rng);
        battle.Start(party, new List<Enemy> { enemy });

        for (var i = 0; i < 10 && battle.Phase == BattlePhase.Ongoing; i++)
        {
            if (battle.IsPlayerTurn)
                battle.SubmitPlayerAction(new CombatAction(battle.CurrentActor!, ActionType.Defend));
            else
                battle.AdvanceEnemyTurn();
        }

        Assert.Equal(BattlePhase.Defeat, battle.Phase);
        Assert.Contains(battle.Log, l => l.Contains("Defeat!"));
    }

    [Fact]
    public void Flee_LowRoll_SucceedsAndSetsFledPhase()
    {
        // 2 combatants -> initiative consumes 2 ints (d10) + 2 doubles (tiebreak); the 3rd
        // scripted double is the flee roll itself (0.0 guarantees success, chance is always >= 0.05).
        var rng = new ScriptedRandomSource(doubles: new[] { 0.5, 0.5, 0.0 }, ints: new[] { 1, 1 });
        var character = NewCharacter("Runner", "knight");
        var party = new Party();
        party.AddMember(character);
        var enemy = TestGameData.NewEnemyRegistry().Spawn("neon_grunt", floor: 1);
        var battle = NewBattleEngine(rng);
        battle.Start(party, new List<Enemy> { enemy });

        var result = battle.SubmitPlayerAction(new CombatAction(character, ActionType.Flee));

        Assert.Equal(BattlePhase.Fled, battle.Phase);
        Assert.Contains("escapes", result.LogLines[0]);
    }

    [Fact]
    public void AdvanceEnemyTurn_StunnedEnemy_LosesTurn_AndStunIsRemoved()
    {
        var rng = new SystemRandomSource(seed: 7);
        var party = NewFullParty();
        var enemy = TestGameData.NewEnemyRegistry().Spawn("neon_grunt", floor: 1);
        var battle = NewBattleEngine(rng);
        battle.Start(party, new List<Enemy> { enemy });

        // Defend through consecutive player turns until it's the (sole) enemy's turn.
        for (var i = 0; i < 20 && battle.IsPlayerTurn; i++)
            battle.SubmitPlayerAction(new CombatAction(battle.CurrentActor!, ActionType.Defend));

        Assert.False(battle.IsPlayerTurn);
        enemy.AddStatus("stun");

        var result = battle.AdvanceEnemyTurn();

        Assert.False(enemy.HasStatus("stun"));
        Assert.Contains(result.LogLines, l => l.Contains("stunned"));
    }

    [Fact]
    public void CanUseAbility_False_WhenInsufficientMp()
    {
        var battle = NewBattleEngine(new SystemRandomSource(seed: 1));
        var mage = NewCharacter("Mage", "mage");
        mage.CurrentMp = 0;

        Assert.False(battle.CanUseAbility(mage, "fireball"));
    }

    [Fact]
    public void CanUseAbility_False_WhenOnCooldown()
    {
        var battle = NewBattleEngine(new SystemRandomSource(seed: 1));
        var mage = NewCharacter("Mage", "mage");
        battle.Cooldowns.SetCooldown(mage, "fireball", 3);

        Assert.False(battle.CanUseAbility(mage, "fireball"));
    }

    [Fact]
    public void SubmitPlayerAction_Ability_FailsGracefully_WhenNotEnoughMp_UsesClassResourceName()
    {
        var rng = new SystemRandomSource(seed: 1);
        var mage = NewCharacter("Mage", "mage");
        mage.CurrentMp = 0;
        var party = new Party();
        party.AddMember(mage);
        var enemy = TestGameData.NewEnemyRegistry().Spawn("neon_grunt", floor: 1);
        var battle = NewBattleEngine(rng);
        battle.Start(party, new List<Enemy> { enemy });

        var result = battle.SubmitPlayerAction(new CombatAction(mage, ActionType.Ability, "fireball", new List<Combatant> { enemy }));

        Assert.False(result.Success);
        Assert.Contains("doesn't have enough Mana", result.LogLines[0]);
    }

    [Fact]
    public void SubmitPlayerAction_Ability_SetsCooldown_OnSuccessfulUse()
    {
        // lightning_bolt has cooldown=3: PostTurn ticks it once immediately (3->2), so unlike
        // a cooldown=1 ability (which would tick straight back to ready in the same call),
        // this one reliably stays on cooldown right after use.
        var rng = new SystemRandomSource(seed: 9);
        var mage = NewCharacter("Mage", "mage");
        var party = new Party();
        party.AddMember(mage);
        var enemy = TestGameData.NewEnemyRegistry().Spawn("neon_grunt", floor: 1);
        var battle = NewBattleEngine(rng);
        battle.Start(party, new List<Enemy> { enemy });

        Assert.True(battle.CanUseAbility(mage, "lightning_bolt"));
        battle.SubmitPlayerAction(new CombatAction(mage, ActionType.Ability, "lightning_bolt", new List<Combatant> { enemy }));

        Assert.False(battle.CanUseAbility(mage, "lightning_bolt")); // now on cooldown
        Assert.Equal(2, battle.Cooldowns.GetCooldown(mage, "lightning_bolt")); // 3 set, then ticked once by PostTurn
    }

    [Fact]
    public void SubmitPlayerAction_RecklessStrike_DealsSelfDamage()
    {
        var rng = new SystemRandomSource(seed: 11);
        var berserker = NewCharacter("Berserker", "berserker");
        var party = new Party();
        party.AddMember(berserker);
        var enemy = TestGameData.NewEnemyRegistry().Spawn("neon_grunt", floor: 1);
        var battle = NewBattleEngine(rng);
        battle.Start(party, new List<Enemy> { enemy });

        var hpBefore = berserker.CurrentHp;
        var result = battle.SubmitPlayerAction(new CombatAction(berserker, ActionType.Ability, "reckless_strike", new List<Combatant> { enemy }));

        Assert.True(result.ActorSelfDmg > 0);
        Assert.Equal(hpBefore - result.ActorSelfDmg, berserker.CurrentHp);
        Assert.Contains(result.LogLines, l => l.Contains("exertion"));
    }

    [Fact]
    public void SubmitPlayerAction_LifeDrain_HealsActor_ByFractionOfDamageDealt()
    {
        var rng = new SystemRandomSource(seed: 13);
        var mage = NewCharacter("Mage", "mage");
        mage.TakeDamage(50);
        var party = new Party();
        party.AddMember(mage);
        var enemy = TestGameData.NewEnemyRegistry().Spawn("neon_grunt", floor: 1);
        var battle = NewBattleEngine(rng);
        battle.Start(party, new List<Enemy> { enemy });

        var hpBefore = mage.CurrentHp;
        var result = battle.SubmitPlayerAction(new CombatAction(mage, ActionType.Ability, "life_drain", new List<Combatant> { enemy }));

        Assert.True(mage.CurrentHp > hpBefore); // healed via drain_ratio
        Assert.Contains(result.LogLines, l => l.Contains("absorbs"));
    }

    [Fact]
    public void SubmitPlayerAction_Transmute_ConvertsMpToHp_AndEndsEarly_NoTargetLoopSideEffects()
    {
        var rng = new SystemRandomSource(seed: 17);
        var knight = NewCharacter("Knight", "knight");
        knight.TakeDamage(20);
        var party = new Party();
        party.AddMember(knight);
        var enemy = TestGameData.NewEnemyRegistry().Spawn("neon_grunt", floor: 1);
        var battle = NewBattleEngine(rng);
        battle.Start(party, new List<Enemy> { enemy });

        var mpBefore = knight.CurrentMp;
        var hpBefore = knight.CurrentHp;
        var result = battle.SubmitPlayerAction(new CombatAction(knight, ActionType.Ability, "transmute"));

        Assert.True(knight.CurrentMp < mpBefore);
        Assert.True(knight.CurrentHp > hpBefore);
        Assert.Contains(result.LogLines, l => l.Contains("converts"));
        Assert.Empty(result.Hits); // early return — no per-target Hit entries created
    }

    [Fact]
    public void SubmitPlayerAction_Cure_CleansesDebuffs_DefaultsToSelfWhenNoTarget()
    {
        var rng = new SystemRandomSource(seed: 19);
        var cleric = NewCharacter("Cleric", "cleric");
        cleric.AddStatus("poison");
        cleric.AddStatus("stun");
        var party = new Party();
        party.AddMember(cleric);
        var enemy = TestGameData.NewEnemyRegistry().Spawn("neon_grunt", floor: 1);
        var battle = NewBattleEngine(rng);
        battle.Start(party, new List<Enemy> { enemy });

        // "cure" targets SINGLE_ALLY but we submit with no explicit targets, exercising the
        // cleanse_debuffs flag's own default-to-self fill.
        var result = battle.SubmitPlayerAction(new CombatAction(cleric, ActionType.Ability, "cure", new List<Combatant>()));

        Assert.False(cleric.HasStatus("poison"));
        Assert.False(cleric.HasStatus("stun"));
        Assert.Contains(result.LogLines, l => l.Contains("cleansed"));
    }
}
