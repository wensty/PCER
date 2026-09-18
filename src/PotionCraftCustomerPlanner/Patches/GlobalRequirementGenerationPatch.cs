using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using PotionCraft.ManagersSystem;
using PotionCraft.Npc.MonoBehaviourScripts;
using PotionCraft.QuestSystem;

namespace PotionCraftCustomerPlanner;

internal static class GlobalRequirementGenerationPatch
{
    private static ManualLogSource logger;
    public static bool IsInstalled { get; private set; }
    private static int suppressionDepth;
    private static PreparedRequirements preparedRequirements;

    // Replay the native quest-application lifecycle with an already finalized group.
    // This also lets other mods observe generation without a dependency on their APIs.
    public static void ApplyPreparedRequirements(Quest quest, NpcMonoBehaviour npc,
        List<GeneratedQuestRequirement> mandatory, List<GeneratedQuestRequirement> optional)
    {
        if (!IsInstalled)
            throw new InvalidOperationException("Requirement generation hook is unavailable; cannot apply prepared requirements.");
        var previous = preparedRequirements;
        preparedRequirements = new PreparedRequirements(quest, npc, mandatory, optional);
        try
        {
            quest.ApplyPartTo(npc, npc.settings);
        }
        finally
        {
            preparedRequirements = previous;
        }
    }

    private sealed class PreparedRequirements
    {
        public readonly Quest Quest;
        public readonly NpcMonoBehaviour Npc;
        public readonly Tuple<List<GeneratedQuestRequirement>, List<GeneratedQuestRequirement>> Result;

        public PreparedRequirements(Quest quest, NpcMonoBehaviour npc,
            List<GeneratedQuestRequirement> mandatory, List<GeneratedQuestRequirement> optional)
        {
            Quest = quest;
            Npc = npc;
            Result = Tuple.Create(mandatory ?? new List<GeneratedQuestRequirement>(),
                optional ?? new List<GeneratedQuestRequirement>());
        }
    }

    public static IDisposable SuppressGeneration()
    {
        if (!IsInstalled)
            throw new InvalidOperationException("Requirement generation hook is unavailable; cannot force a plan.");
        suppressionDepth++;
        return new GenerationSuppression();
    }

    private sealed class GenerationSuppression : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            suppressionDepth--;
        }
    }

    public static void Install(Harmony harmony, ManualLogSource logSource)
    {
        logger = logSource;
        try
        {
            var original = AccessTools.Method(typeof(Quest), "GetGeneratedQuestRequirements",
                new[] { typeof(NpcMonoBehaviour) });
            if (original == null)
                throw new MissingMethodException(typeof(Quest).FullName, "GetGeneratedQuestRequirements");
            harmony.Patch(original, prefix: new HarmonyMethod(typeof(GlobalRequirementGenerationPatch), nameof(Prefix)));
            IsInstalled = true;
        }
        catch (Exception exception)
        {
            IsInstalled = false;
            logger?.LogWarning($"Global requirement rules unavailable: {exception}");
        }
    }

    private static bool Prefix(Quest __instance, NpcMonoBehaviour __0,
        ref Tuple<List<GeneratedQuestRequirement>, List<GeneratedQuestRequirement>> __result)
    {
        if (preparedRequirements != null
            && ReferenceEquals(preparedRequirements.Quest, __instance)
            && ReferenceEquals(preparedRequirements.Npc, __0))
        {
            __result = preparedRequirements.Result;
            return false;
        }
        // Explicit plans, including empty plans, bypass native random generation
        // independently of the global random-rule switch and difficulty.
        if (suppressionDepth > 0)
        {
            __result = Tuple.Create(new List<GeneratedQuestRequirement>(), new List<GeneratedQuestRequirement>());
            return false;
        }
        var rule = NextCustomerWindow.GlobalRandomRule;
        if (rule == null || !rule.HasOverrides || __0 == null)
            return true;

        // Match the game's per-NPC seed without advancing the shared random stream.
        var previousState = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.state = __0.randomStatesContainer.Get("requirementsGeneration");
            if (!NextCustomerDirector.TryGenerateRandomRequirements(
                __instance, Managers.Goals.GetCurrentChapterNumber(0), rule,
                out var mandatory, out var optional, out string reason,
                // Native generation validates incrementally in GetGeneratedRequirement.
                // The planner's extra whole-group validation is for edited previews only.
                validateGeneratedGroup: false))
            {
                logger?.LogWarning($"Global requirement rules fell back to native generation: {reason}");
                return true;
            }

            __result = Tuple.Create(mandatory.ToList(), optional.ToList());
            return false;
        }
        catch (Exception exception)
        {
            logger?.LogWarning($"Global requirement rules fell back to native generation: {exception}");
            return true;
        }
        finally
        {
            UnityEngine.Random.state = previousState;
        }
    }
}
