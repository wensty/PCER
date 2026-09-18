using PotionCraft.QuestSystem;

namespace BepInEx.Logging
{
    public class ManualLogSource
    {
        public void LogWarning(object message) => throw new Exception(message.ToString());
    }
}
namespace UnityEngine
{
    public static class Random
    {
        public struct State { public int Value; }
        public static State state;
    }
}
namespace PotionCraftCustomerPlanner
{
    public class TestRandomRule { public bool HasOverrides; }
    public static class NextCustomerWindow { public static TestRandomRule GlobalRandomRule; }
    public static class NextCustomerDirector
    {
        public static int RandomGenerationCalls;
        public static bool TryGenerateRandomRequirements(Quest quest, int chapter, TestRandomRule rule,
            out List<GeneratedQuestRequirement> mandatory, out List<GeneratedQuestRequirement> optional,
            out string reason, bool validateGeneratedGroup)
        {
            RandomGenerationCalls++;
            mandatory = new();
            optional = new();
            reason = "";
            return true;
        }
    }
}
