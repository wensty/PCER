using BepInEx.Logging;
using HarmonyLib;
using PotionCraft.ManagersSystem.Input;
using PotionCraft.ManagersSystem.Npc;
#if DEBUG
using PotionCraft.ManagersSystem.Debug;
using PotionCraft.Npc.MonoBehaviourScripts;
using PotionCraft.ObjectBased.UIElements.Dialogue;
#endif

namespace PotionCraftCustomerPlanner;

internal static class CustomerPatchInstaller
{
    private const string HarmonyId = Plugin.PluginGuid + ".next-customer";
    private static Harmony harmony;
    private static bool attempted;

    public static bool EnsurePatched(ManualLogSource logger)
    {
        if (harmony != null)
            return true;
        if (attempted)
            return false;

        attempted = true;
        harmony = new Harmony(HarmonyId);

        System.Reflection.MethodInfo callNextNpc = AccessTools.Method(
            typeof(NpcManager),
            "CallNextNpc",
            new[] { typeof(bool) });
        if (callNextNpc == null)
        {
            logger?.LogWarning("Next customer patch failed: NpcManager.CallNextNpc overload was not found.");
            harmony = null;
            return false;
        }

        System.Reflection.MethodInfo onBeforeLoad = AccessTools.Method(
            typeof(NpcManager),
            "OnBeforeLoad",
            System.Type.EmptyTypes);
        if (onBeforeLoad == null)
            logger?.LogWarning("Next customer load-state guard was not installed: NpcManager.OnBeforeLoad was not found.");

        System.Reflection.MethodInfo onLoad = AccessTools.Method(
            typeof(NpcManager),
            "OnLoad",
            System.Type.EmptyTypes);
        if (onLoad == null)
            logger?.LogWarning("Next customer load-state guard was not installed: NpcManager.OnLoad was not found.");

        harmony.Patch(
            callNextNpc,
            postfix: new HarmonyMethod(typeof(CustomerPatchInstaller), nameof(CallNextNpcPostfix)));
        if (onBeforeLoad != null)
        {
            harmony.Patch(
                onBeforeLoad,
                prefix: new HarmonyMethod(typeof(CustomerPatchInstaller), nameof(NpcManagerOnBeforeLoadPrefix)));
        }
        if (onLoad != null)
        {
            harmony.Patch(
                onLoad,
                postfix: new HarmonyMethod(typeof(CustomerPatchInstaller), nameof(NpcManagerOnLoadPostfix)));
        }
#if DEBUG
        RejectionDiagnosticsPatches.Install(harmony, logger);
#endif
        logger?.LogInfo("Current-customer planner patches installed.");
        return true;
    }

    public static void Unpatch()
    {
        harmony?.UnpatchSelf();
        harmony = null;
        attempted = false;
    }

    private static void CallNextNpcPostfix(NpcManager __instance)
    {
        NextCustomerDirector.ReplaceCurrentNpcIfNeeded(__instance);
    }

    private static void NpcManagerOnBeforeLoadPrefix()
    {
        NextCustomerDirector.ResetTransientStateForSaveLoad("before NPC load");
    }

    private static void NpcManagerOnLoadPostfix()
    {
        NextCustomerDirector.ResetTransientStateForSaveLoad("after NPC load");
    }
}

#if DEBUG
internal static class RejectionDiagnosticsPatches
{
    private static ManualLogSource logger;

    public static void Install(Harmony harmony, ManualLogSource logSource)
    {
        logger = logSource;
        Patch(harmony, typeof(NpcManager), "KickNpc", nameof(NpcManagerKickNpcPrefix));
        Patch(harmony, typeof(DebugManager), "SkipNpc", nameof(DebugManagerSkipNpcPrefix));
        Patch(harmony, typeof(DialogueBox), "ForceEndDialogue", nameof(DialogueBoxForceEndDialoguePrefix));
        Patch(harmony, typeof(NpcTrading), "GetRewardOnKick", nameof(NpcTradingGetRewardOnKickPrefix));
        Patch(harmony, typeof(NpcTrading), "UpdateMoodByPotion", nameof(NpcTradingUpdateMoodByPotionPrefix));
    }

    private static void Patch(Harmony harmony, System.Type type, string methodName, string prefixName)
    {
        System.Reflection.MethodInfo original = AccessTools.Method(type, methodName);
        System.Reflection.MethodInfo prefix = AccessTools.Method(typeof(RejectionDiagnosticsPatches), prefixName);
        if (original == null || prefix == null)
        {
            logger?.LogWarning($"Rejection diagnostics patch skipped: {type.FullName}.{methodName}");
            return;
        }

        harmony.Patch(original, prefix: new HarmonyMethod(prefix));
    }

    private static void NpcManagerKickNpcPrefix(NpcManager __instance)
    {
        RejectionDiagnostics.Write("NpcManager.KickNpc", RejectionDiagnostics.CurrentNpc(__instance), includeStack: true);
    }

    private static void DebugManagerSkipNpcPrefix(bool incrementClosenessManuallyOnKickCurrentNpc)
    {
        RejectionDiagnostics.Write(
            $"DebugManager.SkipNpc(incrementCloseness={incrementClosenessManuallyOnKickCurrentNpc})",
            RejectionDiagnostics.CurrentNpc(PotionCraft.ManagersSystem.Managers.Npc),
            includeStack: true);
    }

    private static void DialogueBoxForceEndDialoguePrefix()
    {
        RejectionDiagnostics.Write(
            "DialogueBox.ForceEndDialogue",
            RejectionDiagnostics.CurrentNpc(PotionCraft.ManagersSystem.Managers.Npc),
            includeStack: true);
    }

    private static void NpcTradingGetRewardOnKickPrefix(NpcTrading __instance)
    {
        RejectionDiagnostics.Write("NpcTrading.GetRewardOnKick", __instance?.npc, includeStack: true);
    }

    private static void NpcTradingUpdateMoodByPotionPrefix(NpcTrading __instance, bool isPotionSuitable)
    {
        RejectionDiagnostics.Write(
            $"NpcTrading.UpdateMoodByPotion(isSuitable={isPotionSuitable})",
            __instance?.npc,
            includeStack: !isPotionSuitable);
    }
}
#endif

[HarmonyPatch(typeof(InputManager), nameof(InputManager.HasInputGotToBeDisabled))]
internal static class PlannerInputBlockPatch
{
    private static void Postfix(ref bool __result)
    {
        if (NextCustomerWindow.ShouldBlockGameInput)
            __result = true;
    }
}
