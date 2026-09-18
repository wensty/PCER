using HarmonyLib;
using PotionCraft.ManagersSystem.Npc;
using PotionCraft.Npc.MonoBehaviourScripts;
using PotionCraft.QuestSystem;

namespace PotionCraftExtraRequirements;

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
