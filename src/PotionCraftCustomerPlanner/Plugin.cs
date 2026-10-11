using BepInEx;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace PotionCraftCustomerPlanner;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "cn.potioncraft.customer-planner";
    public const string PluginName = "Potion Craft Customer Planner";
    public const string PluginVersion = "0.1.0";

    private Harmony harmony;
    private bool inputPatchesAttempted;

    private void Awake()
    {
        NextCustomerDirector.Configure(Logger);
        NextCustomerWindow.Configure(Config, Logger);

        harmony = new Harmony(PluginGuid);
        Logger.LogInfo("Patching planner input blocking.");
        harmony.PatchAll(typeof(PlannerInputBlockPatch));
        GlobalRequirementGenerationPatch.Install(harmony, Logger);
        harmony.PatchAll(typeof(ExtendedRequirementTextPatch));
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void Update()
    {
        InstallDeferredInputPatchesWhenReady();
        NextCustomerDirector.Update();
        NextCustomerWindow.Update();
    }

    private void InstallDeferredInputPatchesWhenReady()
    {
        if (inputPatchesAttempted || harmony == null)
            return;

        // Detouring ScrollByMouse can initialize Commands and NewInputWrapper.
        // NewInputWrapper indexes PlayerInput.all[0] in its static initializer;
        // triggering it during BepInEx Awake permanently poisons these types.
        var players = PlayerInput.all;
        if (players.Count == 0 || players[0] == null || players[0].actions == null)
            return;

        inputPatchesAttempted = true;
        try
        {
            harmony.PatchAll(typeof(PlannerScrollBlockPatch));
            harmony.PatchAll(typeof(PlannerEscapeBlockPatch));
            Logger.LogInfo("Planner scroll and Escape handling installed after player input became ready.");
        }
        catch (System.Exception exception)
        {
            Logger.LogError($"Planner deferred input patches could not be installed: {exception}");
        }
    }

    private void OnGUI()
    {
        NextCustomerWindow.OnGui();
    }

    private void OnDestroy()
    {
        CustomerPatchInstaller.Unpatch();
        harmony?.UnpatchSelf();
    }
}
