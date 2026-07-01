using DungeonOfChrome.Core.Entities;
using DungeonOfChrome.Core.Tests.TestUtil;

namespace DungeonOfChrome.Core.Tests.Entities;

public class PartyTests
{
    private static Character MakeCharacter(string name, string classId)
    {
        var classes = TestGameData.NewClassRegistry();
        var items = TestGameData.NewItemRegistry();
        var leveling = TestGameData.NewLeveling();
        return new Character(name, classes.Get(classId), items, leveling);
    }

    [Fact]
    public void DefaultParty_Has4Members_MatchingDefaultNamesAndClasses()
    {
        var party = Party.DefaultParty(TestGameData.NewClassRegistry(), TestGameData.NewItemRegistry(), TestGameData.NewLeveling());

        Assert.Equal(4, party.AllMembers.Count);
        Assert.Equal(Party.DefaultNames, party.AllMembers.Select(m => m.Name).ToList());
        Assert.Equal(Party.DefaultClassIds, party.AllMembers.Select(m => m.ClassDef.Id).ToList());
    }

    [Fact]
    public void DefaultParty_EquipsStartingWeapon()
    {
        var party = Party.DefaultParty(TestGameData.NewClassRegistry(), TestGameData.NewItemRegistry(), TestGameData.NewLeveling());
        var knight = party.AllMembers.First(m => m.ClassDef.Id == "knight");
        Assert.Equal("iron_sword", knight.Equipment["weapon"]);
    }

    [Fact]
    public void AddMember_FillsFirstEmptySlot_WhenNoSlotSpecified()
    {
        var party = new Party();
        var idx = party.AddMember(MakeCharacter("A", "knight"));
        Assert.Equal(0, idx);
        var idx2 = party.AddMember(MakeCharacter("B", "mage"));
        Assert.Equal(1, idx2);
    }

    [Fact]
    public void AddMember_ExplicitSlot_ThrowsIfOccupied()
    {
        var party = new Party();
        party.AddMember(MakeCharacter("A", "knight"), slot: 0);
        Assert.Throws<InvalidOperationException>(() => party.AddMember(MakeCharacter("B", "mage"), slot: 0));
    }

    [Fact]
    public void AddMember_ThrowsWhenPartyFull()
    {
        var party = new Party();
        for (var i = 0; i < Party.MaxPartySize; i++)
            party.AddMember(MakeCharacter($"M{i}", "knight"));

        Assert.Throws<InvalidOperationException>(() => party.AddMember(MakeCharacter("Overflow", "knight")));
    }

    [Fact]
    public void DeadMember_StaysInSlot_NotCompacted()
    {
        var party = new Party();
        var a = MakeCharacter("A", "knight");
        var b = MakeCharacter("B", "mage");
        party.AddMember(a, slot: 0);
        party.AddMember(b, slot: 1);

        a.TakeDamage(a.MaxHp + 100); // KO

        Assert.Equal(2, party.AllMembers.Count);   // A is still "in" the party (dead)
        Assert.Single(party.LivingMembers);         // only B is alive
        Assert.Same(a, party.GetMember(0));         // A's slot 0 identity preserved
        Assert.Same(b, party.GetMember(1));
    }

    [Fact]
    public void IsWiped_TrueOnlyWhenAllMembersDead()
    {
        var party = new Party();
        var a = MakeCharacter("A", "knight");
        party.AddMember(a);
        Assert.False(party.IsWiped);

        a.TakeDamage(a.MaxHp + 100);
        Assert.True(party.IsWiped);
    }

    [Fact]
    public void IsFull_ReflectsAllFourSlotsOccupied()
    {
        var party = new Party();
        Assert.False(party.IsFull);
        for (var i = 0; i < Party.MaxPartySize; i++)
            party.AddMember(MakeCharacter($"M{i}", "knight"));
        Assert.True(party.IsFull);
    }

    [Fact]
    public void RemoveMember_ClearsSlot_ReturnsRemovedCharacter()
    {
        var party = new Party();
        var a = MakeCharacter("A", "knight");
        party.AddMember(a, slot: 2);

        var removed = party.RemoveMember(2);

        Assert.Same(a, removed);
        Assert.Null(party.GetMember(2));
    }
}
