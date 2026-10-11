using System.Reflection;
using HarmonyLib;
using PotionCraftCustomerPlanner;
using UnityEngine.InputSystem;

void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

void Invoke(Plugin plugin, string method) => typeof(Plugin)
    .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plugin, null);

var plugin = new Plugin();
Invoke(plugin, "Awake");
Check(Harmony.ScrollAttempts == 0 && Harmony.EscapeAttempts == 0,
    "Awake must not install the deferred input patches.");
for (int i = 0; i < 10; i++) Invoke(plugin, "Update");
Check(Harmony.ScrollAttempts == 0, "Empty player list must remain safe across startup frames.");
PlayerInput.all.Add(null);
Invoke(plugin, "Update");
Check(Harmony.ScrollAttempts == 0, "A missing first player must not initialize game input.");
PlayerInput.all[0] = new PlayerInput();
Invoke(plugin, "Update");
Check(Harmony.ScrollAttempts == 0, "Wait for the player's input actions.");
PlayerInput.all[0].actions = new object();
Invoke(plugin, "Update");
for (int i = 0; i < 10; i++) Invoke(plugin, "Update");
Check(Harmony.ScrollAttempts == 1, "Ready input must install scroll blocking exactly once.");
Check(Harmony.EscapeAttempts == 1, "Ready input must install Escape handling exactly once.");
Check(NextCustomerWindow.UpdateCalls == 23, "Waiting must not stop the planner window from updating.");
Invoke(plugin, "OnDestroy");
Check(Harmony.UnpatchCalls == 1 && CustomerPatchInstaller.UnpatchCalls == 1,
    "Both patch owners must be removed during teardown.");

// A failing detour must not escape Update or retry on every frame.
Harmony.FailScrollPatch = true;
var failing = new Plugin();
Invoke(failing, "Awake");
Invoke(failing, "Update");
Invoke(failing, "Update");
Check(Harmony.ScrollAttempts == 2 && failing.Logger.Errors == 1,
    "Installation failure must be logged once without per-frame retries.");
Console.WriteLine("Passed planner startup, delayed input readiness, single installation, failure handling, and teardown checks.");
