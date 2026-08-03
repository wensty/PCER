using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using PotionCraft.QuestSystem;
using PotionCraft.ScriptableObjects.Ingredient;
using PotionCraft.ScriptableObjects.Potion;

namespace PotionCraftExtraRequirements;

public sealed class RequirementDefinition
{
    public string Id { get; }
    public RequirementTexts Texts { get; }
    public int DefaultUnlockChapter { get; }
    public float DefaultSpawnWeight { get; }
    public float DefaultCostMultiplier { get; }
    public int DefaultPopularityBonus { get; }
    public IReadOnlyCollection<string> Tags { get; }
    public IReadOnlyCollection<string> ConflictingTags { get; }
    public Func<Potion, Quest, GeneratedQuestRequirement, bool> IsSatisfied { get; }
    public Func<Quest, IReadOnlyList<GeneratedQuestRequirement>, bool> CanGenerate { get; }
    public Func<Ingredient, bool> IsIngredientAllowed { get; }

    internal ConfigEntry<bool> Enabled { get; set; }
    internal ConfigEntry<int> UnlockChapter { get; set; }
    internal ConfigEntry<float> SpawnWeight { get; set; }
    internal ConfigEntry<float> CostMultiplier { get; set; }
    internal ConfigEntry<int> PopularityBonus { get; set; }

    public RequirementDefinition(
        string id,
        RequirementTexts texts,
        Func<Potion, Quest, GeneratedQuestRequirement, bool> isSatisfied,
        int unlockChapter = 1,
        float spawnWeight = 1f,
        float costMultiplier = 1.25f,
        int popularityBonus = 1,
        IEnumerable<string> tags = null,
        IEnumerable<string> conflictingTags = null,
        Func<Quest, IReadOnlyList<GeneratedQuestRequirement>, bool> canGenerate = null,
        Func<Ingredient, bool> isIngredientAllowed = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Texts = texts ?? throw new ArgumentNullException(nameof(texts));
        IsSatisfied = isSatisfied ?? throw new ArgumentNullException(nameof(isSatisfied));
        DefaultUnlockChapter = unlockChapter;
        DefaultSpawnWeight = spawnWeight;
        DefaultCostMultiplier = costMultiplier;
        DefaultPopularityBonus = popularityBonus;
        Tags = new List<string>(tags ?? Array.Empty<string>());
        ConflictingTags = new List<string>(conflictingTags ?? Array.Empty<string>());
        CanGenerate = canGenerate ?? ((_, __) => true);
        IsIngredientAllowed = isIngredientAllowed;
    }
}

public sealed class RequirementTexts
{
    public IReadOnlyList<LocalizedLine> Mandatory { get; }
    public IReadOnlyList<LocalizedLine> Optional { get; }
    public IReadOnlyList<LocalizedLine> MandatoryReactions { get; }
    public IReadOnlyList<LocalizedLine> OptionalReactions { get; }

    public RequirementTexts(
        IEnumerable<LocalizedLine> mandatory,
        IEnumerable<LocalizedLine> optional,
        IEnumerable<LocalizedLine> mandatoryReactions,
        IEnumerable<LocalizedLine> optionalReactions)
    {
        Mandatory = Required(mandatory, nameof(mandatory));
        Optional = Required(optional, nameof(optional));
        MandatoryReactions = Required(mandatoryReactions, nameof(mandatoryReactions));
        OptionalReactions = Required(optionalReactions, nameof(optionalReactions));
    }

    private static IReadOnlyList<LocalizedLine> Required(
        IEnumerable<LocalizedLine> lines,
        string parameterName)
    {
        List<LocalizedLine> result = new List<LocalizedLine>(
            lines ?? throw new ArgumentNullException(parameterName));
        if (result.Count == 0)
            throw new ArgumentException("At least one localized line is required.", parameterName);
        return result;
    }
}

public sealed class LocalizedLine
{
    public string En { get; }
    public string Zh { get; }

    public LocalizedLine(string en, string zh)
    {
        En = en ?? throw new ArgumentNullException(nameof(en));
        Zh = zh ?? throw new ArgumentNullException(nameof(zh));
    }
}
