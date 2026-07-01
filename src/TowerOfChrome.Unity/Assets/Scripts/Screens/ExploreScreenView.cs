using System.Collections.Generic;
using TowerOfChrome.Core.Combat;
using TowerOfChrome.Core.Dungeon;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Unity.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerOfChrome.Unity.Screens
{
    public enum ExploreUiState
    {
        Navigating,
        StairsConfirm,
        LootShow,
    }

    /// <summary>
    /// Port of Python's ExploreScreen: room-node dungeon map, room interaction (encounter/boss/
    /// treasure/stairs), stairs-confirm and loot-show modals. Navigation/interaction logic is
    /// public methods separate from Input polling, so tests can drive the screen directly.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ExploreScreenView : MonoBehaviour
    {
        private static readonly Dictionary<RoomType, string> RoomActionHint = new()
        {
            [RoomType.Start] = "Starting room.",
            [RoomType.Normal] = "Nothing of note here.",
            [RoomType.Encounter] = "Enemies lurk here.  [Enter] to fight.",
            [RoomType.Treasure] = "A chest waits.  [Enter] to open.",
            [RoomType.Boss] = "A powerful enemy!  [Enter] to fight.",
            [RoomType.Stairs] = "The stairs descend.  [Enter] to proceed.",
        };

        private static readonly Dictionary<RoomType, Color> RoomColors = new()
        {
            [RoomType.Start] = new Color(40 / 255f, 120 / 255f, 40 / 255f),
            [RoomType.Normal] = new Color(45 / 255f, 55 / 255f, 75 / 255f),
            [RoomType.Encounter] = new Color(110 / 255f, 38 / 255f, 38 / 255f),
            [RoomType.Treasure] = new Color(110 / 255f, 95 / 255f, 25 / 255f),
            [RoomType.Boss] = new Color(110 / 255f, 25 / 255f, 110 / 255f),
            [RoomType.Stairs] = new Color(25 / 255f, 110 / 255f, 110 / 255f),
        };

        private static readonly Dictionary<RoomType, string> RoomLabels = new()
        {
            [RoomType.Start] = "START",
            [RoomType.Normal] = "ROOM",
            [RoomType.Encounter] = "FIGHT",
            [RoomType.Treasure] = "CHEST",
            [RoomType.Boss] = "BOSS",
            [RoomType.Stairs] = "EXIT",
        };

        [SerializeField] private GameManager gameManager;

        private UIDocument _uiDocument;
        private VisualElement _mapArea;
        private VisualElement _hudContainer;
        private Label _header;
        private Label _infoStrip;
        private VisualElement _stairsModal;
        private VisualElement _lootModal;
        private VisualElement _lootLinesContainer;
        private Label _stairsConfirmButton;
        private Label _stairsCancelButton;
        private Label _lootContinueButton;

        public ExploreUiState State { get; private set; } = ExploreUiState.Navigating;

        private string _msg = "";
        private float _msgTimer;
        private List<string> _lootLines = new();

        public string Message => _msg;
        public IReadOnlyList<string> LootLines => _lootLines;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;
            _header = root.Q<Label>("header");
            _mapArea = root.Q<VisualElement>("map-area");
            _hudContainer = root.Q<VisualElement>("hud-container");
            _infoStrip = root.Q<Label>("info-strip");
            _stairsModal = root.Q<VisualElement>("stairs-modal");
            _lootModal = root.Q<VisualElement>("loot-modal");
            _lootLinesContainer = root.Q<VisualElement>("loot-lines");
            _stairsConfirmButton = root.Q<Label>("stairs-confirm-button");
            _stairsCancelButton = root.Q<Label>("stairs-cancel-button");
            _lootContinueButton = root.Q<Label>("loot-continue-button");
            _stairsConfirmButton.RegisterCallback<ClickEvent>(_ => ConfirmStairs());
            _stairsCancelButton.RegisterCallback<ClickEvent>(_ => CancelStairs());
            _lootContinueButton.RegisterCallback<ClickEvent>(_ => DismissLoot());

            _mapArea.style.width = DungeonGenerator.MapW;
            _mapArea.style.height = DungeonGenerator.MapH;

            // Generate floor on first entry or when the floor number advances.
            if (gameManager.DungeonFloor == null || gameManager.DungeonFloor.FloorNumber != gameManager.CurrentFloor)
                gameManager.DungeonFloor = DungeonGenerator.GenerateFloor(gameManager.CurrentFloor, gameManager.Rng, gameManager.LootTables);

            // Mark the combat room cleared when returning from a Victory.
            if (gameManager.Battle != null && gameManager.Battle.Phase == BattlePhase.Victory)
            {
                gameManager.EnemiesDefeated += gameManager.Battle.GetEnemyList().Count;
                var rid = gameManager.CombatRoomId;
                var df = gameManager.DungeonFloor;
                if (rid >= 0 && df.Rooms.ContainsKey(rid))
                    df.Rooms[rid].Cleared = true;
                gameManager.CombatRoomId = -1;
            }

            State = ExploreUiState.Navigating;
            _msg = "";
            _msgTimer = 0f;
            _lootLines.Clear();

            Render();
        }

        // ------------------------------------------------------------------
        // Navigation / interaction (mirrors ExploreScreen.handle_event's NAVIGATING branch)
        // ------------------------------------------------------------------

        /// <summary>Mouse equivalent of the arrow-key movement/interact model: clicking the
        /// current room interacts with it, clicking a directly-connected room steps into it
        /// (one room per click, same as one room per key press -- no multi-room path-clicking).</summary>
        public void ClickRoom(int roomId)
        {
            if (State != ExploreUiState.Navigating)
                return;

            var df = gameManager.DungeonFloor;
            if (df == null || !df.Rooms.TryGetValue(roomId, out var room))
                return;

            if (roomId == df.PlayerRoomId)
            {
                Interact();
                return;
            }

            if (df.CurrentRoom.Connections.Contains(roomId))
            {
                df.MoveTo(roomId);
                OnEnterRoom(room);
                Render();
            }
        }

        public void MoveDirection(string direction)
        {
            if (State != ExploreUiState.Navigating)
                return;

            var df = gameManager.DungeonFloor;
            if (df == null)
                return;

            var rid = df.RoomInDirection(direction);
            if (rid.HasValue)
            {
                df.MoveTo(rid.Value);
                OnEnterRoom(df.Rooms[rid.Value]);
            }
            Render();
        }

        /// <summary>Called whenever the player steps into a new room — flavor flash text only,
        /// never auto-triggers combat/loot (the player must press Enter/Interact).</summary>
        private void OnEnterRoom(Room room)
        {
            if (room.RoomType == RoomType.Encounter && !room.Cleared)
                Flash("Enemies nearby!  Press Enter to engage.");
            else if (room.RoomType == RoomType.Boss && !room.Cleared)
                Flash("A powerful enemy blocks the way!  Press Enter to fight.");
            else if (room.RoomType == RoomType.Treasure && !room.Cleared)
                Flash("A treasure chest!  Press Enter to open.");
            else if (room.RoomType == RoomType.Stairs)
                Flash(gameManager.DungeonFloor.CanAccessStairs()
                    ? "The stairs descend.  Press Enter to go deeper."
                    : "The boss guards the way!  Defeat it first.");
            else if (room.RoomType == RoomType.Boss && room.Cleared)
                Flash("Boss defeated.  Press Enter to take the stairs.");
        }

        /// <summary>Enter/Space pressed while standing in a room.</summary>
        public void Interact()
        {
            if (State != ExploreUiState.Navigating)
                return;

            var df = gameManager.DungeonFloor;
            if (df == null)
                return;

            var room = df.CurrentRoom;

            if (room.RoomType == RoomType.Encounter && !room.Cleared)
            {
                StartEncounter(room.Id);
            }
            else if (room.RoomType == RoomType.Boss && !room.Cleared)
            {
                var bossId = BossEnemyId();
                var boss = gameManager.EnemyRegistry.Spawn(bossId, gameManager.CurrentFloor);
                gameManager.PendingEncounter.Clear();
                gameManager.PendingEncounter.Add(boss);
                StartEncounter(room.Id);
            }
            else if (room.RoomType == RoomType.Boss && room.Cleared)
            {
                State = ExploreUiState.StairsConfirm;
                Render();
            }
            else if (room.RoomType == RoomType.Treasure && !room.Cleared)
            {
                OpenTreasure(room);
            }
            else if (room.RoomType == RoomType.Stairs)
            {
                if (df.CanAccessStairs())
                    State = ExploreUiState.StairsConfirm;
                else
                    Flash("Defeat all bosses first!");
                Render();
            }
            else
            {
                Flash(RoomActionHint.GetValueOrDefault(room.RoomType, ""));
                Render();
            }
        }

        private void StartEncounter(int roomId)
        {
            gameManager.CombatRoomId = roomId;
            gameManager.SwitchTo(GameState.Combat);
        }

        private void OpenTreasure(Room room)
        {
            var log = gameManager.LootTables.AwardDrops(gameManager.Party, room.Loot);
            room.Cleared = true;
            _lootLines = log.Count > 0 ? log : new List<string> { "  The chest was empty." };
            State = ExploreUiState.LootShow;
            Render();
        }

        private string BossEnemyId() => gameManager.CurrentFloor % 10 == 0 ? "circuit_mage" : "nexus_core";

        private void Flash(string msg, float dur = 2.5f)
        {
            _msg = msg;
            _msgTimer = dur;
        }

        public void ConfirmStairs()
        {
            if (State != ExploreUiState.StairsConfirm)
                return;
            gameManager.AdvanceFloor();
            State = ExploreUiState.Navigating;
            Render();
        }

        public void CancelStairs()
        {
            if (State != ExploreUiState.StairsConfirm)
                return;
            State = ExploreUiState.Navigating;
            Render();
        }

        public void DismissLoot()
        {
            if (State != ExploreUiState.LootShow)
                return;
            State = ExploreUiState.Navigating;
            _lootLines.Clear();
            Render();
        }

        public void OpenInventory()
        {
            if (State == ExploreUiState.Navigating)
                gameManager.SwitchTo(GameState.Inventory);
        }

        public void ReturnToMenu()
        {
            if (State == ExploreUiState.Navigating)
                gameManager.SwitchTo(GameState.Menu);
        }

        // ------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------

        private void Render()
        {
            var df = gameManager.DungeonFloor;

            if (df != null)
            {
                var total = 0;
                var cleared = 0;
                foreach (var r in df.Rooms.Values)
                {
                    if (r.RoomType == RoomType.Encounter || r.RoomType == RoomType.Boss)
                    {
                        total++;
                        if (r.Cleared)
                            cleared++;
                    }
                }
                _header.text = $"[ TOWER OF CHROME  —  Floor {df.FloorNumber} ]    {cleared}/{total} fights";
                RenderMap(df);
            }

            var livingCount = gameManager.Party.LivingMembers.Count;
            var totalCount = gameManager.Party.AllMembers.Count;
            _infoStrip.text = string.IsNullOrEmpty(_msg)
                ? $"Party: {livingCount}/{totalCount} alive    Floor {gameManager.CurrentFloor}"
                : _msg;

            PartyHudBuilder.Render(_hudContainer, gameManager.Party);

            _stairsModal.EnableInClassList("modal-overlay--visible", State == ExploreUiState.StairsConfirm);
            _lootModal.EnableInClassList("modal-overlay--visible", State == ExploreUiState.LootShow);

            if (State == ExploreUiState.LootShow)
            {
                _lootLinesContainer.Clear();
                foreach (var line in _lootLines)
                {
                    var label = new Label(line);
                    label.AddToClassList("modal-line");
                    _lootLinesContainer.Add(label);
                }
            }
        }

        private void RenderMap(DungeonFloor df)
        {
            _mapArea.Clear();

            // Corridors first, so room nodes render on top.
            foreach (var (a, b) in df.Corridors)
            {
                if (!df.Rooms.TryGetValue(a, out var roomA) || !df.Rooms.TryGetValue(b, out var roomB))
                    continue;

                var (x1, y1) = roomA.Center;
                var (x2, y2) = roomB.Center;
                var dx = x2 - x1;
                var dy = y2 - y1;
                var length = Mathf.Sqrt(dx * dx + dy * dy);
                var angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

                var line = new VisualElement();
                line.AddToClassList("corridor-line");
                line.style.left = x1;
                line.style.top = y1 - 1f;
                line.style.width = length;
                line.style.height = 2f;
                line.style.transformOrigin = new StyleTransformOrigin(new TransformOrigin(Length.Percent(0), Length.Percent(50)));
                line.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
                _mapArea.Add(line);
            }

            foreach (var room in df.Rooms.Values)
            {
                var node = new VisualElement();
                node.AddToClassList("room-node");
                node.style.left = room.X;
                node.style.top = room.Y;
                node.style.width = room.W;
                node.style.height = room.H;
                node.style.backgroundColor = RoomColors[room.RoomType];

                var isCurrent = room.Id == df.PlayerRoomId;
                if (isCurrent)
                    node.AddToClassList("room-node--current");
                else if (room.Visited)
                    node.AddToClassList("room-node--visited");
                else
                    node.AddToClassList("room-node--unvisited");

                var icon = RoomIcons.Get(room.RoomType);
                if (icon != null)
                {
                    var iconEl = new VisualElement();
                    iconEl.AddToClassList("room-node-icon");
                    iconEl.style.backgroundImage = new StyleBackground(icon);
                    node.Add(iconEl);
                }

                var label = new Label(RoomLabels[room.RoomType]);
                label.AddToClassList("room-node-label");
                node.Add(label);

                node.RegisterCallback<ClickEvent>(_ => ClickRoom(room.Id));

                _mapArea.Add(node);
            }
        }

        private void Update()
        {
            if (_msgTimer > 0f)
            {
                _msgTimer = Mathf.Max(0f, _msgTimer - Time.deltaTime);
                if (_msgTimer == 0f)
                {
                    _msg = "";
                    Render();
                }
            }

            if (State == ExploreUiState.StairsConfirm)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                    ConfirmStairs();
                else if (Input.GetKeyDown(KeyCode.Escape))
                    CancelStairs();
                return;
            }

            if (State == ExploreUiState.LootShow)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
                    DismissLoot();
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                MoveDirection("right");
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                MoveDirection("left");
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                MoveDirection("down");
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                MoveDirection("up");
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                Interact();
            else if (Input.GetKeyDown(KeyCode.I))
                OpenInventory();
            else if (Input.GetKeyDown(KeyCode.Escape))
                ReturnToMenu();
        }
    }
}
