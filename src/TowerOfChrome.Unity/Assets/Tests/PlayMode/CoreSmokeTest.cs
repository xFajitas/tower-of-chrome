using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TowerOfChrome.Core.Fsm;

/// <summary>
/// Phase 1 -> Phase 2 integration smoke test: confirms TowerOfChrome.Core.dll (built outside
/// Unity via `dotnet build`) loads and runs correctly inside the Unity Editor's Mono runtime,
/// from within an actual Play Mode frame.
/// </summary>
public class CoreSmokeTest
{
    [UnityTest]
    public IEnumerator StateMachine_TransitionsCorrectly_InsidePlayMode()
    {
        var go = new GameObject("CoreSmokeTestHost");

        var fsm = new StateMachine(GameState.Menu);
        var ok = fsm.Transition(GameState.ClassSelect);

        yield return null; // ensure at least one real Play Mode frame elapses

        Debug.Log($"[CoreSmokeTest] transition ok={ok} state={fsm.State}");

        Assert.IsTrue(ok);
        Assert.AreEqual(GameState.ClassSelect, fsm.State);

        Object.Destroy(go);
    }
}
