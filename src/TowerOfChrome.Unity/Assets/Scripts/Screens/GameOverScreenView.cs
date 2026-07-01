using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Fsm;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerOfChrome.Unity.Screens
{
    /// <summary>Port of Python's GameOverScreen: a floor/kills/survivors summary shown after a
    /// party wipe, with Enter/Space/ESC returning to Menu.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameOverScreenView : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        private UIDocument _uiDocument;
        private Label _summary;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;
            _summary = root.Q<Label>("summary");
            Render();
        }

        private void Render()
        {
            var floor = gameManager.CurrentFloor;
            var kills = gameManager.EnemiesDefeated;
            var living = gameManager.Party.LivingMembers.Count;
            _summary.text = $"Floor {floor}  —  {kills} enemies defeated  —  {living}/{Party.MaxPartySize} survivors";
        }

        public void ReturnToMenu() => gameManager.SwitchTo(GameState.Menu);

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
                ReturnToMenu();
        }
    }
}
