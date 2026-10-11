using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PotionCraft.ObjectBased.ElementSystem;
using PotionCraft.ScriptableObjects.Ingredient;

namespace PotionCraftCustomerPlanner;

internal static class IngredientElementAffinityCache
{
    private static readonly ConditionalWeakTable<Ingredient, Snapshot> Snapshots =
        new ConditionalWeakTable<Ingredient, Snapshot>();

    internal static float GetPotential(Ingredient ingredient, bool shortDistance, ElementType element)
    {
        if (ReferenceEquals(ingredient, null))
            throw new ArgumentNullException(nameof(ingredient));
        return Snapshots.GetValue(ingredient, Capture).GetPotential(shortDistance, element);
    }

    private static Snapshot Capture(Ingredient ingredient)
    {
        // Copy numbers, not the mutable game objects. Capture both distances together
        // on first use, after the ingredient has been registered by its owner.
        ElementalPotential normal = ingredient.GetElementalPotential(false);
        ElementalPotential shortDistance = ingredient.GetElementalPotential(true);
        var normalValues = new Dictionary<ElementType, float>();
        var shortValues = new Dictionary<ElementType, float>();
        foreach (ElementType element in Enum.GetValues(typeof(ElementType)))
        {
            normalValues.Add(element, normal?.GetPotential(element, 0, false) ?? 0f);
            shortValues.Add(element, shortDistance?.GetPotential(element, 0, false) ?? 0f);
        }
        return new Snapshot(normalValues, shortValues);
    }

    private sealed class Snapshot
    {
        private readonly Dictionary<ElementType, float> normal;
        private readonly Dictionary<ElementType, float> shortDistance;

        internal Snapshot(Dictionary<ElementType, float> normal, Dictionary<ElementType, float> shortDistance)
        {
            this.normal = normal;
            this.shortDistance = shortDistance;
        }

        internal float GetPotential(bool useShortDistance, ElementType element)
        {
            return (useShortDistance ? shortDistance : normal)[element];
        }
    }
}
