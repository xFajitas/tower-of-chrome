using System.Collections.Generic;
using System.Linq;
using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Fsm;
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

        private List<string> _classIds = new(); // sorted by display name
        private int[] _choiceIdx = new int[Party.MaxPartySize];
        private int _slotSel;

        public int SlotSelected => _slotSel;
        public IReadOnlyList<string> ClassIds => _classIds;
        public IReadOnlyList<int> ChoiceIndex => _choiceIdx;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;
            _slotList = root.Q<VisualElement>("slot-list");
            _detailPanel = root.Q<VisualElement>("detail-panel");

            _classIds = gameManager.ClassRegistry.All().OrderBy(c => c.Name).Select(c => c.Id).ToList();

            _slotSel = 0;
            _choiceIdx = new int[Party.MaxPartySize];
            for (var i = 0; i < Party.MaxPartySize; i++)
            {
                var idx = _classIds.IndexOf(Party.DefaultClassIds[i]);
                _choiceIdx[i] = idx >= 0 ? idx : 0;
            }

            Render();
        }

        public string ClassIdForSlot(int slot) => _classIds[_choiceIdx[slot]];

        public void NavigateSlotUp()
        {
            _slotSel = (_slotSel - 1 + Party.MaxPartySize) % Party.MaxPartySize;
            Render();
        }

        public void NavigateSlotDown()
        {
            _slotSel = (_slotSel + 1) % Party.MaxPartySize;
            Render();
        }

        public void CycleClassLeft() => Cycle(-1);
        public void CycleClassRight() => Cycle(1);

        private void Cycle(int direction)
        {
            var n = _classIds.Count;
            _choiceIdx[_slotSel] = (_choiceIdx[_slotSel] + direction + n) % n;
            Render();
        }

        /// <summary>Builds the party from current selections and starts a new game.</summary>
        public void Confirm()
        {
            var starters = new List<(string Name, string ClassId)>();
            for (var i = 0; i < Party.MaxPartySize; i++)
                starters.Add((Party.DefaultNames[i], _classIds[_choiceIdx[i]]));

            gameManager.NewGame(starters);
            gameManager.SwitchTo(GameState.Explore);
        }

        public void Cancel() => gameManager.SwitchTo(GameState.Menu);

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
                var classDef = reg.Get(_classIds[_choiceIdx[i]]);
                var selected = i == _slotSel;

                var row = new VisualElement();
                row.AddToClassList("slot-row");
                if (selected)
                    row.AddToClassList("slot-row--selected");

                var nameLabel = new Label(Party.DefaultNames[i]);
                nameLabel.AddToClassList("slot-name");
                if (selected)
                    nameLabel.AddToClassList("slot-name--selected");
                row.Add(nameLabel);

                var classLabel = new Label(classDef.Name);
                classLabel.AddToClassList("slot-class");
                row.Add(classLabel);

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
            if (Input.GetKeyDown(KeyCode.UpArrow))
                NavigateSlotUp();
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                NavigateSlotDown();
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                CycleClassLeft();
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                CycleClassRight();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                Confirm();
            else if (Input.GetKeyDown(KeyCode.Escape))
                Cancel();
        }
    }
}
