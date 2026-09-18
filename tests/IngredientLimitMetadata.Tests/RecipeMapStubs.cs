namespace PotionCraft.ScriptableObjects
{
    public class PotionEffect { }
}
namespace PotionCraft.ManagersSystem.RecipeMap
{
    public class PositionAndRotation { public UnityEngine.Vector2 Position; }
    public class RecipeMapManagerPotionBasesSettings
    {
        public PotionCraft.ScriptableObjects.PotionBase defaultPotionBase = new();
    }
}
namespace PotionCraft.Settings
{
    public static class Settings<T> where T : new() { public static T Asset = new(); }
}
namespace UnityEngine
{
    public struct Vector2 { public float magnitude; }
    public static class Mathf
    {
        public static int FloorToInt(float value) => (int)Math.Floor(value);
        public static float Max(float a, float b) => Math.Max(a, b);
    }
}
namespace PotionCraftExtraRequirements
{
    internal static class PotionIngredientFacts
    {
        public static int IngredientAmount(PotionCraft.ScriptableObjects.Potion.Potion potion) => potion?.Amount ?? 0;
    }
}
