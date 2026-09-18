// Minimal type shapes for executing the production conflict postfix outside Unity.
namespace PotionCraft.QuestSystem
{
    public class QuestRequirement
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public virtual bool IsCompatibleWithOtherRequirements(List<GeneratedQuestRequirement> generatedRequirements) => true;
    }
    public class QuestRequirementMainIngredient : QuestRequirement { [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public override bool IsCompatibleWithOtherRequirements(List<GeneratedQuestRequirement> generatedRequirements) => true; }
    public class QuestRequirementNeedOneParticularIngredient : QuestRequirement { [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public override bool IsCompatibleWithOtherRequirements(List<GeneratedQuestRequirement> generatedRequirements) => true; }
    public class QuestRequirementMaxIngredients : QuestRequirement
    {
        public int maxIngredientsAmount;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] public override bool IsCompatibleWithOtherRequirements(List<GeneratedQuestRequirement> generatedRequirements) => true;
    }
    public class QuestRequirementInQuest { public QuestRequirement requirement; }
    public class GeneratedQuestRequirement { public QuestRequirementInQuest requirementInQuest; }
}
