using TowerOfChrome.Core.Entities;
using TowerOfChrome.Core.Loot;
using TowerOfChrome.Core.Tests.TestUtil;

namespace TowerOfChrome.Core.Tests.Loot;

public class LootTablesTests
{
    [Fact]
    public void GenerateDrops_UnknownTable_ReturnsEmpty()
    {
        var lt = TestGameData.NewLootTables(new ScriptedRandomSource());
        Assert.Empty(lt.GenerateDrops("not_a_table", 1));
    }

    [Fact]
    public void GenerateDrops_RollAboveDropChance_ReturnsEmpty()
    {
        // common_floor1 drop_chance=0.60; a roll of 0.99 fails the check (roll > chance).
        var rng = new ScriptedRandomSource(doubles: new[] { 0.99 });
        var lt = TestGameData.NewLootTables(rng);

        Assert.Empty(lt.GenerateDrops("common_floor1", 1));
    }

    [Fact]
    public void GenerateDrops_SuccessfulRoll_ReturnsRequestedRollCount()
    {
        // boss table: drop_chance=1.0 (always succeeds), rolls=3.
        // 1 double for the drop-chance check, then per roll: rarity weighted pick (1 double),
        // category weighted pick (1 double), item index pick (1 int) = 3*2+1 = 7 doubles total.
        var rng = new ScriptedRandomSource(
            doubles: new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 },
            ints: new[] { 0, 0, 0 });
        var lt = TestGameData.NewLootTables(rng);

        var drops = lt.GenerateDrops("boss", 1);

        Assert.Equal(3, drops.Count);
    }

    [Fact]
    public void AwardDrops_EmptyDropList_ReturnsEmptyLog()
    {
        var lt = TestGameData.NewLootTables(new ScriptedRandomSource());
        var party = Party.DefaultParty(TestGameData.NewClassRegistry(), TestGameData.NewItemRegistry(), TestGameData.NewLeveling());

        Assert.Empty(lt.AwardDrops(party, Array.Empty<string>()));
    }

    [Fact]
    public void AwardDrops_PrefersMemberWithMostFreeBagSpace()
    {
        var lt = TestGameData.NewLootTables(new ScriptedRandomSource());
        var party = Party.DefaultParty(TestGameData.NewClassRegistry(), TestGameData.NewItemRegistry(), TestGameData.NewLeveling());

        // Fill member 0's bag almost full, leave member 1 empty.
        var members = party.AllMembers;
        for (var i = 0; i < Inventory.MaxBagSize - 1; i++)
            members[0].Inventory.Add("health_potion_small");

        var log = lt.AwardDrops(party, new[] { "health_potion_small" });

        Assert.Contains(members[1].Name, log[0]); // went to the member with more free space
    }

    [Fact]
    public void AwardDrops_AllBagsFull_LogsLost()
    {
        var lt = TestGameData.NewLootTables(new ScriptedRandomSource());
        var party = Party.DefaultParty(TestGameData.NewClassRegistry(), TestGameData.NewItemRegistry(), TestGameData.NewLeveling());

        foreach (var member in party.AllMembers)
            for (var i = 0; i < Inventory.MaxBagSize; i++)
                member.Inventory.Add("health_potion_small");

        var log = lt.AwardDrops(party, new[] { "health_potion_small" });

        Assert.Contains("lost (bags full)", log[0]);
    }

    [Fact]
    public void AwardDrops_LogsRarityAndName_OnSuccess()
    {
        var lt = TestGameData.NewLootTables(new ScriptedRandomSource());
        var party = Party.DefaultParty(TestGameData.NewClassRegistry(), TestGameData.NewItemRegistry(), TestGameData.NewLeveling());

        var log = lt.AwardDrops(party, new[] { "health_potion_small" });

        Assert.Contains("Small Health Potion", log[0]);
        Assert.Contains("Common", log[0]);
    }
}
