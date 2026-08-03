using System.Collections.Generic;
using System.Linq;
using PotionCraft.QuestSystem;
using PotionCraft.ScriptableObjects.Ingredient;
using PotionCraft.ScriptableObjects.Potion;

namespace PotionCraftExtraRequirements;

/// <summary>
/// Inherits a concrete native requirement so the game's private-protected
/// abstract localization member is already implemented by the game assembly.
/// </summary>
public sealed class ConfigurableQuestRequirement : QuestRequirementNoSalts
{
    private RequirementDefinition Definition
    {
        get
        {
            if (!RequirementCatalog.TryGet(this, out RequirementDefinition definition))
                throw new KeyNotFoundException($"Unknown custom requirement: {name}");
            return definition;
        }
    }

    public override float GetCostMultiplier(Potion potion, Quest quest)
    {
        return Definition.CostMultiplier.Value;
    }

    public override int GetPopularityToAdd(Potion potion, Quest quest)
    {
        return Definition.PopularityBonus.Value;
    }

    public override bool IsRequirementCompleted(
        Potion potion,
        Quest quest,
        GeneratedQuestRequirement generatedRequirement)
    {
        return Definition.IsSatisfied(potion, quest, generatedRequirement);
    }

    public override bool UpdateGeneratedRequirement(
        Quest quest,
        List<GeneratedQuestRequirement> generatedRequirements,
        GeneratedQuestRequirement requirement)
    {
        return Definition.CanGenerate(quest, generatedRequirements);
    }

    public override bool IsCompatibleWithOtherRequirements(
        List<GeneratedQuestRequirement> generatedRequirements)
    {
        RequirementDefinition candidate = Definition;
        if (!RequirementCatalog.IsCompatible(candidate, generatedRequirements))
            return false;

        foreach (GeneratedQuestRequirement generated in generatedRequirements)
        {
            QuestRequirement existing = generated.requirementInQuest.requirement;

            if (candidate.Tags.Contains(BuiltInRequirementDefinitions.BroadCategoryTag))
            {
                if (existing is QuestRequirementNoSalts)
                    return false;

                if (existing is QuestRequirementCertainIngredient)
                {
                    Ingredient ingredient = Ingredient.GetByName(
                        generated.stringValue1,
                        returnFirst: false,
                        warning: false);
                    if (ingredient != null
                        && candidate.IsIngredientAllowed != null
                        && !candidate.IsIngredientAllowed(ingredient))
                        return false;
                }
            }

            if (candidate.Tags.Contains(BuiltInRequirementDefinitions.HighlanderTag)
                && (existing is QuestRequirementMaxIngredients
                    || existing is QuestRequirementMainIngredient))
                return false;
        }

        return true;
    }
}
