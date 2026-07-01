using System.Collections;
using System.IO;
using NUnit.Framework;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Verifies the actual Main.unity scene (built by Assets/Editor/SceneBuilder.cs) boots
/// correctly: registries load real data counts, a default party is built, and the FSM's
/// OnChange callback correctly toggles the scene's screen-root GameObjects.
/// </summary>
public class GameManagerBootTest
{
    /// <summary>
    /// SceneManager.LoadScene's actual scene swap doesn't take effect until the end of the
    /// current frame, so searching for objects immediately after calling it (before a frame
    /// boundary) can still see the *previous* test's scene/GameManager — always yield once
    /// before searching. GameObject.Find is also avoided in the tests below since it skips
    /// inactive objects; GameManager.GetScreenRoot is used instead.
    /// </summary>
    private static IEnumerator LoadGameManager(System.Action<GameManager> onLoaded)
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return null; // let the scene swap actually take effect
        var gm = Object.FindFirstObjectByType<GameManager>();
        yield return null; // let Awake() settle
        onLoaded(gm);
    }

    [UnityTest]
    public IEnumerator GameManager_Boots_WithRealDataCounts()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);

        Assert.IsNotNull(gm, "GameManager should exist in Main.unity");
        Assert.AreEqual(16, gm.ClassRegistry.All().Count, "16 playable classes");
        Assert.AreEqual(70, gm.ItemRegistry.All().Count, "70 items");
        Assert.AreEqual(89, gm.AbilityRegistry.All().Count, "89 abilities");
    }

    [UnityTest]
    public IEnumerator GameManager_BuildsDefaultParty_OnBoot()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);

        Assert.AreEqual(4, gm.Party.AllMembers.Count);
        CollectionAssert.AreEqual(
            new[] { "Sir Gareth", "Lysandra", "Brother Aldric", "Sylvara" },
            System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(gm.Party.AllMembers, m => m.Name)));
    }

    [UnityTest]
    public IEnumerator Fsm_StartsInMenuState_WithOnlyMenuRootActive()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);

        Assert.AreEqual(GameState.Menu, gm.Fsm.State);

        var menuRoot = gm.GetScreenRoot(GameState.Menu);
        var classSelectRoot = gm.GetScreenRoot(GameState.ClassSelect);
        Assert.IsNotNull(menuRoot);
        Assert.IsNotNull(classSelectRoot);
        Assert.IsTrue(menuRoot.activeSelf);
        Assert.IsFalse(classSelectRoot.activeSelf);
    }

    [UnityTest]
    public IEnumerator SwitchTo_TogglesScreenRoots_ExclusivelyToTheNewState()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);

        gm.SwitchTo(GameState.ClassSelect);
        yield return null;

        Assert.AreEqual(GameState.ClassSelect, gm.Fsm.State);
        Assert.IsFalse(gm.GetScreenRoot(GameState.Menu).activeSelf);
        Assert.IsTrue(gm.GetScreenRoot(GameState.ClassSelect).activeSelf);
        Assert.IsFalse(gm.GetScreenRoot(GameState.Explore).activeSelf);
    }

    [UnityTest]
    public IEnumerator SwitchTo_InvalidTransition_LogsWarning_AndStateUnchanged()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*Invalid transition.*"));

        gm.SwitchTo(GameState.Combat); // Menu -> Combat is not a valid edge
        yield return null;

        Assert.AreEqual(GameState.Menu, gm.Fsm.State);
    }

    [UnityTest]
    public IEnumerator StartCombat_CreatesLiveBattleEngine_AgainstARandomEncounter()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);

        gm.StartCombat();

        Assert.IsNotNull(gm.Battle);
        Assert.AreEqual(TowerOfChrome.Core.Combat.BattlePhase.Ongoing, gm.Battle!.Phase);
        Assert.IsTrue(gm.Battle.GetEnemyList().Count > 0);
    }

    /// <summary>Confirms SaveGame() (only called from AdvanceFloor) actually writes a real file
    /// at Application.persistentDataPath -- not just that SaveLoadService's in-memory logic
    /// round-trips (already covered in Core.Tests), but that the Unity-side path plumbing does
    /// too. Mutates CurrentFloor before LoadGame() so a passing assertion proves the reload came
    /// from disk, not leftover in-memory state.</summary>
    [UnityTest]
    public IEnumerator AdvanceFloor_WritesRealSaveFile_AndLoadGame_RestoresFromDisk()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);

        var floorBefore = gm.CurrentFloor;
        gm.AdvanceFloor();

        var savePath = Path.Combine(Application.persistentDataPath, "save", "savegame.json");
        Assert.IsTrue(File.Exists(savePath), $"Expected a save file at {savePath}");

        gm.CurrentFloor = 999;
        var loaded = gm.LoadGame();

        Assert.IsTrue(loaded);
        Assert.AreEqual(floorBefore + 1, gm.CurrentFloor);
    }
}
