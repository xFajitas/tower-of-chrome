using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NUnit.Framework;
using TowerOfChrome.Core.Combat;
using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Unity;
using TowerOfChrome.Unity.Screens;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Exercises CombatScreenView by calling its public navigate/submit methods directly,
/// using ForceAdvance() to bypass the real-time resolve-delay pacing so battles can be driven
/// to completion deterministically without waiting on wall-clock time.</summary>
public class CombatScreenTests
{
    private static IEnumerator LoadGameManager(System.Action<GameManager> onLoaded)
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return null;
        var gm = Object.FindFirstObjectByType<GameManager>();
        yield return null;
        onLoaded(gm);
    }

    private static CombatScreenView GetCombatView(GameManager gm) =>
        gm.GetScreenRoot(GameState.Combat).GetComponent<CombatScreenView>();

    /// <summary>A 1-HP enemy guarantees any single attack ends the battle in Victory, isolating
    /// tests from combat-formula randomness (mirrors ExploreScreenTests' NewOneHitEnemy trick).</summary>
    private static EnemyDefinition OneHitEnemyDef(string id = "test_dummy") => new EnemyDefinition(
        id, "Test Dummy", "", "common",
        new Dictionary<string, int> { ["hp"] = 1, ["mp"] = 0, ["str"] = 0, ["dex"] = 0, ["int"] = 0, ["vit"] = 0, ["spd"] = 0, ["luck"] = 0 },
        new Dictionary<string, double>(),
        ImmutableArray.Create("basic_attack"), "aggressive", 50, "common_floor1");

    /// <summary>Drives Menu -> ClassSelect -> Explore -> Combat with the given enemies queued,
    /// mirroring the real Explore -> Combat handoff (queue PendingEncounter, switch state;
    /// CombatScreenView.OnEnable() calls StartCombat() itself).</summary>
    private static IEnumerator SetUpCombat(GameManager gm, IEnumerable<EnemyDefinition> enemyDefs)
    {
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        gm.PendingEncounter.Clear();
        foreach (var def in enemyDefs)
            gm.PendingEncounter.Add(new Enemy(def, gm.CurrentFloor));

        gm.SwitchTo(GameState.Combat);
        yield return null;
    }

    /// <summary>Bypasses the real-time resolve-delay pacing to fast-forward through enemy turns
    /// until it's the player's turn again or the battle ends.</summary>
    private static void DriveToPlayerTurnOrEnd(CombatScreenView view, GameManager gm, int maxIterations = 50)
    {
        var i = 0;
        while (gm.Battle.Phase == BattlePhase.Ongoing && !gm.Battle.IsPlayerTurn && i < maxIterations)
        {
            view.ForceAdvance();
            i++;
        }
    }

    [UnityTest]
    public IEnumerator OnEnable_StartsCombat_MatchesInitialTurnOrder()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return SetUpCombat(gm, new[] { OneHitEnemyDef() });
        var view = GetCombatView(gm);

        Assert.IsNotNull(gm.Battle);
        Assert.AreEqual(gm.Battle.IsPlayerTurn ? CombatUiState.PlayerMain : CombatUiState.EnemyTurn, view.State);
    }

    [UnityTest]
    public IEnumerator NavigateMain_WrapsAroundInBothDirections()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return SetUpCombat(gm, new[] { OneHitEnemyDef() });
        var view = GetCombatView(gm);
        DriveToPlayerTurnOrEnd(view, gm);
        Assert.AreEqual(CombatUiState.PlayerMain, view.State);

        view.NavigateMainUp(); // wraps from 0 to 3 (Flee)
        Assert.AreEqual(3, view.MainSelected);

        view.NavigateMainDown();
        view.NavigateMainDown();
        view.NavigateMainDown();
        view.NavigateMainDown(); // wraps back around to 3
        Assert.AreEqual(3, view.MainSelected);
    }

    [UnityTest]
    public IEnumerator ActivateMain_Attack_OpensTargetPicker_WithLivingEnemies()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return SetUpCombat(gm, new[] { OneHitEnemyDef(), OneHitEnemyDef("test_dummy_2") });
        var view = GetCombatView(gm);
        DriveToPlayerTurnOrEnd(view, gm);
        Assert.AreEqual(CombatUiState.PlayerMain, view.State);

        view.ActivateMain(0); // Attack

        Assert.AreEqual(CombatUiState.PlayerTarget, view.State);
        Assert.AreEqual(2, view.CurrentTargetList().Count);
    }

    [UnityTest]
    public IEnumerator CancelTarget_FromAttack_ReturnsToPlayerMain()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return SetUpCombat(gm, new[] { OneHitEnemyDef() });
        var view = GetCombatView(gm);
        DriveToPlayerTurnOrEnd(view, gm);
        view.ActivateMain(0);

        view.CancelTarget();

        Assert.AreEqual(CombatUiState.PlayerMain, view.State);
    }

    [UnityTest]
    public IEnumerator ConfirmTarget_Attack_DamagesEnemy_EntersResolving()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return SetUpCombat(gm, new[] { OneHitEnemyDef() });
        var view = GetCombatView(gm);
        DriveToPlayerTurnOrEnd(view, gm);
        view.ActivateMain(0);

        view.ConfirmTarget();

        Assert.AreEqual(CombatUiState.Resolving, view.State);
        // The 1-HP enemy dies to any attack, so the battle should already be over.
        Assert.AreEqual(BattlePhase.Victory, gm.Battle.Phase);
    }

    [UnityTest]
    public IEnumerator CancelAbility_ReturnsToPlayerMain()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return SetUpCombat(gm, new[] { OneHitEnemyDef() });
        var view = GetCombatView(gm);
        DriveToPlayerTurnOrEnd(view, gm);
        view.ActivateMain(1); // Abilities
        Assert.AreEqual(CombatUiState.PlayerAbility, view.State);

        view.CancelAbility();

        Assert.AreEqual(CombatUiState.PlayerMain, view.State);
    }

    [UnityTest]
    public IEnumerator SelectAbility_SingleEnemyTargeting_OpensTargetPicker()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return SetUpCombat(gm, new[] { OneHitEnemyDef() });
        var view = GetCombatView(gm);
        DriveToPlayerTurnOrEnd(view, gm);

        var actor = (Character)gm.Battle.CurrentActor;
        var abilities = actor.ClassDef.Abilities;
        var targetIdx = -1;
        for (var i = 0; i < abilities.Length; i++)
        {
            if (gm.AbilityRegistry.Get(abilities[i]).Targeting == "SINGLE_ENEMY")
            {
                targetIdx = i;
                break;
            }
        }
        if (targetIdx < 0)
            Assert.Ignore($"{actor.ClassDef.Name} has no single-enemy-targeting ability to test with.");

        view.ActivateMain(1); // Abilities
        while (view.AbilitySelected != targetIdx)
            view.NavigateAbilityDown();

        view.SelectAbility();

        Assert.AreEqual(CombatUiState.PlayerTarget, view.State);
    }

    [UnityTest]
    public IEnumerator SelectAbility_SelfOrAoeTargeting_SubmitsDirectly()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return SetUpCombat(gm, new[] { OneHitEnemyDef() });
        var view = GetCombatView(gm);
        DriveToPlayerTurnOrEnd(view, gm);

        var actor = (Character)gm.Battle.CurrentActor;
        var abilities = actor.ClassDef.Abilities;
        var targetIdx = -1;
        for (var i = 0; i < abilities.Length; i++)
        {
            var targeting = gm.AbilityRegistry.Get(abilities[i]).Targeting;
            if (targeting is "SELF" or "ALL_ALLIES" or "ALL_ENEMIES")
            {
                targetIdx = i;
                break;
            }
        }
        if (targetIdx < 0)
            Assert.Ignore($"{actor.ClassDef.Name} has no self/AOE-targeting ability to test with.");

        view.ActivateMain(1); // Abilities
        while (view.AbilitySelected != targetIdx)
            view.NavigateAbilityDown();

        view.SelectAbility();

        // Submits immediately without a target picker, unless it couldn't afford the MP cost
        // (in which case Resolve still runs and reports failure -- either way, never PlayerTarget).
        Assert.AreNotEqual(CombatUiState.PlayerTarget, view.State);
    }

    [UnityTest]
    public IEnumerator ForceAdvance_FromResolving_ProgressesTurnOrder()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        // Two enemies so the battle survives past the first kill.
        yield return SetUpCombat(gm, new[] { OneHitEnemyDef("a"), OneHitEnemyDef("b") });
        var view = GetCombatView(gm);
        DriveToPlayerTurnOrEnd(view, gm);
        view.ActivateMain(0);
        view.ConfirmTarget();
        Assert.AreEqual(CombatUiState.Resolving, view.State);

        view.ForceAdvance();

        Assert.AreNotEqual(CombatUiState.Resolving, view.State);
    }

    [UnityTest]
    public IEnumerator Victory_PopulatesXpAwards_ContinueReturnsToExplore()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        yield return SetUpCombat(gm, new[] { OneHitEnemyDef() });
        var view = GetCombatView(gm);
        DriveToPlayerTurnOrEnd(view, gm);
        view.ActivateMain(0);
        view.ConfirmTarget();
        Assert.AreEqual(BattlePhase.Victory, gm.Battle.Phase);

        yield return null; // let Update() notice the phase flip

        Assert.AreEqual(CombatUiState.Victory, view.State);
        Assert.IsNotEmpty(gm.Battle.XpAwards);

        view.ContinueFromTerminal();
        yield return null;

        Assert.AreEqual(GameState.Explore, gm.Fsm.State);
    }

    [UnityTest]
    public IEnumerator Defeat_ContinueReturnsToGameOver()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        // A durable enemy so the party wipes before it does.
        var durable = new EnemyDefinition(
            "test_tank", "Test Tank", "", "common",
            new Dictionary<string, int> { ["hp"] = 999, ["mp"] = 0, ["str"] = 0, ["dex"] = 0, ["int"] = 0, ["vit"] = 0, ["spd"] = 0, ["luck"] = 0 },
            new Dictionary<string, double>(),
            ImmutableArray.Create("basic_attack"), "aggressive", 50, "common_floor1");
        yield return SetUpCombat(gm, new[] { durable });
        var view = GetCombatView(gm);

        foreach (var member in gm.Party.AllMembers)
            member.TakeDamage(99999);

        // Resolve exactly one turn (whoever's) so CheckBattleEnd() runs and notices the wipe.
        if (view.State == CombatUiState.PlayerMain)
            view.ActivateMain(2); // Defend
        else
            view.ForceAdvance();

        Assert.AreEqual(BattlePhase.Defeat, gm.Battle.Phase);

        yield return null; // let Update() notice the phase flip

        Assert.AreEqual(CombatUiState.Defeat, view.State);

        view.ContinueFromTerminal();
        yield return null;

        Assert.AreEqual(GameState.GameOver, gm.Fsm.State);
    }
}
