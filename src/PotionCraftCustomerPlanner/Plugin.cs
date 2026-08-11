using BepInEx;
using HarmonyLib;

namespace PotionCraftCustomerPlanner;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "cn.potioncraft.customer-planner";
    public const string PluginName = "Potion Craft Customer Planner";
    public const string PluginVersion = "0.1.0";

    private Harmony harmony;

    private void Awake()
    {
        NextCustomerDirector.Configure(Logger);
        NextCustomerWindow.Configure(Config, Logger);

        harmony = new Harmony(PluginGuid);
        Logger.LogInfo("Patching planner input blocking.");
        harmony.PatchAll(typeof(PlannerInputBlockPatch));
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void Update()
    {
        NextCustomerWindow.Update();
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
