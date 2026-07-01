using System.Collections;
using DungeonOfChrome.Core.Loot;

namespace DungeonOfChrome.Core.Entities;

/// <summary>
/// Container for up to four characters with convenience queries. Port of entities/party.py.
/// Slots are positional (indices 0-3) — a slot may be null if the party is not yet full. The
/// combat engine references slots by index so targeting stays stable even when members die
/// (dead members are NOT compacted out of their slot).
/// </summary>
public sealed class Party : IEnumerable<Character?>
{
    public const int MaxPartySize = 4;

    private readonly Character?[] _slots = new Character?[MaxPartySize];

    // Flavor names tied to party slot position, independent of class choice.
    public static readonly IReadOnlyList<string> DefaultNames = new[] { "Sir Gareth", "Lysandra", "Brother Aldric", "Sylvara" };
    public static readonly IReadOnlyList<string> DefaultClassIds = new[] { "knight", "mage", "cleric", "ranger" };

    /// <summary>Place `character` into `slot`, or the first empty slot if slot is null.
    /// Returns the slot index used. Throws InvalidOperationException if the party is full
    /// or the requested slot is already occupied.</summary>
    public int AddMember(Character character, int? slot = null)
    {
        if (slot.HasValue)
        {
            if (_slots[slot.Value] != null)
                throw new InvalidOperationException($"Slot {slot.Value} is already occupied.");
            _slots[slot.Value] = character;
            return slot.Value;
        }

        for (var i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null)
            {
                _slots[i] = character;
                return i;
            }
        }

        throw new InvalidOperationException("Party is full.");
    }

    /// <summary>Remove and return the character in `slot` (or null if empty).</summary>
    public Character? RemoveMember(int slot)
    {
        var removed = _slots[slot];
        _slots[slot] = null;
        return removed;
    }

    public Character? GetMember(int slot) => _slots[slot];

    /// <summary>Direct slot access for save/load reconstruction (bypasses AddMember's full-party check).</summary>
    public void SetSlot(int slot, Character? character) => _slots[slot] = character;

    /// <summary>All slots, including null entries.</summary>
    public IReadOnlyList<Character?> Members => _slots;

    public IReadOnlyList<Character> LivingMembers =>
        _slots.Where(c => c != null && c.IsAlive).Cast<Character>().ToList();

    public IReadOnlyList<Character> AllMembers =>
        _slots.Where(c => c != null).Cast<Character>().ToList();

    public bool IsFull => _slots.All(s => s != null);
    public bool IsWiped => LivingMembers.Count == 0;
    public int Size => _slots.Count(s => s != null);

    public IEnumerator<Character?> GetEnumerator() => ((IEnumerable<Character?>)_slots).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Creates the balanced default starting party.</summary>
    public static Party DefaultParty(ClassRegistry classes, ItemRegistry items, Leveling leveling) =>
        Build(DefaultNames.Zip(DefaultClassIds, (n, c) => (Name: n, ClassId: c)), classes, items, leveling);

    /// <summary>Creates a party from (name, class_id) pairs, each equipped with their class's
    /// starting weapon if one exists.</summary>
    public static Party Build(
        IEnumerable<(string Name, string ClassId)> starters,
        ClassRegistry classes,
        ItemRegistry items,
        Leveling leveling)
    {
        var party = new Party();

        foreach (var (name, classId) in starters)
        {
            var character = new Character(name, classes.Get(classId), items, leveling);

            var startingWeapon = character.ClassDef.StartingWeapon;
            if (!string.IsNullOrEmpty(startingWeapon))
            {
                try
                {
                    items.Get(startingWeapon); // verify it exists
                    character.Inventory.Add(startingWeapon);
                    character.Inventory.Equip(startingWeapon);
                }
                catch (Exception)
                {
                    // Mirrors Python's `except (KeyError, ValueError): print(...)` —
                    // a missing/invalid starting weapon shouldn't block party creation.
                }
            }

            party.AddMember(character);
        }

        return party;
    }
}
