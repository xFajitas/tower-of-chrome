using System.Collections.Generic;
using System.Linq;
using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Fsm;
using TowerOfChrome.Core.Loot;
using TowerOfChrome.Unity.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace TowerOfChrome.Unity.Screens
{
    public enum InventoryUiState
    {
        MemberSelect,
        ItemSelect,
        TradeTarget,
    }

    /// <summary>One row in the combined equipped+bag list a member's item cursor moves through.
    /// Mirrors Python's ("equipped", slot, item_id) / ("bag", index, item_id) row tuples.</summary>
    public readonly struct ItemRow
    {
        public readonly bool IsEquipped;
        public readonly string Slot;
        public readonly string? ItemId;

        public ItemRow(bool isEquipped, string slot, string? itemId)
        {
            IsEquipped = isEquipped;
            Slot = slot;
            ItemId = itemId;
        }
    }

    /// <summary>
    /// Port of Python's InventoryScreen: party member list, equipped-slots + bag panel per
    /// member, equip/use/unequip/drop/trade actions, and a trade-target picker. Navigation/
    /// action logic is exposed as public methods separate from Input polling, so tests can
    /// drive the screen directly.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InventoryScreenView : MonoBehaviour
    {
        private static readonly string[] SlotOrder = { "weapon", "armor", "accessory", "charm" };
        private static readonly IReadOnlyDictionary<string, string> SlotLabels = new Dictionary<string, string>
        {
            ["weapon"] = "Weapon",
            ["armor"] = "Armor",
            ["accessory"] = "Accessory",
            ["charm"] = "Charm",
        };

        private const float MsgDuration = 2.0f;

        [SerializeField] private GameManager gameManager;

        private UIDocument _uiDocument;
        private VisualElement _memberPanel;
        private VisualElement _itemPanel;
        private Label _itemHeader;
        private VisualElement _equippedRows;
        private Label _bagHeader;
        private VisualElement _bagRows;
        private Label _flashMessage;
        private VisualElement _tradeModal;
        private Label _tradeTitle;
        private VisualElement _tradeRows;
        private Label _hint;

        public InventoryUiState State { get; private set; } = InventoryUiState.MemberSelect;
        public int MemberSelected { get; private set; }
        public int ItemCursor { get; private set; }
        public string? PendingTradeItemId { get; private set; }
        public int TradeTargetSlot { get; private set; }

        private string _msg = "";
        private float _msgTimer;

        public string Message => _msg;

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            var root = _uiDocument.rootVisualElement;
            _memberPanel = root.Q<VisualElement>("member-panel");
            _itemPanel = root.Q<VisualElement>("item-panel");
            _itemHeader = root.Q<Label>("item-header");
            _equippedRows = root.Q<VisualElement>("equipped-rows");
            _bagHeader = root.Q<Label>("bag-header");
            _bagRows = root.Q<VisualElement>("bag-rows");
            _flashMessage = root.Q<Label>("flash-message");
            _tradeModal = root.Q<VisualElement>("trade-modal");
            _tradeTitle = root.Q<Label>("trade-title");
            _tradeRows = root.Q<VisualElement>("trade-rows");
            _hint = root.Q<Label>("hint");

            State = InventoryUiState.MemberSelect;
            MemberSelected = 0;
            ItemCursor = 0;
            _msg = "";
            _msgTimer = 0f;

            Render();
        }

        // ------------------------------------------------------------------
        // Row model (mirrors _build_rows / _cur_member)
        // ------------------------------------------------------------------

        private Character CurrentMember() => gameManager.Party.AllMembers[MemberSelected % gameManager.Party.AllMembers.Count];

        private static List<ItemRow> BuildRows(Character member)
        {
            var rows = new List<ItemRow>();
            foreach (var slot in SlotOrder)
            {
                if (member.Equipment.ContainsKey(slot))
                    rows.Add(new ItemRow(true, slot, member.Equipment[slot]));
            }
            foreach (var itemId in member.Inventory.Bag)
                rows.Add(new ItemRow(false, "", itemId));
            return rows;
        }

        // ------------------------------------------------------------------
        // Member select
        // ------------------------------------------------------------------

        public void NavigateMemberUp()
        {
            if (State != InventoryUiState.MemberSelect)
                return;
            var n = gameManager.Party.AllMembers.Count;
            if (n == 0)
                return;
            MemberSelected = (MemberSelected - 1 + n) % n;
            Render();
        }

        public void NavigateMemberDown()
        {
            if (State != InventoryUiState.MemberSelect)
                return;
            var n = gameManager.Party.AllMembers.Count;
            if (n == 0)
                return;
            MemberSelected = (MemberSelected + 1) % n;
            Render();
        }

        public void ConfirmMember()
        {
            if (State != InventoryUiState.MemberSelect)
                return;
            if (gameManager.Party.AllMembers.Count == 0)
                return;
            State = InventoryUiState.ItemSelect;
            ItemCursor = 0;
            Render();
        }

        /// <summary>Mirrors Python's _go_back: return to whichever screen last led here, per
        /// FSM history, falling back to Explore. Only reachable from MemberSelect.</summary>
        public void Back()
        {
            if (State != InventoryUiState.MemberSelect)
                return;

            var validTargets = Transitions.Map[GameState.Inventory];
            for (var i = gameManager.Fsm.History.Count - 1; i >= 0; i--)
            {
                if (validTargets.Contains(gameManager.Fsm.History[i]))
                {
                    gameManager.SwitchTo(gameManager.Fsm.History[i]);
                    return;
                }
            }
            gameManager.SwitchTo(GameState.Explore);
        }

        // ------------------------------------------------------------------
        // Item select
        // ------------------------------------------------------------------

        public void CancelItems()
        {
            if (State != InventoryUiState.ItemSelect)
                return;
            State = InventoryUiState.MemberSelect;
            Render();
        }

        public void NavigateItemUp()
        {
            if (State != InventoryUiState.ItemSelect)
                return;
            var n = BuildRows(CurrentMember()).Count;
            if (n == 0)
                return;
            ItemCursor = (ItemCursor - 1 + n) % n;
            Render();
        }

        public void NavigateItemDown()
        {
            if (State != InventoryUiState.ItemSelect)
                return;
            var n = BuildRows(CurrentMember()).Count;
            if (n == 0)
                return;
            ItemCursor = (ItemCursor + 1) % n;
            Render();
        }

        /// <summary>E/Enter: equip an equippable bag item, use a consumable, or show info about
        /// an equipped slot. Mirrors Python's _activate.</summary>
        public void ActivateItem()
        {
            if (State != InventoryUiState.ItemSelect)
                return;

            var member = CurrentMember();
            var rows = BuildRows(member);
            if (rows.Count == 0)
                return;
            var row = rows[ItemCursor % rows.Count];

            if (row.IsEquipped)
            {
                if (row.ItemId != null)
                {
                    var item = gameManager.ItemRegistry.Get(row.ItemId);
                    Flash($"{item.Name} equipped. Press R to unequip.");
                }
                else
                {
                    Flash("Slot is empty.");
                }
                Render();
                return;
            }

            var bagItem = gameManager.ItemRegistry.Get(row.ItemId!);
            if (bagItem.Consumable)
            {
                try
                {
                    var log = member.Inventory.Use(row.ItemId!);
                    Flash(log);
                    ItemCursor = Mathf.Max(0, ItemCursor - 1);
                }
                catch (InventoryException exc)
                {
                    Flash(exc.Message);
                }
            }
            else if (!string.IsNullOrEmpty(bagItem.Slot))
            {
                try
                {
                    var old = member.Inventory.Equip(row.ItemId!);
                    Flash(old != null
                        ? $"Equipped {bagItem.Name}. {gameManager.ItemRegistry.Get(old).Name} moved to bag."
                        : $"Equipped {bagItem.Name}.");
                    ItemCursor = Mathf.Max(0, ItemCursor - 1);
                }
                catch (InventoryException exc)
                {
                    Flash(exc.Message);
                }
            }
            else
            {
                Flash($"{bagItem.Name} cannot be equipped.");
            }
            Render();
        }

        /// <summary>R: unequip the currently selected equipped-slot row, if occupied.</summary>
        public void UnequipSelected()
        {
            if (State != InventoryUiState.ItemSelect)
                return;
            var member = CurrentMember();
            var rows = BuildRows(member);
            if (rows.Count == 0)
                return;
            var row = rows[ItemCursor % rows.Count];
            if (!row.IsEquipped || row.ItemId == null)
                return;

            try
            {
                var itemId = member.Inventory.Unequip(row.Slot);
                if (itemId != null)
                    Flash($"Unequipped {gameManager.ItemRegistry.Get(itemId).Name}.");
            }
            catch (InventoryException exc)
            {
                Flash(exc.Message);
            }
            Render();
        }

        /// <summary>D: drop the currently selected bag row.</summary>
        public void DropSelected()
        {
            if (State != InventoryUiState.ItemSelect)
                return;
            var member = CurrentMember();
            var rows = BuildRows(member);
            if (rows.Count == 0)
                return;
            var row = rows[ItemCursor % rows.Count];
            if (row.IsEquipped)
                return;

            var item = gameManager.ItemRegistry.Get(row.ItemId!);
            member.Inventory.Remove(row.ItemId!);
            Flash($"Dropped {item.Name}.");
            ItemCursor = Mathf.Max(0, ItemCursor - 1);
            Render();
        }

        /// <summary>T: begin trading the currently selected bag row to another party member.</summary>
        public void BeginTrade()
        {
            if (State != InventoryUiState.ItemSelect)
                return;
            var rows = BuildRows(CurrentMember());
            if (rows.Count == 0)
                return;
            var row = rows[ItemCursor % rows.Count];
            if (row.IsEquipped)
                return;

            var others = OtherMemberSlots();
            if (others.Count == 0)
            {
                Flash("No one else to trade with.");
                Render();
                return;
            }

            PendingTradeItemId = row.ItemId;
            TradeTargetSlot = others[0];
            State = InventoryUiState.TradeTarget;
            Render();
        }

        // ------------------------------------------------------------------
        // Trade target
        // ------------------------------------------------------------------

        private List<int> OtherMemberSlots()
        {
            var all = gameManager.Party.AllMembers;
            var result = new List<int>();
            for (var i = 0; i < all.Count; i++)
            {
                if (i != MemberSelected)
                    result.Add(i);
            }
            return result;
        }

        public void NavigateTradeTargetUp()
        {
            if (State != InventoryUiState.TradeTarget)
                return;
            var others = OtherMemberSlots();
            if (others.Count == 0)
            {
                State = InventoryUiState.ItemSelect;
                Render();
                return;
            }
            var idx = others.IndexOf(TradeTargetSlot);
            if (idx < 0)
                idx = 0;
            TradeTargetSlot = others[(idx - 1 + others.Count) % others.Count];
            Render();
        }

        public void NavigateTradeTargetDown()
        {
            if (State != InventoryUiState.TradeTarget)
                return;
            var others = OtherMemberSlots();
            if (others.Count == 0)
            {
                State = InventoryUiState.ItemSelect;
                Render();
                return;
            }
            var idx = others.IndexOf(TradeTargetSlot);
            if (idx < 0)
                idx = 0;
            TradeTargetSlot = others[(idx + 1) % others.Count];
            Render();
        }

        public void CancelTrade()
        {
            if (State != InventoryUiState.TradeTarget)
                return;
            State = InventoryUiState.ItemSelect;
            Render();
        }

        /// <summary>Mirrors Python's _do_trade.</summary>
        public void ConfirmTrade()
        {
            if (State != InventoryUiState.TradeTarget)
                return;

            var all = gameManager.Party.AllMembers;
            var giver = all[MemberSelected];
            var receiver = all[TradeTargetSlot];
            var itemId = PendingTradeItemId!;
            var item = gameManager.ItemRegistry.Get(itemId);

            if (receiver.Inventory.BagFull)
            {
                Flash($"{receiver.Name}'s bag is full.");
            }
            else if (!giver.Inventory.Remove(itemId))
            {
                Flash($"{item.Name} is no longer in {giver.Name}'s bag.");
            }
            else
            {
                receiver.Inventory.Add(itemId);
                ItemCursor = Mathf.Max(0, ItemCursor - 1);
                Flash($"Sent {item.Name} to {receiver.Name}.");
            }

            State = InventoryUiState.ItemSelect;
            Render();
        }

        private void Flash(string msg)
        {
            _msg = msg.Length > 90 ? msg[..90] : msg;
            _msgTimer = MsgDuration;
        }

        // ------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------

        private void Render()
        {
            RenderMemberPanel();
            RenderItemPanel();

            _flashMessage.text = _msg;
            _tradeModal.EnableInClassList("modal-overlay--visible", State == InventoryUiState.TradeTarget);
            if (State == InventoryUiState.TradeTarget)
                RenderTradeModal();

            _hint.text = State switch
            {
                InventoryUiState.MemberSelect => "Up/Down: select member    Enter: manage items    ESC/I: back",
                InventoryUiState.ItemSelect => "E/Enter: equip/use    R: unequip    D: drop    T: trade    ESC: back",
                _ => "Up/Down: choose recipient    Enter: send    ESC: cancel",
            };
        }

        private void RenderMemberPanel()
        {
            _memberPanel.Clear();
            var members = gameManager.Party.AllMembers;
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                var row = new VisualElement();
                row.AddToClassList("member-row");
                if (i == MemberSelected)
                    row.AddToClassList("member-row--selected");

                var header = new VisualElement();
                header.AddToClassList("member-row-header");

                var avatarTex = ArchetypeIcons.ForRole(member.ClassDef.Role);
                if (avatarTex != null)
                {
                    var avatar = new VisualElement();
                    avatar.AddToClassList("member-avatar");
                    avatar.style.backgroundImage = new StyleBackground(avatarTex);
                    header.Add(avatar);
                }

                var nameLabel = new Label(member.Name);
                nameLabel.AddToClassList("member-name");
                if (i == MemberSelected)
                    nameLabel.AddToClassList("member-name--selected");
                header.Add(nameLabel);
                row.Add(header);

                var classLabel = new Label($"{member.ClassDef.Name}  Lv.{member.Level}");
                classLabel.AddToClassList("member-class");
                row.Add(classLabel);

                var barBg = new VisualElement();
                barBg.AddToClassList("member-hp-bar-bg");
                var barFill = new VisualElement();
                barFill.AddToClassList("member-hp-bar-fill");
                barFill.style.width = new StyleLength(Length.Percent((float)(System.Math.Clamp(member.HpFraction, 0.0, 1.0) * 100.0)));
                barBg.Add(barFill);
                row.Add(barBg);

                var bagLabel = new Label($"HP {member.CurrentHp}/{member.MaxHp}    bag {member.Inventory.BagSize}/{Inventory.MaxBagSize}");
                bagLabel.AddToClassList("member-bag-count");
                if (member.Inventory.BagFull)
                    bagLabel.AddToClassList("member-bag-count--full");
                row.Add(bagLabel);

                _memberPanel.Add(row);
            }
        }

        private void RenderItemPanel()
        {
            var members = gameManager.Party.AllMembers;
            if (members.Count == 0)
            {
                _itemHeader.text = "";
                _equippedRows.Clear();
                _bagHeader.text = "";
                _bagRows.Clear();
                return;
            }

            var member = CurrentMember();
            _itemHeader.text = $"{member.Name}  —  {member.ClassDef.Name}  Lv.{member.Level}";

            var rows = BuildRows(member);
            var cursor = rows.Count > 0 ? ItemCursor % rows.Count : 0;
            var selecting = State == InventoryUiState.ItemSelect;

            _equippedRows.Clear();
            var eqCount = rows.Count(r => r.IsEquipped);
            for (var i = 0; i < eqCount; i++)
            {
                var row = rows[i];
                var rowEl = new VisualElement();
                rowEl.AddToClassList("equipped-row");
                var selected = selecting && i == cursor;
                if (selected)
                    rowEl.AddToClassList("equipped-row--selected");

                var slotLabel = new Label(SlotLabels.GetValueOrDefault(row.Slot, row.Slot));
                slotLabel.AddToClassList("slot-label");
                if (selected)
                    slotLabel.AddToClassList("slot-label--selected");
                rowEl.Add(slotLabel);

                if (row.ItemId != null)
                {
                    var item = gameManager.ItemRegistry.Get(row.ItemId);
                    var nameLabel = new Label(item.Name);
                    nameLabel.AddToClassList("item-name");
                    nameLabel.style.color = RarityColors.Get(item.Rarity);
                    rowEl.Add(nameLabel);

                    if (item.StatBonuses.Count > 0)
                    {
                        var bonusText = string.Join("  ", item.StatBonuses.Take(5).Select(kv => $"+{kv.Value}{kv.Key}"));
                        var bonusLabel = new Label(bonusText);
                        bonusLabel.AddToClassList("item-stat-bonus");
                        rowEl.Add(bonusLabel);
                    }
                }
                else
                {
                    var emptyLabel = new Label("—");
                    emptyLabel.AddToClassList("item-name");
                    rowEl.Add(emptyLabel);
                }

                _equippedRows.Add(rowEl);
            }

            _bagHeader.text = $"BAG  ({member.Inventory.BagSize}/{Inventory.MaxBagSize}):";
            _bagRows.Clear();
            var bagRows = rows.Skip(eqCount).ToList();
            if (bagRows.Count == 0)
            {
                var emptyLabel = new Label("(empty)");
                emptyLabel.AddToClassList("bag-item-category");
                _bagRows.Add(emptyLabel);
            }
            else
            {
                for (var i = 0; i < bagRows.Count; i++)
                {
                    var globalIndex = eqCount + i;
                    var row = bagRows[i];
                    var item = gameManager.ItemRegistry.Get(row.ItemId!);
                    var selected = selecting && globalIndex == cursor;

                    var rowEl = new VisualElement();
                    rowEl.AddToClassList("bag-row");
                    if (selected)
                        rowEl.AddToClassList("bag-row--selected");

                    var nameLabel = new Label(item.Name);
                    nameLabel.AddToClassList("bag-item-name");
                    nameLabel.style.color = RarityColors.Get(item.Rarity);
                    rowEl.Add(nameLabel);

                    var rarityLabel = new Label(RarityExtensions.Labels.GetValueOrDefault(item.Rarity, ""));
                    rarityLabel.AddToClassList("bag-item-rarity");
                    rowEl.Add(rarityLabel);

                    var categoryLabel = new Label(item.Category);
                    categoryLabel.AddToClassList("bag-item-category");
                    rowEl.Add(categoryLabel);

                    if (item.StatBonuses.Count > 0)
                    {
                        var bonusText = string.Join(" ", item.StatBonuses.Take(4).Select(kv => $"+{kv.Value}{kv.Key}"));
                        var bonusLabel = new Label(bonusText);
                        bonusLabel.AddToClassList("item-stat-bonus");
                        rowEl.Add(bonusLabel);
                    }
                    else if (item.Consumable && item.Effect != null)
                    {
                        var effectLabel = new Label($"{item.Effect.Type} {item.Effect.Value}".Trim());
                        effectLabel.AddToClassList("bag-item-effect");
                        rowEl.Add(effectLabel);
                    }

                    _bagRows.Add(rowEl);
                }
            }
        }

        private void RenderTradeModal()
        {
            var item = gameManager.ItemRegistry.Get(PendingTradeItemId!);
            _tradeTitle.text = $"Send {item.Name} to:";

            _tradeRows.Clear();
            var members = gameManager.Party.AllMembers;
            foreach (var i in OtherMemberSlots())
            {
                var m = members[i];
                var label = new Label($"{m.Name}  (bag {m.Inventory.BagSize}/{Inventory.MaxBagSize})");
                label.AddToClassList("trade-row");
                if (i == TradeTargetSlot)
                    label.AddToClassList("trade-row--selected");
                _tradeRows.Add(label);
            }
        }

        // ------------------------------------------------------------------
        // Input polling
        // ------------------------------------------------------------------

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

            if (State == InventoryUiState.MemberSelect)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) NavigateMemberUp();
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) NavigateMemberDown();
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) ConfirmMember();
                else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.I)) Back();
            }
            else if (State == InventoryUiState.ItemSelect)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) NavigateItemUp();
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) NavigateItemDown();
                else if (Input.GetKeyDown(KeyCode.Escape)) CancelItems();
                else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) ActivateItem();
                else if (Input.GetKeyDown(KeyCode.R)) UnequipSelected();
                else if (Input.GetKeyDown(KeyCode.D)) DropSelected();
                else if (Input.GetKeyDown(KeyCode.T)) BeginTrade();
            }
            else if (State == InventoryUiState.TradeTarget)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) NavigateTradeTargetUp();
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) NavigateTradeTargetDown();
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) ConfirmTrade();
                else if (Input.GetKeyDown(KeyCode.Escape)) CancelTrade();
            }
        }
    }
}
