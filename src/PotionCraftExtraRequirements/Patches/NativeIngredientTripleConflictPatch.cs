using System.Collections.Generic;
using HarmonyLib;
using PotionCraft.QuestSystem;

namespace PotionCraftExtraRequirements;

// Check all three entry points so the last requirement is rejected regardless
// of generation order. A pairwise tag conflict would reject valid pairs.
[HarmonyPatch]
internal static class NativeIngredientTripleConflictPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuestRequirementMaxIngredients), nameof(QuestRequirementMaxIngredients.IsCompatibleWithOtherRequirements))]
    private static void MaxIngredientsPostfix(QuestRequirementMaxIngredients __instance,
        List<GeneratedQuestRequirement> generatedRequirements, ref bool __result)
        => Postfix(__instance, generatedRequirements, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuestRequirementMainIngredient), nameof(QuestRequirementMainIngredient.IsCompatibleWithOtherRequirements))]
    private static void MainIngredientPostfix(QuestRequirementMainIngredient __instance,
        List<GeneratedQuestRequirement> generatedRequirements, ref bool __result)
        => Postfix(__instance, generatedRequirements, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuestRequirementNeedOneParticularIngredient), nameof(QuestRequirementNeedOneParticularIngredient.IsCompatibleWithOtherRequirements))]
    private static void ParticularIngredientPostfix(QuestRequirementNeedOneParticularIngredient __instance,
        List<GeneratedQuestRequirement> generatedRequirements, ref bool __result)
        => Postfix(__instance, generatedRequirements, ref __result);

    internal static void Postfix(QuestRequirement __instance,
        List<GeneratedQuestRequirement> generatedRequirements, ref bool __result)
    {
        if (!__result)
            return;

        bool main = false;
        bool particular = false;
        bool lowlanderTwo = false;
        Include(__instance);
        if (generatedRequirements != null)
        {
            foreach (GeneratedQuestRequirement generated in generatedRequirements)
                Include(generated?.requirementInQuest?.requirement);
        }
        __result = !(main && particular && lowlanderTwo);

        void Include(QuestRequirement requirement)
        {
            main |= requirement is QuestRequirementMainIngredient;
            particular |= requirement is QuestRequirementNeedOneParticularIngredient;
            lowlanderTwo |= requirement is QuestRequirementMaxIngredients maximum
                && maximum.maxIngredientsAmount == 2;
        }
    }
}
