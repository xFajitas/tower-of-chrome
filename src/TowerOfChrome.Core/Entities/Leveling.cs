using TowerOfChrome.Core.Data;
using TowerOfChrome.Core.Data.DataModels;

namespace TowerOfChrome.Core.Entities;

/// <summary>Port of entities/leveling.py. Config-driven from leveling.json.
/// xp_to_next(level) = floor(xp_base * level ^ xp_exponent).</summary>
public sealed class Leveling
{
    private readonly LevelingData _cfg;
    private readonly Dictionary<int, int> _xpToNextCache = new();

    public Leveling(LevelingData cfg) => _cfg = cfg;
    public Leveling(IGameDataSource dataSource) : this(dataSource.LoadLeveling()) { }

    public int MaxLevel => _cfg.MaxLevel;

    /// <summary>XP required to advance from `level` to `level + 1`.</summary>
    public int XpToNext(int level)
    {
        if (_xpToNextCache.TryGetValue(level, out var cached))
            return cached;
        var value = (int)Math.Floor(_cfg.XpBase * Math.Pow(level, _cfg.XpExponent));
        _xpToNextCache[level] = value;
        return value;
    }

    /// <summary>Cumulative XP required to reach `targetLevel` from level 1.</summary>
    public int TotalXpForLevel(int targetLevel)
    {
        var sum = 0;
        for (var lvl = 1; lvl < targetLevel; lvl++)
            sum += XpToNext(lvl);
        return sum;
    }
}
