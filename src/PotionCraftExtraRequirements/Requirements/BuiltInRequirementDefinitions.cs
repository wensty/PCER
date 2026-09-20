using PotionCraft.ScriptableObjects;

namespace PotionCraftExtraRequirements;

public static class BuiltInRequirementDefinitions
{
    public const string BroadCategoryTag = "broad-ingredient-category";
    public const string HighlanderTag = "highlander";
    public const string SimpleRecipeId = "SimpleRecipe";
    public const string SimpleRecipeTag = "simple-recipe";

    public static void Register()
    {
        RegisterCategoryPair("Herbs", "herbal", "草药", InventoryItemType.Herb, 7);
        RegisterCategoryPair("Mushrooms", "mushroom", "蘑菇", InventoryItemType.Mushroom, 7);
        RegisterCategoryPair("Crystals", "crystal", "矿石", InventoryItemType.Crystal, 9);

        // Fewer allowed copies make the requirement harder: increase both
        // its price reward and the Simple Recipe ingredient allowance.
        RegisterHighlander(3, 1.5f, 1f);
        RegisterHighlander(2, 2f, 2f);
        RegisterHighlander(1, 3f, 3f);

        RegisterSimpleRecipe();
    }

    private static void RegisterCategoryPair(
        string idSuffix,
        string categoryNameEn,
        string categoryNameZh,
        InventoryItemType itemType,
        int unlockChapter)
    {
        RequirementCatalog.Register(new RequirementDefinition(
            id: "No" + idSuffix,
            texts: NoCategoryTexts(categoryNameEn, categoryNameZh),
            isSatisfied: (potion, _, __) =>
                PotionIngredientFacts.UsesNone(potion, itemType),
            unlockChapter: unlockChapter,
            spawnWeight: 0.33f,
            costMultiplier: 2f,
            tags: new[] { BroadCategoryTag },
            conflictingTags: new[] { BroadCategoryTag, HighlanderTag, SimpleRecipeTag },
            isIngredientAllowed: ingredient => ingredient.GetItemType() != itemType,
            declaredTarget: RequirementTargetMetadata.FixedIngredientCategory(itemType, categoryNameEn)));

        RequirementCatalog.Register(new RequirementDefinition(
            id: "Only" + idSuffix,
            texts: OnlyCategoryTexts(categoryNameEn, categoryNameZh),
            isSatisfied: (potion, _, __) =>
                PotionIngredientFacts.UsesOnly(potion, itemType),
            unlockChapter: unlockChapter,
            spawnWeight: 0.33f,
            costMultiplier: 3f,
            tags: new[] { BroadCategoryTag },
            conflictingTags: new[] { BroadCategoryTag, HighlanderTag, SimpleRecipeTag },
            isIngredientAllowed: ingredient => ingredient.GetItemType() == itemType,
            declaredTarget: RequirementTargetMetadata.FixedIngredientCategory(itemType, categoryNameEn)));
    }

    private static void RegisterHighlander(int maximumAmount, float multiplier, float ingredientAllowance)
    {
        RequirementCatalog.Register(new RequirementDefinition(
            id: "Highlander" + maximumAmount,
            texts: HighlanderTexts(maximumAmount),
            isSatisfied: (potion, _, __) =>
                PotionIngredientFacts.UsesAtMostPerIngredient(potion, maximumAmount),
            unlockChapter: 4,
            costMultiplier: multiplier,
            tags: new[] { HighlanderTag },
            conflictingTags: new[] { HighlanderTag, BroadCategoryTag },
            popularityBonus: maximumAmount >= 2 ? 1 : 2)
        {
            IngredientLimitModifier = _ => new IngredientLimitModifier(ingredientAllowance)
        });
    }

    private static void RegisterSimpleRecipe()
    {
        RequirementCatalog.Register(new RequirementDefinition(
            id: SimpleRecipeId,
            texts: SimpleRecipeTexts(),
            isSatisfied: SimpleRecipeRequirementLogic.IsSatisfied,
            unlockChapter: 9,
            costMultiplier: 2f,
            tags: new[] { SimpleRecipeTag },
            conflictingTags: new[] { BroadCategoryTag },
            canGenerate: SimpleRecipeRequirementLogic.CanGenerate,
            popularityBonus: 1)
        {
            DefaultEnhancedCostMultiplier = 3f,
            DynamicCostMultiplier = SimpleRecipeRequirementLogic.GetCostMultiplier,
            DefaultEnhancedPopularityBonus = 2,
            DynamicPopularityBonus = SimpleRecipeRequirementLogic.GetPopularityBonus
        });
    }

    private static RequirementTexts NoCategoryTexts(string categoryEn, string categoryZh)
    {
        return new RequirementTexts(
            mandatory: new[]
            {
                L($"Do not use any {categoryEn} ingredients.", $"不能使用任何{categoryZh}类素材。"),
                L($"The potion must contain no {categoryEn} ingredients.", $"药剂中绝对不能含有{categoryZh}类素材。")
            },
            optional: new[]
            {
                L($"I would prefer a potion without {categoryEn} ingredients.", $"我更想要一瓶不使用{categoryZh}类素材的药剂。"),
                L($"If possible, avoid using {categoryEn} ingredients.", $"如果可以，请不要使用{categoryZh}类素材。")
            },
            mandatoryReactions: new[]
            {
                L($"This contains {categoryEn} ingredients. I cannot accept it.", $"这里面有{categoryZh}类素材，我不能接受。"),
                L($"I specifically said no {categoryEn} ingredients.", $"我明确说过不能使用{categoryZh}类素材。")
            },
            optionalReactions: new[]
            {
                L($"A pity—you used {categoryEn} ingredients after all.", $"真遗憾，你还是使用了{categoryZh}类素材。"),
                L($"I would have preferred it without {categoryEn} ingredients.", $"要是没有{categoryZh}类素材就更合我意了。")
            });
    }

    private static RequirementTexts OnlyCategoryTexts(string categoryEn, string categoryZh)
    {
        return new RequirementTexts(
            mandatory: new[]
            {
                L($"Use only {categoryEn} ingredients.", $"只能使用{categoryZh}类素材。"),
                L($"Every ingredient must be a {categoryEn} ingredient.", $"所有素材都必须属于{categoryZh}类。")
            },
            optional: new[]
            {
                L($"I would prefer it made only with {categoryEn} ingredients.", $"我更想要一瓶只用{categoryZh}类素材制作的药剂。"),
                L($"If possible, use nothing but {categoryEn} ingredients.", $"如果可以，请只使用{categoryZh}类素材。")
            },
            mandatoryReactions: new[]
            {
                L($"This was not made solely with {categoryEn} ingredients.", $"这瓶药剂并非只用{categoryZh}类素材制成。"),
                L($"There are other kinds of ingredients in this potion.", $"这瓶药剂里混入了其他种类的素材。")
            },
            optionalReactions: new[]
            {
                L($"A pity—it is not made entirely from {categoryEn} ingredients.", $"可惜，它并不完全由{categoryZh}类素材制成。"),
                L($"I would have liked only {categoryEn} ingredients.", $"我本来更希望你只使用{categoryZh}类素材。")
            });
    }

    private static RequirementTexts HighlanderTexts(int maximumAmount)
    {
        return new RequirementTexts(
            mandatory: new[]
            {
                L($"Use no more than {maximumAmount} of each ingredient.", $"每种素材最多使用 {maximumAmount} 个。"),
                L($"No single ingredient may be used more than {maximumAmount} times.", $"任何一种素材都不能使用超过 {maximumAmount} 个。")
            },
            optional: new[]
            {
                L($"I would prefer no more than {maximumAmount} of each ingredient.", $"我希望每种素材最多使用 {maximumAmount} 个。"),
                L($"If possible, limit every ingredient to {maximumAmount}.", $"如果可以，请把每种素材的用量限制在 {maximumAmount} 个以内。")
            },
            mandatoryReactions: new[]
            {
                L($"You used too much of the same ingredient.", $"有一种素材的用量太多了。"),
                L($"One of the ingredients exceeds my limit of {maximumAmount}.", $"其中一种素材超过了我要求的 {maximumAmount} 个上限。")
            },
            optionalReactions: new[]
            {
                L($"A pity—one ingredient was used more than {maximumAmount} times.", $"可惜，有一种素材使用了超过 {maximumAmount} 个。"),
                L($"I would have preferred smaller amounts of each ingredient.", $"我更希望每种素材都少用一些。")
            });
    }

    private static RequirementTexts SimpleRecipeTexts()
    {
        return new RequirementTexts(
            mandatory: new[]
            {
                L("Make this potion with no more than {0} ingredients total.", "制作这瓶药剂时，素材总数不能超过 {0} 个。"),
                L("Keep the recipe simple: use at most {0} ingredients.", "配方要简单：最多使用 {0} 个素材。")
            },
            optional: new[]
            {
                L("If possible, make it with no more than {0} ingredients total.", "如果可以，请把素材总数限制在 {0} 个以内。"),
                L("I would prefer a simple recipe, at most {0} ingredients.", "我更想要简单配方，最多 {0} 个素材。")
            },
            mandatoryReactions: new[]
            {
                L("This recipe uses too many ingredients.", "这份配方使用的素材太多了。"),
                L("I asked for no more than {0} ingredients.", "我要求素材总数不能超过 {0} 个。")
            },
            optionalReactions: new[]
            {
                L("A pity—the recipe is more complicated than I hoped.", "可惜，这份配方比我希望的更复杂。"),
                L("I would have preferred no more than {0} ingredients.", "我本来更希望素材总数不超过 {0} 个。")
            })
        {
            PartialReactions = new[]
            {
                L("This will do. But if you could make it with no more than {1} ingredients, I'd gladly pay a little extra.",
                  "这瓶可以。不过，要是只用 {1} 个以内的素材就能做出来，我愿意再多付些钱。"),
                L("A fine potion! I'd pay more for one made with just {1} ingredients or fewer.",
                  "药剂不错！要是用不超过 {1} 个素材就能配出来，我还愿意多付些钱。")
            }
        };
    }

    private static LocalizedLine L(string en, string zh)
    {
        return new LocalizedLine(en, zh);
    }
}
