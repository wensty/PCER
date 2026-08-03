using System.Collections.Generic;
using System.Linq;
using PotionCraft.ManagersSystem.Potion.Entities;
using PotionCraft.ScriptableObjects;
using PotionCraft.ScriptableObjects.Ingredient;
using PotionCraft.ScriptableObjects.Potion;

namespace PotionCraftExtraRequirements;

public static class PotionIngredientFacts
{
    public static IReadOnlyList<AlchemySubstanceComponent> Ingredients(Potion potion)
    {
        if (potion?.GetUsedComponents() is SimpleAlchemySubstanceComponents components)
            return components.GetSummaryComponents()
                .Where(component => component.Component is Ingredient && component.Amount > 0)
                .ToList();
        return new List<AlchemySubstanceComponent>();
    }

    public static bool UsesOnly(Potion potion, InventoryItemType type)
    {
        IReadOnlyList<AlchemySubstanceComponent> ingredients = Ingredients(potion);
        return ingredients.Count > 0
            && ingredients.All(component => ((Ingredient)component.Component).GetItemType() == type);
    }

    public static bool UsesNone(Potion potion, InventoryItemType type)
    {
        return Ingredients(potion)
            .All(component => ((Ingredient)component.Component).GetItemType() != type);
    }

    public static bool UsesAtMostPerIngredient(Potion potion, int maximumAmount)
    {
        IReadOnlyList<AlchemySubstanceComponent> ingredients = Ingredients(potion);
        return ingredients.Count > 0
            && ingredients.All(component => component.Amount <= maximumAmount);
    }
}
