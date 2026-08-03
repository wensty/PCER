using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using PotionCraft.QuestSystem;
using UnityEngine;

namespace PotionCraftExtraRequirements;

public static class RequirementCatalog
{
    private const string NamePrefix = "PCER_";
    private static readonly Dictionary<string, RequirementDefinition> Definitions =
        new Dictionary<string, RequirementDefinition>(StringComparer.Ordinal);
    private static readonly List<RequirementDefinition> RegistrationOrder =
        new List<RequirementDefinition>();

    private static ConfigFile config;

    public static void Configure(ConfigFile configFile)
    {
        config = configFile;
    }

    public static void Register(RequirementDefinition definition)
    {
        if (config == null)
            throw new InvalidOperationException("RequirementCatalog.Configure must be called first.");
        if (Definitions.ContainsKey(definition.Id))
            throw new InvalidOperationException($"Duplicate requirement id: {definition.Id}");

        string section = $"Requirement: {definition.Id}";
        definition.Enabled = config.Bind(section, "Enabled", true, "Whether this requirement can be generated.");
        definition.UnlockChapter = config.Bind(section, "UnlockChapter", definition.DefaultUnlockChapter);
        definition.SpawnWeight = config.Bind(section, "SpawnWeight", definition.DefaultSpawnWeight);
        definition.CostMultiplier = config.Bind(section, "CostMultiplier", definition.DefaultCostMultiplier);
        definition.PopularityBonus = config.Bind(section, "PopularityBonus", definition.DefaultPopularityBonus);
        Definitions.Add(definition.Id, definition);
        RegistrationOrder.Add(definition);
        ModLocalization.Register(definition);
    }

    public static bool TryGet(QuestRequirement requirement, out RequirementDefinition definition)
    {
        definition = null;
        if (requirement == null || !requirement.name.StartsWith(NamePrefix, StringComparison.Ordinal))
            return false;
        return Definitions.TryGetValue(requirement.name.Substring(NamePrefix.Length), out definition);
    }

    public static bool TryGet(GeneratedQuestRequirement generated, out RequirementDefinition definition)
    {
        definition = null;
        return generated?.requirementInQuest != null
            && TryGet(generated.requirementInQuest.requirement, out definition);
    }

    public static void InjectIntoGamePool()
    {
        foreach (RequirementDefinition definition in RegistrationOrder.Where(d => d.Enabled.Value))
        {
            string objectName = NamePrefix + definition.Id;
            bool exists = QuestRequirementInQuest.allRequirements.Any(
                item => item?.requirement != null && item.requirement.name == objectName);
            if (exists)
                continue;

            // Inherit a concrete native class: its inaccessible abstract localization
            // member is already implemented inside the game assembly.
            ConfigurableQuestRequirement carrier =
                ScriptableObject.CreateInstance<ConfigurableQuestRequirement>();
            carrier.name = objectName;
            carrier.spawnChance = Math.Max(0f, definition.SpawnWeight.Value);
            carrier.UpdateTextsPools();

            QuestRequirementInQuest wrapper = new QuestRequirementInQuest(carrier);
            QuestRequirementInQuest.allRequirements.Add(wrapper);
        }
    }

    public static bool IsCompatible(
        RequirementDefinition candidate,
        IReadOnlyList<GeneratedQuestRequirement> generated)
    {
        foreach (GeneratedQuestRequirement item in generated)
        {
            if (!TryGet(item, out RequirementDefinition existing))
                continue;
            if (existing.Id == candidate.Id)
                return false;
            if (candidate.ConflictingTags.Intersect(existing.Tags).Any()
                || existing.ConflictingTags.Intersect(candidate.Tags).Any())
                return false;
        }
        return true;
    }
}
