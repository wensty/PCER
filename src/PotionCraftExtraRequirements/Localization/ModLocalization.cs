using System;
using System.Collections.Generic;
using PotionCraft.LocalizationSystem;

namespace PotionCraftExtraRequirements;

public static class ModLocalization
{
    private static readonly Dictionary<string, LocalizedLine> Entries =
        new Dictionary<string, LocalizedLine>(StringComparer.Ordinal);

    public static void Register(RequirementDefinition definition)
    {
        string suffix = "PCER_" + definition.Id;
        AddPool("quest_condition_must_" + suffix, definition.Texts.Mandatory);
        AddPool("quest_condition_can_" + suffix, definition.Texts.Optional);
        AddPool(
            "quest_condition_must_reaction_" + suffix,
            definition.Texts.MandatoryReactions);
        AddPool(
            "quest_condition_can_reaction_" + suffix,
            definition.Texts.OptionalReactions);
    }

    public static bool Contains(string key)
    {
        return key != null && Entries.ContainsKey(key);
    }

    public static bool TryGetText(
        string key,
        LocalizationManager.Locale locale,
        out string text)
    {
        text = null;
        if (!Entries.TryGetValue(key, out LocalizedLine entry))
            return false;

        text = locale == LocalizationManager.Locale.zh ? entry.Zh : entry.En;
        return true;
    }

    private static void AddPool(string rootKey, IReadOnlyList<LocalizedLine> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string key = i == 0 ? rootKey : rootKey + "_" + i;
            Entries[key] = lines[i];
        }
    }
}
