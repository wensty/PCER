using PotionCraftCustomerPlanner;

void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

int Count(int[] chances, params float[] samples)
{
    int index = 0;
    return RequirementSlotRules.RollSlots(chances, () => samples[index++]).Count();
}

Check(Count(Array.Empty<int>()) == 0, "An empty override must not roll or fall back.");
Check(Count(new[] { 0, 100, 100 }, 0) == 0, "Zero blocks later conditional slots.");
Check(Count(new[] { 100, 0, 100 }, 1, 0) == 1, "Stop at a failed middle slot.");
Check(Count(new[] { 50, 50 }, .6f) == 0, "Native first-slot miss.");
Check(Count(new[] { 50, 50 }, .2f, .6f) == 1, "Native second-slot miss.");
Check(Count(new[] { 50, 50 }, .2f, .2f) == 2, "Native two-slot success.");
Check(Count(Enumerable.Repeat(100, 12).ToArray(), Enumerable.Repeat(1f, 12).ToArray()) == 12,
    "More than four slots, with 100% guaranteed even at the RNG endpoint.");

var sources = Enumerable.Repeat(100, 3).ToArray();
var converted = RequirementSlotRules.ConvertedRanges(sources, sources, true, false);
Check(converted.Mandatory == (6, 6) && converted.Optional == (0, 0), "All-mandatory conversion must retain six slots.");
converted = RequirementSlotRules.ConvertedRanges(sources, sources, false, true);
Check(converted.Mandatory == (0, 0) && converted.Optional == (6, 6), "All-optional conversion must retain six slots.");
converted = RequirementSlotRules.ConvertedRanges(Array.Empty<int>(), Array.Empty<int>(), true, false);
Check(converted.Mandatory == (0, 0) && converted.Optional == (0, 0), "Difficulty conversion must not add a requirement to an empty override.");
converted = RequirementSlotRules.ConvertedRanges(new[] {100}, new[] {50, 100}, false, false);
Check(converted.Mandatory == (1, 1) && converted.Optional == (0, 2), "Unconverted sources retain separate ranges.");

// Exhaustively compare reported ranges to possible roll outcomes, including
// zero in the middle, guaranteed prefixes, and arbitrary uncertain suffixes.
int cases = 0;
int[] values = { 0, 50, 100 };
for (int length = 0; length <= 6; length++)
{
    int configs = (int)Math.Pow(3, length);
    for (int config = 0; config < configs; config++)
    {
        var chances = new int[length];
        int code = config;
        for (int i = 0; i < length; i++) { chances[i] = values[code % 3]; code /= 3; }
        var outcomes = new List<int>();
        for (int bits = 0; bits < (1 << length); bits++)
        {
            float[] rolls = Enumerable.Range(0, length).Select(i => (bits & (1 << i)) == 0 ? .25f : .75f).ToArray();
            outcomes.Add(Count(chances, rolls));
        }
        var range = RequirementSlotRules.Range(chances);
        Check(range.Min == outcomes.Min() && range.Max == outcomes.Max(), "Range/generator disagreement.");
        cases++;
    }
}
Console.WriteLine($"Passed slot generation boundaries and {cases} exhaustive range cases.");
