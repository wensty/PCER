#if DEBUG
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using HarmonyLib;
using PotionCraft.ManagersSystem;
using PotionCraft.Npc.MonoBehaviourScripts;
using PotionCraft.ObjectBased.ScalesSystem;
using PotionCraft.ObjectBased.UIElements.Dialogue;

namespace PotionCraftCustomerPlanner;

internal static class RejectionDiagnostics
{
    private static readonly System.Reflection.MethodInfo GetPotionOfferedTimesCountMethod =
        AccessTools.Method(typeof(NpcTrading), "get_PotionOfferedTimesCount");
    private static readonly System.Reflection.PropertyInfo CurrentNpcProperty =
        AccessTools.Property(typeof(PotionCraft.ManagersSystem.Npc.NpcManager), "CurrentNpcMonoBehaviour");
    private static readonly System.Reflection.FieldInfo LastInteractionField =
        AccessTools.Field(typeof(PotionCraft.ManagersSystem.Npc.NpcManager), "lastInteraction");

    public static NpcMonoBehaviour CurrentNpc(PotionCraft.ManagersSystem.Npc.NpcManager npcManager)
    {
        try
        {
            return CurrentNpcProperty?.GetValue(npcManager, null) as NpcMonoBehaviour;
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string eventName, NpcMonoBehaviour npc, bool includeStack)
    {
        try
        {
            string directory = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            if (string.IsNullOrWhiteSpace(directory))
                directory = Environment.CurrentDirectory;
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, "RejectionDiagnostics.txt");
            File.AppendAllText(path, BuildEntry(eventName, npc, includeStack));
        }
        catch
        {
            // Diagnostics must never change game behavior.
        }
    }

    private static string BuildEntry(string eventName, NpcMonoBehaviour npc, bool includeStack)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("-----");
        builder.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        builder.AppendLine(eventName);

        DialogueState dialogueState = Managers.Dialogue != null ? Managers.Dialogue.State : default;
        builder.AppendLine($"dialogueState={dialogueState}");
        builder.AppendLine($"npcState={NpcStateText(npc)}");
        builder.AppendLine($"npc={NpcText(npc)}");
        builder.AppendLine($"quest={npc?.currentQuest?.name ?? "-"}");
        builder.AppendLine($"mandatory={npc?.mandatoryQuestRequirements?.Count ?? -1}");
        builder.AppendLine($"optional={npc?.optionalQuestRequirements?.Count ?? -1}");
        builder.AppendLine($"offerCount={PotionOfferCount(npc)} max={MaxPotionOfferAttempts()}");
        builder.AppendLine($"scales={ScalesText()}");
        builder.AppendLine($"lastInteraction={LastInteraction()}");

        if (includeStack)
            builder.AppendLine(new StackTrace(skipFrames: 2, fNeedFileInfo: false).ToString());

        return builder.ToString();
    }

    private static string NpcStateText(NpcMonoBehaviour npc)
    {
        if (npc == null)
            return "-";
        return npc.CurrentState.ToString();
    }

    private static string NpcText(NpcMonoBehaviour npc)
    {
        if (npc == null)
            return "-";
        string template = npc.template != null ? npc.template.name : "-";
        string faction = npc.faction != null ? npc.faction.name : "-";
        string factionClass = npc.factionClass != null ? npc.factionClass.name : "-";
        return $"{template} faction={faction} class={factionClass}";
    }

    private static string PotionOfferCount(NpcMonoBehaviour npc)
    {
        try
        {
            if (npc?.trading == null || GetPotionOfferedTimesCountMethod == null)
                return "-";
            return Convert.ToString(GetPotionOfferedTimesCountMethod.Invoke(npc.trading, null));
        }
        catch
        {
            return "?";
        }
    }

    private static string MaxPotionOfferAttempts()
    {
        try
        {
            return Convert.ToString(Managers.Npc?.GetMaxPotionOfferAttemptsCount());
        }
        catch
        {
            return "?";
        }
    }

    private static string LastInteraction()
    {
        try
        {
            return Convert.ToString(LastInteractionField?.GetValue(Managers.Npc)) ?? "-";
        }
        catch
        {
            return "?";
        }
    }

    private static string ScalesText()
    {
        try
        {
            Scales scales = Scales.Instance;
            ScalesCupDisplay display = scales?.rightCupScript?.display;
            bool hasPotion = display?.currentPotionItem != null;
            bool suitable = display != null && display.isCurrentPotionSuitable;
            bool wrong = scales != null && scales.isWrongPotionOnTheScales;
            return $"hasPotion={hasPotion} suitable={suitable} wrong={wrong}";
        }
        catch
        {
            return "?";
        }
    }
}
#endif
