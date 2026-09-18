using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PotionCraft.ManagersSystem;
using PotionCraft.Npc.Parts.Settings;
using PotionCraft.ObjectBased.UIElements.Dialogue;
using PotionCraft.QuestSystem;
using PotionCraft.ScriptableObjects.Potion;

namespace PotionCraftExtraRequirements;

[HarmonyPatch]
internal static class DynamicRequirementRewardPatches
{
    // This layer has the generated requirement (including its raw limit), unlike
    // QuestRequirement.GetCostMultiplier, which receives only the potion/quest.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GeneratedQuestRequirement), "GetRequirementsCostBonuses")]
    internal static void CostBonusesPostfix(Potion potion, Quest quest,
        List<GeneratedQuestRequirement> requirements, ref IEnumerable<float> __result)
    {
        __result = __result.Select((ordinaryMultiplier, index) =>
        {
            GeneratedQuestRequirement generated = requirements[index];
            if (generated.requirementInQuest.requirement is ConfigurableQuestRequirement
                && CurrentCustomerBudget.TryGet(quest, generated.requirementInQuest.requirement, out var current)
                && ReferenceEquals(current, generated))
                return ordinaryMultiplier; // Already evaluated by the requirement's native interface.
            if (RequirementCatalog.TryGet(generated, out RequirementDefinition definition)
                && definition.DynamicCostMultiplier != null
                && generated.IsRequirementCompleted(potion, quest))
                // Non-current groups still carry their own generated parameters.
                // Do not use a native value evaluated against the active customer.
                return definition.DynamicCostMultiplier(potion, quest, generated,
                    definition.CostMultiplier?.Value ?? ordinaryMultiplier);
            return ordinaryMultiplier;
        });
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GeneratedQuestRequirement), "GetRequirementsPopularityBonuses")]
    internal static void PopularityBonusesPostfix(Potion potion, Quest quest,
        List<GeneratedQuestRequirement> requirements, ref IEnumerable<int> __result)
    {
        __result = __result.Select((ordinaryBonus, index) =>
        {
            GeneratedQuestRequirement generated = requirements[index];
            if (generated.requirementInQuest.requirement is ConfigurableQuestRequirement
                && CurrentCustomerBudget.TryGet(quest, generated.requirementInQuest.requirement, out var current)
                && ReferenceEquals(current, generated))
                return ordinaryBonus;
            if (RequirementCatalog.TryGet(generated, out RequirementDefinition definition)
                && definition.DynamicPopularityBonus != null
                && generated.IsRequirementCompleted(potion, quest))
                return definition.DynamicPopularityBonus(potion, quest, generated,
                    definition.PopularityBonus?.Value ?? ordinaryBonus);
            return ordinaryBonus;
        });
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
