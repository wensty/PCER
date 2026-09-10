using System;
using System.Collections.Generic;
using System.Linq;
using PotionCraft.ManagersSystem.RecipeMap;
using PotionCraft.LocalizationSystem;
using PotionCraft.QuestSystem;
using PotionCraft.ScriptableObjects;
using PotionCraft.ScriptableObjects.Potion;
using PotionCraft.Settings;
using UnityEngine;

namespace PotionCraftExtraRequirements;

internal static class SimpleRecipeRequirementLogic
{
    private const string FullRequirementName = "PCER_" + BuiltInRequirementDefinitions.SimpleRecipeId;
    private const int BaseIngredientAmount = 1;
    private const float DistanceStep = 8.0f;
    private const int AdditionalEffectsAllowance = 4;
    private const int DullRecipeAllowance = 2;
    private static readonly Dictionary<Quest, int> EffectDistanceAllowanceCache =
        new Dictionary<Quest, int>();

    public static bool IsSatisfied(
        Potion potion,
        Quest quest,
        GeneratedQuestRequirement generatedRequirement)
    {
        int maximumAmount = Math.Max(BaseIngredientAmount, generatedRequirement?.intValue1 ?? BaseIngredientAmount);
        int actualAmount = PotionIngredientFacts.IngredientAmount(potion);
        return actualAmount > 0 && actualAmount <= maximumAmount;
    }

    public static bool CanGenerate(
        Quest quest,
        IReadOnlyList<GeneratedQuestRequirement> generatedRequirements)
    {
        if (generatedRequirements != null && generatedRequirements.Any(IsBroadCategoryRequirement))
            return false;

        return true;
    }

    public static void UpdateGeneratedRequirement(
        Quest quest,
        IReadOnlyList<GeneratedQuestRequirement> generatedRequirements,
        GeneratedQuestRequirement simpleRecipeRequirement)
    {
        if (simpleRecipeRequirement == null)
            return;

        simpleRecipeRequirement.intValue1 = CalculateMaximumIngredientAmount(
            quest,
            generatedRequirements);
        MaterializeText(simpleRecipeRequirement);
    }

    public static int CalculateMaximumIngredientAmount(
        Quest quest,
        IReadOnlyList<GeneratedQuestRequirement> generatedRequirements)
    {
        int maximum = BaseIngredientAmount + EffectDistanceAllowance(quest);

        if (generatedRequirements == null)
            return maximum;

        foreach (GeneratedQuestRequirement generated in generatedRequirements)
        {
            if (generated?.requirementInQuest?.requirement == null)
                continue;

            QuestRequirement requirement = generated.requirementInQuest.requirement;
            if (IsSimpleRecipeRequirement(generated))
                continue;

            if (requirement is QuestRequirementsAdditionalEffects)
            {
                maximum += AdditionalEffectsAllowance;
                continue;
            }

            if (requirement.GetType() == typeof(QuestRequirementNoSalts))
            {
                maximum += DullRecipeAllowance;
                continue;
            }

            if (TryGetHighlanderOrLowlanderLimit(generated, out int limit))
                maximum += AllowanceForLimit(limit);
        }

        return Math.Max(BaseIngredientAmount, maximum);
    }

    public static bool IsSimpleRecipeRequirement(GeneratedQuestRequirement generated)
    {
        return IsSimpleRecipeRequirement(generated?.requirementInQuest?.requirement);
    }

    public static bool IsSimpleRecipeRequirement(QuestRequirement requirement)
    {
        return requirement != null
            && string.Equals(requirement.name, FullRequirementName, StringComparison.Ordinal);
    }

    private static void MaterializeText(GeneratedQuestRequirement generated)
    {
        string limit = LimitText(generated);
        generated.textMale = MaterializeTextField(generated.textMale, limit);
        generated.textFemale = MaterializeTextField(generated.textFemale, limit);
        generated.reactionMale = MaterializeTextField(generated.reactionMale, limit);
        generated.reactionFemale = MaterializeTextField(generated.reactionFemale, limit);
        generated.reactionCompletedPartiallyMale = MaterializeTextField(
            generated.reactionCompletedPartiallyMale,
            limit);
        generated.reactionCompletedPartiallyFemale = MaterializeTextField(
            generated.reactionCompletedPartiallyFemale,
            limit);
    }

    public static string FormatDisplayedText(
        GeneratedQuestRequirement generated,
        string value)
    {
        if (!IsSimpleRecipeRequirement(generated) || string.IsNullOrEmpty(value))
            return value;

        string result = value.Replace("{0}", LimitText(generated));
        if (result.StartsWith("#", StringComparison.Ordinal)
            && !ModLocalization.Contains(result.Substring(1)))
            return result.Substring(1);
        return result;
    }

    private static string LimitText(GeneratedQuestRequirement generated)
    {
        return Math.Max(BaseIngredientAmount, generated?.intValue1 ?? BaseIngredientAmount).ToString();
    }

    private static string MaterializeTextField(string value, string limit)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.StartsWith("#", StringComparison.Ordinal))
        {
            string key = value.Substring(1);
            if (ModLocalization.TryGetText(
                    key,
                    LocalizationManager.CurrentLocale,
                    out string localizedKeyText))
                return localizedKeyText.Replace("{0}", limit);

            return "#" + key.Replace("{0}", limit);
        }

        if (ModLocalization.TryGetText(
                value,
                LocalizationManager.CurrentLocale,
                out string localizedText))
            return localizedText.Replace("{0}", limit);

        return value.Replace("{0}", limit);
    }

    private static int EffectDistanceAllowance(Quest quest)
    {
        if (quest != null && EffectDistanceAllowanceCache.TryGetValue(quest, out int cached))
            return cached;

        float farthestDistance = FarthestDesiredEffectDistanceOnDefaultBase(quest);
        int allowance = farthestDistance <= 0f
            ? 0
            : Mathf.FloorToInt(farthestDistance / DistanceStep);

        if (quest != null)
            EffectDistanceAllowanceCache[quest] = allowance;
        return allowance;
    }

    private static float FarthestDesiredEffectDistanceOnDefaultBase(Quest quest)
    {
        PotionBase potionBase = Settings<RecipeMapManagerPotionBasesSettings>.Asset?.defaultPotionBase;
        if (potionBase == null || quest?.desiredEffects == null)
            return 0f;

        float farthest = 0f;
        foreach (PotionEffect effect in quest.desiredEffects)
        {
            if (effect == null)
                continue;

            try
            {
                if (!potionBase.potionEffectsDictionary.TryGetValue(effect, out PositionAndRotation positionAndRotation))
                    continue;

                farthest = Mathf.Max(farthest, positionAndRotation.Position.magnitude);
            }
            catch
            {
                // Some custom bases/effects may not expose a stable map position.
                // In that case the distance simply contributes no extra allowance.
            }
        }

        return farthest;
    }

    private static bool IsBroadCategoryRequirement(GeneratedQuestRequirement generated)
    {
        return RequirementCatalog.TryGet(generated, out RequirementDefinition definition)
            && definition.Tags.Contains(BuiltInRequirementDefinitions.BroadCategoryTag);
    }

    private static bool TryGetHighlanderOrLowlanderLimit(
        GeneratedQuestRequirement generated,
        out int limit)
    {
        limit = 0;
        QuestRequirement requirement = generated?.requirementInQuest?.requirement;
        if (requirement == null)
            return false;

        if (requirement is QuestRequirementMaxIngredients)
        {
            limit = generated.intValue1;
            return limit >= 1 && limit <= 3;
        }

        string requirementName = requirement.name;
        if (requirementName != null
            && requirementName.StartsWith("PCER_Highlander", StringComparison.Ordinal))
        {
            string suffix = requirementName.Substring("PCER_Highlander".Length);
            return int.TryParse(suffix, out limit) && limit >= 1 && limit <= 3;
        }

        return false;
    }

    private static int AllowanceForLimit(int limit)
    {
        switch (limit)
        {
            case 1:
                return 4;
            case 2:
                return 2;
            case 3:
                return 1;
            default:
                return 0;
        }
    }
}
