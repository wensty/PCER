using System.Collections.Generic;
using PotionCraft.QuestSystem;

namespace PotionCraftCustomerPlanner;

internal sealed class PlannedCustomer
{
    public RegularCustomerOption Customer { get; }
    public Quest TargetQuest { get; }
    public int ChapterOnAddToSpawn { get; }
    public bool StrictPlanningMode { get; }
    public IReadOnlyList<PlannedRequirement> MandatoryRequirements { get; }
    public IReadOnlyList<PlannedRequirement> OptionalRequirements { get; }

    public PlannedCustomer(
        RegularCustomerOption customer,
        Quest targetQuest,
        int chapterOnAddToSpawn,
        bool strictPlanningMode,
        IReadOnlyList<PlannedRequirement> mandatoryRequirements,
        IReadOnlyList<PlannedRequirement> optionalRequirements)
    {
        Customer = customer;
        TargetQuest = targetQuest;
        ChapterOnAddToSpawn = chapterOnAddToSpawn;
        StrictPlanningMode = strictPlanningMode;
        MandatoryRequirements = mandatoryRequirements;
        OptionalRequirements = optionalRequirements;
    }
}
