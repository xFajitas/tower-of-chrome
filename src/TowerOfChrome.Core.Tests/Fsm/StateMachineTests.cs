using TowerOfChrome.Core.Fsm;

namespace TowerOfChrome.Core.Tests.Fsm;

public class StateMachineTests
{
    [Fact]
    public void InitialState_IsWhatConstructorReceived()
    {
        var fsm = new StateMachine(GameState.Menu);
        Assert.Equal(GameState.Menu, fsm.State);
        Assert.Empty(fsm.History);
    }

    [Theory]
    [InlineData(GameState.Menu, GameState.ClassSelect)]
    [InlineData(GameState.Menu, GameState.GameOver)]
    [InlineData(GameState.ClassSelect, GameState.Menu)]
    [InlineData(GameState.ClassSelect, GameState.Explore)]
    [InlineData(GameState.Explore, GameState.Combat)]
    [InlineData(GameState.Explore, GameState.Inventory)]
    [InlineData(GameState.Explore, GameState.Menu)]
    [InlineData(GameState.Explore, GameState.GameOver)]
    [InlineData(GameState.Combat, GameState.Explore)]
    [InlineData(GameState.Combat, GameState.Inventory)]
    [InlineData(GameState.Combat, GameState.GameOver)]
    [InlineData(GameState.Inventory, GameState.Explore)]
    [InlineData(GameState.Inventory, GameState.Combat)]
    [InlineData(GameState.GameOver, GameState.Menu)]
    public void ValidTransitions_Succeed(GameState from, GameState to)
    {
        var fsm = new StateMachine(from);
        Assert.True(fsm.CanTransition(to));
        Assert.True(fsm.Transition(to));
        Assert.Equal(to, fsm.State);
        Assert.Equal(new[] { from }, fsm.History);
    }

    [Theory]
    [InlineData(GameState.Menu, GameState.Combat)]
    [InlineData(GameState.Menu, GameState.Explore)]
    [InlineData(GameState.Explore, GameState.ClassSelect)]
    [InlineData(GameState.Combat, GameState.Menu)]
    [InlineData(GameState.Combat, GameState.ClassSelect)]
    [InlineData(GameState.Inventory, GameState.Menu)]
    [InlineData(GameState.Inventory, GameState.GameOver)]
    [InlineData(GameState.GameOver, GameState.Explore)]
    public void InvalidTransitions_Fail_AndLogWarning(GameState from, GameState to)
    {
        var fsm = new StateMachine(from);
        string? warning = null;
        fsm.OnInvalidTransition = msg => warning = msg;

        Assert.False(fsm.CanTransition(to));
        Assert.False(fsm.Transition(to));
        Assert.Equal(from, fsm.State); // unchanged
        Assert.Empty(fsm.History);     // no history push on failure
        Assert.NotNull(warning);
        Assert.Contains(from.ToString(), warning);
        Assert.Contains(to.ToString(), warning);
    }

    [Fact]
    public void Back_ReturnsToMostRecentHistoryEntry_ViaRevalidatedTransition()
    {
        var fsm = new StateMachine(GameState.Menu);
        fsm.Transition(GameState.ClassSelect);
        fsm.Transition(GameState.Explore); // history: [Menu, ClassSelect]

        // Back() re-validates via Transition(history[^1]=ClassSelect); Explore->ClassSelect is NOT
        // a valid edge, so this call fails and leaves state unchanged at Explore.
        Assert.False(fsm.Back());
        Assert.Equal(GameState.Explore, fsm.State);
    }

    [Fact]
    public void Back_WithNoHistory_ReturnsFalse()
    {
        var fsm = new StateMachine(GameState.Menu);
        Assert.False(fsm.Back());
    }

    [Fact]
    public void Back_SucceedsWhenPreviousStateIsAValidTarget()
    {
        // Explore -> Inventory -> Explore is a valid edge both ways, so Back() from
        // Inventory should successfully return to Explore.
        var fsm = new StateMachine(GameState.Explore);
        fsm.Transition(GameState.Inventory); // history: [Explore]

        Assert.True(fsm.Back());
        Assert.Equal(GameState.Explore, fsm.State);
    }

    [Fact]
    public void Hooks_FireInCorrectOrder_ExitThenEnterThenChange()
    {
        var fsm = new StateMachine(GameState.Menu);
        var order = new List<string>();

        fsm.OnExit(GameState.Menu, s => order.Add($"exit:{s}"));
        fsm.OnEnter(GameState.ClassSelect, s => order.Add($"enter:{s}"));
        fsm.OnChange((from, to) => order.Add($"change:{from}->{to}"));

        fsm.Transition(GameState.ClassSelect);

        Assert.Equal(new[] { "exit:Menu", "enter:ClassSelect", "change:Menu->ClassSelect" }, order);
    }

    [Fact]
    public void Hooks_DoNotFire_OnInvalidTransition()
    {
        var fsm = new StateMachine(GameState.Menu);
        var fired = false;
        fsm.OnExit(GameState.Menu, _ => fired = true);
        fsm.OnChange((_, _) => fired = true);

        fsm.Transition(GameState.Combat); // invalid

        Assert.False(fired);
    }
}
