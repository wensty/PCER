namespace HarmonyLib
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class HarmonyPatch : System.Attribute
    {
        public HarmonyPatch(System.Type type, string method) { }
    }
}
namespace PotionCraft.ManagersSystem.Input
{
    public sealed class InputManager { }
}
namespace UnityEngine
{
    public enum KeyCode { Escape }
    public static class Input
    {
        public static bool GetKeyDown(KeyCode key) => throw new System.InvalidOperationException(
            "Legacy input is disabled in this game.");
    }
    public static class Time
    {
        public static int frameCount = 10;
    }
}
namespace UnityEngine.InputSystem
{
    public sealed class Keyboard
    {
        public static Keyboard current;
        public readonly TestKey escapeKey = new();
    }
    public sealed class TestKey
    {
        public bool wasPressedThisFrame;
    }
}
namespace PotionCraftCustomerPlanner
{
    internal static class NextCustomerWindow
    {
        public static bool Visible = true;
        public static readonly PlannerChildWindows Windows = new();
        static NextCustomerWindow() => Windows.Open(PlannerChildWindow.Main);
        public static bool WasEscapeHandledThisFrame => Windows.WasEscapeHandled(UnityEngine.Time.frameCount);
        public static bool TryHandleEscape()
        {
            if (WasEscapeHandledThisFrame) return true;
            if (!Visible || !Windows.TryCloseLast(UnityEngine.Time.frameCount, out var window)) return false;
            if (window == PlannerChildWindow.Main) Visible = false;
            return true;
        }
    }
}
