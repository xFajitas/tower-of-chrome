using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Loot;
using TowerOfChrome.Core.Tests.TestUtil;

namespace TowerOfChrome.Core.Tests.Loot;

public class InventoryTests
{
    private static Character NewKnight()
    {
        var classes = TestGameData.NewClassRegistry();
        var items = TestGameData.NewItemRegistry();
        var leveling = TestGameData.NewLeveling();
        return new Character("Test Knight", classes.Get("knight"), items, leveling);
    }

    private static Character NewMage()
    {
        var classes = TestGameData.NewClassRegistry();
        var items = TestGameData.NewItemRegistry();
        var leveling = TestGameData.NewLeveling();
        return new Character("Test Mage", classes.Get("mage"), items, leveling);
    }

    [Fact]
    public void Add_FailsWhenBagFull()
    {
        var c = NewKnight();
        for (var i = 0; i < Inventory.MaxBagSize; i++)
            Assert.True(c.Inventory.Add("health_potion_small"));

        Assert.True(c.Inventory.BagFull);
        Assert.False(c.Inventory.Add("health_potion_small"));
    }

    [Fact]
    public void Equip_MovesItemFromBagToSlot_ReturnsNullWhenSlotWasEmpty()
    {
        var c = NewKnight();
        c.Inventory.Add("iron_sword");

        var displaced = c.Inventory.Equip("iron_sword");

        Assert.Null(displaced);
        Assert.Equal("iron_sword", c.Equipment["weapon"]);
        Assert.DoesNotContain("iron_sword", c.Inventory.Bag);
    }

    [Fact]
    public void Equip_ReturnsDisplacedItem_BackInBag()
    {
        var c = NewKnight();
        c.Inventory.Add("iron_sword");
        c.Inventory.Equip("iron_sword");
        c.Inventory.Add("steel_sword");

        var displaced = c.Inventory.Equip("steel_sword");

        Assert.Equal("iron_sword", displaced);
        Assert.Equal("steel_sword", c.Equipment["weapon"]);
        Assert.Contains("iron_sword", c.Inventory.Bag);
    }

    [Fact]
    public void Equip_ThrowsWhenItemNotInBag()
    {
        var c = NewKnight();
        Assert.Throws<InventoryException>(() => c.Inventory.Equip("iron_sword"));
    }

    [Fact]
    public void Equip_ThrowsForConsumable()
    {
        var c = NewKnight();
        c.Inventory.Add("health_potion_small");
        Assert.Throws<InventoryException>(() => c.Inventory.Equip("health_potion_small"));
    }

    [Fact]
    public void Equip_ThrowsWhenWeaponTypeIncompatibleWithClass()
    {
        // Mage's weapon_types are staff/wand (verified from data/classes.json); a sword-type
        // weapon like iron_sword should be rejected for a mage.
        var mage = NewMage();
        mage.Inventory.Add("iron_sword");
        Assert.Throws<InventoryException>(() => mage.Inventory.Equip("iron_sword"));
    }

    [Fact]
    public void Unequip_MovesItemBackToBag()
    {
        var c = NewKnight();
        c.Inventory.Add("iron_sword");
        c.Inventory.Equip("iron_sword");

        var unequipped = c.Inventory.Unequip("weapon");

        Assert.Equal("iron_sword", unequipped);
        Assert.Null(c.Equipment["weapon"]);
        Assert.Contains("iron_sword", c.Inventory.Bag);
    }

    [Fact]
    public void Unequip_EmptySlot_ReturnsNull()
    {
        var c = NewKnight();
        Assert.Null(c.Inventory.Unequip("accessory"));
    }

    [Fact]
    public void Unequip_ThrowsWhenBagFull()
    {
        var c = NewKnight();
        c.Inventory.Add("iron_sword");
        c.Inventory.Equip("iron_sword");
        for (var i = 0; i < Inventory.MaxBagSize; i++)
            c.Inventory.Add("health_potion_small");

        Assert.Throws<InventoryException>(() => c.Inventory.Unequip("weapon"));
    }

    [Fact]
    public void Use_HealHp_RestoresAndRemovesFromBag()
    {
        var c = NewKnight();
        c.TakeDamage(30);
        c.Inventory.Add("health_potion_small"); // heal_hp value=50

        var log = c.Inventory.Use("health_potion_small");

        Assert.Equal(c.MaxHp, c.CurrentHp); // only needed 30 of the 50
        Assert.DoesNotContain("health_potion_small", c.Inventory.Bag);
        Assert.Contains("Restored 30 HP", log);
    }

    [Fact]
    public void Use_ThrowsForNonConsumable()
    {
        var c = NewKnight();
        c.Inventory.Add("iron_sword");
        Assert.Throws<InventoryException>(() => c.Inventory.Use("iron_sword"));
    }

    [Fact]
    public void EquippedStatBonus_SumsAcrossAllSlots()
    {
        var c = NewKnight();
        var before = c.GetStat("str");
        c.Inventory.Add("iron_sword"); // +4 str, +1 dex
        c.Inventory.Equip("iron_sword");

        Assert.Equal(before + 4, c.GetStat("str"));
    }

    [Fact]
    public void RestoreBag_BypassesFullCheck_ForSaveLoadReconstruction()
    {
        var c = NewKnight();
        var many = Enumerable.Repeat("health_potion_small", Inventory.MaxBagSize + 5).ToList();

        c.Inventory.RestoreBag(many);

        Assert.Equal(many.Count, c.Inventory.BagSize); // exceeds MaxBagSize but is allowed
    }
}
