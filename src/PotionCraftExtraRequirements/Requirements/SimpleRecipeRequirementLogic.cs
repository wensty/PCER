using System;
using System.Collections.Generic;
using System.Linq;
using PotionCraft.ManagersSystem.RecipeMap;
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
    private const float BaseAllowance = 0.2f;
    private const float DistanceStep = 8.0f;
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

        float rawMaximum = CalculateRawMaximumIngredientAmount(quest, generatedRequirements);
        simpleRecipeRequirement.intValue1 = Math.Max(MinimumAmount, Mathf.FloorToInt(rawMaximum));
        // Keep the unrounded value with the generated requirement, so planner
        // cloning/snapshots preserve the bonus threshold independently of NPC state.
        simpleRecipeRequirement.stringValue1 = SimpleRecipeReward.EncodeRawMaximum(rawMaximum);
        // Text fields remain localization keys; expand live amounts only when displayed.
        RequirementLocalizationDiagnostics.LogGenerated(quest, generatedRequirements, simpleRecipeRequirement);
    }

    public static int CalculateMaximumIngredientAmount(
        Quest quest,
        IReadOnlyList<GeneratedQuestRequirement> generatedRequirements)
    {
        return Math.Max(MinimumAmount, Mathf.FloorToInt(CalculateRawMaximumIngredientAmount(quest, generatedRequirements)));
    }

    public static float CalculateRawMaximumIngredientAmount(
        Quest quest, IReadOnlyList<GeneratedQuestRequirement> generatedRequirements)
    {
        return IngredientLimitModifier.Combine(BaseAllowance + EffectDistanceAllowance(quest),
            (generatedRequirements ?? Array.Empty<GeneratedQuestRequirement>())
                .Where(generated => !IsSimpleRecipeRequirement(generated))
                .Select(IngredientLimitRegistry.Get));
    }

    public static bool IsEfficient(Potion potion, GeneratedQuestRequirement generated)
    {
        if (!SimpleRecipeReward.TryReadRawMaximum(generated?.stringValue1, out float rawMaximum))
            return false;
        return SimpleRecipeReward.IsEfficient(PotionIngredientFacts.IngredientAmount(potion),
            Math.Max(MinimumAmount, generated.intValue1), rawMaximum);
    }

    public static float GetCostMultiplier(Potion potion, Quest quest,
        GeneratedQuestRequirement generated, float ordinaryMultiplier)
    {
        if (!IsEfficient(potion, generated)
            || !RequirementCatalog.TryGet(generated, out RequirementDefinition definition))
            return ordinaryMultiplier;
        return definition.EnhancedCostMultiplier?.Value
            ?? definition.DefaultEnhancedCostMultiplier ?? ordinaryMultiplier;
    }

    public static bool TryGetEfficiencyThreshold(GeneratedQuestRequirement generated, out int threshold)
    {
        threshold = 0;
        return SimpleRecipeReward.TryReadRawMaximum(generated?.stringValue1, out float rawMaximum)
            && SimpleRecipeReward.TryGetEfficiencyThreshold(
                Math.Max(MinimumAmount, generated.intValue1), rawMaximum, out threshold);
    }

    public static int GetPopularityBonus(Potion potion, Quest quest,
        GeneratedQuestRequirement generated, int ordinaryBonus)
    {
        if (!IsEfficient(potion, generated)
            || !RequirementCatalog.TryGet(generated, out RequirementDefinition definition))
            return ordinaryBonus;
        return definition.EnhancedPopularityBonus?.Value
            ?? definition.DefaultEnhancedPopularityBonus ?? ordinaryBonus;
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

    public static string FormatDisplayedText(
        GeneratedQuestRequirement generated,
        string value)
    {
        if (!IsSimpleRecipeRequirement(generated) || string.IsNullOrEmpty(value))
            return value;

        string thresholdText = TryGetEfficiencyThreshold(generated, out int threshold)
            ? threshold.ToString() : "—";
        string result = value.Replace("{0}", LimitText(generated)).Replace("{1}", thresholdText);
        RequirementLocalizationDiagnostics.LogDisplayed(generated, value, result);
        return result;
    }

    private static string LimitText(GeneratedQuestRequirement generated)
    {
        return Math.Max(MinimumAmount, generated?.intValue1 ?? MinimumAmount).ToString();
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

}
