using System.Collections.Generic;
using PotionCraft.FactionSystem;
using PotionCraft.Npc.Parts;
using PotionCraft.Npc.Parts.Settings;
using PotionCraft.QuestSystem;

namespace PotionCraftCustomerPlanner;

internal sealed class RegularCustomerOption
{
    public CustomerCandidateSource Source { get; }
    public Faction Faction { get; }
    public FactionClassInFaction ClassInFaction { get; }
    private readonly FactionClass directFactionClass;
    private readonly Gender.GenderSet directGender;
    public FactionClass FactionClass => ClassInFaction?.factionClass ?? directFactionClass;
    public NpcTemplate Template { get; }
    public Gender.GenderSet Gender => ClassInFaction == null ? directGender : ClassInFaction.gender;
    public string CachedListLabel { get; set; }
    public string CachedTooltip { get; set; }
    public List<Quest> CachedMatchingQuests { get; set; }
    public int CachedEnabledQuestCount { get; set; }

    public RegularCustomerOption(Faction faction, FactionClassInFaction classInFaction)
    {
        Source = CustomerCandidateSource.RegularFactionQuest;
        Faction = faction;
        ClassInFaction = classInFaction;
        Template = classInFaction?.factionClass?.npcTemplate;
        directGender = PotionCraft.Npc.Parts.Settings.Gender.GenderSet.Male;
    }

    public RegularCustomerOption(Faction faction, FactionClass factionClass, NpcTemplate template, Gender.GenderSet gender)
    {
        Source = CustomerCandidateSource.RegularFactionQuest;
        Faction = faction;
        directFactionClass = factionClass;
        Template = template;
        directGender = gender;
    }

    public RegularCustomerOption(NpcTemplate template, CustomerCandidateSource source)
    {
        Source = source;
        Template = template;
        directGender = PotionCraft.Npc.Parts.Settings.Gender.GenderSet.Male;
    }

    public string DisplayName
    {
        get
        {
            string faction = Faction == null ? "-" : Faction.name;
            string factionClass = FactionClass == null ? "-" : FactionClass.name;
            string template = Template == null ? "-" : Template.name;
            string source = Source == CustomerCandidateSource.PlotRandomClosenessQuest ? "Plot random closeness" : "Regular";
            return $"{template}  |  {faction} / {factionClass}  |  {Gender}  |  {source}";
        }
    }
}

internal enum CustomerCandidateSource
{
    RegularFactionQuest,
    PlotRandomClosenessQuest,
}
