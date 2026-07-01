namespace TowerOfChrome.Core.Fsm;

/// <summary>
/// Adjacency table — only listed edges are permitted.
///
/// Deviates from Python's TRANSITIONS dict in exactly one place: Menu now also allows a direct
/// transition to Explore. The Python original only allows Menu -> {ClassSelect, GameOver}, but
/// MenuScreen._activate()'s "continue" branch calls switch_to(GameState.EXPLORE) directly from
/// Menu — an edge the table never permitted. This isn't a deliberate quirk to preserve; it's an
/// unintended regression from when Class Select was added (Menu's allowed edges were narrowed to
/// [CLASS_SELECT, GAME_OVER] without updating the pre-existing Continue flow), and it means
/// "Continue" is currently non-functional in the shipped Python game: load_game() runs, mutating
/// engine state, but the screen switch silently fails and the player never leaves the menu.
/// Fixed here since it directly blocks a core feature; flagged for a matching fix upstream.
/// </summary>
public static class Transitions
{
    public static readonly IReadOnlyDictionary<GameState, IReadOnlyList<GameState>> Map =
        new Dictionary<GameState, IReadOnlyList<GameState>>
        {
            [GameState.Menu]        = new[] { GameState.ClassSelect, GameState.Explore, GameState.GameOver },
            [GameState.ClassSelect] = new[] { GameState.Menu, GameState.Explore },
            [GameState.Explore]     = new[] { GameState.Combat, GameState.Inventory, GameState.Menu, GameState.GameOver },
            [GameState.Combat]      = new[] { GameState.Explore, GameState.Inventory, GameState.GameOver },
            [GameState.Inventory]   = new[] { GameState.Explore, GameState.Combat },
            [GameState.GameOver]    = new[] { GameState.Menu },
        };
}
