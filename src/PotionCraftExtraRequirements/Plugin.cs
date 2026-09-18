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
        NativeRequirementDefinitions.Register();

        harmony = new Harmony(PluginGuid);
        PatchStartupFeatures();
        Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void PatchStartupFeatures()
    {
        // Install localization before any custom requirements can enter the pool.
        Logger.LogInfo("Patching mod localization.");
        harmony.PatchAll(typeof(ModLocalizationPatches));
        Logger.LogInfo("Patching custom requirement unlock chapters.");
        harmony.PatchAll(typeof(RequirementUnlockChapterPatch));
        Logger.LogInfo("Patching native requirement compatibility.");
        harmony.PatchAll(typeof(NativeRequirementCompatibilityPatches));
        harmony.PatchAll(typeof(NativeIngredientTripleConflictPatch));
        Logger.LogInfo("Patching generated requirement text formatting.");
        harmony.PatchAll(typeof(SimpleRecipePatches));
        harmony.PatchAll(typeof(DynamicRequirementRewardPatches));
        harmony.PatchAll(typeof(CurrentCustomerBudgetPatches));
        Logger.LogInfo("Patching requirement pool injection.");
        harmony.PatchAll(typeof(RequirementPoolPatch));
        Logger.LogInfo("Startup patches installed.");
    }

    private void OnDestroy()
    {
        CurrentCustomerBudget.Clear();
        harmony?.UnpatchSelf();
    }
}
