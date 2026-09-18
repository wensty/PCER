using PotionCraft.QuestSystem;
using PotionCraft.ScriptableObjects.Ingredient;
using PotionCraft.ScriptableObjects.Potion;

namespace PotionCraft.ManagersSystem.Npc
{
    public class NpcManager
    {
        private PotionCraft.Npc.MonoBehaviourScripts.NpcMonoBehaviour current;
        public Action OnCurrentNpcChanged;
        public PotionCraft.Npc.MonoBehaviourScripts.NpcMonoBehaviour CurrentNpcMonoBehaviour
        {
            get => current;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            set { current = value; OnCurrentNpcChanged?.Invoke(); }
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void OnBeforeLoad() { }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void OnLoad() { }
    }
}
namespace PotionCraft.Npc.MonoBehaviourScripts
{
    public class NpcMonoBehaviour
    {
        public Quest currentQuest;
        public object settings;
        public TestRandomStates randomStatesContainer = new();
        public List<GeneratedQuestRequirement> mandatoryQuestRequirements = new();
        public List<GeneratedQuestRequirement> optionalQuestRequirements = new();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void ResetComponentFields()
        {
            currentQuest = null;
            mandatoryQuestRequirements = new();
            optionalQuestRequirements = new();
        }
    }
    public class TestRandomStates
    {
        public UnityEngine.Random.State Get(string name) => new UnityEngine.Random.State();
    }
}
namespace PotionCraft.ScriptableObjects.Ingredient
{
    public class Ingredient
    {
        public static Ingredient GetByName(string name, bool returnFirst, bool warning) => null;
    }
}
namespace PotionCraft.QuestSystem
{
    public partial class QuestRequirement
    {
        public string name;
        public virtual float GetCostMultiplier(Potion potion, Quest quest) => 1;
        public virtual int GetPopularityToAdd(Potion potion, Quest quest) => 0;
        public virtual bool IsRequirementCompleted(Potion potion, Quest quest, GeneratedQuestRequirement generated) => true;
        public virtual bool UpdateGeneratedRequirement(Quest quest, List<GeneratedQuestRequirement> all, GeneratedQuestRequirement generated) => true;
        public virtual bool IsCompatibleWithOtherRequirements(List<GeneratedQuestRequirement> all) => true;
    }
    public class QuestRequirementCertainIngredient : QuestRequirement { }
}
namespace PotionCraftExtraRequirements
{
    public class TestIntConfigValue { public int Value; }
    public partial class RequirementDefinition
    {
        public string Id;
        public TestConfigValue EnhancedCostMultiplier;
        public float? DefaultEnhancedCostMultiplier;
        public TestIntConfigValue PopularityBonus = new() {Value = 1};
        public HashSet<string> Tags = new();
        public Func<Potion, Quest, GeneratedQuestRequirement, bool> IsSatisfied = (p, q, g) => g.Completed;
        public Func<Quest, IReadOnlyList<GeneratedQuestRequirement>, bool> CanGenerate = (q, g) => true;
        public Func<Ingredient, bool> IsIngredientAllowed;
    }
    public static partial class RequirementCatalog
    {
        public static bool IsCompatible(RequirementDefinition definition, List<GeneratedQuestRequirement> all) => true;
    }
    public static class BuiltInRequirementDefinitions
    {
        public const string SimpleRecipeId = "SimpleRecipe";
        public const string BroadCategoryTag = "broad";
        public const string HighlanderTag = "highlander";
    }
}
