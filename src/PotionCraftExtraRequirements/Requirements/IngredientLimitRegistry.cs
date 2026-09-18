using System;
using System.Collections.Generic;
using PotionCraft.QuestSystem;

namespace PotionCraftExtraRequirements;

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
