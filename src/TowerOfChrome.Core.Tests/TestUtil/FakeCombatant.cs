using TowerOfChrome.Core.Entities;

namespace TowerOfChrome.Core.Tests.TestUtil;

/// <summary>Minimal concrete Combatant with fully controllable stats, for tests that need
/// precise control over speed/HP without depending on real class/enemy data.</summary>
public sealed class FakeCombatant : Combatant
{
    private readonly Dictionary<string, int> _stats;

    public FakeCombatant(
        string name, int hp = 100, int mp = 50, int speed = 10,
        int str = 0, int dex = 0, int intStat = 0, int vit = 0, int luck = 0) : base(name)
    {
        _stats = new Dictionary<string, int>
        {
            ["hp"] = hp, ["mp"] = mp, ["spd"] = speed,
            ["str"] = str, ["dex"] = dex, ["int"] = intStat, ["vit"] = vit, ["luck"] = luck,
        };
        CurrentHpRaw = hp;
        CurrentMpRaw = mp;
    }

    public override int MaxHp => _stats["hp"];
    public override int MaxMp => _stats["mp"];
    public override int GetStat(string stat) => _stats.GetValueOrDefault(stat, 0);
}
