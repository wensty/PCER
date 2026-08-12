using PotionCraft.ScriptableObjects;

namespace PotionCraftExtraRequirements;

public static class BuiltInRequirementDefinitions
{
    public const string BroadCategoryTag = "broad-ingredient-category";
    public const string HighlanderTag = "highlander";

    public static void Register()
    {
        RegisterCategoryPair("Herbs", "herbal", "草药", InventoryItemType.Herb, 7);
        RegisterCategoryPair("Mushrooms", "mushroom", "蘑菇", InventoryItemType.Mushroom, 7);
        RegisterCategoryPair("Crystals", "crystal", "矿石", InventoryItemType.Crystal, 9);

        RegisterHighlander(1, 3f);
        RegisterHighlander(2, 2f);
        RegisterHighlander(3, 1.5f);
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
            costMultiplier: 2f,
            tags: new[] { BroadCategoryTag },
            conflictingTags: new[] { BroadCategoryTag, HighlanderTag },
            isIngredientAllowed: ingredient => ingredient.GetItemType() != itemType,
            declaredTarget: RequirementTargetMetadata.FixedIngredientCategory(itemType, categoryNameEn)));

        RequirementCatalog.Register(new RequirementDefinition(
            id: "Only" + idSuffix,
            texts: OnlyCategoryTexts(categoryNameEn, categoryNameZh),
            isSatisfied: (potion, _, __) =>
                PotionIngredientFacts.UsesOnly(potion, itemType),
            unlockChapter: unlockChapter,
            costMultiplier: 3f,
            tags: new[] { BroadCategoryTag },
            conflictingTags: new[] { BroadCategoryTag, HighlanderTag },
            isIngredientAllowed: ingredient => ingredient.GetItemType() == itemType,
            declaredTarget: RequirementTargetMetadata.FixedIngredientCategory(itemType, categoryNameEn)));
    }

    private static void RegisterHighlander(int maximumAmount, float multiplier)
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
            popularityBonus: maximumAmount >= 2 ? 1 : 2));
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

    private static LocalizedLine L(string en, string zh)
    {
        return new LocalizedLine(en, zh);
    }
}
