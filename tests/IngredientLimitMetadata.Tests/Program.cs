using System.Globalization;
using PotionCraftExtraRequirements;
using PotionCraft.QuestSystem;
using PotionCraft.ScriptableObjects;
using PotionCraft.ObjectBased.RecipeMap;

void Check(bool result, string message) { if (!result) throw new Exception(message); }
IngredientLimitModifier Read(QuestRequirement requirement, string target = null, PotionBase fixedBase = null)
    => IngredientLimitRegistry.Get(new GeneratedQuestRequirement
    {
        requirementInQuest = new QuestRequirementInQuest {requirement = requirement, potionBase = fixedBase},
        stringValue1 = target
    });
void Expect(IngredientLimitModifier value, float allowance, float multiplier)
    => Check(value.Allowance == allowance && value.Multiplier == multiplier, "Wrong metadata mapping.");

NativeRequirementDefinitions.Register();
Expect(Read(new QuestRequirementsAdditionalEffects()), 4, 1);
Expect(Read(new QuestRequirementMainIngredient()), .5f, 1.4f);
Expect(Read(new QuestRequirementNeedOneParticularIngredient()), 1, 1);
Expect(Read(new QuestRequirementNoParticularIngredient()), 0, 1);
Expect(Read(new QuestRequirementNoSalts()), 0, 1.75f);
Expect(Read(new QuestRequirement()), 0, 1);
float[] multipliers = {1.6f, 1.3f, 1.1f, 1f};
for (int cap = 1; cap <= 4; cap++)
    Expect(Read(new QuestRequirementMaxIngredients {maxIngredientsAmount = cap}), 0, multipliers[cap - 1]);
foreach (string target in new[] {"Water", "Oil", "Wine", "Unknown"})
{
    Expect(Read(new QuestRequirementOnlyParticularBase(), target), 0, target == "Water" ? 1.25f : 1f);
    Expect(Read(new QuestRequirementNoParticularBase(), target), 0, target == "Oil" || target == "Wine" ? 1.25f : 1f);
}
Expect(Read(new QuestRequirementOnlyParticularBase(), fixedBase: new PotionBase {Index = RecipeMapIndexEnum.Water}), 0, 1.25f);
var custom = new CustomCarrier();
Expect(Read(custom), 0, 1); // No borrowed NoSalts multiplier.
RequirementCatalog.Definitions[custom] = new RequirementDefinition {IngredientLimitModifier = _ => new IngredientLimitModifier(3, 1.2f)};
Expect(Read(custom), 3, 1.2f);
RequirementCatalog.Definitions[custom] = new RequirementDefinition();
Expect(Read(custom), 0, 1);

var modifiers = new[] {new IngredientLimitModifier(4), new IngredientLimitModifier(.5f, 1.4f), new IngredientLimitModifier(multiplier: 1.25f)};
float combined = IngredientLimitModifier.Combine(.2f + 16f / 8f, modifiers);
Check(Math.Abs(combined - ((.2f + 16f / 8f + 4f + .5f) * 1.4f * 1.25f)) < .00001f, "Formula changed.");
Check(combined == IngredientLimitModifier.Combine(.2f + 16f / 8f, modifiers.Reverse()), "Order changed raw limit.");
Check(SimpleRecipeReward.IsEfficient(4, 6, 6.99f), "Must use raw 6.99, not rounded 6.");
Check(!SimpleRecipeReward.IsEfficient(5, 6, 6.99f), "Above bonus threshold.");
Check(SimpleRecipeReward.IsEfficient(6, 10, 10), "60% boundary must be inclusive.");
Check(!SimpleRecipeReward.IsEfficient(7, 10, 10), "Ordinary success is not bonus success.");
Check(!SimpleRecipeReward.IsEfficient(0, 10, 10), "Empty potion must not earn a bonus.");
Check(!SimpleRecipeReward.IsEfficient(4, 3, 10), "Failed requirement must not earn a bonus.");
Check(!SimpleRecipeReward.IsEfficient(1, 1, .2f), "Minimum-one clamp must not inflate the raw threshold.");
Check(SimpleRecipeReward.TryGetEfficiencyThreshold(6, 6.99f, out int threshold) && threshold == 4,
    "Reaction must show the integer threshold 4, calculated from unrounded 6.99.");
Check(SimpleRecipeReward.TryGetEfficiencyThreshold(10, 10f, out threshold) && threshold == 6,
    "Reaction threshold must include the exact 60% boundary.");
Check(!SimpleRecipeReward.TryGetEfficiencyThreshold(1, .2f, out _),
    "Do not advertise an impossible zero-ingredient reward.");
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
string encoded = SimpleRecipeReward.EncodeRawMaximum(6.99f);
Check(SimpleRecipeReward.TryReadRawMaximum(encoded, out float decoded) && decoded == 6.99f, "Raw limit must survive locale-independent copy.");
Check(!SimpleRecipeReward.TryReadRawMaximum("", out _), "Legacy/absent raw values must not invent a threshold.");
Check(!SimpleRecipeReward.TryReadRawMaximum("PCER_SimpleRecipeRaw:NaN", out _), "Invalid raw value accepted.");

var potion = new PotionCraft.ScriptableObjects.Potion.Potion {Amount = 4};
var quest = new Quest();
var simple = new GeneratedQuestRequirement
{
    requirementInQuest = new QuestRequirementInQuest {requirement = custom},
    Simple = true, Limit = 6, stringValue1 = SimpleRecipeReward.EncodeRawMaximum(6.99f)
};
float enhancedMultiplier = 3f;
RequirementCatalog.Definitions[custom].CostMultiplier = new TestConfigValue {Value = 2f};
RequirementCatalog.Definitions[custom].DynamicCostMultiplier = (p, q, g, ordinary) =>
    SimpleRecipeRequirementLogic.IsEfficient(p, g) ? enhancedMultiplier : ordinary;
var other = new GeneratedQuestRequirement {requirementInQuest = new QuestRequirementInQuest {requirement = new QuestRequirement()}};
var group = new List<GeneratedQuestRequirement> {other, simple};
RequirementCatalog.Definitions[custom].DynamicPopularityBonus = (p, q, g, ordinary) =>
    SimpleRecipeRequirementLogic.IsEfficient(p, g) ? 2 : ordinary;
IEnumerable<int> popularity = new[] {5, 1};
DynamicRequirementRewardPatches.PopularityBonusesPostfix(potion, quest, group, ref popularity);
Check(popularity.SequenceEqual(new[] {5, 2}), "Enhanced popularity must replace +1 with +2, not add to it.");
IEnumerable<float> bonuses = new[] {1.5f, 3f};
DynamicRequirementRewardPatches.CostBonusesPostfix(potion, quest, group, ref bonuses);
Check(bonuses.SequenceEqual(new[] {1.5f, 3f}), "Bonus must replace only its own factor, not stack on top of 2.");
enhancedMultiplier = 4.25f;
bonuses = new[] {1.5f, 4.25f};
DynamicRequirementRewardPatches.CostBonusesPostfix(potion, quest, group, ref bonuses);
Check(bonuses.SequenceEqual(new[] {1.5f, 4.25f}), "Enhanced reward must not be fixed at 3.");
simple.Completed = false;
popularity = new[] {5, 0};
DynamicRequirementRewardPatches.PopularityBonusesPostfix(potion, quest, group, ref popularity);
Check(popularity.SequenceEqual(new[] {5, 0}), "Incomplete requirement must not award popularity.");
bonuses = new[] {1.5f, 1f};
DynamicRequirementRewardPatches.CostBonusesPostfix(potion, quest, group, ref bonuses);
Check(bonuses.SequenceEqual(new[] {1.5f, 1f}), "Incomplete requirement was rewarded.");
simple.Completed = true;
potion.Amount = 5;
popularity = new[] {5, 1};
DynamicRequirementRewardPatches.PopularityBonusesPostfix(potion, quest, group, ref popularity);
Check(popularity.SequenceEqual(new[] {5, 1}), "Ordinary success must retain +1 popularity.");
bonuses = new[] {1.5f, 4.25f};
DynamicRequirementRewardPatches.CostBonusesPostfix(potion, quest, group, ref bonuses);
Check(bonuses.SequenceEqual(new[] {1.5f, 2f}), "Ordinary tier changed.");
RequirementCatalog.Definitions[custom].CostMultiplier.Value = 1.75f;
bonuses = new[] {1.5f, 4.25f};
DynamicRequirementRewardPatches.CostBonusesPostfix(potion, quest, group, ref bonuses);
Check(bonuses.SequenceEqual(new[] {1.5f, 1.75f}), "Partial tier must use its configured value, not the direct-read maximum.");
string Reaction(string native, bool complete = true)
{
    DynamicRequirementRewardPatches.ReactionPostfix(quest, potion,
        PotionCraft.Npc.Parts.Settings.Gender.GenderSet.Male, group, new List<bool> {complete, true}, ref native);
    return native;
}
foreach (string gender in new[] {"Male", "Female"})
foreach (int tier in new[] {1, 2, 3})
foreach (string sideEffects in new[] {"", "side_effects_"})
    Check(Reaction($"#potion_reaction_max_tier_{tier}_{sideEffects}{gender}") == "partial feedback",
        "Partial fulfillment must replace the generic tier reaction, including Excellent.");
Check(Reaction("existing multi-effect feedback") == "existing multi-effect feedback\npartial feedback", "Native multi-effect feedback lost.");
Check(Reaction("failure", false) == "failure", "Failure priority lost.");
potion.Amount = 4;
Check(Reaction("#potion_reaction_max_tier_3_Male") == "#potion_reaction_max_tier_3_Male", "Efficient recipe incorrectly got partial feedback.");
string savedRaw = simple.stringValue1;
simple.stringValue1 = "";
Check(Reaction("success") == "success", "Missing raw threshold must not advertise an unknown reward count.");
simple.stringValue1 = SimpleRecipeReward.EncodeRawMaximum(.2f);
Check(Reaction("success") == "success", "Unreachable bonus tier must not advertise zero ingredients.");
simple.stringValue1 = savedRaw;
potion.Amount = 5;
PotionCraft.ScriptableObjects.Potion.Potion.Tier = 0;
Check(Reaction("wrong_effect") == "wrong_effect", "Wrong-effect priority lost.");
PotionCraft.ScriptableObjects.Potion.Potion.Tier = 3;
PotionCraft.ManagersSystem.Managers.Dialogue.State = PotionCraft.ObjectBased.UIElements.Dialogue.DialogueState.ClosenessPotionRequest;
Check(Reaction("exchange") == "exchange", "Closeness exchange priority lost.");
Console.WriteLine("Passed metadata, formula, raw-limit/60% boundaries, dynamic price routing and reaction-priority tests.");

// Exercise the production current-customer trigger and native requirement interface.
var carrier = new ConfigurableQuestRequirement {name = "PCER_SimpleRecipe"};
var definition = new RequirementDefinition
{
    Id = "SimpleRecipe", CostMultiplier = new TestConfigValue {Value = 2},
    EnhancedCostMultiplier = new TestConfigValue {Value = 3}
};
int evaluations = 0;
definition.DynamicCostMultiplier = (p, q, g, partial) =>
{
    evaluations++;
    return SimpleRecipeRequirementLogic.IsEfficient(p, g) ? definition.EnhancedCostMultiplier.Value : partial;
};
definition.DynamicPopularityBonus = (p, q, g, partial) => SimpleRecipeRequirementLogic.IsEfficient(p, g) ? 2 : partial;
RequirementCatalog.Definitions[carrier] = definition;
var currentSimple = new GeneratedQuestRequirement
{
    requirementInQuest = new QuestRequirementInQuest {requirement = carrier}, Simple = true
};
var npc = new PotionCraft.Npc.MonoBehaviourScripts.NpcMonoBehaviour
{
    currentQuest = quest, mandatoryQuestRequirements = new() {currentSimple}
};
PotionCraft.ManagersSystem.Managers.Npc.CurrentNpcMonoBehaviour = npc;
CurrentCustomerBudget.Refresh();
Check(CurrentCustomerBudget.RawMaximum == 10 && currentSimple.Limit == 10, "Initial trigger did not synchronize budget and display.");
potion.Amount = 7;
Check(carrier.GetCostMultiplier(potion, quest) == 2 && carrier.GetPopularityToAdd(potion, quest) == 1,
    "Native interfaces must return partial-fulfillment rewards for the current customer.");

// Preview/edit mutates the same interactive NPC, including optional requirements.
npc.optionalQuestRequirements = new() {new GeneratedQuestRequirement
{
    requirementInQuest = new QuestRequirementInQuest {requirement = new QuestRequirementsAdditionalEffects()}
}};
CurrentCustomerBudget.Refresh();
Check(CurrentCustomerBudget.RawMaximum == 14 && currentSimple.Limit == 14, "Optional modifiers missing from the full group.");
Check(carrier.GetCostMultiplier(potion, quest) == 3 && carrier.GetPopularityToAdd(potion, quest) == 2,
    "Edited budget did not reach native interfaces.");
evaluations = 0;
var currentGroup = new List<GeneratedQuestRequirement> {currentSimple};
bonuses = currentGroup.Select(g => g.requirementInQuest.requirement.GetCostMultiplier(potion, quest));
DynamicRequirementRewardPatches.CostBonusesPostfix(potion, quest, currentGroup, ref bonuses);
Check(bonuses.Single() == 3 && evaluations == 1, "Current customer's native result was evaluated again in aggregation.");
var detached = new GeneratedQuestRequirement
{
    requirementInQuest = currentSimple.requirementInQuest, Simple = true, Limit = 10,
    stringValue1 = SimpleRecipeReward.EncodeRawMaximum(10)
};
var detachedGroup = new List<GeneratedQuestRequirement> {detached};
bonuses = new[] {carrier.GetCostMultiplier(potion, quest)};
DynamicRequirementRewardPatches.CostBonusesPostfix(potion, quest, detachedGroup, ref bonuses);
popularity = new[] {carrier.GetPopularityToAdd(potion, quest)};
DynamicRequirementRewardPatches.PopularityBonusesPostfix(potion, quest, detachedGroup, ref popularity);
Check(bonuses.Single() == 2 && popularity.Single() == 1,
    "A different generated group inherited the active customer's full-fulfillment rewards.");

// Revert uses restored lists and a cloned generated requirement, on the same NPC.
var restored = new GeneratedQuestRequirement {requirementInQuest = currentSimple.requirementInQuest, Simple = true};
npc.mandatoryQuestRequirements = new() {restored};
npc.optionalQuestRequirements = new();
CurrentCustomerBudget.Refresh();
Check(CurrentCustomerBudget.RawMaximum == 10 && restored.Limit == 10 && carrier.GetCostMultiplier(potion, quest) == 2,
    "Preview restoration kept the edited budget or old generated requirement.");
restored.Completed = false;
Check(carrier.GetCostMultiplier(potion, quest) == 1 && carrier.GetPopularityToAdd(potion, quest) == 0,
    "Native interfaces rewarded an unmet requirement.");
restored.Completed = true;
Check(carrier.GetCostMultiplier(potion, new Quest()) == 3, "Unrelated quest used the current customer's partial tier.");

var nextNpc = new PotionCraft.Npc.MonoBehaviourScripts.NpcMonoBehaviour
{
    currentQuest = quest, mandatoryQuestRequirements = new() {currentSimple},
    optionalQuestRequirements = new() {new GeneratedQuestRequirement
    {
        requirementInQuest = new QuestRequirementInQuest {requirement = new QuestRequirementsAdditionalEffects()}
    }}
};
PotionCraft.ManagersSystem.Managers.Npc.CurrentNpcMonoBehaviour = nextNpc;
Check(!CurrentCustomerBudget.TryGet(quest, carrier, out _), "Customer switch exposed another customer's cached data.");
CurrentCustomerBudget.Refresh();
Check(CurrentCustomerBudget.RawMaximum == 14 && carrier.GetCostMultiplier(potion, quest) == 3,
    "Different customer sharing the same quest did not receive its own budget.");
nextNpc.mandatoryQuestRequirements.Clear();
CurrentCustomerBudget.Refresh();
Check(CurrentCustomerBudget.RawMaximum == null && !CurrentCustomerBudget.TryGet(quest, carrier, out _),
    "Removing Simple Recipe left an active budget.");
PotionCraft.ManagersSystem.Managers.Npc.CurrentNpcMonoBehaviour = null;
CurrentCustomerBudget.Refresh();
Check(CurrentCustomerBudget.RawMaximum == null, "Customer departure did not clear the budget.");
Console.WriteLine("Passed current-customer refresh, same-NPC edit/revert, customer switching, direct reward interfaces and single-evaluation aggregation.");

// Install the production lifecycle patches using the game's real Harmony.
var harmony = new HarmonyLib.Harmony("pcer.tests.current-customer-budget");
harmony.PatchAll(typeof(CurrentCustomerBudgetPatches));
var manager = PotionCraft.ManagersSystem.Managers.Npc;
manager.OnCurrentNpcChanged = () => Check(CurrentCustomerBudget.RawMaximum == 10,
    "Budget must be refreshed before native customer-change listeners run.");
manager.CurrentNpcMonoBehaviour = npc;
manager.OnCurrentNpcChanged = null;
quest.RawMaximum = 12;
quest.ApplyPartTo(npc);
Check(CurrentCustomerBudget.RawMaximum == 12, "Native quest application did not refresh the budget.");
manager.OnBeforeLoad();
Check(CurrentCustomerBudget.RawMaximum == null, "Load start did not clear the old budget.");
manager.OnLoad();
Check(CurrentCustomerBudget.RawMaximum == 12, "Load completion did not restore the active budget.");
npc.ResetComponentFields();
Check(CurrentCustomerBudget.RawMaximum == null, "NPC reset did not clear the budget.");
manager.CurrentNpcMonoBehaviour = null;
Check(CurrentCustomerBudget.RawMaximum == null, "Native customer departure left a stale budget.");
harmony.UnpatchSelf();
Console.WriteLine("Passed real Harmony installation and native switch/event ordering, quest application, load and reset hooks.");

// Planner commits edited/restored groups through the game's generation method.
// Budget updates must happen solely through the installed game lifecycle patches.
harmony = new HarmonyLib.Harmony("pcer.tests.prepared-generation");
harmony.PatchAll(typeof(CurrentCustomerBudgetPatches));
PotionCraftCustomerPlanner.GlobalRequirementGenerationPatch.Install(harmony, new BepInEx.Logging.ManualLogSource());
quest.RawMaximum = 10;
npc.currentQuest = quest;
manager.CurrentNpcMonoBehaviour = npc;
var preparedMandatory = new List<GeneratedQuestRequirement> {currentSimple};
var preparedOptional = new List<GeneratedQuestRequirement>
{
    new GeneratedQuestRequirement {requirementInQuest = new QuestRequirementInQuest {requirement = new QuestRequirementsAdditionalEffects()}}
};
int nativeCalls = quest.NativeGenerationCalls;
UnityEngine.Random.state = new UnityEngine.Random.State {Value = 123};
PotionCraftCustomerPlanner.NextCustomerWindow.GlobalRandomRule = new() {HasOverrides = true};
quest.BeforeGeneration = () => Check(CurrentCustomerBudget.RawMaximum == null,
    "Quest application must invalidate the previous budget before generation.");
PotionCraftCustomerPlanner.GlobalRequirementGenerationPatch.ApplyPreparedRequirements(quest, npc, preparedMandatory, preparedOptional);
Check(ReferenceEquals(npc.mandatoryQuestRequirements, preparedMandatory)
    && ReferenceEquals(npc.optionalQuestRequirements, preparedOptional), "Prepared lists were rerolled or copied incorrectly.");
Check(CurrentCustomerBudget.RawMaximum == 14 && carrier.GetCostMultiplier(potion, quest) == 3,
    "Native quest application did not publish the edited budget through its own lifecycle.");
Check(quest.NativeGenerationCalls == nativeCalls && PotionCraftCustomerPlanner.NextCustomerDirector.RandomGenerationCalls == 0
    && UnityEngine.Random.state.Value == 123, "Prepared group invoked random generation.");

var restoredMandatory = new List<GeneratedQuestRequirement> {restored};
PotionCraftCustomerPlanner.GlobalRequirementGenerationPatch.ApplyPreparedRequirements(quest, npc, restoredMandatory, new());
Check(CurrentCustomerBudget.RawMaximum == 10 && carrier.GetCostMultiplier(potion, quest) == 2,
    "Restoring a saved group did not use the same native lifecycle.");
using (PotionCraftCustomerPlanner.GlobalRequirementGenerationPatch.SuppressGeneration())
    PotionCraftCustomerPlanner.GlobalRequirementGenerationPatch.ApplyPreparedRequirements(quest, npc, restoredMandatory, new());
Check(npc.mandatoryQuestRequirements.Count == 1, "Prepared group lost to an enclosing suppression scope.");
PotionCraftCustomerPlanner.GlobalRequirementGenerationPatch.ApplyPreparedRequirements(quest, npc, new(), new());
Check(npc.mandatoryQuestRequirements.Count == 0 && npc.optionalQuestRequirements.Count == 0
    && CurrentCustomerBudget.RawMaximum == null, "Explicit empty group was regenerated or retained a budget.");

// A nested application for another customer must not consume the outer group.
PotionCraftCustomerPlanner.NextCustomerWindow.GlobalRandomRule = null;
var nestedQuest = new Quest();
var nestedNpc = new PotionCraft.Npc.MonoBehaviourScripts.NpcMonoBehaviour {currentQuest = nestedQuest};
quest.BeforeGeneration = () =>
{
    nestedQuest.ApplyPartTo(nestedNpc);
    Check(nestedQuest.NativeGenerationCalls == 1, "Prepared override leaked to an unrelated quest/customer.");
    PotionCraftCustomerPlanner.GlobalRequirementGenerationPatch.ApplyPreparedRequirements(nestedQuest, nestedNpc, new(), new());
};
PotionCraftCustomerPlanner.GlobalRequirementGenerationPatch.ApplyPreparedRequirements(quest, npc, preparedMandatory, preparedOptional);
Check(CurrentCustomerBudget.RawMaximum == 14 && ReferenceEquals(npc.mandatoryQuestRequirements, preparedMandatory),
    "Nested generation did not restore the outer prepared context.");

quest.BeforeGeneration = () => throw new InvalidOperationException("test failure");
try
{
    PotionCraftCustomerPlanner.GlobalRequirementGenerationPatch.ApplyPreparedRequirements(quest, npc, new(), new());
    throw new Exception("Expected generation failure.");
}
catch (InvalidOperationException exception) when (exception.Message == "test failure") { }
quest.BeforeGeneration = null;
quest.ApplyPartTo(npc);
Check(quest.NativeGenerationCalls == nativeCalls + 1, "Prepared context leaked after an exception.");
Check(CurrentCustomerBudget.RawMaximum == 14, "Normal generation failed after prepared-context cleanup.");
harmony.UnpatchSelf();
Console.WriteLine("Passed prepared generation lifecycle, restored/empty groups, suppression, RNG preservation and nested/exception cleanup.");

// Planner's native application remains functional without any budget hooks.
harmony = new HarmonyLib.Harmony("pcer.tests.planner-alone");
PotionCraftCustomerPlanner.GlobalRequirementGenerationPatch.Install(harmony, new BepInEx.Logging.ManualLogSource());
PotionCraftCustomerPlanner.GlobalRequirementGenerationPatch.ApplyPreparedRequirements(quest, npc, restoredMandatory, new());
Check(ReferenceEquals(npc.mandatoryQuestRequirements, restoredMandatory) && npc.optionalQuestRequirements.Count == 0,
    "Planner requires the budget mod to apply a prepared group.");
harmony.UnpatchSelf();
Console.WriteLine("Passed planner-only prepared generation without budget hooks.");
