namespace TowerOfChrome.Core.Fsm;

/// <summary>
/// Validated FSM with enter/exit lifecycle hooks and a history stack.
/// Direct port of core/game_state.py's StateMachine.
/// </summary>
public sealed class StateMachine
{
    private readonly List<GameState> _history = new();
    private readonly Dictionary<GameState, List<Action<GameState>>> _enterHooks = new();
    private readonly Dictionary<GameState, List<Action<GameState>>> _exitHooks = new();
    private readonly List<Action<GameState, GameState>> _changeHooks = new();

    /// <summary>Invoked with a warning message when an invalid transition is attempted (mirrors Python's print()).</summary>
    public Action<string>? OnInvalidTransition { get; set; }

    public GameState State { get; private set; }

    /// <summary>Read-only snapshot of the traversal history.</summary>
    public IReadOnlyList<GameState> History => _history.AsReadOnly();

    public StateMachine(GameState initial)
    {
        State = initial;
        foreach (GameState s in (GameState[])Enum.GetValues(typeof(GameState)))
        {
            _enterHooks[s] = new List<Action<GameState>>();
            _exitHooks[s] = new List<Action<GameState>>();
        }
    }

    public bool CanTransition(GameState to) =>
        Transitions.Map.TryGetValue(State, out var allowed) && allowed.Contains(to);

    /// <summary>Attempt a state change. Returns true on success, false if the transition is not allowed.</summary>
    public bool Transition(GameState to)
    {
        if (!CanTransition(to))
        {
            OnInvalidTransition?.Invoke($"[FSM] Invalid transition: {State} -> {to}");
            return false;
        }

        var old = State;

        foreach (var hook in _exitHooks[old])
            hook(old);

        _history.Add(old);
        State = to;

        foreach (var hook in _enterHooks[to])
            hook(to);

        foreach (var hook in _changeHooks)
            hook(old, to);

        return true;
    }

    /// <summary>Return to the most recent previous state, if the transition is valid.</summary>
    public bool Back()
    {
        if (_history.Count == 0)
            return false;
        return Transition(_history[^1]);
    }

    public void OnEnter(GameState state, Action<GameState> callback) => _enterHooks[state].Add(callback);

    public void OnExit(GameState state, Action<GameState> callback) => _exitHooks[state].Add(callback);

    /// <summary>Fired after every successful transition with (old, new) states.</summary>
    public void OnChange(Action<GameState, GameState> callback) => _changeHooks.Add(callback);
}
