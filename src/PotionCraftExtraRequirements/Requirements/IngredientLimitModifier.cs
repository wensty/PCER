using System;
using System.Collections.Generic;

namespace PotionCraftExtraRequirements;

public sealed class IngredientLimitModifier
{
    public static IngredientLimitModifier Identity { get; } = new IngredientLimitModifier();
    public float Allowance { get; }
    public float Multiplier { get; }

    public IngredientLimitModifier(float allowance = 0f, float multiplier = 1f)
    {
        if (float.IsNaN(allowance) || float.IsInfinity(allowance))
            throw new ArgumentOutOfRangeException(nameof(allowance));
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        Allowance = allowance;
        Multiplier = multiplier;
    }

    public static float Combine(float baseAllowance, IEnumerable<IngredientLimitModifier> modifiers)
    {
        float allowance = baseAllowance;
        float multiplier = 1f;
        var ordered = new List<IngredientLimitModifier>(modifiers);
        ordered.Sort((a, b) => a.Allowance != b.Allowance
            ? a.Allowance.CompareTo(b.Allowance) : a.Multiplier.CompareTo(b.Multiplier));
        // Stable arithmetic order keeps the raw limit identical in planner
        // generation-order trials, cloning and whole-group revalidation.
        foreach (var modifier in ordered)
        {
            allowance += modifier.Allowance;
            multiplier *= modifier.Multiplier;
        }
        return allowance * multiplier;
    }
}

