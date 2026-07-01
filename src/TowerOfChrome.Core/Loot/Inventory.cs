using TowerOfChrome.Core.Entities;

namespace TowerOfChrome.Core.Loot;

/// <summary>Raised for equip/unequip validation failures — mirrors Python's ValueError raises
/// in Inventory.equip()/unequip() (callers are expected to catch/handle these explicitly).</summary>
public sealed class InventoryException : Exception
{
    public InventoryException(string message) : base(message) { }
}

/// <summary>
/// Manages one Character's bag and equipment slot swapping. Port of loot/inventory.py.
/// The owner Character stores `Equipment` (slot -> item_id | null); this class is responsible
/// for validating and performing the bag &lt;-&gt; equipment slot moves.
/// </summary>
public sealed class Inventory
{
    public const int MaxBagSize = 20;

    private readonly Character _owner;
    private readonly ItemRegistry _items;
    private readonly List<string> _bag = new();

    public Inventory(Character owner, ItemRegistry items)
    {
        _owner = owner;
        _items = items;
    }

    public IReadOnlyList<string> Bag => _bag.AsReadOnly();
    public int BagSize => _bag.Count;
    public bool BagFull => _bag.Count >= MaxBagSize;

    /// <summary>Add an item to the bag. Returns false if the bag is full.</summary>
    public bool Add(string itemId)
    {
        if (BagFull)
            return false;
        _bag.Add(itemId);
        return true;
    }

    /// <summary>Remove the first occurrence. Returns false if not found.</summary>
    public bool Remove(string itemId) => _bag.Remove(itemId);

    /// <summary>
    /// Move `itemId` from the bag into its equipment slot. Returns the item that was displaced
    /// (now back in the bag), or null. Throws InventoryException for class restriction
    /// violations or if the item is not currently in the bag.
    /// </summary>
    public string? Equip(string itemId)
    {
        var item = _items.Get(itemId);

        if (item.Consumable)
            throw new InventoryException($"{item.Name} is a consumable — use it instead.");

        var slot = item.Slot;
        if (string.IsNullOrEmpty(slot))
            throw new InventoryException($"{item.Name} has no equipment slot.");

        if (!_owner.Equipment.ContainsKey(slot))
            throw new InventoryException($"{_owner.ClassDef.Name} has no {slot} slot.");

        if (item.WeaponType != null && !_owner.ClassDef.WeaponTypes.Contains(item.WeaponType))
            throw new InventoryException($"{_owner.ClassDef.Name} cannot wield {item.Name} (needs {item.WeaponType}).");

        if (item.ArmorType != null && !_owner.ClassDef.ArmorTypes.Contains(item.ArmorType))
            throw new InventoryException($"{_owner.ClassDef.Name} cannot wear {item.Name} (needs {item.ArmorType} proficiency).");

        if (!_bag.Remove(itemId))
            throw new InventoryException($"{item.Name} is not in {_owner.Name}'s bag.");

        var oldId = _owner.Equipment.GetValueOrDefault(slot);
        _owner.Equipment[slot] = itemId;
        if (oldId != null)
            _bag.Add(oldId);

        ClampResources();
        return oldId;
    }

    /// <summary>Move the item in `slot` back into the bag. Returns the displaced item_id, or
    /// null if the slot was empty. Throws InventoryException if the bag is full.</summary>
    public string? Unequip(string slot)
    {
        var itemId = _owner.Equipment.GetValueOrDefault(slot);
        if (itemId == null)
            return null;
        if (BagFull)
            throw new InventoryException("Bag is full — cannot unequip.");

        _owner.Equipment[slot] = null;
        _bag.Add(itemId);
        ClampResources();
        return itemId;
    }

    /// <summary>Use a consumable from the bag. Returns a human-readable log line. Throws
    /// InventoryException if the item is not usable or not in the bag.</summary>
    public string Use(string itemId)
    {
        var item = _items.Get(itemId);

        if (!item.Consumable)
            throw new InventoryException($"{item.Name} is not usable.");

        if (!_bag.Remove(itemId))
            throw new InventoryException($"{item.Name} is not in {_owner.Name}'s bag.");

        var effect = item.Effect;
        var type = effect?.Type ?? "";
        var value = effect?.Value ?? 0;
        var log = $"{_owner.Name} uses {item.Name}.";

        switch (type)
        {
            case "heal_hp":
                log += $" Restored {_owner.Heal(value)} HP.";
                break;
            case "restore_mp":
                log += $" Restored {_owner.RestoreMp(value)} MP.";
                break;
            case "full_restore":
                _owner.Heal(_owner.MaxHp);
                _owner.RestoreMp(_owner.MaxMp);
                log += " Fully restored HP and MP!";
                break;
            case "cleanse":
                _owner.ClearDebuffs();
                log += " All debuffs cleansed.";
                break;
            case "buff":
                var status = effect?.Status ?? "blessed";
                _owner.AddStatus(status);
                log += $" Applied {status}.";
                break;
        }

        return log;
    }

    /// <summary>Sum of `stat` bonuses across all currently equipped items.</summary>
    public int EquippedStatBonus(string stat)
    {
        var total = 0;
        foreach (var itemId in _owner.Equipment.Values)
        {
            if (itemId == null)
                continue;
            total += _items.Get(itemId).StatBonuses.GetValueOrDefault(stat, 0);
        }
        return total;
    }

    /// <summary>Replaces the bag contents wholesale — used when restoring from a save, where
    /// the data is already known-valid and shouldn't be re-validated against MaxBagSize.</summary>
    public void RestoreBag(IEnumerable<string> items)
    {
        _bag.Clear();
        _bag.AddRange(items);
    }

    /// <summary>After equip/unequip, max stats may change — clamp current values.</summary>
    private void ClampResources()
    {
        _owner.CurrentHp = Math.Min(_owner.CurrentHp, _owner.MaxHp);
        _owner.CurrentMp = Math.Min(_owner.CurrentMp, _owner.MaxMp);
    }
}
