using System;
using System.Collections.Generic;
using System.IO;
using TowerOfChrome.Core.Combat;
using TowerOfChrome.Core.Dungeon;
using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Core.Loot;
using TowerOfChrome.Core.Persistence;
using TowerOfChrome.Core.Rng;
using TowerOfChrome.Unity.Data;
using UnityEngine;

namespace TowerOfChrome.Unity
{
    /// <summary>
    /// Root of the Unity presentation layer. Owns one instance of every Core service (mirrors
    /// Python's Engine.__init__) and the single persistent-scene FSM: screen GameObjects are
    /// toggled active/inactive by StateMachine.OnChange rather than using additive scene
    /// loading, matching the original Engine.switch_to()/BaseScreen.on_enter()/on_exit()
    /// lifecycle 1:1.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        [Serializable]
        public struct ScreenRootMapping
        {
            public GameState State;
            public GameObject Root;
        }

        [SerializeField] private List<ScreenRootMapping> screenRoots = new();

        // ------------------------------------------------------------------
        // Core services (constructed once in Awake)
        // ------------------------------------------------------------------

        public ClassRegistry ClassRegistry { get; private set; } = null!;
        public ItemRegistry ItemRegistry { get; private set; } = null!;
        public EnemyRegistry EnemyRegistry { get; private set; } = null!;
        public AbilityRegistry AbilityRegistry { get; private set; } = null!;
        public Leveling Leveling { get; private set; } = null!;
        public DamageCalculator DamageCalculator { get; private set; } = null!;
        public LootTables LootTables { get; private set; } = null!;
        public SaveLoadService SaveLoadService { get; private set; } = null!;
        public IRandomSource Rng { get; private set; } = null!;
        public StateMachine Fsm { get; private set; } = null!;

        // ------------------------------------------------------------------
        // Game session state (mirrors Python Engine's mutable session fields)
        // ------------------------------------------------------------------

        public Party Party { get; set; } = null!;
        public BattleEngine? Battle { get; set; }
        public DungeonFloor? DungeonFloor { get; set; }
        public int CurrentFloor { get; set; } = 1;
        public int EnemiesDefeated { get; set; }
        public List<Enemy> PendingEncounter { get; } = new();
        public int CombatRoomId { get; set; } = -1;

        /// <summary>
        /// Unlike the Python original (a relative "save/savegame.json" next to the executable),
        /// this uses Application.persistentDataPath — the correct, permission-safe per-user
        /// location for a real installed Windows game (the executable's own directory is often
        /// read-only once installed under Program Files).
        /// </summary>
        private string SavePath => Path.Combine(Application.persistentDataPath, "save", "savegame.json");

        private void Awake()
        {
            var dataSource = new UnityStreamingAssetsDataSource();

            ClassRegistry = new ClassRegistry(dataSource);
            ItemRegistry = new ItemRegistry(dataSource);
            EnemyRegistry = new EnemyRegistry(dataSource);
            AbilityRegistry = new AbilityRegistry(dataSource);
            Leveling = new Leveling(dataSource);
            Rng = new SystemRandomSource();
            DamageCalculator = new DamageCalculator(dataSource.LoadCombatConfig(), Rng);
            LootTables = new LootTables(dataSource, ItemRegistry, Rng);
            SaveLoadService = new SaveLoadService(ClassRegistry, ItemRegistry, Leveling);

            Party = Party.DefaultParty(ClassRegistry, ItemRegistry, Leveling);

            Fsm = new StateMachine(GameState.Menu);
            Fsm.OnInvalidTransition = msg => Debug.LogWarning(msg);
            Fsm.OnChange((from, to) => ActivateScreenRoot(to));

            ActivateScreenRoot(Fsm.State);
        }

        private void ActivateScreenRoot(GameState state)
        {
            foreach (var mapping in screenRoots)
            {
                if (mapping.Root != null)
                    mapping.Root.SetActive(mapping.State == state);
            }
        }

        /// <summary>Looks up a screen root by state regardless of its current active state
        /// (unlike GameObject.Find, which skips inactive objects). Returns null if state isn't
        /// mapped to a root (e.g. the mapping wasn't wired up in the Inspector/scene).</summary>
        public GameObject? GetScreenRoot(GameState state)
        {
            foreach (var mapping in screenRoots)
            {
                if (mapping.State == state)
                    return mapping.Root;
            }
            return null;
        }

        public void SwitchTo(GameState state) => Fsm.Transition(state);

        /// <summary>Create a fresh BattleEngine. Uses PendingEncounter if set, else a random encounter.</summary>
        public void StartCombat()
        {
            List<Enemy> enemies;
            if (PendingEncounter.Count > 0)
            {
                enemies = new List<Enemy>(PendingEncounter);
                PendingEncounter.Clear();
            }
            else
            {
                enemies = EnemyRegistry.RandomEncounter(CurrentFloor, 3, Rng);
            }

            Battle = new BattleEngine(AbilityRegistry, DamageCalculator, Rng, LootTables);
            Battle.Start(Party, enemies, CurrentFloor);
        }

        /// <summary>Increment the floor counter and regenerate the dungeon level.</summary>
        public void AdvanceFloor()
        {
            CurrentFloor += 1;
            DungeonFloor = DungeonGenerator.GenerateFloor(CurrentFloor, Rng, LootTables);
            Battle = null;
            CombatRoomId = -1;
            SaveGame();
        }

        public bool SaveGame() =>
            SaveLoadService.SaveGame(new GameSessionState(CurrentFloor, EnemiesDefeated, Party, DungeonFloor), SavePath);

        public bool LoadGame()
        {
            if (!SaveLoadService.LoadGame(SavePath, out var state) || state == null)
                return false;

            CurrentFloor = state.CurrentFloor;
            EnemiesDefeated = state.EnemiesDefeated;
            Party = state.Party;
            DungeonFloor = state.DungeonFloor;
            Battle = null;
            CombatRoomId = -1;
            PendingEncounter.Clear();
            return true;
        }

        public bool SaveExists() => SaveLoadService.SaveExists(SavePath);

        public void DeleteSave() => SaveLoadService.DeleteSave(SavePath);
    }
}
