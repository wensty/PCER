using System.Linq;
using HarmonyLib;
using PotionCraft.LocalizationSystem;
using PotionCraft.ManagersSystem;
using PotionCraft.ManagersSystem.Npc;
using PotionCraft.Npc.MonoBehaviourScripts;

namespace PotionCraftCustomerPlanner;

// Long requests can exceed the game's finite parameters_N localization keys.
// Supply only a missing concatenation template for the current long request;
// Key.GetText continues doing the normal parameter localization/formatting.
[HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.GetText),
    new[] { typeof(string), typeof(LocalizationManager.Locale) })]
internal static class ExtendedRequirementTextPatch
{
    private static readonly System.Reflection.PropertyInfo CurrentNpcProperty =
        AccessTools.Property(typeof(NpcManager), "CurrentNpcMonoBehaviour");

    private static void Postfix(string __0, ref string __result)
    {
        const string prefix = "parameters_";
        if (__0 == null || !__0.StartsWith(prefix, System.StringComparison.Ordinal)
            || __result != "#" + __0
            || !int.TryParse(__0.Substring(prefix.Length), out int count))
            return;
        var npc = Managers.Npc == null ? null :
            CurrentNpcProperty?.GetValue(Managers.Npc, null) as NpcMonoBehaviour;
        if (npc == null)
            return;
        int requirements = (npc.mandatoryQuestRequirements?.Count ?? 0)
            + (npc.optionalQuestRequirements?.Count ?? 0);
        if (requirements > 4 && count >= requirements + 1 && count <= requirements + 3)
            __result = string.Concat(Enumerable.Range(0, count).Select(i => "[" + i + "]"));
    }
}
