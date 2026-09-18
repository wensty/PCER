using System;
using System.Collections.Generic;

namespace PotionCraftCustomerPlanner;

// Slots form a conditional chain: a failed roll stops this source's chain.
// This preserves the native first/second-slot relationship for any slot count.
internal static class RequirementSlotRules
{
    public static IEnumerable<int> RollSlots(IReadOnlyList<int> chances, Func<float> roll)
    {
        for (int i = 0; i < chances.Count; i++)
        {
            if (!Passes(chances[i], roll()))
                yield break;
            yield return i;
        }
    }

    public static bool Passes(int chance, float sample)
    {
        return chance >= 100 || (chance > 0 && sample < chance / 100f);
    }

    public static (int Min, int Max) Range(IReadOnlyList<int> chances)
    {
        int min = 0;
        int max = 0;
        bool guaranteed = true;
        foreach (int chance in chances)
        {
            if (chance <= 0)
                break;
            max++;
            guaranteed &= chance >= 100;
            if (guaranteed)
                min++;
        }
        return (min, max);
    }

    public static ((int Min, int Max) Mandatory, (int Min, int Max) Optional) ConvertedRanges(
        IReadOnlyList<int> mandatory, IReadOnlyList<int> optional, bool allMandatory, bool allOptional)
    {
        var must = Range(mandatory);
        var can = Range(optional);
        var total = (must.Min + can.Min, must.Max + can.Max);
        if (allMandatory)
            return (total, (0, 0));
        if (allOptional)
            return ((0, 0), total);
        return (must, can);
    }
}
