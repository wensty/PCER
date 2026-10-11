using HarmonyLib;
using PotionCraft.ManagersSystem.Input;
using UnityEngine.InputSystem;

namespace PotionCraftCustomerPlanner;

[HarmonyPatch(typeof(InputManager), "OnEscapeClicked")]
internal static class PlannerEscapeBlockPatch
{
    private static bool Prefix(ref bool __result)
    {
        // Only intercept keyboard Escape. Gamepad back keeps its normal behavior.
        if (!(Keyboard.current?.escapeKey.wasPressedThisFrame ?? false)
            && !NextCustomerWindow.WasEscapeHandledThisFrame)
            return true;
        if (!NextCustomerWindow.TryHandleEscape())
            return true;

        // The close-command listener opens the pause menu when this returns false.
        __result = true;
        return false;
    }
}
