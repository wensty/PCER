// Minimal type shapes for executing the production conflict postfix outside Unity.
namespace HarmonyLib
{
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HarmonyPatch : System.Attribute
    {
        public HarmonyPatch() { }
        public HarmonyPatch(System.Type type, string method) { }
    }
    public sealed class HarmonyPostfix : System.Attribute { }
}
namespace PotionCraft.QuestSystem
{
    public class QuestRequirement
    {
        public bool IsCompatibleWithOtherRequirements() => true;
    }
    public class QuestRequirementMainIngredient : QuestRequirement { }
    public class QuestRequirementNeedOneParticularIngredient : QuestRequirement { }
    public class QuestRequirementMaxIngredients : QuestRequirement
    {
        public int maxIngredientsAmount;
    }
    public class QuestRequirementInQuest { public QuestRequirement requirement; }
    public class GeneratedQuestRequirement { public QuestRequirementInQuest requirementInQuest; }
}
