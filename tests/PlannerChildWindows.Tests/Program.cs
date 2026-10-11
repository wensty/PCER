using System.Reflection;
using PotionCraftCustomerPlanner;
using UnityEngine;
using UnityEngine.InputSystem;

void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var mainWindows = new PlannerChildWindows();
mainWindows.Open(PlannerChildWindow.Main);
mainWindows.Open(PlannerChildWindow.RandomRule);
mainWindows.Open(PlannerChildWindow.Picker);
mainWindows.Open(PlannerChildWindow.Main); // Reopening the parent keeps it first.
Check(mainWindows.TryCloseLast(7, out var last) && last == PlannerChildWindow.Picker,
    "The main window must stay below all children.");
Check(mainWindows.TryCloseLast(8, out last) && last == PlannerChildWindow.RandomRule,
    "Close the other child before the main window.");
Check(mainWindows.TryCloseLast(9, out last) && last == PlannerChildWindow.Main,
    "The main planner is the final window to close.");
Check(mainWindows.TryCloseLast(9, out last) && last == null,
    "Closing the main window must consume duplicate dispatches.");
Check(!mainWindows.TryCloseLast(10, out _), "A later press must pass through after all windows close.");

foreach (var first in new[] { PlannerChildWindow.RandomRule, PlannerChildWindow.Picker })
{
    var second = first == PlannerChildWindow.Picker ? PlannerChildWindow.RandomRule : PlannerChildWindow.Picker;
    var windows = new PlannerChildWindows();
    Check(!windows.TryCloseLast(0, out _), "No children must leave Escape unhandled.");
    windows.Open(first);
    windows.Open(second);
    Check(windows.TryCloseLast(1, out var closed) && closed == second, "Close in opening order, newest first.");
    Check(windows.TryCloseLast(1, out closed) && closed == null && windows.HasOpenWindows,
        "Multiple input paths in a frame must consume without closing another child.");
    Check(windows.TryCloseLast(2, out closed) && closed == first, "Next press closes the remaining child.");
    Check(windows.TryCloseLast(2, out closed) && closed == null,
        "The final child must still consume the same press after it closes.");
    Check(!windows.TryCloseLast(3, out _), "A later press with no children must pass through.");

    windows.Open(first);
    windows.Open(second);
    windows.Close(second); // Close button or outside click.
    Check(windows.TryCloseLast(4, out closed) && closed == first, "Manual close must remove the stale entry.");
    windows.Open(first);
    windows.Open(second);
    windows.Open(first); // Replace/reopen a picker without duplicating entries.
    Check(windows.TryCloseLast(5, out closed) && closed == first, "Reopened child becomes newest.");
    Check(windows.TryCloseLast(6, out closed) && closed == second, "Reopening must not duplicate a child.");
    Check(!windows.HasOpenWindows, "All children must be removed.");
}

var prefix = typeof(PlannerEscapeBlockPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!;
(bool runOriginal, bool handled) GameEscape()
{
    object[] args = { false };
    bool runOriginal = (bool)prefix.Invoke(null, args)!;
    return (runOriginal, (bool)args[0]);
}

NextCustomerWindow.Windows.Open(PlannerChildWindow.RandomRule);
Keyboard.current = null;
Check(GameEscape() == (true, false), "A missing keyboard must not throw or consume game input.");
Keyboard.current = new Keyboard();
Keyboard.current.escapeKey.wasPressedThisFrame = false;
Check(GameEscape() == (true, false), "Gamepad back must not close planner children.");
Keyboard.current.escapeKey.wasPressedThisFrame = true;
Check(GameEscape() == (false, true), "Intercepted Escape must report handled to prevent the pause menu.");
Check(GameEscape() == (false, true), "Repeated game dispatch in this frame must remain handled.");
Time.frameCount++;
Check(GameEscape() == (false, true) && !NextCustomerWindow.Visible,
    "With no children, Escape must close the main planner and prevent the pause menu.");
Check(GameEscape() == (false, true), "The now-hidden main planner must consume duplicate dispatches.");
Time.frameCount++;
Check(GameEscape() == (true, false), "The next press after closing the planner must preserve native handling.");
NextCustomerWindow.Windows.Open(PlannerChildWindow.Picker);
NextCustomerWindow.Visible = false;
Check(GameEscape() == (true, false), "A hidden planner must not intercept Escape.");
Console.WriteLine("Passed child opening order, reopen/manual close, frame deduplication, and game Escape routing checks.");
