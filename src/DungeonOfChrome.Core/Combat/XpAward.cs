namespace DungeonOfChrome.Core.Combat;

/// <summary>One party member's share of a victory's XP, for the victory-screen UI. Port of the
/// {name, xp, leveled, new_level} dicts appended to Python's BattleEngine.xp_awards.</summary>
public sealed class XpAward
{
    public string Name { get; }
    public int Xp { get; }
    public bool Leveled { get; }
    public int NewLevel { get; }

    public XpAward(string name, int xp, bool leveled, int newLevel)
    {
        Name = name;
        Xp = xp;
        Leveled = leveled;
        NewLevel = newLevel;
    }
}
