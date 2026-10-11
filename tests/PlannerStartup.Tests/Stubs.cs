// Lifecycle doubles let the actual Plugin run without loading Unity or the game.
namespace BepInEx
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class BepInPlugin : System.Attribute
    {
        public BepInPlugin(string guid, string name, string version) { }
    }
    public class BaseUnityPlugin
    {
        public TestLogger Logger { get; } = new();
        public object Config { get; } = new();
    }
    public sealed class TestLogger
    {
        public int Errors;
        public void LogInfo(object message) { }
        public void LogError(object message) => Errors++;
    }
}
namespace UnityEngine.InputSystem
{
    public sealed class PlayerInput
    {
        public static System.Collections.Generic.List<PlayerInput> all = new();
        public object actions;
    }
}
namespace HarmonyLib
{
    public sealed class Harmony
    {
        public static int ScrollAttempts, EscapeAttempts, UnpatchCalls;
        public static bool FailScrollPatch;
        public Harmony(string id) { }
        public void PatchAll(System.Type target)
        {
            if (target == typeof(PotionCraftCustomerPlanner.PlannerEscapeBlockPatch))
            {
                EscapeAttempts++;
                return;
            }
            if (target != typeof(PotionCraftCustomerPlanner.PlannerScrollBlockPatch)) return;
            ScrollAttempts++;
            if (UnityEngine.InputSystem.PlayerInput.all.Count == 0)
                throw new System.Exception("Game input initialized before a player exists.");
            if (FailScrollPatch) throw new System.Exception("Simulated detour failure.");
        }
        public void UnpatchSelf() => UnpatchCalls++;
    }
}
namespace PotionCraftCustomerPlanner
{
    internal static class NextCustomerDirector
    {
        public static void Configure(object logger) { }
        public static void Update() { }
    }
    internal static class NextCustomerWindow
    {
        public static int UpdateCalls;
        public static void Configure(object config, object logger) { }
        public static void Update() => UpdateCalls++;
        public static void OnGui() { }
    }
    internal static class GlobalRequirementGenerationPatch
    {
        public static void Install(HarmonyLib.Harmony harmony, object logger) { }
    }
    internal static class CustomerPatchInstaller
    {
        public static int UnpatchCalls;
        public static void Unpatch() => UnpatchCalls++;
    }
    internal sealed class PlannerInputBlockPatch { }
    internal sealed class PlannerScrollBlockPatch { }
    internal sealed class PlannerEscapeBlockPatch { }
    internal sealed class ExtendedRequirementTextPatch { }
}
