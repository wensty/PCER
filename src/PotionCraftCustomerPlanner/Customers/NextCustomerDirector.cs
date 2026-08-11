using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using PotionCraft.FactionSystem;
using PotionCraft.ManagersSystem;
using PotionCraft.ManagersSystem.Npc;
using PotionCraft.Npc;
using PotionCraft.Npc.MonoBehaviourScripts;
using PotionCraft.Npc.Parts;
using PotionCraft.Npc.Parts.Settings;
using PotionCraft.ObjectBased.UIElements.Dialogue;
using PotionCraft.ObjectBased.ScalesSystem;
using PotionCraft.QuestSystem;
using PotionCraft.ScriptableObjects;
using PotionCraft.ScriptableObjects.Ingredient;
using PotionCraft.Settings;
using UnityEngine;

namespace PotionCraftCustomerPlanner;

internal static class NextCustomerDirector
{
    private static ManualLogSource logger;
    private static PlannedCustomer pendingCustomer;
    private static readonly List<PlannedCustomer> scheduledCustomers = new List<PlannedCustomer>();
    private static readonly HashSet<PlannedCustomer> appliedAppointments = new HashSet<PlannedCustomer>();
    private static NpcMonoBehaviour customerWithAppliedAppointment;
    private static readonly System.Reflection.PropertyInfo CurrentNpcProperty =
        AccessTools.Property(typeof(NpcManager), "CurrentNpcMonoBehaviour");
    private static readonly MethodInfo CheckPotionOnSuitabilityMethod =
        AccessTools.Method(typeof(ScalesCupDisplay), "CheckPotionOnSuitability");

    public static PlannedCustomer PendingCustomer => scheduledCustomers.FirstOrDefault()
        ?? pendingCustomer;

    public static int ScheduledCount => scheduledCustomers.Count;

    public static void Configure(ManualLogSource logSource)
    {
        logger = logSource;
    }

    public static ScheduleResult Schedule(PlannedCustomer plan)
    {
        if (!CustomerPatchInstaller.EnsurePatched(logger))
        {
            logger?.LogWarning("Cannot schedule next customer because spawn patches could not be installed.");
            return ScheduleResult.Blocked("Spawn patches could not be installed.");
        }

        int removedAppliedAppointments = RemoveAppliedAppointments();
        if (TryApplyPlanToCurrentCustomer(plan, allowRebuild: true, out string reason))
        {
            scheduledCustomers.Add(plan);
            appliedAppointments.Add(plan);
            logger?.LogInfo($"Scheduled plan applied immediately and marked until the next schedule add: {plan.Customer.DisplayName}; quest={plan.TargetQuest?.name ?? "-"}");
            return ScheduleResult.AppliedNow(removedAppliedAppointments);
        }

        scheduledCustomers.Add(plan);
        logger?.LogInfo($"Scheduled next customer: {plan.Customer.DisplayName}; quest={plan.TargetQuest?.name ?? "-"}; immediateApply={reason}");
        return ScheduleResult.Queued(removedAppliedAppointments, reason);
    }

    public static void Clear()
    {
        scheduledCustomers.Clear();
        appliedAppointments.Clear();
        pendingCustomer = null;
        customerWithAppliedAppointment = null;
    }

    public static void ResetTransientStateForSaveLoad(string phase)
    {
        if (scheduledCustomers.Count == 0
            && appliedAppointments.Count == 0
            && pendingCustomer == null
            && customerWithAppliedAppointment == null)
        {
            return;
        }

        Clear();
        logger?.LogInfo($"Cleared scheduled customer planner transient state {phase}.");
    }

    public static bool TryGetCurrentCustomer(
        out RegularCustomerOption customer,
        out Quest currentQuest,
        out string reason)
    {
        return TryGetCurrentCustomer(
            out customer,
            out currentQuest,
            out _,
            out _,
            out reason);
    }

    public static bool TryGetCurrentCustomer(
        out RegularCustomerOption customer,
        out Quest currentQuest,
        out IReadOnlyList<GeneratedQuestRequirement> mandatoryRequirements,
        out IReadOnlyList<GeneratedQuestRequirement> optionalRequirements,
        out string reason)
    {
        customer = null;
        currentQuest = null;
        mandatoryRequirements = new List<GeneratedQuestRequirement>();
        optionalRequirements = new List<GeneratedQuestRequirement>();
        NpcMonoBehaviour current = GetCurrentNpc(Managers.Npc);
        if (current == null)
        {
            reason = "There is no current customer.";
            return false;
        }

        currentQuest = current.currentQuest;
        mandatoryRequirements = current.mandatoryQuestRequirements?.ToList()
            ?? new List<GeneratedQuestRequirement>();
        optionalRequirements = current.optionalQuestRequirements?.ToList()
            ?? new List<GeneratedQuestRequirement>();
        return RegularCustomerPool.TryCreateOptionFromNpc(current, out customer, out reason);
    }

    public static bool ApplyToCurrentCustomer(
        PlannedCustomer plan,
        bool allowRebuild,
        out string reason)
    {
        return TryApplyPlanToCurrentCustomer(plan, allowRebuild, out reason);
    }

    private static bool TryApplyPlanToCurrentCustomer(
        PlannedCustomer plan,
        bool allowRebuild,
        out string reason)
    {
        reason = string.Empty;
        if (plan == null)
        {
            reason = "No plan is selected.";
            return false;
        }
        if (Managers.Npc == null)
        {
            reason = "NPC manager is not ready.";
            return false;
        }

        PlannedCustomer previousPendingCustomer = pendingCustomer;
        pendingCustomer = plan;

        NpcMonoBehaviour current = GetCurrentNpc(Managers.Npc);
        if (current == null)
        {
            reason = "There is no current customer.";
            RestorePendingState(previousPendingCustomer);
            return false;
        }
        if (current == customerWithAppliedAppointment)
        {
            reason = "The current customer has already received a scheduled plan.";
            RestorePendingState(previousPendingCustomer);
            return false;
        }

        if (IsPendingCustomer(current))
        {
            ApplyPlanToNpc(current);
            logger?.LogInfo($"Applied plan to current customer: {plan.Customer.DisplayName}; quest={current.currentQuest?.name ?? "-"}");
            RefreshDialogueBox();
            RestorePendingState(previousPendingCustomer);
            return true;
        }

        if (!allowRebuild)
        {
            reason = "The current customer does not match the selected plan.";
            RestorePendingState(previousPendingCustomer);
            return false;
        }

        if (!CanRebuildCurrentNpcForPlan(current, plan, out reason))
        {
            RestorePendingState(previousPendingCustomer);
            return false;
        }

        if (!TryRebuildCurrentNpc(Managers.Npc, current))
        {
            reason = "Failed to rebuild the current customer.";
            RestorePendingState(previousPendingCustomer);
            return false;
        }

        ApplyPlanToNpc(current);
        logger?.LogInfo($"Rebuilt current customer: {plan.Customer.DisplayName}; quest={current.currentQuest?.name ?? "-"}");
        RefreshDialogueBox();
        RestorePendingState(previousPendingCustomer);
        return true;
    }

    private static void RestorePendingState(PlannedCustomer customer)
    {
        pendingCustomer = customer;
    }

    public static void ReplaceCurrentNpcIfNeeded(NpcManager npcManager)
    {
        if (npcManager == null)
            return;

        NpcMonoBehaviour current = GetCurrentNpc(npcManager);
        if (current == null)
            return;

        if (current == customerWithAppliedAppointment)
        {
            logger?.LogInfo($"Scheduled customer reached the counter; refreshing dialogue and trade state: {current.currentQuest?.name ?? "-"}");
            RefreshDialogueBox();
            customerWithAppliedAppointment = null;
            return;
        }

        if (scheduledCustomers.Count == 0)
            return;

        PlannedCustomer naturalPlan = FirstNaturalPlanForNpc(current);
        if (naturalPlan != null)
        {
            pendingCustomer = naturalPlan;
            ApplyPlanToNpc(current);
            logger?.LogInfo($"Applied scheduled current customer: {naturalPlan.Customer.DisplayName}; quest={current.currentQuest?.name ?? "-"}");
            RefreshDialogueBox();
            CompleteScheduledPlan(naturalPlan);
            return;
        }

        PlannedCustomer plan = FirstCurrentRebuildPlan(current, npcManager);
        if (plan == null)
            return;
        pendingCustomer = plan;

        if (!TryRebuildCurrentNpc(npcManager, current))
            return;

        ApplyPlanToNpc(current);
        logger?.LogInfo($"Rebuilt current NPC as scheduled customer: {plan.Customer.DisplayName}; quest={current.currentQuest?.name ?? "-"}");
        RefreshDialogueBox();
        CompleteScheduledPlan(plan);
    }

    public static bool IsRequirementGroupAllowed(
        RegularCustomerOption customer,
        int chapter,
        int karma,
        bool strict,
        Quest targetQuest,
        IReadOnlyList<PlannedRequirement> mandatoryRequirements,
        IReadOnlyList<PlannedRequirement> optionalRequirements,
        out string reason)
    {
        reason = string.Empty;
        if (customer == null)
        {
            reason = "No customer selected.";
            return false;
        }

        if (!RegularCustomerPool.IsRegularCustomerAvailable(customer, chapter, karma, strict))
        {
            reason = strict
                ? "The selected customer cannot naturally spawn with the current chapter and karma."
                : "The selected customer is not mechanically spawnable in the preview chapter.";
            return false;
        }

        if (targetQuest == null)
        {
            reason = "No target quest selected.";
            return false;
        }

        List<Quest> enabledQuests = RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(customer, chapter);
        if (enabledQuests.Count == 0)
        {
            reason = "No spawnable quest exists for this customer in the current chapter.";
            return false;
        }

        if (!enabledQuests.Contains(targetQuest))
        {
            reason = "The target quest is not spawnable for this customer in the preview chapter.";
            return false;
        }

        if (mandatoryRequirements.Count == 0 && optionalRequirements.Count == 0)
            return true;

        return AreRequirementsAllowedForQuest(
            targetQuest,
            mandatoryRequirements,
            optionalRequirements,
            out reason);
    }

    public static bool AreRequirementsAllowedForQuest(
        Quest targetQuest,
        IReadOnlyList<PlannedRequirement> mandatoryRequirements,
        IReadOnlyList<PlannedRequirement> optionalRequirements,
        out string reason)
    {
        reason = string.Empty;
        if (targetQuest == null)
        {
            reason = "No target quest selected.";
            return false;
        }

        if (mandatoryRequirements.Count == 0 && optionalRequirements.Count == 0)
            return true;

        RequirementGenerationResult generated = TryGenerateRequirementGroup(
            targetQuest,
            mandatoryRequirements,
            optionalRequirements,
            preservedMandatory: null,
            preservedOptional: null,
            logSkipped: false);
        if (!generated.Success)
        {
            reason = generated.Reason;
            return false;
        }

        return true;
    }

    private static void ApplyTargetQuest(NpcMonoBehaviour npc)
    {
        if (pendingCustomer.TargetQuest == null)
            return;
        pendingCustomer.TargetQuest.ApplyPartTo(npc, npc.settings);
    }

    private static int RemoveAppliedAppointments()
    {
        if (appliedAppointments.Count == 0)
            return 0;
        int count = appliedAppointments.Count;
        scheduledCustomers.RemoveAll(plan => appliedAppointments.Contains(plan));
        appliedAppointments.Clear();
        customerWithAppliedAppointment = null;
        return count;
    }

    private static void CompleteScheduledPlan(PlannedCustomer plan)
    {
        scheduledCustomers.Remove(plan);
        appliedAppointments.Remove(plan);
        pendingCustomer = null;
    }

    private static void ApplyPlanToNpc(NpcMonoBehaviour npc)
    {
        ApplyTargetQuest(npc);
        ApplySelectedRequirements(npc);
        customerWithAppliedAppointment = npc;
    }

    private static void RefreshDialogueBox()
    {
        RefreshPotionOnScales();
        if (DialogueBox.Instance == null || Managers.Dialogue == null)
            return;

        bool previousInstant = Managers.Dialogue.changeStateInstantly;
        try
        {
            Managers.Dialogue.changeStateInstantly = true;
            DialogueBox.Instance.UpdateBoxState(Managers.Dialogue.State);
        }
        finally
        {
            Managers.Dialogue.changeStateInstantly = previousInstant;
        }
    }

    private static void RefreshPotionOnScales()
    {
        Scales scales = Scales.Instance;
        ScalesCupDisplay display = scales?.rightCupScript?.display;
        if (display?.currentPotionItem == null)
        {
            Managers.Trade?.RecalculateDealCost();
            return;
        }

        if (CheckPotionOnSuitabilityMethod != null)
        {
            CheckPotionOnSuitabilityMethod.Invoke(display, null);
        }
        else
        {
            Managers.Trade?.RecalculateDealCost();
        }

        Managers.Trade?.RecalculateDealCost();
        DialogueBox.Instance?.UpdateTradeButtons();
        if (Managers.Dialogue != null
            && (Managers.Dialogue.State == DialogueState.PotionRequest
                || Managers.Dialogue.State == DialogueState.ClosenessPotionRequest))
        {
            DialogueBox.Instance?.UpdatePotionRequestText(1f);
        }
    }

    private static PlannedCustomer FirstNaturalPlanForNpc(NpcMonoBehaviour npc)
    {
        return scheduledCustomers
            .Where(plan => !appliedAppointments.Contains(plan))
            .FirstOrDefault(plan => IsPlanCustomer(plan, npc));
    }

    private static PlannedCustomer FirstCurrentRebuildPlan(NpcMonoBehaviour current, NpcManager npcManager)
    {
        foreach (PlannedCustomer plan in scheduledCustomers)
        {
            if (appliedAppointments.Contains(plan))
                continue;
            if (!CanRebuildCurrentNpcForPlan(current, plan, out _))
                continue;
            return plan;
        }

        return null;
    }

    private static bool CanRebuildCurrentNpcForPlan(
        NpcMonoBehaviour current,
        PlannedCustomer plan,
        out string reason)
    {
        reason = string.Empty;
        if (current == null)
        {
            reason = "There is no current customer.";
            return false;
        }
        if (plan == null)
        {
            reason = "No plan is selected.";
            return false;
        }

        if (!plan.StrictPlanningMode)
        {
            if (IsCurrentNpcAnyScheduledCustomer(current, exceptPlan: plan))
            {
                reason = "The current customer matches another scheduled plan.";
                return false;
            }
            if (IsMerchantNpc(current))
            {
                reason = "The current NPC is a trader/merchant.";
                return false;
            }
            return true;
        }

        if (!IsReplaceableRegularCurrentNpc(current))
        {
            reason = "The current NPC is not a regular faction/class customer.";
            return false;
        }
        return true;
    }

    private static bool IsCurrentNpcAnyScheduledCustomer(NpcMonoBehaviour current, PlannedCustomer exceptPlan)
    {
        return scheduledCustomers
            .Where(plan => plan != exceptPlan && !appliedAppointments.Contains(plan))
            .Any(plan => IsPlanCustomer(plan, current));
    }

    private static bool IsMerchantNpc(NpcMonoBehaviour npc)
    {
        if (npc == null)
            return true;
        NpcManagerSettings settings = Settings<NpcManagerSettings>.Asset;
        if (settings == null || npc.template == null)
            return npc.faction == null && npc.factionClass == null;

        return ContainsTemplate(settings.mainTraders, npc.template)
            || ContainsTemplate(settings.extraTraders1?.templates, npc.template)
            || ContainsTemplate(settings.extraTraders2?.templates, npc.template)
            || ContainsTemplate(settings.extraTraders3?.templates, npc.template)
            || ContainsTemplate(settings.extraTraders4?.templates, npc.template)
            || ContainsTemplate(settings.karmicTraders?.templates, npc.template);
    }

    private static bool ContainsTemplate(NpcTemplateList list, NpcTemplate template)
    {
        return list?.templates != null && list.templates.Contains(template);
    }

    private static bool ContainsTemplate(List<NpcTemplate> list, NpcTemplate template)
    {
        return list != null && list.Contains(template);
    }

    private static bool TryRebuildCurrentNpc(NpcManager npcManager, NpcMonoBehaviour current)
    {
        SerializedNpcForSpawn serialized = npcManager.CreateNewSerializedNpcForSpawn(
            pendingCustomer.Customer.Template,
            PendingFaction(),
            PendingFactionClass());
        serialized.chapterOnAddToSpawn = pendingCustomer.ChapterOnAddToSpawn;

        int closeness = current.Closeness;
        int maxCloseness = current.MaxCloseness;
        NpcState state = current.CurrentState;
        UnityEngine.Vector3 localPosition = current.transform.localPosition;

        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        UnityEngine.Random.state = serialized.randomStatesContainer.Get("partsGeneration");
        StringIntDictionary clonedQuestsOnCooldown = new StringIntDictionary();
        if (current.questsOnCooldown != null)
        {
            foreach (KeyValuePair<string, int> pair in current.questsOnCooldown)
                clonedQuestsOnCooldown[pair.Key] = pair.Value;
        }
        System.Tuple<List<NonAppearancePart>, List<AppearanceContainer>, NpcPrefab> parts =
            pendingCustomer.Customer.Template.GetListOfPartsToApply(
                serialized.randomStatesContainer,
                closeness,
                serialized.chapterOnAddToSpawn,
                PendingFaction(),
                PendingFactionClass(),
                clonedQuestsOnCooldown);

        current.ResetComponentFields();
        current.isNpcInPool = false;
        current.Closeness = closeness;
        current.MaxCloseness = maxCloseness;
        current.randomStatesContainer = serialized.randomStatesContainer;
        current.chapterOnAddToSpawn = serialized.chapterOnAddToSpawn;
        current.faction = PendingFaction();
        current.factionClass = PendingFactionClass();
        current.questsOnCooldown = clonedQuestsOnCooldown;
        pendingCustomer.Customer.Template.ApplyToNpcMonoBehaviour(current, parts);
        current.trading.SetPotionOfferedTimesCount(serialized.potionOfferedTimesCount, updateMood: false);
        current.SetMood(serialized.npcMood);
        current.transform.localPosition = localPosition;
        current.CurrentState = state;
        UnityEngine.Random.state = randomState;
        current.usedTemplates.templates.Add(pendingCustomer.Customer.Template);
        current.usedTemplates.templates.AddRange(NpcTemplate.UsedSubTemplatesOnGetParts.templates);
        return true;
    }

    private static void ApplySelectedRequirements(NpcMonoBehaviour npc)
    {
        if (npc.currentQuest == null)
            return;

        bool hasMandatory = pendingCustomer.MandatoryRequirements.Count > 0;
        bool hasOptional = pendingCustomer.OptionalRequirements.Count > 0;
        if (!hasMandatory && !hasOptional)
            return;

        RequirementGenerationResult generated = TryGenerateRequirementGroup(
            npc.currentQuest,
            hasMandatory ? pendingCustomer.MandatoryRequirements : new List<PlannedRequirement>(),
            hasOptional ? pendingCustomer.OptionalRequirements : new List<PlannedRequirement>(),
            hasMandatory ? null : npc.mandatoryQuestRequirements,
            hasOptional ? null : npc.optionalQuestRequirements);

        if (!generated.Success)
        {
            logger?.LogWarning(
                "Scheduled requirement group was blocked by the spawned quest; keeping native generated requirements. "
                + $"quest={npc.currentQuest?.name ?? "-"}; "
                + $"mandatory={pendingCustomer.MandatoryRequirements.Count}; "
                + $"optional={pendingCustomer.OptionalRequirements.Count}; "
                + generated.Reason);
            return;
        }

        npc.mandatoryQuestRequirements = generated.Mandatory;
        npc.optionalQuestRequirements = generated.Optional;
    }

    private static RequirementGenerationResult TryGenerateRequirementGroup(
        Quest quest,
        IReadOnlyList<PlannedRequirement> mandatoryRequirements,
        IReadOnlyList<PlannedRequirement> optionalRequirements,
        IReadOnlyList<GeneratedQuestRequirement> preservedMandatory,
        IReadOnlyList<GeneratedQuestRequirement> preservedOptional,
        bool logSkipped = true)
    {
        List<RequirementGenerationEntry> entries = new List<RequirementGenerationEntry>();
        entries.AddRange(mandatoryRequirements.Select(requirement =>
            new RequirementGenerationEntry(requirement, isMandatory: true)));
        entries.AddRange(optionalRequirements.Select(requirement =>
            new RequirementGenerationEntry(requirement, isMandatory: false)));

        List<GeneratedQuestRequirement> preserved = new List<GeneratedQuestRequirement>();
        if (preservedMandatory != null)
            preserved.AddRange(preservedMandatory.Where(item => item != null));
        if (preservedOptional != null)
            preserved.AddRange(preservedOptional.Where(item => item != null));

        if (entries.Count == 0)
        {
            List<GeneratedQuestRequirement> finalPreserved = CloneGeneratedRequirements(preserved);
            if (AllGeneratedRequirementsValid(quest, finalPreserved, out string preservedReason))
                return RequirementGenerationResult.Allowed(
                    preservedMandatory?.ToList() ?? new List<GeneratedQuestRequirement>(),
                    preservedOptional?.ToList() ?? new List<GeneratedQuestRequirement>());
            return RequirementGenerationResult.Blocked(preservedReason);
        }

        if (!PreflightValidateRequirements(entries, out string preflightReason))
            return RequirementGenerationResult.Blocked(preflightReason);

        Random.State randomState = Random.state;
        try
        {
            foreach (List<RequirementGenerationEntry> order in CandidateOrders(entries))
            {
                RequirementGenerationResult attempt = TryGenerateRequirementOrder(
                    quest,
                    order,
                    preservedMandatory,
                    preservedOptional,
                    logSkipped: false);
                if (attempt.Success)
                    return attempt;
            }
        }
        finally
        {
            Random.state = randomState;
        }

        if (logSkipped)
            logger?.LogWarning("Scheduled requirement group failed all generation orders.");
        return RequirementGenerationResult.Blocked(
            "The selected requirement group or target is incompatible with the target quest.");
    }

    private static RequirementGenerationResult TryGenerateRequirementOrder(
        Quest quest,
        IReadOnlyList<RequirementGenerationEntry> order,
        IReadOnlyList<GeneratedQuestRequirement> preservedMandatory,
        IReadOnlyList<GeneratedQuestRequirement> preservedOptional,
        bool logSkipped)
    {
        List<GeneratedQuestRequirement> mandatory =
            preservedMandatory?.ToList() ?? new List<GeneratedQuestRequirement>();
        List<GeneratedQuestRequirement> optional =
            preservedOptional?.ToList() ?? new List<GeneratedQuestRequirement>();
        List<GeneratedQuestRequirement> allGenerated = new List<GeneratedQuestRequirement>();
        allGenerated.AddRange(mandatory);
        allGenerated.AddRange(optional);

        foreach (RequirementGenerationEntry entry in order)
        {
            if (!TryGenerateRequirement(
                quest,
                entry.PlannedRequirement,
                allGenerated,
                entry.IsMandatory,
                out GeneratedQuestRequirement generated,
                out string reason))
            {
                if (logSkipped)
                    logger?.LogWarning(reason);
                return RequirementGenerationResult.Blocked(reason);
            }

            if (entry.IsMandatory)
                mandatory.Add(generated);
            else
                optional.Add(generated);
            allGenerated.Add(generated);
        }

        if (!AllGeneratedRequirementsValid(quest, allGenerated, out string conflictReason))
            return RequirementGenerationResult.Blocked(conflictReason);

        return RequirementGenerationResult.Allowed(mandatory, optional);
    }

    private static bool TryGenerateRequirement(
        Quest quest,
        PlannedRequirement plannedRequirement,
        List<GeneratedQuestRequirement> allGenerated,
        bool isMandatory,
        out GeneratedQuestRequirement generated,
        out string reason)
    {
        generated = null;
        QuestRequirementInQuest requirement =
            QuestRequirementInQuest.GetByName(plannedRequirement.RequirementName, returnFirst: false, warning: false);
        if (requirement == null)
        {
            reason = $"Scheduled requirement not found: {plannedRequirement.RequirementName}";
            return false;
        }

        QuestRequirementInQuest wrapper = plannedRequirement.CreateWrapper(requirement);
        if (HasMissingTarget(plannedRequirement, requirement, wrapper))
        {
            reason = $"Scheduled requirement target not found: {plannedRequirement.RequirementName} -> {plannedRequirement.StringTargetName}";
            return false;
        }

        generated = wrapper.GetGeneratedRequirement(quest, allGenerated, isMandatory);
        if (generated == null)
        {
            reason = $"Scheduled requirement skipped as incompatible: {plannedRequirement.RequirementName}";
            return false;
        }

        if (plannedRequirement.IntTarget.HasValue)
            generated.intValue1 = plannedRequirement.IntTarget.Value;
        reason = string.Empty;
        return true;
    }

    private static bool PreflightValidateRequirements(
        IReadOnlyList<RequirementGenerationEntry> entries,
        out string reason)
    {
        reason = string.Empty;
        HashSet<string> duplicateNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (RequirementGenerationEntry entry in entries)
        {
            PlannedRequirement plannedRequirement = entry.PlannedRequirement;
            if (plannedRequirement == null)
            {
                reason = "A selected requirement is empty.";
                return false;
            }

            if (!duplicateNames.Add(plannedRequirement.RequirementName))
            {
                reason = $"Requirement selected more than once: {plannedRequirement.RequirementName}";
                return false;
            }

            QuestRequirementInQuest source = QuestRequirementInQuest.GetByName(
                plannedRequirement.RequirementName,
                returnFirst: false,
                warning: false);
            if (source == null)
            {
                reason = $"Scheduled requirement not found: {plannedRequirement.RequirementName}";
                return false;
            }

            QuestRequirementInQuest wrapper = plannedRequirement.CreateWrapper(source);
            if (HasMissingTarget(plannedRequirement, source, wrapper))
            {
                reason = $"Scheduled requirement target not found: {plannedRequirement.RequirementName} -> {plannedRequirement.StringTargetName}";
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<List<RequirementGenerationEntry>> CandidateOrders(
        IReadOnlyList<RequirementGenerationEntry> entries)
    {
        yield return entries.ToList();
        yield return entries.OrderBy(entry => entry.IsMandatory ? 0 : 1)
            .ThenByDescending(IsNoSaltsEntry)
            .ToList();
        yield return entries.OrderByDescending(entry => entry.IsMandatory ? 0 : 1)
            .ThenByDescending(IsNoSaltsEntry)
            .ToList();
        yield return entries.OrderByDescending(IsNoSaltsEntry)
            .ThenBy(entry => entry.IsMandatory ? 0 : 1)
            .ToList();
        if (entries.Count <= 7)
        {
            foreach (List<RequirementGenerationEntry> permutation in Permutations(entries.ToList(), 0))
                yield return permutation;
        }
    }

    private static IEnumerable<List<RequirementGenerationEntry>> Permutations(
        List<RequirementGenerationEntry> entries,
        int index)
    {
        if (index >= entries.Count)
        {
            yield return entries.ToList();
            yield break;
        }

        for (int i = index; i < entries.Count; i++)
        {
            Swap(entries, index, i);
            foreach (List<RequirementGenerationEntry> permutation in Permutations(entries, index + 1))
                yield return permutation;
            Swap(entries, index, i);
        }
    }

    private static void Swap<T>(IList<T> list, int left, int right)
    {
        if (left == right)
            return;
        T temp = list[left];
        list[left] = list[right];
        list[right] = temp;
    }

    private static bool IsNoSaltsEntry(RequirementGenerationEntry entry)
    {
        QuestRequirementInQuest requirement =
            QuestRequirementInQuest.GetByName(entry.PlannedRequirement.RequirementName, returnFirst: false, warning: false);
        return requirement?.requirement?.GetType() == typeof(QuestRequirementNoSalts);
    }

    private static bool AllGeneratedRequirementsValid(
        Quest quest,
        IReadOnlyList<GeneratedQuestRequirement> generatedRequirements,
        out string reason)
    {
        reason = string.Empty;
        for (int i = 0; i < generatedRequirements.Count; i++)
        {
            GeneratedQuestRequirement generated = generatedRequirements[i];
            QuestRequirement requirement = generated?.requirementInQuest?.requirement;
            if (requirement == null)
                continue;

            List<GeneratedQuestRequirement> others = generatedRequirements
                .Where((_, index) => index != i)
                .ToList();
            if (requirement.IsCompatibleWithOtherRequirements(others))
            {
                GeneratedQuestRequirement clone = CloneGeneratedRequirementWithFixedTarget(generated);
                if (requirement.UpdateGeneratedRequirement(quest, others, clone)
                    && GeneratedValuesMatch(generated, clone))
                {
                    continue;
                }
            }

            reason = $"Requirement conflict: {RequirementName(generated)} is incompatible with the selected group or target quest.";
            return false;
        }

        return true;
    }

    private static GeneratedQuestRequirement CloneGeneratedRequirementWithFixedTarget(
        GeneratedQuestRequirement source)
    {
        GeneratedQuestRequirement clone = CloneGeneratedRequirement(source);
        QuestRequirementInQuest wrapper = new QuestRequirementInQuest(source.requirementInQuest.requirement)
        {
            textKey = source.requirementInQuest.textKey,
            reactionKey = source.requirementInQuest.reactionKey,
            reactionCompletedPartiallyKey = source.requirementInQuest.reactionCompletedPartiallyKey,
            ingredient = source.requirementInQuest.ingredient,
            potionBase = source.requirementInQuest.potionBase,
        };

        if (source.requirementInQuest.requirement is QuestRequirementCertainIngredient)
        {
            Ingredient ingredient = Ingredient.GetByName(source.stringValue1, returnFirst: false, warning: false);
            if (ingredient != null)
                wrapper.ingredient = ingredient;
        }
        else if (source.requirementInQuest.requirement is QuestRequirementCertainBase)
        {
            PotionBase potionBase = PotionBase.GetByName(source.stringValue1, returnFirst: false, warning: false);
            if (potionBase != null)
                wrapper.potionBase = potionBase;
        }

        clone.requirementInQuest = wrapper;
        return clone;
    }

    private static bool GeneratedValuesMatch(
        GeneratedQuestRequirement expected,
        GeneratedQuestRequirement actual)
    {
        return expected.intValue1 == actual.intValue1
            && string.Equals(expected.stringValue1, actual.stringValue1, System.StringComparison.Ordinal);
    }

    private static List<GeneratedQuestRequirement> CloneGeneratedRequirements(
        IEnumerable<GeneratedQuestRequirement> generatedRequirements)
    {
        return generatedRequirements
            .Where(item => item != null)
            .Select(CloneGeneratedRequirement)
            .ToList();
    }

    private static GeneratedQuestRequirement CloneGeneratedRequirement(GeneratedQuestRequirement source)
    {
        return new GeneratedQuestRequirement(source.requirementInQuest)
        {
            intValue1 = source.intValue1,
            stringValue1 = source.stringValue1,
            textMale = source.textMale,
            textFemale = source.textFemale,
            reactionMale = source.reactionMale,
            reactionFemale = source.reactionFemale,
            reactionCompletedPartiallyMale = source.reactionCompletedPartiallyMale,
            reactionCompletedPartiallyFemale = source.reactionCompletedPartiallyFemale,
        };
    }

    private static string RequirementName(GeneratedQuestRequirement generated)
    {
        return generated?.requirementInQuest?.requirement?.name ?? "-";
    }

    private static bool HasMissingTarget(
        PlannedRequirement plannedRequirement,
        QuestRequirementInQuest source,
        QuestRequirementInQuest wrapper)
    {
        return !string.IsNullOrWhiteSpace(plannedRequirement.StringTargetName)
            && ((source.requirement is QuestRequirementCertainIngredient && wrapper.ingredient == null)
                || (source.requirement is QuestRequirementCertainBase && wrapper.potionBase == null));
    }

    private static NpcMonoBehaviour GetCurrentNpc(NpcManager npcManager)
    {
        return CurrentNpcProperty?.GetValue(npcManager, null) as NpcMonoBehaviour;
    }

    private static bool IsPendingCustomer(NpcMonoBehaviour npc)
    {
        return IsPlanCustomer(pendingCustomer, npc);
    }

    private static bool IsPlanCustomer(PlannedCustomer plan, NpcMonoBehaviour npc)
    {
        if (plan?.Customer?.Template == null || npc == null || npc.template != plan.Customer.Template)
            return false;
        if (plan.Customer.Source != CustomerCandidateSource.RegularFactionQuest)
            return true;
        return npc.faction == plan.Customer.Faction
            && npc.factionClass == plan.Customer.FactionClass;
    }

    private static bool IsReplaceableRegularCurrentNpc(NpcMonoBehaviour npc)
    {
        return npc != null
            && npc.faction != null
            && npc.factionClass != null;
    }

    private static Faction PendingFaction()
    {
        return pendingCustomer.Customer.Source == CustomerCandidateSource.RegularFactionQuest
            ? pendingCustomer.Customer.Faction
            : null;
    }

    private static FactionClass PendingFactionClass()
    {
        return pendingCustomer.Customer.Source == CustomerCandidateSource.RegularFactionQuest
            ? pendingCustomer.Customer.FactionClass
            : null;
    }

    private readonly struct RequirementGenerationEntry
    {
        public PlannedRequirement PlannedRequirement { get; }
        public bool IsMandatory { get; }

        public RequirementGenerationEntry(PlannedRequirement plannedRequirement, bool isMandatory)
        {
            PlannedRequirement = plannedRequirement;
            IsMandatory = isMandatory;
        }
    }

    private sealed class RequirementGenerationResult
    {
        public bool Success { get; }
        public List<GeneratedQuestRequirement> Mandatory { get; }
        public List<GeneratedQuestRequirement> Optional { get; }
        public string Reason { get; }

        private RequirementGenerationResult(
            bool success,
            List<GeneratedQuestRequirement> mandatory,
            List<GeneratedQuestRequirement> optional,
            string reason)
        {
            Success = success;
            Mandatory = mandatory;
            Optional = optional;
            Reason = reason;
        }

        public static RequirementGenerationResult Allowed(
            List<GeneratedQuestRequirement> mandatory,
            List<GeneratedQuestRequirement> optional)
        {
            return new RequirementGenerationResult(
                success: true,
                mandatory: mandatory,
                optional: optional,
                reason: string.Empty);
        }

        public static RequirementGenerationResult Blocked(string reason)
        {
            return new RequirementGenerationResult(
                success: false,
                mandatory: new List<GeneratedQuestRequirement>(),
                optional: new List<GeneratedQuestRequirement>(),
                reason: string.IsNullOrWhiteSpace(reason)
                    ? "The selected requirement group is incompatible."
                    : reason);
        }
    }

    public readonly struct ScheduleResult
    {
        public bool Success { get; }
        public bool AppliedImmediately { get; }
        public int RemovedAppliedAppointments { get; }
        public string Reason { get; }

        private ScheduleResult(
            bool success,
            bool appliedImmediately,
            int removedAppliedAppointments,
            string reason)
        {
            Success = success;
            AppliedImmediately = appliedImmediately;
            RemovedAppliedAppointments = removedAppliedAppointments;
            Reason = reason ?? string.Empty;
        }

        public static ScheduleResult AppliedNow(int removedAppliedAppointments)
        {
            return new ScheduleResult(true, true, removedAppliedAppointments, string.Empty);
        }

        public static ScheduleResult Queued(int removedAppliedAppointments, string reason)
        {
            return new ScheduleResult(true, false, removedAppliedAppointments, reason);
        }

        public static ScheduleResult Blocked(string reason)
        {
            return new ScheduleResult(false, false, 0, reason);
        }
    }
}
