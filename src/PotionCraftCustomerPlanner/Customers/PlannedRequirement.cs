using PotionCraft.QuestSystem;
using PotionCraft.ScriptableObjects;
using PotionCraft.ScriptableObjects.Ingredient;

namespace PotionCraftCustomerPlanner;

internal sealed class PlannedRequirement
{
    public string RequirementName { get; }
    public string StringTargetName { get; }
    public int? IntTarget { get; }

    public PlannedRequirement(string requirementName, string stringTargetName = null, int? intTarget = null)
    {
        RequirementName = requirementName;
        StringTargetName = string.IsNullOrWhiteSpace(stringTargetName) ? null : stringTargetName.Trim();
        IntTarget = intTarget;
    }

    public QuestRequirementInQuest CreateWrapper(QuestRequirementInQuest source)
    {
        QuestRequirementInQuest wrapper = new QuestRequirementInQuest(source.requirement)
        {
            textKey = source.textKey,
            reactionKey = source.reactionKey,
            reactionCompletedPartiallyKey = source.reactionCompletedPartiallyKey,
            ingredient = source.ingredient,
            potionBase = source.potionBase,
        };

        if (!string.IsNullOrWhiteSpace(StringTargetName))
        {
            if (source.requirement is QuestRequirementCertainIngredient)
                wrapper.ingredient = Ingredient.GetByName(StringTargetName, returnFirst: false, warning: false);
            else if (source.requirement is QuestRequirementCertainBase)
                wrapper.potionBase = PotionBase.GetByName(StringTargetName, returnFirst: false, warning: false);
        }

        return wrapper;
    }
}
