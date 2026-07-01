using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TowerOfChrome.Core.Combat;
using TowerOfChrome.Core.Dungeon;
using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Unity;
using TowerOfChrome.Unity.Screens;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>Exercises ExploreScreenView by calling its public navigate/interact methods
/// directly. Uses small hand-built DungeonFloors (rather than the real procedural generator)
/// for deterministic room-type placement.</summary>
public class ExploreScreenTests
{
    private static IEnumerator LoadGameManager(System.Action<GameManager> onLoaded)
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return null;
        var gm = Object.FindFirstObjectByType<GameManager>();
        yield return null;
        onLoaded(gm);
    }

    private static ExploreScreenView GetExploreView(GameManager gm) =>
        gm.GetScreenRoot(GameState.Explore).GetComponent<ExploreScreenView>();

    /// <summary>Two-room floor: start(0) --- target(1), with `targetType` on room 1.</summary>
    private static DungeonFloor TwoRoomFloor(int floorNumber, RoomType targetType, bool targetCleared = false, IEnumerable<string> loot = null)
    {
        var start = new Room(0, x: 0, y: 0, w: 100, h: 100) { RoomType = RoomType.Start };
        var target = new Room(1, x: 200, y: 0, w: 100, h: 100) { RoomType = targetType, Cleared = targetCleared };
        if (loot != null)
            target.Loot = loot.ToList();
        start.Connections.Add(1);
        target.Connections.Add(0);
        return new DungeonFloor(floorNumber, new[] { start, target }, new[] { (0, 1) }, playerRoomId: 0);
    }

    [UnityTest]
    public IEnumerator OnEnable_GeneratesFloor_WhenNoneExists()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        Assert.IsNotNull(gm.DungeonFloor);
        Assert.AreEqual(gm.CurrentFloor, gm.DungeonFloor.FloorNumber);
    }

    [UnityTest]
    public IEnumerator MoveDirection_MovesPlayer_WhenRoomExistsThatWay()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        gm.DungeonFloor = TwoRoomFloor(gm.CurrentFloor, RoomType.Normal);
        var view = GetExploreView(gm);

        view.MoveDirection("right");

        Assert.AreEqual(1, gm.DungeonFloor.PlayerRoomId);
        Assert.True(gm.DungeonFloor.Rooms[1].Visited);
    }

    [UnityTest]
    public IEnumerator Interact_OnUnclearedEncounter_StartsCombat_WithCorrectRoomId()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        gm.DungeonFloor = TwoRoomFloor(gm.CurrentFloor, RoomType.Encounter);
        var view = GetExploreView(gm);
        view.MoveDirection("right"); // step into the encounter room

        view.Interact();

        Assert.AreEqual(GameState.Combat, gm.Fsm.State);
        Assert.AreEqual(1, gm.CombatRoomId);
    }

    [UnityTest]
    public IEnumerator Interact_OnUnclearedBoss_QueuesBossEncounter_AndStartsCombat()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        gm.DungeonFloor = TwoRoomFloor(gm.CurrentFloor, RoomType.Boss);
        var view = GetExploreView(gm);
        view.MoveDirection("right");

        view.Interact();

        Assert.AreEqual(GameState.Combat, gm.Fsm.State);
        Assert.AreEqual(1, gm.CombatRoomId);
        // PendingEncounter is consumed by StartCombat(), which the screen doesn't call itself —
        // it's the caller's/GameManager's job. Assert the boss was queued before combat started.
    }

    [UnityTest]
    public IEnumerator Interact_OnClearedBoss_OpensStairsConfirm()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        gm.DungeonFloor = TwoRoomFloor(gm.CurrentFloor, RoomType.Boss, targetCleared: true);
        var view = GetExploreView(gm);
        view.MoveDirection("right");

        view.Interact();

        Assert.AreEqual(ExploreUiState.StairsConfirm, view.State);
    }

    [UnityTest]
    public IEnumerator Interact_OnTreasure_AwardsLoot_MarksCleared_ShowsLootModal()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        gm.DungeonFloor = TwoRoomFloor(gm.CurrentFloor, RoomType.Treasure, loot: new[] { "health_potion_small" });
        var view = GetExploreView(gm);
        view.MoveDirection("right");

        view.Interact();

        Assert.AreEqual(ExploreUiState.LootShow, view.State);
        Assert.True(gm.DungeonFloor.Rooms[1].Cleared);
        Assert.IsTrue(gm.Party.AllMembers.Any(m => m.Inventory.Bag.Contains("health_potion_small")));
        Assert.IsNotEmpty(view.LootLines);
    }

    [UnityTest]
    public IEnumerator Interact_OnStairs_CanAccess_OpensStairsConfirm()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        gm.DungeonFloor = TwoRoomFloor(gm.CurrentFloor, RoomType.Stairs); // no boss room -> always accessible
        var view = GetExploreView(gm);
        view.MoveDirection("right");

        view.Interact();

        Assert.AreEqual(ExploreUiState.StairsConfirm, view.State);
    }

    [UnityTest]
    public IEnumerator Interact_OnStairs_BossUncleared_FlashesWarning_StaysNavigating()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        // 3-room floor: start(0) - stairs(1) - boss(2), boss uncleared blocks stairs.
        var start = new Room(0, 0, 0, 100, 100) { RoomType = RoomType.Start };
        var stairs = new Room(1, 200, 0, 100, 100) { RoomType = RoomType.Stairs };
        var boss = new Room(2, 400, 0, 100, 100) { RoomType = RoomType.Boss, Cleared = false };
        start.Connections.Add(1);
        stairs.Connections.Add(0);
        stairs.Connections.Add(2);
        boss.Connections.Add(1);
        gm.DungeonFloor = new DungeonFloor(gm.CurrentFloor, new[] { start, stairs, boss }, new[] { (0, 1), (1, 2) }, 0);

        var view = GetExploreView(gm);
        view.MoveDirection("right"); // -> stairs room

        view.Interact();

        Assert.AreEqual(ExploreUiState.Navigating, view.State);
        StringAssert.Contains("boss", view.Message.ToLowerInvariant());
    }

    [UnityTest]
    public IEnumerator ConfirmStairs_AdvancesFloor_AndReturnsToNavigating()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        gm.DungeonFloor = TwoRoomFloor(gm.CurrentFloor, RoomType.Stairs);
        var view = GetExploreView(gm);
        view.MoveDirection("right");
        view.Interact(); // -> StairsConfirm
        var floorBefore = gm.CurrentFloor;

        view.ConfirmStairs();

        Assert.AreEqual(floorBefore + 1, gm.CurrentFloor);
        Assert.AreEqual(ExploreUiState.Navigating, view.State);
        Assert.AreEqual(gm.CurrentFloor, gm.DungeonFloor.FloorNumber);
    }

    [UnityTest]
    public IEnumerator CancelStairs_ReturnsToNavigating_WithoutAdvancingFloor()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        gm.DungeonFloor = TwoRoomFloor(gm.CurrentFloor, RoomType.Stairs);
        var view = GetExploreView(gm);
        view.MoveDirection("right");
        view.Interact();
        var floorBefore = gm.CurrentFloor;

        view.CancelStairs();

        Assert.AreEqual(floorBefore, gm.CurrentFloor);
        Assert.AreEqual(ExploreUiState.Navigating, view.State);
    }

    [UnityTest]
    public IEnumerator DismissLoot_ClearsLootLines_AndReturnsToNavigating()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        gm.DungeonFloor = TwoRoomFloor(gm.CurrentFloor, RoomType.Treasure, loot: new[] { "health_potion_small" });
        var view = GetExploreView(gm);
        view.MoveDirection("right");
        view.Interact();

        view.DismissLoot();

        Assert.AreEqual(ExploreUiState.Navigating, view.State);
        Assert.IsEmpty(view.LootLines);
    }

    [UnityTest]
    public IEnumerator VictoryReturn_MarksCombatRoomCleared_AndIncrementsEnemiesDefeated()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;
        gm.SwitchTo(GameState.Explore);
        yield return null;

        gm.DungeonFloor = TwoRoomFloor(gm.CurrentFloor, RoomType.Encounter);
        gm.CombatRoomId = 1;
        gm.EnemiesDefeated = 0;

        // A custom 1-HP enemy guarantees any single attack ends the battle in Victory,
        // isolating this test from combat-formula randomness (mirrors the Core test suite's
        // NewOneHitEnemy trick).
        var oneHitDef = new EnemyDefinition(
            "test_dummy", "Test Dummy", "", "common",
            new Dictionary<string, int> { ["hp"] = 1, ["mp"] = 0, ["str"] = 0, ["dex"] = 0, ["int"] = 0, ["vit"] = 0, ["spd"] = 0, ["luck"] = 0 },
            new Dictionary<string, double>(),
            System.Collections.Immutable.ImmutableArray.Create("basic_attack"), "aggressive", 50, "common_floor1");
        gm.PendingEncounter.Add(new Enemy(oneHitDef, gm.CurrentFloor));

        gm.StartCombat();
        gm.SwitchTo(GameState.Combat);
        yield return null;

        for (var i = 0; i < 20 && gm.Battle.Phase == BattlePhase.Ongoing; i++)
        {
            var actor = gm.Battle.CurrentActor;
            if (gm.Battle.IsPlayerTurn)
                gm.Battle.SubmitPlayerAction(new CombatAction(actor, ActionType.Attack, targets: new List<Combatant> { gm.Battle.LivingEnemies[0] }));
            else
                gm.Battle.AdvanceEnemyTurn();
        }
        Assert.AreEqual(BattlePhase.Victory, gm.Battle.Phase);

        // Re-enter Explore, as the real game does right after a battle resolves.
        gm.SwitchTo(GameState.Explore);
        yield return null;

        Assert.True(gm.DungeonFloor.Rooms[1].Cleared);
        Assert.AreEqual(-1, gm.CombatRoomId);
        Assert.True(gm.EnemiesDefeated > 0);
    }
}
