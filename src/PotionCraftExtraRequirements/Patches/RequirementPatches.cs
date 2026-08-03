using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PotionCraft.LocalizationSystem;
using PotionCraft.QuestSystem;
using PotionCraft.ScriptableObjects.Ingredient;
using PotionCraft.ScriptableObjects.Potion;

namespace PotionCraftExtraRequirements;

[HarmonyPatch]
internal static class RequirementPoolPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuestRequirementInQuest), nameof(QuestRequirementInQuest.Initialize))]
    private static void InitializePostfix()
    {
        RequirementCatalog.InjectIntoGamePool();
    }
}

[HarmonyPatch(typeof(QuestRequirement), nameof(QuestRequirement.GetChapterToUnlock))]
internal static class RequirementUnlockChapterPatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestRequirement __instance, ref int __result)
    {
        if (!RequirementCatalog.TryGet(__instance, out RequirementDefinition definition))
            return true;
        __result = definition.UnlockChapter.Value;
        return false;
    }
}

[HarmonyPatch]
internal static class NativeRequirementCompatibilityPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuestRequirementNoSalts), nameof(QuestRequirementNoSalts.UpdateGeneratedRequirement))]
    private static void NoSaltsPostfix(
        QuestRequirementNoSalts __instance,
        List<GeneratedQuestRequirement> generatedRequirements,
        ref bool __result)
    {
        if (!__result || __instance is ConfigurableQuestRequirement)
            return;
        __result = !generatedRequirements.Any(IsBroadCategoryRequirement);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuestRequirementMaxIngredients), nameof(QuestRequirementMaxIngredients.UpdateGeneratedRequirement))]
    private static void MaxIngredientsPostfix(
        List<GeneratedQuestRequirement> generatedRequirements,
        ref bool __result)
    {
        if (__result)
            __result = !generatedRequirements.Any(IsHighlanderRequirement);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(QuestRequirementCertainIngredient), nameof(QuestRequirementCertainIngredient.UpdateGeneratedRequirement))]
    private static void CertainIngredientPostfix(
        QuestRequirementCertainIngredient __instance,
        GeneratedQuestRequirement requirement,
        List<GeneratedQuestRequirement> generatedRequirements,
        ref bool __result)
    {
        if (!__result)
            return;

        if (__instance is QuestRequirementMainIngredient
            && generatedRequirements.Any(IsHighlanderRequirement))
        {
            __result = false;
            return;
        }

        Ingredient ingredient = Ingredient.GetByName(
            requirement.stringValue1,
            returnFirst: false,
            warning: false);
        if (ingredient == null)
            return;

        foreach (GeneratedQuestRequirement generated in generatedRequirements)
        {
            if (RequirementCatalog.TryGet(generated, out RequirementDefinition definition)
                && definition.Tags.Contains(BuiltInRequirementDefinitions.BroadCategoryTag)
                && definition.IsIngredientAllowed != null
                && !definition.IsIngredientAllowed(ingredient))
            {
                __result = false;
                return;
            }
        }
    }

    private static bool IsBroadCategoryRequirement(GeneratedQuestRequirement generated)
    {
        return RequirementCatalog.TryGet(generated, out RequirementDefinition definition)
            && definition.Tags.Contains(BuiltInRequirementDefinitions.BroadCategoryTag);
    }

    private static bool IsHighlanderRequirement(GeneratedQuestRequirement generated)
    {
        return RequirementCatalog.TryGet(generated, out RequirementDefinition definition)
            && definition.Tags.Contains(BuiltInRequirementDefinitions.HighlanderTag);
    }
}

[HarmonyPatch]
internal static class ModLocalizationPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(
        typeof(LocalizationManager),
        nameof(LocalizationManager.GetText),
        new[] { typeof(string), typeof(LocalizationManager.Locale) })]
    private static bool GetTextPrefix(
        string key,
        LocalizationManager.Locale locale,
        ref string __result)
    {
        if (!ModLocalization.TryGetText(key, locale, out string text))
            return true;
        __result = text;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.ContainsKey))]
    private static bool ContainsKeyPrefix(string key, ref bool __result)
    {
        if (!ModLocalization.Contains(key))
            return true;
        __result = true;
        return false;
    }
}
