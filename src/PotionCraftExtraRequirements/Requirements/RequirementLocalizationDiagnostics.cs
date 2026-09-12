using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using PotionCraft.QuestSystem;

namespace PotionCraftExtraRequirements;

internal static class RequirementLocalizationDiagnostics
{
#if DEBUG
    private const int MaxEntries = 500;
    private static readonly object Gate = new object();
    private static readonly HashSet<string> Seen = new HashSet<string>();

    public static void LogGenerated(
        Quest quest,
        IReadOnlyList<GeneratedQuestRequirement> context,
        GeneratedQuestRequirement generated)
    {
        if (!SimpleRecipeRequirementLogic.IsSimpleRecipeRequirement(generated))
            return;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Generated simple recipe requirement");
        builder.AppendLine($"quest={quest?.name ?? "-"}");
        builder.AppendLine($"context=[{string.Join(", ", (context ?? System.Array.Empty<GeneratedQuestRequirement>()).Select(RequirementSummary).ToArray())}]");
        AppendGeneratedFields(builder, generated);
        WriteOnce("generated:" + Fingerprint(generated), builder.ToString());
    }

    public static void LogDisplayed(
        GeneratedQuestRequirement generated,
        string before,
        string after)
    {
        if (!SimpleRecipeRequirementLogic.IsSimpleRecipeRequirement(generated))
            return;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Displayed simple recipe requirement text");
        builder.AppendLine($"before={Printable(before)}");
        builder.AppendLine($"after={Printable(after)}");
        AppendGeneratedFields(builder, generated);
        WriteOnce("displayed:" + Fingerprint(generated) + ":" + before + "=>" + after, builder.ToString());
    }

    private static void AppendGeneratedFields(StringBuilder builder, GeneratedQuestRequirement generated)
    {
        builder.AppendLine($"requirement={generated?.requirementInQuest?.requirement?.name ?? "-"}");
        builder.AppendLine($"textKey={generated?.requirementInQuest?.textKey ?? "-"}");
        builder.AppendLine($"reactionKey={generated?.requirementInQuest?.reactionKey ?? "-"}");
        builder.AppendLine($"partialReactionKey={generated?.requirementInQuest?.reactionCompletedPartiallyKey ?? "-"}");
        builder.AppendLine($"intValue1={generated?.intValue1.ToString() ?? "-"}");
        builder.AppendLine($"stringValue1={generated?.stringValue1 ?? "-"}");
        builder.AppendLine($"textMale={Printable(generated?.textMale)}");
        builder.AppendLine($"textFemale={Printable(generated?.textFemale)}");
        builder.AppendLine($"reactionMale={Printable(generated?.reactionMale)}");
        builder.AppendLine($"reactionFemale={Printable(generated?.reactionFemale)}");
        builder.AppendLine($"reactionPartialMale={Printable(generated?.reactionCompletedPartiallyMale)}");
        builder.AppendLine($"reactionPartialFemale={Printable(generated?.reactionCompletedPartiallyFemale)}");
    }

    private static string RequirementSummary(GeneratedQuestRequirement generated)
    {
        if (generated == null)
            return "<null>";

        return $"{generated.requirementInQuest?.requirement?.name ?? "-"}(int={generated.intValue1}, string={generated.stringValue1 ?? "-"})";
    }

    private static string Fingerprint(GeneratedQuestRequirement generated)
    {
        if (generated == null)
            return "<null>";

        return string.Join("|", new[]
        {
            generated.requirementInQuest?.requirement?.name ?? "-",
            generated.requirementInQuest?.textKey ?? "-",
            generated.intValue1.ToString(),
            generated.stringValue1 ?? "-",
            generated.textMale ?? "-",
            generated.textFemale ?? "-",
        });
    }

    private static string Printable(string value)
    {
        return value == null
            ? "<null>"
            : value.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static void WriteOnce(string key, string text)
    {
        lock (Gate)
        {
            if (Seen.Count >= MaxEntries || !Seen.Add(key))
                return;

            string directory = Path.Combine(Paths.PluginPath, "PotionCraftExtraRequirements");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "RequirementLocalizationDiagnostics.txt"),
                "----\n" + text);
        }
    }
#else
    public static void LogGenerated(
        Quest quest,
        IReadOnlyList<GeneratedQuestRequirement> context,
        GeneratedQuestRequirement generated)
    {
    }

    public static void LogDisplayed(
        GeneratedQuestRequirement generated,
        string before,
        string after)
    {
    }
#endif
}
