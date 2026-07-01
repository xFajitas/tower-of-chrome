using TowerOfChrome.Core.Dungeon;
using TowerOfChrome.Core.Entities;

namespace TowerOfChrome.Core.Persistence;

/// <summary>The subset of Engine state that gets persisted. Mirrors what core/save_load.py's
/// save_game()/load_game() read from/write onto the Python Engine instance. Notably, mid-combat
/// state (battle, combat_room_id, pending_encounter) is never part of this — saves only ever
/// happen between encounters, and loading always resumes in Explore.</summary>
public sealed class GameSessionState
{
    public int CurrentFloor { get; }
    public int EnemiesDefeated { get; }
    public Party Party { get; }
    public DungeonFloor? DungeonFloor { get; }

    public GameSessionState(int currentFloor, int enemiesDefeated, Party party, DungeonFloor? dungeonFloor)
    {
        CurrentFloor = currentFloor;
        EnemiesDefeated = enemiesDefeated;
        Party = party;
        DungeonFloor = dungeonFloor;
    }
}

public sealed class SaveMetadata
{
    public int Floor { get; }
    public SaveMetadata(int floor) => Floor = floor;
}
