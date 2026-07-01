using TowerOfChrome.Core.Combat;
using TowerOfChrome.Core.Tests.TestUtil;

namespace TowerOfChrome.Core.Tests.Combat;

public class DamageCalculatorTests
{
    private static DamageCalculator NewCalc(params double[] scriptedDoubles) =>
        new(TestGameData.NewCombatConfig(), new ScriptedRandomSource(doubles: scriptedDoubles));

    [Fact]
    public void CalcPhysical_BasicFormula_NoStatusNoCrit()
    {
        var actor = new FakeCombatant("Actor", str: 10);
        var target = new FakeCombatant("Target", vit: 10);
        // atk = 10*1*1.8=18; def = 10*0.5=5; raw = 13. critRoll=0.5 (no crit, base chance 0.05).
        // varianceRoll=0.5 -> variance = 0.88 + 0.5*0.24 = 1.0.
        var calc = NewCalc(0.5, 0.5);

        var (damage, wasCrit) = calc.CalcPhysical(actor, target);

        Assert.False(wasCrit);
        Assert.Equal(13, damage);
    }

    [Fact]
    public void CalcPhysical_Crit_DoublesRawDamageBeforeVariance()
    {
        var actor = new FakeCombatant("Actor", str: 10);
        var target = new FakeCombatant("Target", vit: 10);
        // raw=13; crit_multiplier=2.0 -> 26; variance=1.0 via raw 0.5.
        var calc = NewCalc(0.01, 0.5); // 0.01 < 0.05 base crit chance -> crit

        var (damage, wasCrit) = calc.CalcPhysical(actor, target);

        Assert.True(wasCrit);
        Assert.Equal(26, damage);
    }

    [Fact]
    public void CalcPhysical_MinimumDamageIsOne_EvenWhenDefenseExceedsAttack()
    {
        var actor = new FakeCombatant("Actor", str: 1);
        var target = new FakeCombatant("Target", vit: 1000);
        var calc = NewCalc(0.99, 0.5); // no crit

        var (damage, _) = calc.CalcPhysical(actor, target);

        Assert.Equal(1, damage);
    }

    [Fact]
    public void CalcPhysical_BerserkActor_IncreasesAttack()
    {
        var actor = new FakeCombatant("Actor", str: 10);
        actor.AddStatus("berserk");
        var target = new FakeCombatant("Target", vit: 10);
        // atk = 18*1.40=25.2; def=5; raw=20.2; variance=1.0.
        var calc = NewCalc(0.99, 0.5);

        var (damage, _) = calc.CalcPhysical(actor, target);

        Assert.Equal((int)(20.2 * 1.0), damage);
    }

    [Fact]
    public void CalcPhysical_GuardingTarget_IncreasesEffectiveDefense()
    {
        var actor = new FakeCombatant("Actor", str: 10);
        var target = new FakeCombatant("Target", vit: 10);
        target.AddStatus("guarding");
        // def = 5 / 0.55 = 9.0909...; atk=18; raw=8.909...; variance=1.0.
        var calc = NewCalc(0.99, 0.5);

        var (damage, _) = calc.CalcPhysical(actor, target);

        var expectedRaw = 18.0 - (10 * 0.5 / 0.55);
        Assert.Equal((int)expectedRaw, damage);
    }

    [Fact]
    public void CalcMagical_CritChance_UsesPhysicalBaseChance_CombinedWithMagicalLuckFactor()
    {
        // Verified quirk (combat/damage.py:102-104): magical.json has no crit_base_chance, so
        // the formula is physical.CritBaseChance + actor.Luck * MAGICAL's LuckCritFactor —
        // not a full fallback to physical's luck factor. With luck=100:
        //   correct (magical factor 0.003):  0.05 + 100*0.003 = 0.35
        //   wrong   (physical factor 0.004): 0.05 + 100*0.004 = 0.45
        // A scripted crit roll of 0.40 falls strictly between these, so it discriminates
        // between a correct and an incorrect (over-simplified) port.
        var actor = new FakeCombatant("Actor", intStat: 10, luck: 100);
        var target = new FakeCombatant("Target", intStat: 10);
        var calc = NewCalc(0.40, 0.5);

        var (_, wasCrit) = calc.CalcMagical(actor, target);

        Assert.False(wasCrit); // 0.40 is not < 0.35 (correct), but IS < 0.45 (the wrong formula)
    }

    [Fact]
    public void CalcMagical_Crit_AlwaysUsesPhysicalCritMultiplier()
    {
        var actor = new FakeCombatant("Actor", intStat: 10, luck: 100);
        var target = new FakeCombatant("Target", intStat: 10);
        // atk=10*1*2.0=20; res=10*0.4=4; raw=16; crit (roll 0.01 < 0.35) -> *2.0 (physical's) = 32.
        var calc = NewCalc(0.01, 0.5);

        var (damage, wasCrit) = calc.CalcMagical(actor, target);

        Assert.True(wasCrit);
        Assert.Equal(32, damage);
    }

    [Fact]
    public void CalcPhysicalMagical_AveragesBothFormulas_IntegerDivisionTruncates()
    {
        var actor = new FakeCombatant("Actor", str: 10, intStat: 10, luck: 0);
        var target = new FakeCombatant("Target", vit: 10, intStat: 10);
        // physical: raw=13, magical: raw=16 (both no crit, variance=1.0 each).
        // (13+16)/2 = 14 (integer division of 29/2=14).
        var calc = NewCalc(0.99, 0.5, 0.99, 0.5);

        var (damage, wasCrit) = calc.CalcPhysicalMagical(actor, target);

        Assert.False(wasCrit);
        Assert.Equal(14, damage);
    }

    [Fact]
    public void CalcHeal_BasicFormula()
    {
        var actor = new FakeCombatant("Healer", intStat: 10);
        // raw = 10*1*2.2=22; variance=1.0.
        var calc = NewCalc(0.5);

        var healed = calc.CalcHeal(actor);

        Assert.Equal(22, healed);
    }

    [Fact]
    public void CalcHeal_Blessed_IncreasesAmount()
    {
        var actor = new FakeCombatant("Healer", intStat: 10);
        actor.AddStatus("blessed");
        // raw = 22*1.15=25.3; variance=1.0.
        var calc = NewCalc(0.5);

        var healed = calc.CalcHeal(actor);

        Assert.Equal((int)(25.3), healed);
    }

    [Fact]
    public void CalcDot_PoisonAndBurn_StackAdditively()
    {
        var target = new FakeCombatant("Target", hp: 200);
        target.AddStatus("poison"); // 5% of 200 = 10
        target.AddStatus("burn");   // flat 6
        var calc = NewCalc();

        Assert.Equal(16, calc.CalcDot(target));
    }

    [Fact]
    public void CalcDot_NoStatuses_ReturnsZero()
    {
        var target = new FakeCombatant("Target", hp: 200);
        Assert.Equal(0, NewCalc().CalcDot(target));
    }

    [Fact]
    public void CalcRegen_RequiresRegeneratingStatus()
    {
        var target = new FakeCombatant("Target", hp: 200);
        var calc = NewCalc();
        Assert.Equal(0, calc.CalcRegen(target));

        target.AddStatus("regenerating"); // 6% of 200 = 12
        Assert.Equal(12, calc.CalcRegen(target));
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.40)]   // equal speed -> base chance
    [InlineData(100.0, 0.0, 0.95)] // huge party speed advantage -> clamped to max 0.95
    [InlineData(0.0, 100.0, 0.05)] // huge enemy speed advantage -> clamped to min 0.05
    public void FleeChance_ClampsBetween5And95Percent(double partySpd, double enemySpd, double expected)
    {
        var calc = NewCalc();
        Assert.Equal(expected, calc.FleeChance(partySpd, enemySpd), precision: 5);
    }
}
