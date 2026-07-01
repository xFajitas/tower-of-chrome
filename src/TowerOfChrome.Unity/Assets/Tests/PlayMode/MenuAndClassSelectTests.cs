using System.Collections;
using System.Linq;
using NUnit.Framework;
using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Unity;
using TowerOfChrome.Unity.Screens;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Exercises MenuScreenView and ClassSelectScreenView by calling their public
/// navigate/activate/confirm methods directly (not simulating real keyboard input, which
/// legacy UnityEngine.Input can't do reliably in a headless batch run).
/// </summary>
public class MenuAndClassSelectTests
{
    private static IEnumerator LoadGameManager(System.Action<GameManager> onLoaded)
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return null; // let the scene swap actually take effect
        var gm = Object.FindFirstObjectByType<GameManager>();
        yield return null; // let Awake()/OnEnable() settle
        onLoaded(gm);
    }

    private static MenuScreenView GetMenuView(GameManager gm) =>
        gm.GetScreenRoot(GameState.Menu).GetComponent<MenuScreenView>();

    private static ClassSelectScreenView GetClassSelectView(GameManager gm) =>
        gm.GetScreenRoot(GameState.ClassSelect).GetComponent<ClassSelectScreenView>();

    // ------------------------------------------------------------------
    // MenuScreen
    // ------------------------------------------------------------------

    [UnityTest]
    public IEnumerator Menu_WithoutSave_HasNewGameAndQuit_ButNoContinue()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.DeleteSave();

        var menu = GetMenuView(gm);
        menu.BuildItems();

        CollectionAssert.DoesNotContain(menu.Items.Select(i => i.Action).ToList(), "continue");
        CollectionAssert.AreEqual(new[] { "new", "quit" }, menu.Items.Select(i => i.Action).ToList());
    }

    [UnityTest]
    public IEnumerator Menu_WithSave_ShowsContinueWithFloorNumber()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.CurrentFloor = 5;
        gm.SaveGame();

        var menu = GetMenuView(gm);
        menu.BuildItems();

        Assert.AreEqual("continue", menu.Items[0].Action);
        StringAssert.Contains("Floor 5", menu.Items[0].Label);

        gm.DeleteSave();
    }

    [UnityTest]
    public IEnumerator Menu_Navigation_WrapsAroundInBothDirections()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.DeleteSave();

        var menu = GetMenuView(gm);
        menu.BuildItems(); // ["new", "quit"], starts at 0

        menu.NavigateUp(); // wraps to last
        Assert.AreEqual(menu.Items.Count - 1, menu.Selected);

        menu.NavigateDown(); // back to 0
        Assert.AreEqual(0, menu.Selected);

        menu.NavigateDown(); // to last
        Assert.AreEqual(menu.Items.Count - 1, menu.Selected);

        menu.NavigateDown(); // wraps to 0
        Assert.AreEqual(0, menu.Selected);
    }

    [UnityTest]
    public IEnumerator Menu_ActivateNewGame_SwitchesToClassSelect()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.DeleteSave();

        var menu = GetMenuView(gm);
        menu.BuildItems(); // selected=0="new" (no save)

        menu.Activate();

        Assert.AreEqual(GameState.ClassSelect, gm.Fsm.State);
    }

    [UnityTest]
    public IEnumerator Menu_ActivateContinue_LoadsSaveAndSwitchesToExplore()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.CurrentFloor = 7;
        gm.SaveGame();

        var menu = GetMenuView(gm);
        menu.BuildItems(); // selected=0="continue"

        menu.Activate();

        Assert.AreEqual(GameState.Explore, gm.Fsm.State);
        Assert.AreEqual(7, gm.CurrentFloor);

        gm.DeleteSave();
    }

    // ------------------------------------------------------------------
    // ClassSelectScreen
    // ------------------------------------------------------------------

    [UnityTest]
    public IEnumerator ClassSelect_DefaultChoices_MatchPartyDefaults()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);

        gm.SwitchTo(GameState.ClassSelect);
        yield return null;

        var view = GetClassSelectView(gm);

        for (var i = 0; i < Party.MaxPartySize; i++)
            Assert.AreEqual(Party.DefaultClassIds[i], view.ClassIdForSlot(i));
    }

    [UnityTest]
    public IEnumerator ClassSelect_CycleRight_OnlyAffectsSelectedSlot()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;

        var view = GetClassSelectView(gm);
        var slot0Before = view.ClassIdForSlot(0);
        var slot1Before = view.ClassIdForSlot(1);

        view.CycleClassRight(); // slot 0 is selected by default

        Assert.AreNotEqual(slot0Before, view.ClassIdForSlot(0));
        Assert.AreEqual(slot1Before, view.ClassIdForSlot(1)); // untouched
    }

    [UnityTest]
    public IEnumerator ClassSelect_CycleFullCircle_ReturnsToOriginalChoice()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;

        var view = GetClassSelectView(gm);
        var original = view.ClassIdForSlot(0);
        var classCount = view.ClassIds.Count;
        Assert.AreEqual(16, classCount);

        for (var i = 0; i < classCount; i++)
            view.CycleClassRight();

        Assert.AreEqual(original, view.ClassIdForSlot(0));
    }

    [UnityTest]
    public IEnumerator ClassSelect_NavigateSlot_WrapsAround()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;

        var view = GetClassSelectView(gm);
        Assert.AreEqual(0, view.SlotSelected);

        view.NavigateSlotUp();
        Assert.AreEqual(Party.MaxPartySize - 1, view.SlotSelected);

        view.NavigateSlotDown();
        Assert.AreEqual(0, view.SlotSelected);
    }

    [UnityTest]
    public IEnumerator ClassSelect_Confirm_BuildsPartyWithChosenClasses_AndSwitchesToExplore()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;

        var view = GetClassSelectView(gm);
        view.CycleClassRight(); // change slot 0's class away from the default
        var chosenClassId = view.ClassIdForSlot(0);

        view.Confirm();

        Assert.AreEqual(GameState.Explore, gm.Fsm.State);
        Assert.AreEqual(4, gm.Party.AllMembers.Count);
        Assert.AreEqual(chosenClassId, gm.Party.AllMembers[0].ClassDef.Id);
        Assert.AreEqual(Party.DefaultNames[0], gm.Party.AllMembers[0].Name);
        Assert.AreEqual(1, gm.CurrentFloor);
    }

    [UnityTest]
    public IEnumerator ClassSelect_Cancel_ReturnsToMenu()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.ClassSelect);
        yield return null;

        var view = GetClassSelectView(gm);
        view.Cancel();

        Assert.AreEqual(GameState.Menu, gm.Fsm.State);
    }
}
