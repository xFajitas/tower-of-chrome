namespace TowerOfChrome.Core.Fsm;

/// <summary>Adjacency table — only listed edges are permitted. Mirrors Python's TRANSITIONS dict exactly.</summary>
public static class Transitions
{
    public static readonly IReadOnlyDictionary<GameState, IReadOnlyList<GameState>> Map =
        new Dictionary<GameState, IReadOnlyList<GameState>>
        {
            [GameState.Menu]        = new[] { GameState.ClassSelect, GameState.GameOver },
            [GameState.ClassSelect] = new[] { GameState.Menu, GameState.Explore },
            [GameState.Explore]     = new[] { GameState.Combat, GameState.Inventory, GameState.Menu, GameState.GameOver },
            [GameState.Combat]      = new[] { GameState.Explore, GameState.Inventory, GameState.GameOver },
            [GameState.Inventory]   = new[] { GameState.Explore, GameState.Combat },
            [GameState.GameOver]    = new[] { GameState.Menu },
        };
}
