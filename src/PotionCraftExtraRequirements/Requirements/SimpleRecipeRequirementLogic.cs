using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using PotionCraft.ManagersSystem;
using PotionCraft.ManagersSystem.RecipeMap;
using PotionCraft.Npc.Parts.Settings;
using PotionCraft.ObjectBased.UIElements.Dialogue;
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
    }

    public static float CalculateRawMaximumIngredientAmount(
        Quest quest, IReadOnlyList<GeneratedQuestRequirement> generatedRequirements)
    {
        return IngredientLimitModifier.Combine(BaseAllowance + FarthestDesiredEffectDistanceOnDefaultBase(quest) / DistanceStep,
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
        return value.Replace("{0}", LimitText(generated)).Replace("{1}", thresholdText);
    }

    private static string LimitText(GeneratedQuestRequirement generated)
    {
        return Math.Max(MinimumAmount, generated?.intValue1 ?? MinimumAmount).ToString();
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

internal static class SimpleRecipeReward
{
    private const string RawMaximumPrefix = "PCER_SimpleRecipeRaw:";

    public static string EncodeRawMaximum(float rawMaximum)
        => RawMaximumPrefix + rawMaximum.ToString("R", CultureInfo.InvariantCulture);

    public static bool TryReadRawMaximum(string encoded, out float rawMaximum)
    {
        rawMaximum = 0;
        return encoded != null && encoded.StartsWith(RawMaximumPrefix, StringComparison.Ordinal)
            && float.TryParse(encoded.Substring(RawMaximumPrefix.Length), NumberStyles.Float,
                CultureInfo.InvariantCulture, out rawMaximum)
            && !float.IsNaN(rawMaximum) && !float.IsInfinity(rawMaximum);
    }

    public static bool IsEfficient(int actual, int displayedMaximum, float rawMaximum)
    {
        return TryGetEfficiencyThreshold(displayedMaximum, rawMaximum, out int threshold)
            && actual > 0 && actual <= threshold;
    }

    public static bool TryGetEfficiencyThreshold(int displayedMaximum, float rawMaximum, out int threshold)
    {
        threshold = 0;
        if (displayedMaximum < 1 || rawMaximum <= 0f
            || float.IsNaN(rawMaximum) || float.IsInfinity(rawMaximum))
            return false;
        threshold = (int)Math.Min(displayedMaximum, Math.Floor(rawMaximum * 0.6f));
        return threshold >= 1;
    }
}

[HarmonyPatch]
internal static class SimpleRecipePatches
{
    [HarmonyPostfix]
    [HarmonyPatch(
        typeof(GeneratedQuestRequirement),
        nameof(GeneratedQuestRequirement.GetLocalizedText))]
    private static void GetLocalizedTextPostfix(
        GeneratedQuestRequirement __instance,
        ref string __result)
    {
        __result = SimpleRecipeRequirementLogic.FormatDisplayedText(__instance, __result);
    }

    [HarmonyPostfix]
    [HarmonyPatch(
        typeof(GeneratedQuestRequirement),
        nameof(GeneratedQuestRequirement.GetLocalizedReaction))]
    private static void GetLocalizedReactionPostfix(
        GeneratedQuestRequirement __instance,
        ref string __result)
    {
        __result = SimpleRecipeRequirementLogic.FormatDisplayedText(__instance, __result);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Quest), nameof(Quest.GetPotionReactionKey))]
    internal static void ReactionPostfix(Quest __instance, Potion potion, Gender.GenderSet gender,
        List<GeneratedQuestRequirement> requirements, List<bool> requirementsCompleteStatus,
        ref string __result)
    {
        if (potion == null || requirements == null || requirementsCompleteStatus == null
            || requirements.Count != requirementsCompleteStatus.Count
            || requirementsCompleteStatus.Any(completed => !completed)
            || Managers.Dialogue?.State == DialogueState.ClosenessPotionRequest
            || Potion.GetPotionReview(potion.Effects, __instance.desiredEffects).maxTier == 0)
            return;
        var simple = requirements.FirstOrDefault(SimpleRecipeRequirementLogic.IsSimpleRecipeRequirement);
        if (simple == null || !SimpleRecipeRequirementLogic.IsSatisfied(potion, __instance, simple)
            || !SimpleRecipeRequirementLogic.TryGetEfficiencyThreshold(simple, out _)
            || SimpleRecipeRequirementLogic.IsEfficient(potion, simple))
            return;

        // Like native additional effects, partial fulfillment replaces the generic
        // tier reaction. Preserve an existing requirement-specific partial response.
        string partial = simple.GetLocalizedReaction(gender, isCompletedPartially: true);
        if (string.IsNullOrEmpty(__result)
            || __result.StartsWith("#potion_reaction_max_tier_", System.StringComparison.Ordinal))
            __result = partial;
        else
            __result += "\n" + partial;
    }
}
