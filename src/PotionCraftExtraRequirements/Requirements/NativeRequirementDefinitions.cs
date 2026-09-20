using PotionCraft.QuestSystem;
using PotionCraft.ScriptableObjects;
using PotionCraft.ObjectBased.RecipeMap;

namespace PotionCraftExtraRequirements;

internal static class NativeRequirementDefinitions
{
    public static void Register()
    {
        IngredientLimitRegistry.RegisterNative<QuestRequirementsAdditionalEffects>(_ => new IngredientLimitModifier(4f));
        IngredientLimitRegistry.RegisterNative<QuestRequirementMainIngredient>(_ => new IngredientLimitModifier(0.5f, 1.4f));
        IngredientLimitRegistry.RegisterNative<QuestRequirementNeedOneParticularIngredient>(_ => new IngredientLimitModifier(1f));
        IngredientLimitRegistry.RegisterNative<QuestRequirementNoSalts>(_ => new IngredientLimitModifier(multiplier: 1.35f));
        IngredientLimitRegistry.RegisterNative<QuestRequirementMaxIngredients>(generated =>
        {
            int limit = ((QuestRequirementMaxIngredients)generated.requirementInQuest.requirement).maxIngredientsAmount;
            return new IngredientLimitModifier(multiplier: limit == 1 ? 1.6f : limit == 2 ? 1.3f : limit == 3 ? 1.1f : 1f);
        });
        IngredientLimitRegistry.RegisterNative<QuestRequirementOnlyParticularBase>(generated =>
            BaseIndex(generated) == RecipeMapIndexEnum.Water
                ? new IngredientLimitModifier(multiplier: 1.25f) : IngredientLimitModifier.Identity);
        IngredientLimitRegistry.RegisterNative<QuestRequirementNoParticularBase>(generated =>
        {
            var index = BaseIndex(generated);
            return index == RecipeMapIndexEnum.Oil || index == RecipeMapIndexEnum.Wine
                ? new IngredientLimitModifier(multiplier: 1.25f) : IngredientLimitModifier.Identity;
        });
    }

    private static RecipeMapIndexEnum BaseIndex(GeneratedQuestRequirement generated)
    {
        // Prefer the generated target; generic native base requirements choose it at generation time.
        PotionBase potionBase = string.IsNullOrEmpty(generated.stringValue1)
            ? generated.requirementInQuest.potionBase
            : PotionBase.GetByName(generated.stringValue1, returnFirst: false, warning: false);
        return potionBase == null ? RecipeMapIndexEnum.Unknown : potionBase.GetRecipeMapIndexEnum();
    }
}
