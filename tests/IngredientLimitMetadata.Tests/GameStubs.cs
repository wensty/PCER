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
        public float RawMaximum = 10;
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
        public object desiredEffects;
        public string GetPotionReactionKey() => "native";
    }
    public class GeneratedQuestRequirement
    {
        public QuestRequirementInQuest requirementInQuest;
        public string stringValue1;
        public bool Completed = true;
        public bool Simple;
        public int Limit = 10;
        public bool IsRequirementCompleted(PotionCraft.ScriptableObjects.Potion.Potion potion, Quest quest) => Completed;
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
    // Isolate the patch's routing from game inventory access; the real math is
    // linked and tested separately via SimpleRecipeReward above.
    internal static class SimpleRecipeRequirementLogic
    {
        public static void UpdateGeneratedRequirement(Quest quest, IReadOnlyList<GeneratedQuestRequirement> all,
            GeneratedQuestRequirement generated)
        {
            float raw = IngredientLimitModifier.Combine(quest.RawMaximum,
                all.Where(r => !r.Simple).Select(IngredientLimitRegistry.Get));
            generated.Limit = Math.Max(1, (int)Math.Floor(raw));
            generated.stringValue1 = SimpleRecipeReward.EncodeRawMaximum(raw);
        }
        public static bool IsSimpleRecipeRequirement(GeneratedQuestRequirement generated) => generated.Simple;
        public static bool IsSatisfied(PotionCraft.ScriptableObjects.Potion.Potion potion, Quest quest, GeneratedQuestRequirement generated)
            => generated.Completed;
        public static bool IsEfficient(PotionCraft.ScriptableObjects.Potion.Potion potion, GeneratedQuestRequirement generated)
            => SimpleRecipeReward.TryReadRawMaximum(generated.stringValue1, out float raw)
                && SimpleRecipeReward.IsEfficient(potion.Amount, generated.Limit, raw);
        public static bool TryGetEfficiencyThreshold(GeneratedQuestRequirement generated, out int threshold)
        {
            threshold = 0;
            return SimpleRecipeReward.TryReadRawMaximum(generated.stringValue1, out float raw)
                && SimpleRecipeReward.TryGetEfficiencyThreshold(generated.Limit, raw, out threshold);
        }
    }
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
