using System.Collections.Generic;
using System.Linq;
using TowerOfChrome.Core.Combat;
using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Core.Loot;
using TowerOfChrome.Unity.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerOfChrome.Unity.Screens
{
    public enum CombatUiState
    {
        PlayerMain,
        PlayerAbility,
        PlayerTarget,
        Resolving,
        EnemyTurn,
        Victory,
        Defeat,
    }

    /// <summary>
    /// Port of Python's CombatScreen: main action menu (Attack/Abilities/Defend/Flee), ability
    /// list with a live description line, target picker, resolve/enemy-turn pacing, and
    /// victory/defeat overlays. Navigation/submission logic is exposed as public methods
    /// separate from Input polling, so tests can drive full battles directly without waiting
    /// on real time via the resolve-delay timer (see ForceAdvance).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CombatScreenView : MonoBehaviour
    {
        private static readonly string[] MainActions = { "Attack", "Abilities", "Defend", "Flee" };

        private const float ResolveDelay = 0.55f;

        [SerializeField] private GameManager gameManager;

        private UIDocument _uiDocument;
        private Label _header;
        private Label _turnQueue;
        private VisualElement _enemiesList;
        private VisualElement _logLines;
        private VisualElement _hudContainer;
        private VisualElement _actionArea;
        private Label _actionHint;
        private VisualElement _victoryModal;
        private VisualElement _xpLines;
        private VisualElement _lootLines;
        private VisualElement _defeatModal;
        private Label _victoryContinueButton;
        private Label _defeatContinueButton;

        private float _resolveTimer;

        // Populated right before each Render() call that follows an action resolving, so the
        // hit-reaction shake can find the freshly-built rows for whoever acted/was hit.
        private Combatant _lastActor;
        private List<Combatant> _lastHitTargets = new();

        public CombatUiState State { get; private set; } = CombatUiState.PlayerMain;
        public int MainSelected { get; private set; }
        public int AbilitySelected { get; private set; }
        public int TargetSelected { get; private set; }
        public string PendingActionType { get; private set; } = "attack";

        private BattleEngine Battle => gameManager.Battle;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;
            _header = root.Q<Label>("header");
            _turnQueue = root.Q<Label>("turn-queue");
            _enemiesList = root.Q<VisualElement>("enemies-list");
            _logLines = root.Q<VisualElement>("log-lines");
            _hudContainer = root.Q<VisualElement>("hud-container");
            _actionArea = root.Q<VisualElement>("action-area");
            _actionHint = root.Q<Label>("action-hint");
            _victoryModal = root.Q<VisualElement>("victory-modal");
            _xpLines = root.Q<VisualElement>("xp-lines");
            _lootLines = root.Q<VisualElement>("loot-lines");
            _defeatModal = root.Q<VisualElement>("defeat-modal");
            _victoryContinueButton = root.Q<Label>("victory-continue-button");
            _defeatContinueButton = root.Q<Label>("defeat-continue-button");
            _victoryContinueButton.RegisterCallback<ClickEvent>(_ => ContinueFromTerminal());
            _defeatContinueButton.RegisterCallback<ClickEvent>(_ => ContinueFromTerminal());

            // Mirrors Python's CombatScreen.on_enter: always starts a fresh encounter. The
            // caller (ExploreScreenView) only queues PendingEncounter/CombatRoomId and switches
            // state -- starting the BattleEngine itself is this screen's job.
            gameManager.StartCombat();

            State = Battle.IsPlayerTurn ? CombatUiState.PlayerMain : CombatUiState.EnemyTurn;
            MainSelected = 0;
            AbilitySelected = 0;
            TargetSelected = 0;
            PendingActionType = "attack";
            _resolveTimer = 0f;

            Render();
        }

        // ------------------------------------------------------------------
        // Main menu
        // ------------------------------------------------------------------

        public void NavigateMainUp()
        {
            if (State != CombatUiState.PlayerMain)
                return;
            MainSelected = (MainSelected - 1 + MainActions.Length) % MainActions.Length;
            Render();
        }

        public void NavigateMainDown()
        {
            if (State != CombatUiState.PlayerMain)
                return;
            MainSelected = (MainSelected + 1) % MainActions.Length;
            Render();
        }

        public void ActivateMain(int index)
        {
            if (State != CombatUiState.PlayerMain)
                return;

            var actor = Battle.CurrentActor;
            switch (MainActions[index])
            {
                case "Attack":
                    PendingActionType = "attack";
                    TargetSelected = 0;
                    State = CombatUiState.PlayerTarget;
                    break;
                case "Abilities":
                    AbilitySelected = 0;
                    State = CombatUiState.PlayerAbility;
                    break;
                case "Defend":
                    SubmitAndWait(new CombatAction(actor, ActionType.Defend));
                    return;
                case "Flee":
                    SubmitAndWait(new CombatAction(actor, ActionType.Flee));
                    return;
            }
            Render();
        }

        // ------------------------------------------------------------------
        // Ability list
        // ------------------------------------------------------------------

        private List<string> CurrentActorAbilities()
        {
            var actor = Battle.CurrentActor;
            return actor is Character character ? character.ClassDef.Abilities.ToList() : new List<string>();
        }

        public void NavigateAbilityUp()
        {
            if (State != CombatUiState.PlayerAbility)
                return;
            var abilities = CurrentActorAbilities();
            if (abilities.Count == 0)
                return;
            AbilitySelected = (AbilitySelected - 1 + abilities.Count) % abilities.Count;
            Render();
        }

        public void NavigateAbilityDown()
        {
            if (State != CombatUiState.PlayerAbility)
                return;
            var abilities = CurrentActorAbilities();
            if (abilities.Count == 0)
                return;
            AbilitySelected = (AbilitySelected + 1) % abilities.Count;
            Render();
        }

        public void CancelAbility()
        {
            if (State != CombatUiState.PlayerAbility)
                return;
            State = CombatUiState.PlayerMain;
            Render();
        }

        public void SelectAbility()
        {
            if (State != CombatUiState.PlayerAbility)
                return;

            var actor = Battle.CurrentActor;
            if (actor is not Character)
                return;

            var abilities = CurrentActorAbilities();
            if (abilities.Count == 0)
                return;

            var abId = abilities[AbilitySelected];
            var ab = gameManager.AbilityRegistry.Get(abId);
            PendingActionType = abId;

            if (ab.Targeting is "SELF" or "ALL_ALLIES" or "ALL_ENEMIES")
            {
                var targets = ab.Targeting switch
                {
                    "SELF" => new List<Combatant> { actor },
                    "ALL_ALLIES" => Battle.LivingParty,
                    _ => Battle.LivingEnemies,
                };
                SubmitAndWait(new CombatAction(actor, ActionType.Ability, abId, targets));
                return;
            }

            TargetSelected = 0;
            State = CombatUiState.PlayerTarget;
            Render();
        }

        // ------------------------------------------------------------------
        // Target picker
        // ------------------------------------------------------------------

        public List<Combatant> CurrentTargetList()
        {
            if (PendingActionType == "attack")
                return Battle.LivingEnemies;

            var ab = gameManager.AbilityRegistry.Get(PendingActionType);
            return ab.Targeting is "SINGLE_ENEMY" or "ALL_ENEMIES" ? Battle.LivingEnemies : Battle.LivingParty;
        }

        public void NavigateTargetUp()
        {
            if (State != CombatUiState.PlayerTarget)
                return;
            var targets = CurrentTargetList();
            if (targets.Count == 0)
            {
                State = CombatUiState.PlayerMain;
                Render();
                return;
            }
            TargetSelected = (TargetSelected - 1 + targets.Count) % targets.Count;
            Render();
        }

        public void NavigateTargetDown()
        {
            if (State != CombatUiState.PlayerTarget)
                return;
            var targets = CurrentTargetList();
            if (targets.Count == 0)
            {
                State = CombatUiState.PlayerMain;
                Render();
                return;
            }
            TargetSelected = (TargetSelected + 1) % targets.Count;
            Render();
        }

        public void CancelTarget()
        {
            if (State != CombatUiState.PlayerTarget)
                return;
            State = PendingActionType == "attack" ? CombatUiState.PlayerMain : CombatUiState.PlayerAbility;
            Render();
        }

        public void ConfirmTarget()
        {
            if (State != CombatUiState.PlayerTarget)
                return;

            var targets = CurrentTargetList();
            if (targets.Count == 0)
            {
                State = CombatUiState.PlayerMain;
                Render();
                return;
            }

            var target = targets[TargetSelected % targets.Count];
            var actor = Battle.CurrentActor;
            var action = PendingActionType == "attack"
                ? new CombatAction(actor, ActionType.Attack, targets: new List<Combatant> { target })
                : new CombatAction(actor, ActionType.Ability, PendingActionType, new List<Combatant> { target });
            SubmitAndWait(action);
        }

        // ------------------------------------------------------------------
        // Submission / pacing
        // ------------------------------------------------------------------

        private void SubmitAndWait(CombatAction action)
        {
            var result = Battle.SubmitPlayerAction(action);
            _resolveTimer = 0f;
            State = CombatUiState.Resolving;
            _lastActor = action.Actor;
            _lastHitTargets = result.Hits.Select(h => h.Target).ToList();
            Render();
            PlayHitFx();
        }

        /// <summary>Mirrors Python's _after_player_action, run once the resolve delay elapses.</summary>
        private void AfterPlayerAction()
        {
            if (Battle.Phase != BattlePhase.Ongoing)
                return;
            if (Battle.IsPlayerTurn)
            {
                State = CombatUiState.PlayerMain;
                MainSelected = 0;
            }
            else
            {
                State = CombatUiState.EnemyTurn;
                _resolveTimer = 0f;
            }
            Render();
        }

        /// <summary>Mirrors Python's _do_enemy_turn, run once the resolve delay elapses.</summary>
        private void DoEnemyTurn()
        {
            if (Battle.Phase != BattlePhase.Ongoing)
                return;
            if (Battle.IsPlayerTurn)
            {
                State = CombatUiState.PlayerMain;
                MainSelected = 0;
            }
            else
            {
                var result = Battle.AdvanceEnemyTurn();
                _resolveTimer = 0f;
                _lastActor = result.Action.Actor;
                _lastHitTargets = result.Hits.Select(h => h.Target).ToList();
                Render();
                PlayHitFx();
                return;
            }
            Render();
        }

        /// <summary>Shakes the rows of whoever was hit by the action just resolved (looked up by
        /// name after Render() has rebuilt the rows fresh -- see PartyHudBuilder.RowName).</summary>
        private void PlayHitFx()
        {
            foreach (var target in _lastHitTargets)
            {
                var rowName = PartyHudBuilder.RowName(target.CombatantId());
                var row = _enemiesList.Q(rowName) ?? _hudContainer.Q(rowName);
                CombatFx.Shake(row);
            }
        }

        /// <summary>Test-only bypass for the real-time resolve delay: immediately runs whichever
        /// transition Update() would have run once its timer elapsed.</summary>
        public void ForceAdvance()
        {
            if (State == CombatUiState.Resolving)
                AfterPlayerAction();
            else if (State == CombatUiState.EnemyTurn)
                DoEnemyTurn();
        }

        public void ContinueFromTerminal()
        {
            if (Battle.Phase == BattlePhase.Victory)
                gameManager.SwitchTo(GameState.Explore);
            else if (Battle.Phase == BattlePhase.Defeat)
                gameManager.SwitchTo(GameState.GameOver);
        }

        // ------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------

        private void Render()
        {
            var battle = Battle;
            var actor = battle.CurrentActor;
            var actorTxt = actor != null ? $"  {actor.Name}'s turn" : "";
            _header.text = $"COMBAT — Floor {gameManager.CurrentFloor}   Round {battle.Round}{actorTxt}";

            var queue = battle.TurnQueue;
            _turnQueue.text = queue.Count > 0 ? "Next: " + string.Join("  ->  ", queue.Select(c => c.Name)) : "";

            RenderEnemies(battle);
            RenderLog(battle);
            PartyHudBuilder.Render(_hudContainer, gameManager.Party, gameManager.ItemRegistry);
            RenderActionArea(battle);

            _victoryModal.EnableInClassList("modal-overlay--visible", State == CombatUiState.Victory);
            _defeatModal.EnableInClassList("modal-overlay--visible", State == CombatUiState.Defeat);
            if (State == CombatUiState.Victory)
                RenderVictory(battle);
        }

        private void RenderEnemies(BattleEngine battle)
        {
            _enemiesList.Clear();
            var targets = State == CombatUiState.PlayerTarget ? CurrentTargetList() : new List<Combatant>();

            foreach (var enemy in battle.GetEnemyList())
            {
                var row = new VisualElement();
                row.name = PartyHudBuilder.RowName(enemy.CombatantId());
                row.AddToClassList("enemy-row");

                var selected = State == CombatUiState.PlayerTarget && targets.Contains(enemy) &&
                               targets[TargetSelected % Mathf.Max(1, targets.Count)] == enemy;
                if (selected)
                    row.AddToClassList("enemy-row--selected");

                // Clicking an enemy while picking a target attacks/targets it directly, instead
                // of needing the text target-list below the action menu.
                if (!enemy.IsKo)
                {
                    row.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (State != CombatUiState.PlayerTarget)
                            return;
                        var idx = CurrentTargetList().IndexOf(enemy);
                        if (idx < 0)
                            return;
                        TargetSelected = idx;
                        ConfirmTarget();
                    });
                }

                var avatarTex = ArchetypeIcons.EnemyBase();
                if (avatarTex != null)
                {
                    var avatar = new VisualElement();
                    avatar.AddToClassList("enemy-avatar");
                    avatar.style.backgroundImage = new StyleBackground(avatarTex);
                    avatar.style.unityBackgroundImageTintColor = ArchetypeIcons.EnemyTint(enemy.EnemyDef.Id);
                    row.Add(avatar);
                }

                var nameLabel = new Label(enemy.Name);
                nameLabel.AddToClassList("enemy-name");
                if (enemy.IsKo)
                    nameLabel.AddToClassList("enemy-name--ko");
                row.Add(nameLabel);

                if (enemy.IsKo)
                {
                    var koLabel = new Label("K.O.");
                    koLabel.AddToClassList("enemy-hp-text");
                    row.Add(koLabel);
                }
                else
                {
                    var barBg = new VisualElement();
                    barBg.AddToClassList("enemy-hp-bar-bg");
                    var barFill = new VisualElement();
                    barFill.AddToClassList("enemy-hp-bar-fill");
                    barFill.style.width = new StyleLength(Length.Percent((float)(System.Math.Clamp(enemy.HpFraction, 0.0, 1.0) * 100.0)));
                    barBg.Add(barFill);
                    row.Add(barBg);

                    var hpLabel = new Label($"{enemy.CurrentHp}/{enemy.MaxHp}");
                    hpLabel.AddToClassList("enemy-hp-text");
                    row.Add(hpLabel);

                    if (enemy.StatusEffects.Count > 0)
                    {
                        var statusLabel = new Label(string.Join(", ", enemy.StatusEffects));
                        statusLabel.AddToClassList("enemy-status-text");
                        row.Add(statusLabel);
                    }
                }

                _enemiesList.Add(row);
            }
        }

        private void RenderLog(BattleEngine battle)
        {
            _logLines.Clear();
            foreach (var line in battle.LastLogLines)
            {
                var label = new Label(line);
                label.AddToClassList("log-line");
                _logLines.Add(label);
            }
        }

        private void RenderActionArea(BattleEngine battle)
        {
            _actionArea.Clear();
            _actionHint.text = "";

            if (battle.Phase != BattlePhase.Ongoing)
                return;

            switch (State)
            {
                case CombatUiState.PlayerMain:
                    RenderMainMenu();
                    break;
                case CombatUiState.PlayerAbility:
                    RenderAbilityMenu(battle);
                    break;
                case CombatUiState.PlayerTarget:
                    RenderTargetMenu();
                    break;
                case CombatUiState.Resolving:
                case CombatUiState.EnemyTurn:
                    var label = new Label(State == CombatUiState.Resolving ? "Resolving..." : "Enemy turn...");
                    label.AddToClassList("action-item");
                    _actionArea.Add(label);
                    break;
            }
        }

        private void RenderMainMenu()
        {
            var row = new VisualElement();
            row.AddToClassList("action-row");
            for (var i = 0; i < MainActions.Length; i++)
            {
                var index = i; // per-iteration copy for the click closure
                var cell = new Label($"[{i + 1}] {MainActions[i]}");
                cell.AddToClassList("action-item");
                cell.AddToClassList("action-cell");
                if (i == MainSelected)
                    cell.AddToClassList("action-item--selected");
                cell.RegisterCallback<ClickEvent>(_ => ActivateMain(index));
                row.Add(cell);
            }
            _actionArea.Add(row);
            _actionHint.text = "Up/Down: select    Enter: confirm    1-4: quick select";
        }

        private void RenderAbilityMenu(BattleEngine battle)
        {
            var abilities = CurrentActorAbilities();
            if (abilities.Count == 0)
                return;

            var reg = gameManager.AbilityRegistry;
            var actor = battle.CurrentActor;

            for (var i = 0; i < abilities.Count; i++)
            {
                var index = i; // per-iteration copy for the click closure
                var ab = reg.Get(abilities[i]);
                var ready = battle.CanUseAbility(actor, ab.Id);
                var cd = battle.Cooldowns.GetCooldown(actor, ab.Id);
                var costTxt = cd > 0 ? $" (CD {cd})" : $" ({ab.MpCost}MP)";

                var label = new Label($"{ab.Name}{costTxt}");
                label.AddToClassList("action-item");
                if (i == AbilitySelected && ready)
                    label.AddToClassList("action-item--selected");
                else if (!ready)
                    label.AddToClassList("action-item--disabled");
                label.RegisterCallback<ClickEvent>(_ =>
                {
                    AbilitySelected = index;
                    SelectAbility();
                });
                _actionArea.Add(label);
            }

            var selectedAbility = reg.Get(abilities[AbilitySelected]);
            var tagStr = string.Join(" · ", selectedAbility.Tags);
            var descLine = tagStr.Length > 0 ? $"{tagStr} — {selectedAbility.Description}" : selectedAbility.Description;
            var desc = new Label(descLine);
            desc.AddToClassList("ability-desc");
            _actionArea.Add(desc);

            _actionHint.text = "Up/Down: select    Enter: use    ESC: back";
        }

        private void RenderTargetMenu()
        {
            var targets = CurrentTargetList();
            for (var i = 0; i < targets.Count; i++)
            {
                var index = i; // per-iteration copy for the click closure
                var t = targets[i];
                var label = new Label($"[{i + 1}] {t.Name}  HP {t.CurrentHp}/{t.MaxHp}");
                label.AddToClassList("action-item");
                if (i == TargetSelected % Mathf.Max(1, targets.Count))
                    label.AddToClassList("action-item--selected");
                label.RegisterCallback<ClickEvent>(_ =>
                {
                    TargetSelected = index;
                    ConfirmTarget();
                });
                _actionArea.Add(label);
            }
            _actionHint.text = "Up/Down: select    Enter: confirm    ESC: back";
        }

        private void RenderVictory(BattleEngine battle)
        {
            _xpLines.Clear();
            foreach (var award in battle.XpAwards)
            {
                var line = $"{award.Name}   +{award.Xp} XP" + (award.Leveled ? $"   * LEVEL UP -> Lv.{award.NewLevel}" : "");
                var label = new Label(line);
                label.AddToClassList(award.Leveled ? "xp-line--levelup" : "xp-line");
                _xpLines.Add(label);
            }

            _lootLines.Clear();
            foreach (var group in battle.PendingDrops.GroupBy(id => id))
            {
                var item = gameManager.ItemRegistry.Get(group.Key);
                var count = group.Count();
                var text = count == 1 ? item.Name : $"{item.Name}  x{count}";
                var label = new Label(text);
                label.AddToClassList("loot-line");
                label.style.color = RarityColors.Get(item.Rarity);
                _lootLines.Add(label);
            }
        }

        // ------------------------------------------------------------------
        // Input polling
        // ------------------------------------------------------------------

        private void Update()
        {
            var battle = Battle;
            if (battle == null)
                return;

            if (battle.Phase == BattlePhase.Victory && State != CombatUiState.Victory)
            {
                State = CombatUiState.Victory;
                Render();
                return;
            }
            if (battle.Phase == BattlePhase.Defeat && State != CombatUiState.Defeat)
            {
                State = CombatUiState.Defeat;
                Render();
                return;
            }
            if (battle.Phase == BattlePhase.Fled)
            {
                gameManager.SwitchTo(GameState.Explore);
                return;
            }

            if (State == CombatUiState.Victory || State == CombatUiState.Defeat)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
                    Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
                    ContinueFromTerminal();
                return;
            }

            if (State == CombatUiState.Resolving)
            {
                _resolveTimer += Time.deltaTime;
                if (_resolveTimer >= ResolveDelay)
                {
                    _resolveTimer = 0f;
                    AfterPlayerAction();
                }
                return;
            }

            if (State == CombatUiState.EnemyTurn)
            {
                _resolveTimer += Time.deltaTime;
                if (_resolveTimer >= ResolveDelay)
                {
                    _resolveTimer = 0f;
                    DoEnemyTurn();
                }
                return;
            }

            if (State == CombatUiState.PlayerMain)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) NavigateMainUp();
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) NavigateMainDown();
                else if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ActivateMain(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) ActivateMain(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) ActivateMain(2);
                else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) ActivateMain(3);
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) ActivateMain(MainSelected);
            }
            else if (State == CombatUiState.PlayerAbility)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) CancelAbility();
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) NavigateAbilityUp();
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) NavigateAbilityDown();
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) SelectAbility();
            }
            else if (State == CombatUiState.PlayerTarget)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) CancelTarget();
                else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) NavigateTargetUp();
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) NavigateTargetDown();
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) ConfirmTarget();
            }
        }
    }
}
