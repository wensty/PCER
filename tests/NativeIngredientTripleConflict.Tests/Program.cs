using PotionCraftExtraRequirements;
using PotionCraft.QuestSystem;
using HarmonyLib;

// Use the game's actual Harmony discovery, including convention-named methods.
var harmony = new Harmony("pcer.tests.triple-conflict");
harmony.PatchAll(typeof(NativeIngredientTripleConflictPatch));

var main = new QuestRequirementMainIngredient();
var particular = new QuestRequirementNeedOneParticularIngredient();
var lowlander = new QuestRequirementMaxIngredients { maxIngredientsAmount = 2 };
QuestRequirement[] triple = { main, particular, lowlander };

bool Allowed(QuestRequirement candidate, IEnumerable<QuestRequirement> others, bool original = true)
{
    var generated = others.Select(r => new GeneratedQuestRequirement
    {
        requirementInQuest = new QuestRequirementInQuest { requirement = r }
    }).ToList();
    NativeIngredientTripleConflictPatch.ApplyConflictRule(candidate, generated, ref original);
    if (original)
        Check(candidate.IsCompatibleWithOtherRequirements(generated), "Installed patch rejected valid requirements.");
    else if (generated.Count >= 2)
        Check(!candidate.IsCompatibleWithOtherRequirements(generated), "Installed patch missed the conflict.");
    return original;
}
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
}

int orders = 0;
foreach (var first in triple)
foreach (var second in triple.Where(r => r != first))
{
    var third = triple.Single(r => r != first && r != second);
    Check(Allowed(first, Array.Empty<QuestRequirement>()), "Single requirement rejected.");
    Check(Allowed(second, new[] { first }), "A valid pair was rejected.");
    Check(!Allowed(third, new[] { first, second }), "Third requirement was accepted.");
    orders++;
}
foreach (int limit in new[] { 1, 3, 4 })
{
    var otherLimit = new QuestRequirementMaxIngredients { maxIngredientsAmount = limit };
    Check(Allowed(otherLimit, new QuestRequirement[] {main, particular}), "Patch affected a different cap.");
    Check(Allowed(main, new QuestRequirement[] {otherLimit, particular}), "Patch affected another cap's ingredient check.");
}
Check(!Allowed(main, Array.Empty<QuestRequirement>(), false), "Native rejection was overwritten.");
Check(!Allowed(lowlander, new QuestRequirement[] {main, new QuestRequirement(), particular}), "Unrelated requirement hid the triple.");
Console.WriteLine($"Passed {orders} generation orders, every pair, other caps and native-rejection preservation.");
harmony.UnpatchSelf();
