using System.Collections.Generic;
using System.Linq;
using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Unity.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerOfChrome.Unity.Screens
{
    /// <summary>
    /// Port of Python's ClassSelectScreen: assigns one of the 16 classes to each of the 4 fixed
    /// party slots (Party.DefaultNames), with a live detail panel for the currently-highlighted
    /// slot's class. Navigation/confirm logic is exposed as public methods separate from input
    /// polling, so tests can drive the screen directly.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ClassSelectScreenView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        private UIDocument _uiDocument;
        private VisualElement _slotList;
        private VisualElement _detailPanel;
        private Label _confirmButton;
        private Label _cancelButton;

        private List<string> _classIds = new(); // sorted by display name
        private int[] _choiceIdx = new int[Party.MaxPartySize];
        private string[] _names = new string[Party.MaxPartySize];
        private int _slotSel;

        /// <summary>Which slot's name is currently being edited, or null when not renaming.
        /// While renaming, Update()'s keyboard navigation is suspended so typing doesn't also
        /// cycle slots/classes (the classic conflict between UI Toolkit text focus and this
        /// project's Input.GetKeyDown-based polling, which knows nothing about UI focus).</summary>
        private int? _renamingSlot;

        public int SlotSelected => _slotSel;
        public IReadOnlyList<string> ClassIds => _classIds;
        public IReadOnlyList<int> ChoiceIndex => _choiceIdx;
        public IReadOnlyList<string> Names => _names;
        public bool IsRenaming => _renamingSlot.HasValue;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;
            _slotList = root.Q<VisualElement>("slot-list");
            _detailPanel = root.Q<VisualElement>("detail-panel");
            _confirmButton = root.Q<Label>("confirm-button");
            _cancelButton = root.Q<Label>("cancel-button");
            _confirmButton.RegisterCallback<ClickEvent>(_ => Confirm());
            _cancelButton.RegisterCallback<ClickEvent>(_ => Cancel());

            _classIds = gameManager.ClassRegistry.All().OrderBy(c => c.Name).Select(c => c.Id).ToList();

            _slotSel = 0;
            _renamingSlot = null;
            _choiceIdx = new int[Party.MaxPartySize];
            _names = new string[Party.MaxPartySize];
            for (var i = 0; i < Party.MaxPartySize; i++)
            {
                var idx = _classIds.IndexOf(Party.DefaultClassIds[i]);
                _choiceIdx[i] = idx >= 0 ? idx : 0;
                _names[i] = Party.DefaultNames[i];
            }

            Render();
        }

        public string ClassIdForSlot(int slot) => _classIds[_choiceIdx[slot]];

        public void SelectSlot(int slot)
        {
            if (_renamingSlot.HasValue)
                return;
            _slotSel = slot;
            Render();
        }

        /// <summary>Opens the rename text field for `slot`. Callable directly by tests/mouse
        /// clicks; Enter/ESC on the field itself call ConfirmRename/CancelRename.</summary>
        public void BeginRename(int slot)
        {
            _slotSel = slot;
            _renamingSlot = slot;
            Render();
        }

        public void ConfirmRename(string newName)
        {
            if (!_renamingSlot.HasValue)
                return;
            if (!string.IsNullOrWhiteSpace(newName))
                _names[_renamingSlot.Value] = newName.Trim();
            _renamingSlot = null;
            Render();
        }

        public void CancelRename()
        {
            _renamingSlot = null;
            Render();
        }

        public void NavigateSlotUp()
        {
            if (_renamingSlot.HasValue)
                return;
            _slotSel = (_slotSel - 1 + Party.MaxPartySize) % Party.MaxPartySize;
            Render();
        }

        public void NavigateSlotDown()
        {
            if (_renamingSlot.HasValue)
                return;
            _slotSel = (_slotSel + 1) % Party.MaxPartySize;
            Render();
        }

        public void CycleClassLeft() => Cycle(-1);
        public void CycleClassRight() => Cycle(1);

        /// <summary>Cycles the class for `slot` directly (used by per-row mouse arrows), selecting
        /// that slot first if it wasn't already selected.</summary>
        public void CycleClassForSlot(int slot, int direction)
        {
            _slotSel = slot;
            Cycle(direction);
        }

        private void Cycle(int direction)
        {
            if (_renamingSlot.HasValue)
                return;
            var n = _classIds.Count;
            _choiceIdx[_slotSel] = (_choiceIdx[_slotSel] + direction + n) % n;
            Render();
        }

        /// <summary>Builds the party from current selections and starts a new game.</summary>
        public void Confirm()
        {
            if (_renamingSlot.HasValue)
                return;
            var starters = new List<(string Name, string ClassId)>();
            for (var i = 0; i < Party.MaxPartySize; i++)
                starters.Add((_names[i], _classIds[_choiceIdx[i]]));

            gameManager.NewGame(starters);
            gameManager.SwitchTo(GameState.Explore);
        }

        public void Cancel()
        {
            if (_renamingSlot.HasValue)
                return;
            gameManager.SwitchTo(GameState.Menu);
        }

        private void Render()
        {
            RenderSlotList();
            RenderDetailPanel();
        }

        private void RenderSlotList()
        {
            _slotList.Clear();
            var reg = gameManager.ClassRegistry;

            for (var i = 0; i < Party.MaxPartySize; i++)
            {
                var slot = i; // per-iteration copy for the closures below
                var classDef = reg.Get(_classIds[_choiceIdx[slot]]);
                var selected = slot == _slotSel;

                var row = new VisualElement();
                row.AddToClassList("slot-row");
                if (selected)
                    row.AddToClassList("slot-row--selected");
                row.RegisterCallback<ClickEvent>(_ => SelectSlot(slot));

                var header = new VisualElement();
                header.AddToClassList("slot-row-header");

                if (_renamingSlot == slot)
                {
                    var nameField = new TextField { value = _names[slot], isDelayed = false };
                    nameField.AddToClassList("slot-name-input");
                    nameField.RegisterCallback<KeyDownEvent>(evt =>
                    {
                        if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                        {
                            ConfirmRename(nameField.value);
                            evt.StopPropagation();
                        }
                        else if (evt.keyCode == KeyCode.Escape)
                        {
                            CancelRename();
                            evt.StopPropagation();
                        }
                    });
                    header.Add(nameField);
                    header.schedule.Execute(() => nameField.Focus());
                }
                else
                {
                    var nameLabel = new Label(_names[slot]);
                    nameLabel.AddToClassList("slot-name");
                    if (selected)
                        nameLabel.AddToClassList("slot-name--selected");
                    // First click on an already-selected slot renames it; otherwise it just
                    // selects the slot (mirrors typical "click to select, click again to rename"
                    // file-manager UX) so a stray click never accidentally opens rename mode.
                    nameLabel.RegisterCallback<ClickEvent>(evt =>
                    {
                        if (slot == _slotSel)
                            BeginRename(slot);
                        else
                            SelectSlot(slot);
                        evt.StopPropagation();
                    });
                    header.Add(nameLabel);
                }

                row.Add(header);

                var classRow = new VisualElement();
                classRow.AddToClassList("slot-row-header");

                var leftArrow = new Label("‹");
                leftArrow.AddToClassList("cycle-arrow");
                leftArrow.RegisterCallback<ClickEvent>(evt =>
                {
                    CycleClassForSlot(slot, -1);
                    evt.StopPropagation();
                });
                classRow.Add(leftArrow);

                var classLabel = new Label(classDef.Name);
                classLabel.AddToClassList("slot-class");
                classRow.Add(classLabel);

                var rightArrow = new Label("›");
                rightArrow.AddToClassList("cycle-arrow");
                rightArrow.RegisterCallback<ClickEvent>(evt =>
                {
                    CycleClassForSlot(slot, 1);
                    evt.StopPropagation();
                });
                classRow.Add(rightArrow);

                row.Add(classRow);

                var roleLabel = new Label(FormatRole(classDef.Role));
                roleLabel.AddToClassList("slot-role");
                row.Add(roleLabel);

                _slotList.Add(row);
            }
        }

        private void RenderDetailPanel()
        {
            _detailPanel.Clear();
            var classDef = gameManager.ClassRegistry.Get(_classIds[_choiceIdx[_slotSel]]);

            var portrait = ArchetypeIcons.ForRole(classDef.Role);
            if (portrait != null)
            {
                var portraitEl = new VisualElement();
                portraitEl.AddToClassList("detail-portrait");
                portraitEl.style.backgroundImage = new StyleBackground(portrait);
                _detailPanel.Add(portraitEl);
            }

            var nameLabel = new Label(classDef.Name);
            nameLabel.AddToClassList("detail-name");
            _detailPanel.Add(nameLabel);

            var sublineLabel = new Label($"{FormatRole(classDef.Role)}   |   {classDef.ResourceName}");
            sublineLabel.AddToClassList("detail-subline");
            _detailPanel.Add(sublineLabel);

            var descLabel = new Label(classDef.Description);
            descLabel.AddToClassList("detail-description");
            _detailPanel.Add(descLabel);

            var statsHeader = new Label("BASE STATS (Lv.1)");
            statsHeader.AddToClassList("detail-section-header");
            _detailPanel.Add(statsHeader);

            var statsGrid = new VisualElement();
            statsGrid.AddToClassList("detail-stats-grid");
            var stats = classDef.AllStatsAtLevel(1);
            foreach (var key in StatKeys.All)
            {
                var statLabel = new Label($"{StatKeys.Labels[key]}: {stats[key]}");
                statLabel.AddToClassList("detail-stat");
                statsGrid.Add(statLabel);
            }
            _detailPanel.Add(statsGrid);

            var weaponsLabel = new Label($"Weapons: {(classDef.WeaponTypes.Length > 0 ? string.Join(", ", classDef.WeaponTypes) : "-")}");
            weaponsLabel.AddToClassList("detail-equip-line");
            _detailPanel.Add(weaponsLabel);

            var armorLabel = new Label($"Armor: {(classDef.ArmorTypes.Length > 0 ? string.Join(", ", classDef.ArmorTypes) : "-")}");
            armorLabel.AddToClassList("detail-equip-line");
            _detailPanel.Add(armorLabel);
        }

        private static string FormatRole(string role)
        {
            var parts = role.Split('_');
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join(" ", parts);
        }

        private void Update()
        {
            // While renaming, the TextField itself owns all keyboard input (including Enter/ESC,
            // wired directly on it above) -- Input.GetKeyDown polling below knows nothing about
            // UI Toolkit focus, so it must stay out of the way entirely rather than rely solely
            // on each method's internal _renamingSlot guard.
            if (_renamingSlot.HasValue)
                return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
                NavigateSlotUp();
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                NavigateSlotDown();
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                CycleClassLeft();
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                CycleClassRight();
            else if (Input.GetKeyDown(KeyCode.N))
                BeginRename(_slotSel);
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                Confirm();
            else if (Input.GetKeyDown(KeyCode.Escape))
                Cancel();
        }
    }
}
