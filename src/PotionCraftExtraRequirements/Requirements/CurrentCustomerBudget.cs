using System;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using PotionCraft.ManagersSystem;
using PotionCraft.ManagersSystem.Npc;
using PotionCraft.Npc.MonoBehaviourScripts;
using PotionCraft.QuestSystem;

namespace PotionCraftExtraRequirements;

/// <summary>Current-customer state refreshed by native quest application and NPC lifecycle hooks.
/// Refresh runs after committing the full group, before dialogue and trade updates, on the game thread.</summary>
public static class CurrentCustomerBudget
{
    private static readonly PropertyInfo CurrentNpcProperty = typeof(NpcManager).GetProperty(
        "CurrentNpcMonoBehaviour", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static NpcMonoBehaviour owner;
    private static Quest quest;
    private static GeneratedQuestRequirement[] requirements = Array.Empty<GeneratedQuestRequirement>();

    public static float? RawMaximum { get; private set; }

    public static void Refresh() => RefreshFor(GetCurrentNpc());

    internal static NpcMonoBehaviour GetCurrentNpc()
        => Managers.Npc == null ? null : CurrentNpcProperty?.GetValue(Managers.Npc, null) as NpcMonoBehaviour;

    internal static void Clear()
    {
        owner = null;
        quest = null;
        requirements = Array.Empty<GeneratedQuestRequirement>();
        RawMaximum = null;
    }

    // Also used before the native setter emits OnCurrentNpcChanged, so event
    // listeners see the new budget immediately after the current NPC is assigned.
    internal static void RefreshFor(NpcMonoBehaviour npc)
    {
        Clear();
        if (npc == null || npc.currentQuest == null)
            return;

        owner = npc;
        quest = npc.currentQuest;
        requirements = FinalizeRequirements(npc);
        var simple = requirements.FirstOrDefault(SimpleRecipeRequirementLogic.IsSimpleRecipeRequirement);
        if (simple != null && SimpleRecipeReward.TryReadRawMaximum(simple.stringValue1, out float raw))
            RawMaximum = raw;
    }

    // Finalize queued NPCs too, without changing the active customer's context.
    internal static GeneratedQuestRequirement[] FinalizeRequirements(NpcMonoBehaviour npc)
    {
        if (npc == null || npc.currentQuest == null)
            return Array.Empty<GeneratedQuestRequirement>();
        var all = (npc.mandatoryQuestRequirements ?? Enumerable.Empty<GeneratedQuestRequirement>())
            .Concat(npc.optionalQuestRequirements ?? Enumerable.Empty<GeneratedQuestRequirement>())
            .Where(r => r != null).ToArray();
        foreach (var generated in all.Where(SimpleRecipeRequirementLogic.IsSimpleRecipeRequirement))
            SimpleRecipeRequirementLogic.UpdateGeneratedRequirement(npc.currentQuest, all, generated);
        return all;
    }

    internal static bool TryGet(Quest requestedQuest, QuestRequirement requirement,
        out GeneratedQuestRequirement generated)
    {
        generated = null;
        if (owner == null || !ReferenceEquals(owner, GetCurrentNpc())
            || !ReferenceEquals(quest, requestedQuest) || !ReferenceEquals(owner.currentQuest, quest))
            return false;
        generated = requirements.FirstOrDefault(r => ReferenceEquals(r.requirementInQuest?.requirement, requirement));
        return generated != null;
    }
}

[HarmonyPatch]
internal static class CurrentCustomerBudgetPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(NpcManager), "set_CurrentNpcMonoBehaviour")]
    private static void CurrentNpcChanging(NpcMonoBehaviour value)
        => CurrentCustomerBudget.RefreshFor(value);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Quest), nameof(Quest.ApplyPartTo))]
    private static void QuestApplied(NpcMonoBehaviour npcMonoBehaviour)
    {
        if (ReferenceEquals(npcMonoBehaviour, CurrentCustomerBudget.GetCurrentNpc()))
            CurrentCustomerBudget.Refresh();
        else
            CurrentCustomerBudget.FinalizeRequirements(npcMonoBehaviour);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Quest), nameof(Quest.ApplyPartTo))]
    private static void QuestApplying(NpcMonoBehaviour npcMonoBehaviour)
    {
        if (ReferenceEquals(npcMonoBehaviour, CurrentCustomerBudget.GetCurrentNpc()))
            CurrentCustomerBudget.Clear();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NpcMonoBehaviour), nameof(NpcMonoBehaviour.ResetComponentFields))]
    private static void NpcResetting(NpcMonoBehaviour __instance)
    {
        if (ReferenceEquals(__instance, CurrentCustomerBudget.GetCurrentNpc()))
            CurrentCustomerBudget.Clear();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(NpcManager), "OnBeforeLoad")]
    private static void Loading() => CurrentCustomerBudget.Clear();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(NpcManager), "OnLoad")]
    private static void Loaded() => CurrentCustomerBudget.Refresh();
}
