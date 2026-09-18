// Minimal game type shapes: exercise the production metadata mapping without Unity.
namespace PotionCraft.ObjectBased.RecipeMap
{
    public enum RecipeMapIndexEnum { Unknown, Water, Oil, Wine }
}
namespace PotionCraft.ScriptableObjects
{
    using PotionCraft.ObjectBased.RecipeMap;
    public class PotionBase
    {
        public RecipeMapIndexEnum Index;
        public Dictionary<PotionEffect, PotionCraft.ManagersSystem.RecipeMap.PositionAndRotation> potionEffectsDictionary = new();
        public RecipeMapIndexEnum GetRecipeMapIndexEnum() => Index;
        public static PotionBase GetByName(string name, bool returnFirst, bool warning)
            => Enum.TryParse(name, out RecipeMapIndexEnum index) ? new PotionBase { Index = index } : null;
    }
}
namespace PotionCraft.QuestSystem
{
    using PotionCraft.ScriptableObjects;
    public partial class QuestRequirement { }
    public class QuestRequirementsAdditionalEffects : QuestRequirement { }
    public class QuestRequirementMainIngredient : QuestRequirementCertainIngredient { }
    public class QuestRequirementNeedOneParticularIngredient : QuestRequirementCertainIngredient { }
    public class QuestRequirementNoParticularIngredient : QuestRequirementCertainIngredient { }
    public class QuestRequirementNoSalts : QuestRequirement { }
    public class QuestRequirementMaxIngredients : QuestRequirement { public int maxIngredientsAmount; }
    public class QuestRequirementOnlyParticularBase : QuestRequirement { }
    public class QuestRequirementNoParticularBase : QuestRequirement { }
    public class QuestRequirementInQuest { public QuestRequirement requirement; public PotionBase potionBase; }
    public class Quest
    {
        public Quest() { RawMaximum = 10; }
        public float RawMaximum
        {
            set
            {
                var effect = new PotionEffect();
                desiredEffects = new[] {effect};
                PotionCraft.Settings.Settings<PotionCraft.ManagersSystem.RecipeMap.RecipeMapManagerPotionBasesSettings>.Asset.defaultPotionBase.potionEffectsDictionary[effect] = new PotionCraft.ManagersSystem.RecipeMap.PositionAndRotation { Position = new UnityEngine.Vector2 {magnitude = (value - .2f) * 8f} };
            }
        }
        public int NativeGenerationCalls;
        public Action BeforeGeneration;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void ApplyPartTo(PotionCraft.Npc.MonoBehaviourScripts.NpcMonoBehaviour npcMonoBehaviour, object npcSettings = null)
        {
            npcMonoBehaviour.currentQuest = this;
            BeforeGeneration?.Invoke();
            var generated = GetGeneratedQuestRequirements(npcMonoBehaviour);
            npcMonoBehaviour.mandatoryQuestRequirements = generated.Item1;
            npcMonoBehaviour.optionalQuestRequirements = generated.Item2;
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private Tuple<List<GeneratedQuestRequirement>, List<GeneratedQuestRequirement>> GetGeneratedQuestRequirements(
            PotionCraft.Npc.MonoBehaviourScripts.NpcMonoBehaviour npc)
        {
            NativeGenerationCalls++;
            return Tuple.Create(npc.mandatoryQuestRequirements, npc.optionalQuestRequirements);
        }
        public PotionEffect[] desiredEffects;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public string GetPotionReactionKey(PotionCraft.ScriptableObjects.Potion.Potion potion,
            PotionCraft.Npc.Parts.Settings.Gender.GenderSet gender,
            List<GeneratedQuestRequirement> requirements, List<bool> requirementsCompleteStatus)
            => "#potion_reaction_max_tier_3_Male";
    }
    public class GeneratedQuestRequirement
    {
        public QuestRequirementInQuest requirementInQuest;
        public string stringValue1;
        public bool Completed = true;
        public bool Simple { set { if (value) requirementInQuest.requirement.name = "PCER_SimpleRecipe"; } }
        public int intValue1 = 10;
        public int Limit { get => intValue1; set => intValue1 = value; }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public string GetLocalizedText() => "{0} / {1}";
        public bool IsRequirementCompleted(PotionCraft.ScriptableObjects.Potion.Potion potion, Quest quest) => Completed;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public string GetLocalizedReaction(PotionCraft.Npc.Parts.Settings.Gender.GenderSet gender, bool isCompletedPartially)
            => isCompletedPartially ? "partial feedback" : "failure feedback";
    }
}
namespace PotionCraftExtraRequirements
{
    using PotionCraft.QuestSystem;
    public partial class RequirementDefinition
    {
        public TestConfigValue CostMultiplier { get; set; }
        public Func<GeneratedQuestRequirement, IngredientLimitModifier> IngredientLimitModifier { get; set; }
        public Func<PotionCraft.ScriptableObjects.Potion.Potion, Quest, GeneratedQuestRequirement, float, float> DynamicCostMultiplier { get; set; }
        public Func<PotionCraft.ScriptableObjects.Potion.Potion, Quest, GeneratedQuestRequirement, int, int> DynamicPopularityBonus { get; set; }
    }
    public class TestConfigValue { public float Value; }
    public static partial class RequirementCatalog
    {
        public static readonly Dictionary<QuestRequirement, RequirementDefinition> Definitions = new();
        public static bool TryGet(QuestRequirement requirement, out RequirementDefinition definition)
            => Definitions.TryGetValue(requirement, out definition);
        public static bool TryGet(GeneratedQuestRequirement generated, out RequirementDefinition definition)
            => TryGet(generated.requirementInQuest.requirement, out definition);
    }
    public class CustomCarrier : QuestRequirementNoSalts { }

}
namespace PotionCraft.ScriptableObjects.Potion
{
    public class Potion
    {
        public int Amount = 7;
        public object Effects;
        public static int Tier = 3;
        public static PotionReview GetPotionReview(object effects, object desiredEffects) => new PotionReview {maxTier = Tier};
    }
    public class PotionReview { public int maxTier; }
}
namespace PotionCraft.Npc.Parts.Settings
{
    public class Gender { public enum GenderSet {Male, Female} }
}
namespace PotionCraft.ObjectBased.UIElements.Dialogue
{
    public enum DialogueState {PotionRequest, ClosenessPotionRequest}
}
namespace PotionCraft.ManagersSystem
{
    public static class Managers
    {
        public static DialogueManager Dialogue = new();
        public static PotionCraft.ManagersSystem.Npc.NpcManager Npc = new();
        public static TestGoals Goals = new();
    }
    public class TestGoals { public int GetCurrentChapterNumber(int index) => 1; }
    public class DialogueManager { public PotionCraft.ObjectBased.UIElements.Dialogue.DialogueState State; }
}
namespace PotionCraft.LocalizationSystem
{
    public static class LocalizationManager { public static string GetText(string key) => "localized:" + key; }
}
