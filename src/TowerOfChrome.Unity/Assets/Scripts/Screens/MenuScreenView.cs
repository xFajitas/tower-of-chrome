using System.Collections.Generic;
using TowerOfChrome.Core.Fsm;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerOfChrome.Unity.Screens
{
    /// <summary>
    /// Port of Python's MenuScreen. Navigation/activation logic is exposed as public methods
    /// (NavigateUp/NavigateDown/Activate) separate from input polling in Update(), so tests can
    /// drive the screen directly without needing to simulate real keyboard input.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuScreenView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        private UIDocument _uiDocument;
        private VisualElement _itemsContainer;

        // (label, action) pairs, mirroring Python's self._items.
        private readonly List<(string Label, string Action)> _items = new();
        private int _selected;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;
            _itemsContainer = root.Q<VisualElement>("items-container");
            BuildItems();
        }

        /// <summary>Rebuilds the item list — "Continue (Floor N)" only appears if a save exists,
        /// matching Python's MenuScreen.on_enter().</summary>
        public void BuildItems()
        {
            _items.Clear();

            var meta = gameManager.GetSaveMetadata();
            if (meta != null)
                _items.Add(($"Continue   (Floor {meta.Floor})", "continue"));

            _items.Add(("New Game", "new"));
            _items.Add(("Quit", "quit"));

            _selected = 0;
            Render();
        }

        public IReadOnlyList<(string Label, string Action)> Items => _items;
        public int Selected => _selected;

        public void NavigateUp()
        {
            _selected = (_selected - 1 + _items.Count) % _items.Count;
            Render();
        }

        public void NavigateDown()
        {
            _selected = (_selected + 1) % _items.Count;
            Render();
        }

        public void Activate()
        {
            if (_items.Count == 0)
                return;

            var (_, action) = _items[_selected];
            switch (action)
            {
                case "quit":
                    Application.Quit();
                    break;
                case "new":
                    gameManager.SwitchTo(GameState.ClassSelect);
                    break;
                case "continue":
                    if (gameManager.LoadGame())
                        gameManager.SwitchTo(GameState.Explore);
                    break;
            }
        }

        private void Render()
        {
            _itemsContainer.Clear();
            for (var i = 0; i < _items.Count; i++)
            {
                var index = i; // per-iteration copy -- a `for` loop variable is shared across
                                // iterations in C#, so capturing `i` directly would make every
                                // click handler see its final value instead of its own row.
                var label = new Label(_items[i].Label);
                label.AddToClassList("menu-item");
                if (i == _selected)
                    label.AddToClassList("menu-item--selected");

                // Hover just toggles a class on the *existing* elements -- it must not call
                // Render() (which destroys and recreates every Label). StandaloneInputModule
                // re-picks whatever is under the pointer every frame, so replacing the element a
                // stationary cursor is hovering makes it look "new" again, re-firing
                // MouseEnterEvent -> Render() -> replace -> re-fire, forever, as long as the
                // mouse sits still over an item (this is what made items flicker/vanish).
                label.RegisterCallback<MouseEnterEvent>(_ => SetSelected(index));
                label.RegisterCallback<ClickEvent>(_ =>
                {
                    _selected = index;
                    Activate();
                });

                _itemsContainer.Add(label);
            }
        }

        private void SetSelected(int index)
        {
            _selected = index;
            for (var i = 0; i < _itemsContainer.childCount; i++)
                _itemsContainer[i].EnableInClassList("menu-item--selected", i == _selected);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
                NavigateUp();
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                NavigateDown();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                Activate();
        }
    }
}
