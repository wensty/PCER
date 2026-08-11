using System.Collections.Generic;
using System.Linq;
using PotionCraft.FactionSystem;
using PotionCraft.ManagersSystem;
using PotionCraft.ManagersSystem.Npc;
using PotionCraft.Npc.MonoBehaviourScripts;
using PotionCraft.Npc.Parts;
using PotionCraft.QuestSystem;
using PotionCraft.Settings;
using UnityEngine;

namespace PotionCraftCustomerPlanner;

internal static class RegularCustomerPool
{
    private const float DefaultTinyFactionSpawnChanceThreshold = 0.001f;
    private static float tinyFactionSpawnChanceThreshold = DefaultTinyFactionSpawnChanceThreshold;

    public static void Configure(float tinySpawnChanceThreshold)
    {
        tinyFactionSpawnChanceThreshold = Mathf.Max(0f, tinySpawnChanceThreshold);
    }

    public static List<RegularCustomerOption> GetAvailableRegularCustomers(int chapter, int karma, bool strict)
    {
        List<RegularCustomerOption> result = new List<RegularCustomerOption>();
        if (!IsGameReady())
            return result;

        foreach (Faction faction in Faction.allFactions ?? Enumerable.Empty<Faction>())
        {
            if (!IsFactionAvailable(faction, chapter, karma, strict))
                continue;

            foreach (FactionClassInFaction classInFaction in faction.factionClasses)
            {
                if (!IsClassAvailable(faction, classInFaction, chapter))
                    continue;

                NpcTemplate template = classInFaction.factionClass.npcTemplate;
                if (!IsTemplateAvailable(template, chapter, karma, strict))
                    continue;

                result.Add(new RegularCustomerOption(faction, classInFaction));
            }
        }

        foreach (RegularCustomerOption plotOption in GetAvailablePlotRandomClosenessCustomers(chapter, karma, strict))
            result.Add(plotOption);

        return result
            .OrderBy(option => option.Source)
            .ThenBy(option => option.Faction?.name ?? string.Empty)
            .ThenBy(option => option.FactionClass?.name ?? string.Empty)
            .ThenBy(option => option.Template?.name ?? string.Empty)
            .ToList();
    }

    public static bool IsRegularCustomerAvailable(
        RegularCustomerOption option,
        int chapter,
        int karma,
        bool strict)
    {
        return IsGameReady()
            && option != null
            && IsCandidateAvailable(option, chapter, karma, strict);
    }

    public static bool IsQuestAvailableForRegularCustomer(
        RegularCustomerOption option,
        Quest quest,
        int chapter)
    {
        return option != null
            && quest != null
            && IsQuestAvailableForCandidate(option, quest, chapter);
    }

    public static List<Quest> GetSpawnableQuestsForRegularCustomer(
        RegularCustomerOption option,
        int chapter)
    {
        if (option == null)
            return new List<Quest>();

        if (option.Source == CustomerCandidateSource.PlotRandomClosenessQuest)
            return GetPlotRandomClosenessQuests(option.Template, chapter);

        if (option.FactionClass == null)
            return new List<Quest>();

        return option.FactionClass
            .GetEnabledQuests(option.Faction, chapter)
            .Where(quest => IsQuestAvailableForRegularCustomer(option, quest, chapter))
            .ToList();
    }

    public static float GetFactionSpawnChanceAtKarma(RegularCustomerOption option, int karma)
    {
        return option?.Faction?.spawnChance == null ? 0f : option.Faction.spawnChance.Evaluate(karma);
    }

    public static bool HasTinyPositiveFactionSpawnChance(RegularCustomerOption option, int karma)
    {
        float value = GetFactionSpawnChanceAtKarma(option, karma);
        return value > 0f && value <= tinyFactionSpawnChanceThreshold;
    }

    public static bool HasNoPositiveFactionSpawnChance(RegularCustomerOption option, int karma)
    {
        if (option?.Source != CustomerCandidateSource.RegularFactionQuest)
            return false;
        return GetFactionSpawnChanceAtKarma(option, karma) <= 0f;
    }

    public static bool TryCreateOptionFromNpc(
        NpcMonoBehaviour npc,
        out RegularCustomerOption option,
        out string reason)
    {
        option = null;
        reason = string.Empty;
        if (npc == null)
        {
            reason = "There is no current customer.";
            return false;
        }

        if (npc.faction != null && npc.factionClass != null)
        {
            FactionClassInFaction classInFaction = npc.faction.factionClasses?
                .FirstOrDefault(item => item?.factionClass == npc.factionClass);
            option = classInFaction != null
                ? new RegularCustomerOption(npc.faction, classInFaction)
                : new RegularCustomerOption(npc.faction, npc.factionClass, npc.template, npc.gender);
            return true;
        }

        if (Settings<NpcManagerSettings>.Asset?.plotNpc != null
            && npc.template != null
            && Settings<NpcManagerSettings>.Asset.plotNpc.ContainsKey(npc.template))
        {
            option = new RegularCustomerOption(npc.template, CustomerCandidateSource.PlotRandomClosenessQuest);
            return true;
        }

        reason = "The current NPC is not a supported regular or repeatable plot customer.";
        return false;
    }

    private static IEnumerable<RegularCustomerOption> GetAvailablePlotRandomClosenessCustomers(
        int chapter,
        int karma,
        bool strict)
    {
        NpcManagerSettings settings = Settings<NpcManagerSettings>.Asset;
        if (settings?.plotNpc == null)
            yield break;

        foreach (KeyValuePair<NpcTemplate, float> pair in settings.plotNpc)
        {
            NpcTemplate template = pair.Key;
            if (!IsPlotTemplateAvailable(template, chapter, karma, strict))
                continue;
            if (GetPlotRandomClosenessQuests(template, chapter).Count == 0)
                continue;
            yield return new RegularCustomerOption(template, CustomerCandidateSource.PlotRandomClosenessQuest);
        }
    }

    private static bool IsCandidateAvailable(RegularCustomerOption option, int chapter, int karma, bool strict)
    {
        if (option.Source == CustomerCandidateSource.PlotRandomClosenessQuest)
            return IsPlotTemplateAvailable(option.Template, chapter, karma, strict)
                && GetPlotRandomClosenessQuests(option.Template, chapter).Count > 0;

        return IsFactionAvailable(option.Faction, chapter, karma, strict)
            && IsClassAvailable(option.Faction, option.ClassInFaction, chapter)
            && IsTemplateAvailable(option.Template, chapter, karma, strict);
    }

    private static bool IsQuestAvailableForCandidate(RegularCustomerOption option, Quest quest, int chapter)
    {
        if (option.Source == CustomerCandidateSource.PlotRandomClosenessQuest)
            return GetPlotRandomClosenessQuests(option.Template, chapter).Contains(quest);

        return option.FactionClass != null
            && quest.IsEnabled(option.Faction, chapter)
            && CanFactionGenerateQuest(option.Faction, quest);
    }

    private static bool IsGameReady()
    {
        return Managers.Npc != null
            && Managers.Goals != null
            && Managers.Player != null
            && Managers.Player.karma != null
            && Faction.allFactions != null
            && NpcTemplate.allNpcTemplates != null;
    }

    private static bool IsFactionAvailable(Faction faction, int currentChapter, int karma, bool strict)
    {
        if (faction == null)
            return false;
        if (!faction.IsEnabled(currentChapter))
            return false;
        if (faction.spawnChance == null)
            return false;
        if (strict)
            return faction.spawnChance.Evaluate(karma) > 0f;
        return Enumerable.Range(-100, 201).Any(value => faction.spawnChance.Evaluate(value) > 0f);
    }

    private static bool IsClassAvailable(
        Faction faction,
        FactionClassInFaction classInFaction,
        int currentChapter)
    {
        return classInFaction != null
            && classInFaction.spawnChance > 0f
            && classInFaction.IsEnabled(faction, currentChapter);
    }

    private static bool IsTemplateAvailable(NpcTemplate template, int currentChapter, int karma, bool strict)
    {
        if (template == null)
            return false;
        if (currentChapter < template.unlockAtChapter)
            return false;
        if (strict && !template.karmaForSpawn.IsInRange(karma))
            return false;
        if (!strict && !Enumerable.Range(-100, 201).Any(value => template.karmaForSpawn.IsInRange(value)))
            return false;
        if (Managers.Npc.globalSettings.IsNpcInWhoNeverComeSet(template))
            return false;
        return true;
    }

    private static bool IsPlotTemplateAvailable(NpcTemplate template, int currentChapter, int karma, bool strict)
    {
        if (template == null)
            return false;
        if (currentChapter < template.unlockAtChapter)
            return false;
        if (strict && !template.karmaForSpawn.IsInRange(karma))
            return false;
        if (!strict && !Enumerable.Range(-100, 201).Any(value => template.karmaForSpawn.IsInRange(value)))
            return false;
        if (Managers.Npc.globalSettings.IsNpcInWhoNeverComeSet(template))
            return false;
        return true;
    }

    private static List<Quest> GetPlotRandomClosenessQuests(NpcTemplate template, int currentChapter)
    {
        if (template == null)
            return new List<Quest>();

        int closeness = Managers.Npc?.closeness == null ? 0 : Managers.Npc.closeness.GetCloseness(template);
        return template.GetRandomClosenessQuests(closeness)
            .Where(quest => quest != null
                && quest.desiredEffects != null
                && quest.desiredEffects.Any(effect => effect != null && effect.IsEnabledForChapter(currentChapter)))
            .Distinct()
            .ToList();
    }

    private static bool CanFactionGenerateQuest(Faction faction, Quest quest)
    {
        if (faction == null || quest?.desiredEffects == null)
            return false;
        return quest.desiredEffects.Any(effect => effect != null && faction.GetQuestSpawnChance(quest, effect) > 0f);
    }

    public static float TinyFactionSpawnChanceThreshold
    {
        get { return tinyFactionSpawnChanceThreshold; }
    }
}
