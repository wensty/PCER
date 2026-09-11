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
    private const int MinimumAmount = 1;
    private const float DistanceStep = 7.2f;
    private const float AdditionalEffectsAllowance = 4.0f;
    private const float NoSaltsMultiplier = 1.75f;
    private static readonly Dictionary<Quest, float> EffectDistanceAllowanceCache =
        new Dictionary<Quest, float>();

    public static bool IsSatisfied(
        Potion potion,
        Quest quest,
        GeneratedQuestRequirement generatedRequirement)
    {
        int maximumAmount = Math.Max(MinimumAmount, generatedRequirement?.intValue1 ?? MinimumAmount);
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
        float maximum = EffectDistanceAllowance(quest);
        float multiplier = 1.0f;

        if (generatedRequirements != null)
        {
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
                    multiplier *= NoSaltsMultiplier;
                    continue;
                }

                if (TryGetHighlanderLimit(generated, out int highlanderLimit))
                {
                    maximum += highlanderLimit;
                    continue;
                }

                if (TryGetLowlanderLimit(generated, out int lowlanderLimit))
                    multiplier *= LowlanderMultiplier(lowlanderLimit);
            }
        }

        return Math.Max(MinimumAmount, Mathf.FloorToInt(maximum * multiplier));
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

        return StripBareTextMarker(value.Replace("{0}", LimitText(generated)));
    }

    private static string StripBareTextMarker(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        int firstTextIndex = 0;
        while (firstTextIndex < value.Length && char.IsWhiteSpace(value[firstTextIndex]))
            firstTextIndex++;

        if (firstTextIndex >= value.Length)
            return value;

        if (value[firstTextIndex] == '#')
        {
            if (firstTextIndex + 1 < value.Length && value[firstTextIndex + 1] == '<')
                return value;

            return value.Remove(firstTextIndex, 1);
        }

        if (value[firstTextIndex] != '<')
            return value;

        int tagEndIndex = value.IndexOf('>', firstTextIndex);
        if (tagEndIndex < 0 || tagEndIndex + 1 >= value.Length)
            return value;

        int textAfterTagIndex = tagEndIndex + 1;
        while (textAfterTagIndex < value.Length && char.IsWhiteSpace(value[textAfterTagIndex]))
            textAfterTagIndex++;

        if (textAfterTagIndex < value.Length && value[textAfterTagIndex] == '#')
            return value.Remove(textAfterTagIndex, 1);

        return value;
    }

    private static string LimitText(GeneratedQuestRequirement generated)
    {
        return Math.Max(MinimumAmount, generated?.intValue1 ?? MinimumAmount).ToString();
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

    private static float EffectDistanceAllowance(Quest quest)
    {
        if (quest != null && EffectDistanceAllowanceCache.TryGetValue(quest, out float cached))
            return cached;

        float farthestDistance = FarthestDesiredEffectDistanceOnDefaultBase(quest);
        float allowance = farthestDistance <= 0f
            ? 0f
            : farthestDistance / DistanceStep;

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

    private static bool TryGetHighlanderLimit(
        GeneratedQuestRequirement generated,
        out int limit)
    {
        limit = 0;
        QuestRequirement requirement = generated?.requirementInQuest?.requirement;
        if (requirement == null)
            return false;

        string requirementName = requirement.name;
        if (requirementName != null
            && requirementName.StartsWith("PCER_Highlander", StringComparison.Ordinal))
        {
            string suffix = requirementName.Substring("PCER_Highlander".Length);
            return int.TryParse(suffix, out limit) && limit >= 1 && limit <= 3;
        }

        return false;
    }

    private static bool TryGetLowlanderLimit(
        GeneratedQuestRequirement generated,
        out int limit)
    {
        limit = 0;
        QuestRequirement requirement = generated?.requirementInQuest?.requirement;
        if (requirement == null)
            return false;

        string requirementName = requirement.name;
        if (requirementName == null)
            return false;

        string suffix = null;
        if (requirementName.StartsWith("PCER_Lowlander", StringComparison.Ordinal))
            suffix = requirementName.Substring("PCER_Lowlander".Length);
        else if (requirementName.StartsWith("Lowlander", StringComparison.Ordinal))
            suffix = requirementName.Substring("Lowlander".Length);

        return suffix != null
            && int.TryParse(suffix, out limit)
            && limit >= 1
            && limit <= 3;
    }

    private static float LowlanderMultiplier(int limit)
    {
        switch (limit)
        {
            case 1:
                return 1.6f;
            case 2:
                return 1.3f;
            case 3:
                return 1.1f;
            default:
                return 1.0f;
        }
    }
}
