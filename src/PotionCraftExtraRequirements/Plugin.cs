using BepInEx;
using HarmonyLib;

namespace PotionCraftExtraRequirements;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "cn.potioncraft.extra-requirements";
    public const string PluginName = "Potion Craft Extra Requirements";
    public const string PluginVersion = "0.1.0";

    private Harmony harmony;

    private void Awake()
    {
        RequirementCatalog.Configure(Config);
        BuiltInRequirementDefinitions.Register();

        harmony = new Harmony(PluginGuid);
        harmony.PatchAll();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }
}
