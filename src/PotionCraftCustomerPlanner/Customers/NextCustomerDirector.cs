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
using PotionCraft.ObjectBased;
using PotionCraft.ObjectBased.UIElements.Dialogue;
using PotionCraft.ObjectBased.ScalesSystem;
using PotionCraft.QuestSystem;
using PotionCraft.ScriptableObjects;
using PotionCraft.ScriptableObjects.Ingredient;
using PotionCraft.Settings;
using PotionCraft.Settings.GameDifficultySettings;
using UnityEngine;
using PotionScriptable = PotionCraft.ScriptableObjects.Potion.Potion;

namespace PotionCraftCustomerPlanner;

internal static class NextCustomerDirector
{
    private static ManualLogSource logger;
    private static PlannedCustomer pendingCustomer;
    private static readonly List<PlannedCustomer> scheduledCustomers = new List<PlannedCustomer>();
    private static readonly HashSet<PlannedCustomer> appliedAppointments = new HashSet<PlannedCustomer>();
    private static NpcMonoBehaviour customerWithAppliedAppointment;
    private static NpcMonoBehaviour previewCustomerWithAppliedPlan;
    private static PreviewSnapshot activePreview;
    private static int delayedCurrentApplicationFrames = -1;
    private static readonly System.Reflection.PropertyInfo CurrentNpcProperty =
        AccessTools.Property(typeof(NpcManager), "CurrentNpcMonoBehaviour");
    private static readonly System.Reflection.PropertyInfo InventoryItemProperty =
        AccessTools.Property(typeof(ItemFromInventory), "InventoryItem");
    private static readonly FieldInfo QuestUseListMandatoryRequirementsField =
        typeof(Quest).GetField("useListMandatoryRequirements", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo QuestMandatoryRequirementsField =
        typeof(Quest).GetField("mandatoryRequirements", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo QuestUseListOptionalRequirementsField =
        typeof(Quest).GetField("useListOptionalRequirements", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo QuestOptionalRequirementsField =
        typeof(Quest).GetField("optionalRequirements", BindingFlags.Instance | BindingFlags.NonPublic);

    public static PlannedCustomer PendingCustomer => scheduledCustomers.FirstOrDefault()
        ?? pendingCustomer;

    public static int ScheduledCount => scheduledCustomers.Count;

    public static int PendingScheduledCount => scheduledCustomers.Count(plan => !appliedAppointments.Contains(plan));

    public static int AppliedPlaceholderCount => appliedAppointments.Count;

    public static bool HasPreview
    {
        get
        {
            DropPreviewIfInteracted();
            return activePreview != null;
        }
    }

    public static IReadOnlyList<ScheduledPlanSnapshot> ScheduledPlans =>
        scheduledCustomers
            .Select((plan, index) => new ScheduledPlanSnapshot(
                plan,
                index,
                appliedAppointments.Contains(plan)))
            .ToList();

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
        string reason = string.Empty;
        if (CanApplyPlanToCurrentCustomer(
                plan,
                allowRebuild: true,
                allowAlreadyAppliedAppointment: false,
                allowPreviewRebuildAfterDialogueRefresh: false,
                out _)
            && ApplyPlanToCurrentCustomer(
            plan,
            ApplyPlanMarker.Scheduled,
            allowRebuild: true,
            allowAlreadyAppliedAppointment: false,
            allowPreviewRebuildAfterDialogueRefresh: false,
            out reason))
        {
            ClearPreview();
            scheduledCustomers.Add(plan);
            appliedAppointments.Add(plan);
            logger?.LogInfo($"Scheduled plan applied immediately and marked until the next schedule add: {plan.Customer.DisplayName}; quest={plan.TargetQuest?.name ?? "-"}");
            return ScheduleResult.AppliedNow(removedAppliedAppointments);
        }

        if (string.IsNullOrEmpty(reason))
            reason = "The current customer cannot be modified now.";
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
        previewCustomerWithAppliedPlan = null;
        activePreview = null;
        delayedCurrentApplicationFrames = -1;
    }

    public static void ResetTransientStateForSaveLoad(string phase)
    {
        if (scheduledCustomers.Count == 0
            && appliedAppointments.Count == 0
            && pendingCustomer == null
            && customerWithAppliedAppointment == null
            && previewCustomerWithAppliedPlan == null
            && activePreview == null
            && delayedCurrentApplicationFrames < 0)
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
        return ApplyPlanToCurrentCustomer(
            plan,
            ApplyPlanMarker.Scheduled,
            allowRebuild,
            allowAlreadyAppliedAppointment: false,
            allowPreviewRebuildAfterDialogueRefresh: false,
            out reason);
    }

    public static bool PreviewCurrentCustomer(PlannedCustomer plan, out string reason)
    {
        DropPreviewIfInteracted();
        if (!CanPreviewCurrentCustomer(plan, out reason))
            return false;

        NpcMonoBehaviour current = GetCurrentNpc(Managers.Npc);
        bool previewForCurrent = activePreview?.Npc == current
            || previewCustomerWithAppliedPlan == current;
        PreviewSnapshot previousPreview = activePreview;
        if (!previewForCurrent)
        {
            if (!TryCreatePreviewSnapshot(current, out activePreview, out reason))
                return false;
        }

        if (ApplyPlanToCurrentCustomer(
            plan,
            ApplyPlanMarker.Preview,
            allowRebuild: true,
            allowAlreadyAppliedAppointment: previewForCurrent,
            allowPreviewRebuildAfterDialogueRefresh: previewForCurrent,
            out reason))
        {
            logger?.LogInfo($"Preview applied to current customer: {plan.Customer.DisplayName}; quest={plan.TargetQuest?.name ?? "-"}");
            return true;
        }

        if (!previewForCurrent)
            activePreview = previousPreview;
        return false;
    }

    public static bool CanPreviewCurrentCustomer(PlannedCustomer plan, out string reason)
    {
        DropPreviewIfInteracted();
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

        NpcMonoBehaviour current = GetCurrentNpc(Managers.Npc);
        if (current == null)
        {
            reason = "There is no current customer.";
            return false;
        }

        bool previewForCurrent = activePreview?.Npc == current
            || previewCustomerWithAppliedPlan == current;
        if (current == customerWithAppliedAppointment && !previewForCurrent)
        {
            reason = "The current customer has already been modified by a scheduled plan.";
            return false;
        }

        if (previewForCurrent)
            return true;
        if (IsPlanCustomer(plan, current))
            return true;
        return CanRebuildCurrentNpcForPlan(current, plan, out reason);
    }

    public static bool CanEditCurrentCustomer(out string reason)
    {
        DropPreviewIfInteracted();
        reason = string.Empty;
        if (Managers.Npc == null)
        {
            reason = "NPC manager is not ready.";
            return false;
        }

        NpcMonoBehaviour current = GetCurrentNpc(Managers.Npc);
        if (current == null)
        {
            reason = "There is no current customer.";
            return false;
        }
        if (current == customerWithAppliedAppointment)
        {
            reason = "The current customer has already been modified by a scheduled plan.";
            return false;
        }
        if (IsMerchantNpc(current))
        {
            reason = "The current NPC is a trader/merchant.";
            return false;
        }
        if (activePreview?.Npc == current || previewCustomerWithAppliedPlan == current)
            return true;
        if (!IsReplaceableRegularCurrentNpc(current))
        {
            reason = "The current NPC is not a regular faction/class customer.";
            return false;
        }
        return true;
    }

    public static bool CanPreviewEditCurrentCustomer(out string reason)
    {
        DropPreviewIfInteracted();
        reason = string.Empty;
        if (Managers.Npc == null)
        {
            reason = "NPC manager is not ready.";
            return false;
        }

        NpcMonoBehaviour current = GetCurrentNpc(Managers.Npc);
        if (current == null)
        {
            reason = "There is no current customer.";
            return false;
        }
        if (current == customerWithAppliedAppointment)
        {
            reason = "The current customer has already been modified by a scheduled plan.";
            return false;
        }
        if (IsMerchantNpc(current))
        {
            reason = "The current NPC is a trader/merchant.";
            return false;
        }
        if (!IsReplaceableRegularCurrentNpc(current))
        {
            reason = "The current NPC is not a regular faction/class customer.";
            return false;
        }
        return true;
    }

    public static bool RevertPreview(out string reason)
    {
        reason = string.Empty;
        if (activePreview == null)
        {
            reason = "There is no active preview to revert.";
            return false;
        }
        if (Managers.Npc == null)
        {
            reason = "NPC manager is not ready.";
            return false;
        }

        NpcMonoBehaviour current = GetCurrentNpc(Managers.Npc);
        if (current == null)
        {
            reason = "There is no current customer.";
            return false;
        }
        if (current != activePreview.Npc)
        {
            ClearPreview();
            reason = "The previewed customer is no longer current.";
            return false;
        }
        if (current == customerWithAppliedAppointment)
        {
            ClearPreview();
            reason = "The preview has already been committed by a scheduled plan.";
            return false;
        }

        PreviewSnapshot snapshot = activePreview;
        PlannedCustomer previousPendingCustomer = pendingCustomer;
        pendingCustomer = new PlannedCustomer(
            snapshot.Customer,
            snapshot.Quest,
            snapshot.ChapterOnAddToSpawn,
            strictPlanningMode: false,
            mandatoryRequirements: new List<PlannedRequirement>(),
            optionalRequirements: new List<PlannedRequirement>());
        try
        {
            if (!IsPlanCustomer(pendingCustomer, current) && !TryRebuildCurrentNpc(Managers.Npc, current))
            {
                reason = "Failed to rebuild the original preview customer.";
                return false;
            }

            current.currentQuest = snapshot.Quest;
            current.mandatoryQuestRequirements = CloneGeneratedRequirements(snapshot.MandatoryRequirements);
            current.optionalQuestRequirements = CloneGeneratedRequirements(snapshot.OptionalRequirements);
            current.chapterOnAddToSpawn = snapshot.ChapterOnAddToSpawn;
            ClearPreview();
            logger?.LogInfo($"Preview reverted: {snapshot.Customer.DisplayName}; quest={snapshot.Quest?.name ?? "-"}");
            RefreshDialogueBox();
            return true;
        }
        finally
        {
            RestorePendingState(previousPendingCustomer);
        }
    }

    private static bool ApplyPlanToCurrentCustomer(
        PlannedCustomer plan,
        ApplyPlanMarker marker,
        bool allowRebuild,
        bool allowAlreadyAppliedAppointment,
        bool allowPreviewRebuildAfterDialogueRefresh,
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
        if (current == customerWithAppliedAppointment && !allowAlreadyAppliedAppointment)
        {
            reason = "The current customer has already received a scheduled plan.";
            RestorePendingState(previousPendingCustomer);
            return false;
        }

        if (IsPendingCustomer(current))
        {
            ApplyPlanToNpc(current, marker);
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

        if (!CanRebuildCurrentNpcForPlan(current, plan, out reason)
            && !allowPreviewRebuildAfterDialogueRefresh)
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

        ApplyPlanToNpc(current, marker);
        logger?.LogInfo($"Rebuilt current customer: {plan.Customer.DisplayName}; quest={current.currentQuest?.name ?? "-"}");
        RefreshDialogueBox();
        RestorePendingState(previousPendingCustomer);
        return true;
    }

    private static bool CanApplyPlanToCurrentCustomer(
        PlannedCustomer plan,
        bool allowRebuild,
        bool allowAlreadyAppliedAppointment,
        bool allowPreviewRebuildAfterDialogueRefresh,
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

        NpcMonoBehaviour current = GetCurrentNpc(Managers.Npc);
        if (current == null)
        {
            reason = "There is no current customer.";
            return false;
        }
        if (current == customerWithAppliedAppointment && !allowAlreadyAppliedAppointment)
        {
            reason = "The current customer has already received a scheduled plan.";
            return false;
        }
        if (IsMerchantNpc(current))
        {
            reason = "The current NPC is a trader/merchant.";
            return false;
        }
        if (IsPlanCustomer(plan, current))
            return true;
        if (!allowRebuild)
        {
            reason = "The current customer does not match the selected plan.";
            return false;
        }
        if (CanRebuildCurrentNpcForPlan(current, plan, out reason))
            return true;
        return allowPreviewRebuildAfterDialogueRefresh;
    }

    private static void RestorePendingState(PlannedCustomer customer)
    {
        pendingCustomer = customer;
    }

    public static void ReplaceCurrentNpcIfNeeded(NpcManager npcManager)
    {
        if (scheduledCustomers.Count == 0 && customerWithAppliedAppointment == null)
            return;

        if (TryApplyScheduledPlanToCurrent(npcManager, refreshAfterApply: false))
            return;

        delayedCurrentApplicationFrames = 2;
    }

    public static void Update()
    {
        DropPreviewIfInteracted();

        if (delayedCurrentApplicationFrames < 0)
            return;
        if (delayedCurrentApplicationFrames > 0)
        {
            delayedCurrentApplicationFrames--;
            return;
        }

        delayedCurrentApplicationFrames = -1;
        TryApplyScheduledPlanToCurrent(Managers.Npc, refreshAfterApply: true);
    }

    private static bool TryApplyScheduledPlanToCurrent(NpcManager npcManager, bool refreshAfterApply)
    {
        if (npcManager == null)
            return false;

        NpcMonoBehaviour current = GetCurrentNpc(npcManager);
        if (current == null)
            return false;

        if (current == customerWithAppliedAppointment)
        {
            ClearPreview();
            logger?.LogInfo($"Scheduled customer reached the counter: {current.currentQuest?.name ?? "-"}");
            customerWithAppliedAppointment = null;
            return true;
        }

        if (scheduledCustomers.Count == 0)
            return false;

        PlannedCustomer naturalPlan = FirstNaturalPlanForNpc(current);
        if (naturalPlan != null)
        {
            pendingCustomer = naturalPlan;
            ApplyPlanToNpc(current, ApplyPlanMarker.Scheduled);
            ClearPreview();
            logger?.LogInfo($"Applied scheduled current customer: {naturalPlan.Customer.DisplayName}; quest={current.currentQuest?.name ?? "-"}");
            if (refreshAfterApply)
                RefreshDialogueBox();
            CompleteScheduledPlan(naturalPlan);
            return true;
        }

        PlannedCustomer plan = FirstCurrentRebuildPlan(current, npcManager);
        if (plan == null)
            return false;
        pendingCustomer = plan;

        if (!TryRebuildCurrentNpc(npcManager, current))
            return false;

        ApplyPlanToNpc(current, ApplyPlanMarker.Scheduled);
        ClearPreview();
        logger?.LogInfo($"Rebuilt current NPC as scheduled customer: {plan.Customer.DisplayName}; quest={current.currentQuest?.name ?? "-"}");
        if (refreshAfterApply)
            RefreshDialogueBox();
        CompleteScheduledPlan(plan);
        return true;
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

    public static bool TryGenerateRandomRequirements(
        Quest targetQuest,
        int chapter,
        RandomRequirementRule randomRule,
        out IReadOnlyList<GeneratedQuestRequirement> mandatoryRequirements,
        out IReadOnlyList<GeneratedQuestRequirement> optionalRequirements,
        out string reason)
    {
        mandatoryRequirements = new List<GeneratedQuestRequirement>();
        optionalRequirements = new List<GeneratedQuestRequirement>();
        reason = string.Empty;

        if (targetQuest == null)
        {
            reason = "No target quest selected.";
            return false;
        }

        GameDifficultyQuestRequirements settingsAsset = Settings<GameDifficultyQuestRequirements>.Asset;
        QuestRequirementDifficultySettings settings = settingsAsset?.GetCurrentValue();
        if (settings == null)
        {
            reason = "Quest requirement difficulty settings are not ready.";
            return false;
        }
        if (QuestUseListMandatoryRequirementsField == null
            || QuestMandatoryRequirementsField == null
            || QuestUseListOptionalRequirementsField == null
            || QuestOptionalRequirementsField == null)
        {
            reason = "Quest requirement list fields could not be read.";
            return false;
        }

        randomRule ??= RandomRequirementRule.Default;

        bool useMandatoryList = QuestUsesRequirementList(targetQuest, isMandatoryRequirements: true);
        bool useOptionalList = QuestUsesRequirementList(targetQuest, isMandatoryRequirements: false);
        List<QuestRequirementInQuest> allCandidates =
            useMandatoryList && useOptionalList
                ? new List<QuestRequirementInQuest>()
                : QuestRequirementInQuest.allRequirements
                    .Where(requirement => requirement?.requirement != null
                        && chapter >= requirement.requirement.GetChapterToUnlock())
                    .ToList();

        QuestRequirementsModeConversionType mode = settings.GetModeConversionType();
        List<GeneratedQuestRequirement> allGenerated = new List<GeneratedQuestRequirement>();
        List<GeneratedQuestRequirement> nativeMandatory;
        List<GeneratedQuestRequirement> nativeOptional;

        if (mode == QuestRequirementsModeConversionType.ConvertAllToMandatory)
        {
            nativeMandatory = GenerateNativeRequirementSide(
                targetQuest,
                settings,
                randomRule,
                chapter,
                isMandatoryRequirements: true,
                isMandatoryRequirementTexts: true,
                allCandidates,
                allGenerated);
            nativeMandatory.AddRange(GenerateNativeRequirementSide(
                targetQuest,
                settings,
                randomRule,
                chapter,
                isMandatoryRequirements: false,
                isMandatoryRequirementTexts: true,
                allCandidates,
                allGenerated));
            nativeOptional = new List<GeneratedQuestRequirement>();
        }
        else if (mode == QuestRequirementsModeConversionType.ConvertAllToOptional)
        {
            nativeMandatory = new List<GeneratedQuestRequirement>();
            nativeOptional = GenerateNativeRequirementSide(
                targetQuest,
                settings,
                randomRule,
                chapter,
                isMandatoryRequirements: true,
                isMandatoryRequirementTexts: false,
                allCandidates,
                allGenerated);
            nativeOptional.AddRange(GenerateNativeRequirementSide(
                targetQuest,
                settings,
                randomRule,
                chapter,
                isMandatoryRequirements: false,
                isMandatoryRequirementTexts: false,
                allCandidates,
                allGenerated));
        }
        else
        {
            nativeMandatory = GenerateNativeRequirementSide(
                targetQuest,
                settings,
                randomRule,
                chapter,
                isMandatoryRequirements: true,
                isMandatoryRequirementTexts: true,
                allCandidates,
                allGenerated);
            nativeOptional = GenerateNativeRequirementSide(
                targetQuest,
                settings,
                randomRule,
                chapter,
                isMandatoryRequirements: false,
                isMandatoryRequirementTexts: false,
                allCandidates,
                allGenerated);
        }

        List<GeneratedQuestRequirement> combined = nativeMandatory.Concat(nativeOptional).ToList();
        if (!AllGeneratedRequirementsValid(targetQuest, combined, out reason))
            return false;

        mandatoryRequirements = CloneGeneratedRequirements(nativeMandatory);
        optionalRequirements = CloneGeneratedRequirements(nativeOptional);
        return true;
    }

#if DEBUG
    public static IReadOnlyList<string> DescribeRequirementGroupDryRun(
        Quest targetQuest,
        IReadOnlyList<PlannedRequirement> mandatoryRequirements,
        IReadOnlyList<PlannedRequirement> optionalRequirements)
    {
        List<string> lines = new List<string>
        {
            "Requirement group dry-run diagnostics:",
            $"targetQuest={targetQuest?.name ?? "-"}",
            $"mandatory=[{string.Join(", ", (mandatoryRequirements ?? System.Array.Empty<PlannedRequirement>()).Select(PlannedRequirementText).ToArray())}]",
            $"optional=[{string.Join(", ", (optionalRequirements ?? System.Array.Empty<PlannedRequirement>()).Select(PlannedRequirementText).ToArray())}]",
        };

        if (targetQuest == null)
        {
            lines.Add("- blocked: No target quest selected.");
            return lines;
        }

        List<RequirementGenerationEntry> entries = new List<RequirementGenerationEntry>();
        entries.AddRange((mandatoryRequirements ?? System.Array.Empty<PlannedRequirement>()).Select(requirement =>
            new RequirementGenerationEntry(requirement, isMandatory: true)));
        entries.AddRange((optionalRequirements ?? System.Array.Empty<PlannedRequirement>()).Select(requirement =>
            new RequirementGenerationEntry(requirement, isMandatory: false)));

        if (!PreflightValidateRequirements(entries, out string preflightReason))
        {
            lines.Add($"- preflight blocked: {preflightReason}");
            return lines;
        }

        int orderIndex = 0;
        Random.State randomState = Random.state;
        try
        {
            foreach (List<RequirementGenerationEntry> order in CandidateOrders(entries))
            {
                orderIndex++;
                lines.Add($"- order {orderIndex}: [{string.Join(" -> ", order.Select(EntryText).ToArray())}]");
                RequirementGenerationResult result = TryGenerateRequirementOrderWithDiagnostics(
                    targetQuest,
                    order,
                    preservedMandatory: null,
                    preservedOptional: null,
                    lines);
                lines.Add(result.Success
                    ? $"  order {orderIndex} allowed"
                    : $"  order {orderIndex} blocked: {result.Reason}");
                if (result.Success)
                    break;
            }
        }
        finally
        {
            Random.state = randomState;
        }

        return lines;
    }

    private static RequirementGenerationResult TryGenerateRequirementOrderWithDiagnostics(
        Quest quest,
        IReadOnlyList<RequirementGenerationEntry> order,
        IReadOnlyList<GeneratedQuestRequirement> preservedMandatory,
        IReadOnlyList<GeneratedQuestRequirement> preservedOptional,
        List<string> lines)
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
            bool generatedOk = TryGenerateRequirement(
                quest,
                entry.PlannedRequirement,
                allGenerated,
                entry.IsMandatory,
                out GeneratedQuestRequirement generated,
                out string reason);
            lines.Add(generatedOk
                ? $"  generated {EntryText(entry)} => {GeneratedRequirementText(generated)}"
                : $"  failed {EntryText(entry)}: {reason}");
            if (!generatedOk)
                return RequirementGenerationResult.Blocked(reason);

            if (entry.IsMandatory)
                mandatory.Add(generated);
            else
                optional.Add(generated);
            allGenerated.Add(generated);
        }

        if (!AllGeneratedRequirementsValidWithDiagnostics(quest, allGenerated, lines, out string conflictReason))
            return RequirementGenerationResult.Blocked(conflictReason);

        return RequirementGenerationResult.Allowed(mandatory, optional);
    }

    private static bool AllGeneratedRequirementsValidWithDiagnostics(
        Quest quest,
        IReadOnlyList<GeneratedQuestRequirement> generatedRequirements,
        List<string> lines,
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
            bool compatible = requirement.IsCompatibleWithOtherRequirements(others);
            GeneratedQuestRequirement clone = CloneGeneratedRequirementWithFixedTarget(generated);
            bool updated = compatible && requirement.UpdateGeneratedRequirement(quest, others, clone);
            bool matches = updated && GeneratedValuesMatch(generated, clone);
            lines.Add(
                $"  validate {GeneratedRequirementText(generated)}: compatible={compatible}, "
                + $"updated={updated}, matches={matches}, clone={GeneratedRequirementText(clone)}");
            if (compatible && updated && matches)
                continue;

            reason = $"Requirement conflict: {RequirementName(generated)} is incompatible with the selected group or target quest.";
            return false;
        }

        return true;
    }

    private static string EntryText(RequirementGenerationEntry entry)
    {
        return $"{(entry.IsMandatory ? "Must" : "Can")}:{PlannedRequirementText(entry.PlannedRequirement)}";
    }

    private static string PlannedRequirementText(PlannedRequirement requirement)
    {
        if (requirement == null)
            return "-";
        string target = string.IsNullOrWhiteSpace(requirement.StringTargetName)
            ? string.Empty
            : $" target={requirement.StringTargetName}";
        string intTarget = requirement.IntTarget.HasValue
            ? $" int={requirement.IntTarget.Value}"
            : string.Empty;
        return $"{requirement.RequirementName}{target}{intTarget}";
    }

    private static string GeneratedRequirementText(GeneratedQuestRequirement generated)
    {
        if (generated == null)
            return "-";
        return $"{RequirementName(generated)} string={generated.stringValue1 ?? "-"} int={generated.intValue1}";
    }
#endif

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
        return count;
    }

    private static void CompleteScheduledPlan(PlannedCustomer plan)
    {
        scheduledCustomers.Remove(plan);
        appliedAppointments.Remove(plan);
        pendingCustomer = null;
    }

    private static void ApplyPlanToNpc(NpcMonoBehaviour npc, ApplyPlanMarker marker)
    {
        ApplyTargetQuest(npc);
        ApplySelectedRequirements(npc);
        if (marker == ApplyPlanMarker.Scheduled)
        {
            customerWithAppliedAppointment = npc;
            previewCustomerWithAppliedPlan = null;
        }
        else if (marker == ApplyPlanMarker.Preview)
        {
            previewCustomerWithAppliedPlan = npc;
        }
    }

    private static bool TryCreatePreviewSnapshot(
        NpcMonoBehaviour npc,
        out PreviewSnapshot snapshot,
        out string reason)
    {
        snapshot = null;
        if (!RegularCustomerPool.TryCreateOptionFromNpc(npc, out RegularCustomerOption customer, out reason))
            return false;
        if (npc.currentQuest == null)
        {
            reason = "The current customer has no quest to preview.";
            return false;
        }

        snapshot = new PreviewSnapshot(
            npc,
            customer,
            npc.currentQuest,
            npc.chapterOnAddToSpawn,
            CloneGeneratedRequirements(npc.mandatoryQuestRequirements ?? new List<GeneratedQuestRequirement>()),
            CloneGeneratedRequirements(npc.optionalQuestRequirements ?? new List<GeneratedQuestRequirement>()));
        reason = string.Empty;
        return true;
    }

    private static void ClearPreview()
    {
        activePreview = null;
        previewCustomerWithAppliedPlan = null;
    }

    private static void DropPreviewIfInteracted()
    {
        if (activePreview == null)
            return;

        NpcMonoBehaviour current = GetCurrentNpc(Managers.Npc);
        if (current != activePreview.Npc)
        {
            ClearPreview();
            return;
        }

        if (HasPreviewInteractionStarted(current))
        {
            logger?.LogInfo($"Preview became interactive and can no longer be reverted: {current.currentQuest?.name ?? "-"}");
            ClearPreview();
        }
    }

    private static bool HasPreviewInteractionStarted(NpcMonoBehaviour npc)
    {
        if (npc?.trading == null)
            return false;

        if (npc.trading.isPotionSold
            || npc.trading.potionItem != null
            || npc.trading.IsHaggleCanceled
            || !npc.trading.IsHagglePerfect
            || npc.trading.PotionOfferedTimesCount > 0)
        {
            return true;
        }

        DialogueState state = Managers.Dialogue?.State ?? DialogueState.NoDialogue;
        return state == DialogueState.Haggle || state == DialogueState.Trading;
    }

    private static void RefreshDialogueBox()
    {
        ReturnScalesPotionToInventoryBeforeRefresh();

        if (DialogueBox.Instance == null || Managers.Dialogue == null)
        {
            RefreshPotionOnScales();
            return;
        }

        DialogueState state = Managers.Dialogue.State;
        NpcMonoBehaviour current = GetCurrentNpc(Managers.Npc);
        bool canRebuildPotionRequestInterface =
            current?.currentQuest != null
            && (state == DialogueState.PotionRequest
                || state == DialogueState.ClosenessPotionRequest);

        if (canRebuildPotionRequestInterface)
            RebuildCurrentDialogueInterface(state);

        RefreshPotionOnScales();
    }

    private static void ReturnScalesPotionToInventoryBeforeRefresh()
    {
        Scales scales = Scales.Instance;
        ScalesCupDisplay display = scales?.rightCupScript?.display;
        ItemFromInventory item = display?.currentPotionItem;
        if (item == null)
            return;

        try
        {
            if (item.TryToPutInInventory(
                    spawnCollectedItemText: false,
                    spawnCollectedItemTextNearCursor: false))
            {
                logger?.LogInfo("Returned potion from scales to inventory before refreshing customer dialogue.");
            }
        }
        catch (System.Exception ex)
        {
            logger?.LogWarning($"Failed to return potion from scales before dialogue refresh: {ex.Message}");
        }
    }

    private static void RebuildCurrentDialogueInterface(DialogueState state)
    {
        bool previousInstant = Managers.Dialogue.changeStateInstantly;
        try
        {
            Managers.Dialogue.changeStateInstantly = true;
            DialogueBox.Instance.UpdateBoxState(state);
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
            DialogueBox.Instance?.UpdateTradeButtons();
            return;
        }

        RecalculatePotionSuitabilitySilently(scales, display);

        Managers.Trade?.RecalculateDealCost();
        DialogueBox.Instance?.UpdateTradeButtons();
        if (Managers.Dialogue != null
            && (Managers.Dialogue.State == DialogueState.PotionRequest
                || Managers.Dialogue.State == DialogueState.ClosenessPotionRequest))
        {
            DialogueBox.Instance?.UpdatePotionRequestText(1f);
        }
    }

    private static void RecalculatePotionSuitabilitySilently(Scales scales, ScalesCupDisplay display)
    {
        if (scales == null || display?.currentPotionItem == null)
            return;

        NpcMonoBehaviour npc = GetCurrentNpc(Managers.Npc);
        Quest quest = npc?.currentQuest;
        PotionScriptable potion = InventoryItemProperty?.GetValue(display.currentPotionItem, null) as PotionScriptable;
        if (npc == null || quest == null || potion == null)
            return;

        bool suitable = PotionScriptable.GetPotionReview(potion.Effects, quest.desiredEffects).maxTier > 0
            && GeneratedQuestRequirement.AreRequirementsCompleted(
                potion,
                quest,
                npc.mandatoryQuestRequirements);

        display.isCurrentPotionSuitable = suitable;
        scales.isWrongPotionOnTheScales = !suitable;
        scales.TargetAngle = suitable ? 0f : scales.maxAngleStringsStretched;
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
        if (IsMerchantNpc(current))
        {
            reason = "The current NPC is a trader/merchant.";
            return false;
        }
        if (!IsReplaceableRegularCurrentNpc(current))
        {
            reason = "The current NPC is not a regular faction/class customer.";
            return false;
        }

        if (!plan.StrictPlanningMode)
        {
            if (IsCurrentNpcAnyScheduledCustomer(current, exceptPlan: plan))
            {
                reason = "The current customer matches another scheduled plan.";
                return false;
            }
            return true;
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

    private static List<GeneratedQuestRequirement> GenerateNativeRequirementSide(
        Quest quest,
        QuestRequirementDifficultySettings settings,
        RandomRequirementRule randomRule,
        int chapter,
        bool isMandatoryRequirements,
        bool isMandatoryRequirementTexts,
        List<QuestRequirementInQuest> allCandidates,
        List<GeneratedQuestRequirement> allGenerated)
    {
        List<GeneratedQuestRequirement> result = new List<GeneratedQuestRequirement>();
        if (QuestUsesRequirementList(quest, isMandatoryRequirements))
        {
            foreach (QuestRequirementInQuest requirement in QuestRequirementList(quest, isMandatoryRequirements))
            {
                GeneratedQuestRequirement generated = requirement?.GetGeneratedRequirement(
                    quest,
                    allGenerated,
                    isMandatoryRequirementTexts);
                if (generated == null)
                    continue;

                result.Add(generated);
                allGenerated.Add(generated);
            }

            return result;
        }

        if (Managers.Tutorial != null && Managers.Tutorial.IsTutorialActive())
            return result;

        (int firstChancePercent, int secondChancePercent) =
            randomRule.GetSpawnChances(settings, chapter, isMandatoryRequirements);
        if (UnityEngine.Random.value >= firstChancePercent / 100f)
            return result;

        TryAddRandomNativeRequirement(quest, randomRule, allCandidates, allGenerated, result, isMandatoryRequirementTexts);
        if (UnityEngine.Random.value < secondChancePercent / 100f)
            TryAddRandomNativeRequirement(quest, randomRule, allCandidates, allGenerated, result, isMandatoryRequirementTexts);
        return result;
    }

    private static void TryAddRandomNativeRequirement(
        Quest quest,
        RandomRequirementRule randomRule,
        List<QuestRequirementInQuest> candidates,
        List<GeneratedQuestRequirement> allGenerated,
        List<GeneratedQuestRequirement> result,
        bool isMandatoryRequirementTexts)
    {
        int candidateIndex = -2;
        while (candidateIndex == -2)
        {
            candidateIndex = RandomIndexByWeights(
                candidates
                    .Select(candidate => randomRule.GetWeight(candidate))
                    .ToList());
            if (candidateIndex == -1)
                return;

            QuestRequirementInQuest candidate = candidates[candidateIndex];
            GeneratedQuestRequirement generated = candidate.GetGeneratedRequirement(
                quest,
                allGenerated,
                isMandatoryRequirementTexts);
            candidates.RemoveAt(candidateIndex);
            if (generated == null)
            {
                candidateIndex = -2;
                continue;
            }

            result.Add(generated);
            allGenerated.Add(generated);
        }
    }

    private static int RandomIndexByWeights(IReadOnlyList<float> weights)
    {
        if (weights == null || weights.Count == 0)
            return -1;

        float total = weights.Where(weight => weight > 0f).Sum();
        if (total <= 0f)
            return -1;

        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < weights.Count; i++)
        {
            float weight = Mathf.Max(0f, weights[i]);
            if (weight <= 0f)
                continue;
            if (roll < weight)
                return i;
            roll -= weight;
        }

        return weights.Count - 1;
    }

    private static bool QuestUsesRequirementList(Quest quest, bool isMandatoryRequirements)
    {
        FieldInfo field = isMandatoryRequirements
            ? QuestUseListMandatoryRequirementsField
            : QuestUseListOptionalRequirementsField;
        return field != null && field.GetValue(quest) is bool value && value;
    }

    private static List<QuestRequirementInQuest> QuestRequirementList(Quest quest, bool isMandatoryRequirements)
    {
        FieldInfo field = isMandatoryRequirements
            ? QuestMandatoryRequirementsField
            : QuestOptionalRequirementsField;
        return field?.GetValue(quest) as List<QuestRequirementInQuest>
            ?? new List<QuestRequirementInQuest>();
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

    private sealed class PreviewSnapshot
    {
        public NpcMonoBehaviour Npc { get; }
        public RegularCustomerOption Customer { get; }
        public Quest Quest { get; }
        public int ChapterOnAddToSpawn { get; }
        public List<GeneratedQuestRequirement> MandatoryRequirements { get; }
        public List<GeneratedQuestRequirement> OptionalRequirements { get; }

        public PreviewSnapshot(
            NpcMonoBehaviour npc,
            RegularCustomerOption customer,
            Quest quest,
            int chapterOnAddToSpawn,
            List<GeneratedQuestRequirement> mandatoryRequirements,
            List<GeneratedQuestRequirement> optionalRequirements)
        {
            Npc = npc;
            Customer = customer;
            Quest = quest;
            ChapterOnAddToSpawn = chapterOnAddToSpawn;
            MandatoryRequirements = mandatoryRequirements;
            OptionalRequirements = optionalRequirements;
        }
    }

    private enum ApplyPlanMarker
    {
        None,
        Preview,
        Scheduled,
    }

    public sealed class RandomRequirementRule
    {
        public bool OverrideMandatorySpawnChances { get; set; }
        public int MandatoryFirstChance { get; set; }
        public int MandatorySecondChance { get; set; }
        public bool OverrideOptionalSpawnChances { get; set; }
        public int OptionalFirstChance { get; set; }
        public int OptionalSecondChance { get; set; }
        public Dictionary<string, float> RequirementWeightMultipliers { get; } =
            new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);

        public static RandomRequirementRule Default => new RandomRequirementRule();

        public (int, int) GetSpawnChances(
            QuestRequirementDifficultySettings settings,
            int chapter,
            bool isMandatoryRequirements)
        {
            (int first, int second) native = settings.GetSpawnChances(chapter, isMandatoryRequirements);
            if (isMandatoryRequirements)
            {
                return OverrideMandatorySpawnChances
                    ? (ClampPercent(MandatoryFirstChance), ClampPercent(MandatorySecondChance))
                    : native;
            }

            return OverrideOptionalSpawnChances
                ? (ClampPercent(OptionalFirstChance), ClampPercent(OptionalSecondChance))
                : native;
        }

        public float GetWeight(QuestRequirementInQuest requirement)
        {
            QuestRequirement source = requirement?.requirement;
            if (source == null)
                return 0f;

            float weight = Mathf.Max(0f, source.spawnChance);
            if (RequirementWeightMultipliers.TryGetValue(source.name, out float multiplier))
                weight *= Mathf.Max(0f, multiplier);
            return weight;
        }

        private static int ClampPercent(int value)
        {
            return Mathf.Clamp(value, 0, 100);
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

    public readonly struct ScheduledPlanSnapshot
    {
        public PlannedCustomer Plan { get; }
        public int Index { get; }
        public bool Applied { get; }

        public ScheduledPlanSnapshot(PlannedCustomer plan, int index, bool applied)
        {
            Plan = plan;
            Index = index;
            Applied = applied;
        }
    }
}
