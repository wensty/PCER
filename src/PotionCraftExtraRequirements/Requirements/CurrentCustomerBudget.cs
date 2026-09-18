using System;
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

        var all = (npc.mandatoryQuestRequirements ?? Enumerable.Empty<GeneratedQuestRequirement>())
            .Concat(npc.optionalQuestRequirements ?? Enumerable.Empty<GeneratedQuestRequirement>())
            .Where(r => r != null).ToArray();
        foreach (var generated in all.Where(SimpleRecipeRequirementLogic.IsSimpleRecipeRequirement))
            SimpleRecipeRequirementLogic.UpdateGeneratedRequirement(npc.currentQuest, all, generated);

        owner = npc;
        quest = npc.currentQuest;
        requirements = all;
        var simple = all.FirstOrDefault(SimpleRecipeRequirementLogic.IsSimpleRecipeRequirement);
        if (simple != null && SimpleRecipeReward.TryReadRawMaximum(simple.stringValue1, out float raw))
            RawMaximum = raw;
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
