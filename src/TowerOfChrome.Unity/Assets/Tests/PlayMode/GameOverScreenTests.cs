using System.Collections;
using NUnit.Framework;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Unity;
using TowerOfChrome.Unity.Screens;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

public class GameOverScreenTests
{
    private static IEnumerator LoadGameManager(System.Action<GameManager> onLoaded)
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return null;
        var gm = Object.FindFirstObjectByType<GameManager>();
        yield return null;
        onLoaded(gm);
    }

    private static GameOverScreenView GetGameOverView(GameManager gm) =>
        gm.GetScreenRoot(GameState.GameOver).GetComponent<GameOverScreenView>();

    [UnityTest]
    public IEnumerator OnEnable_ShowsSummary_WithFloorKillsSurvivors()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.CurrentFloor = 3;
        gm.EnemiesDefeated = 7;

        gm.SwitchTo(GameState.GameOver);
        yield return null;

        var view = GetGameOverView(gm);
        var summary = view.GetComponent<UIDocument>().rootVisualElement.Q<Label>("summary");

        StringAssert.Contains("Floor 3", summary.text);
        StringAssert.Contains("7 enemies defeated", summary.text);
        StringAssert.Contains($"{gm.Party.LivingMembers.Count}/4 survivors", summary.text);
    }

    [UnityTest]
    public IEnumerator ReturnToMenu_SwitchesToMenu()
    {
        GameManager gm = null;
        yield return LoadGameManager(loaded => gm = loaded);
        gm.SwitchTo(GameState.GameOver);
        yield return null;
        var view = GetGameOverView(gm);

        view.ReturnToMenu();

        Assert.AreEqual(GameState.Menu, gm.Fsm.State);
    }
}
