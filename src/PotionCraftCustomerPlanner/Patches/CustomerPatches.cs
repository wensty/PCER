using BepInEx.Logging;
using HarmonyLib;
using PotionCraft.ManagersSystem.Input;
using PotionCraft.ManagersSystem.Npc;

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

[HarmonyPatch(typeof(InputManager), nameof(InputManager.HasInputGotToBeDisabled))]
internal static class PlannerInputBlockPatch
{
    private static void Postfix(ref bool __result)
    {
        if (NextCustomerWindow.ShouldBlockGameInput)
            __result = true;
    }
}
