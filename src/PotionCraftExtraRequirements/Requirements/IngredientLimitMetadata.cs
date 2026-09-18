using System;
using PotionCraft.QuestSystem;
using System.Collections.Generic;

namespace PotionCraftExtraRequirements;

// Definition-level budget metadata and its native/custom lookup registry.
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

// Native classes cannot receive fields at runtime. Attach definition metadata
// externally, while custom requirements carry it in RequirementDefinition.
public static class IngredientLimitRegistry
{
    private static readonly Dictionary<Type, Func<GeneratedQuestRequirement, IngredientLimitModifier>> Native =
        new Dictionary<Type, Func<GeneratedQuestRequirement, IngredientLimitModifier>>();

    public static void RegisterNative<T>(Func<GeneratedQuestRequirement, IngredientLimitModifier> modifier)
        where T : QuestRequirement
    {
        Native[typeof(T)] = modifier ?? throw new ArgumentNullException(nameof(modifier));
    }

    public static IngredientLimitModifier Get(GeneratedQuestRequirement generated)
    {
        QuestRequirement requirement = generated?.requirementInQuest?.requirement;
        if (requirement == null)
            return IngredientLimitModifier.Identity;
        if (RequirementCatalog.TryGet(requirement, out RequirementDefinition definition))
            return definition.IngredientLimitModifier?.Invoke(generated) ?? IngredientLimitModifier.Identity;
        // Exact types prevent a custom NoSalts carrier from inheriting native metadata.
        return Native.TryGetValue(requirement.GetType(), out var modifier)
            ? modifier(generated) ?? IngredientLimitModifier.Identity
            : IngredientLimitModifier.Identity;
    }
}
