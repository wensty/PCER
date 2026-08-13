using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using PotionCraft.FactionSystem;
using PotionCraft.ManagersSystem;
using PotionCraft.ManagersSystem.RecipeMap;
using PotionCraft.ObjectBased.ElementSystem;
using PotionCraft.QuestSystem;
using PotionCraft.ScriptableObjects;
using PotionCraft.ScriptableObjects.Ingredient;
using PotionCraft.Settings;
using PotionCraft.Settings.GameDifficultySettings;
using UnityEngine;

namespace PotionCraftCustomerPlanner;

internal static class NextCustomerWindow
{
    private static ManualLogSource logger;
    private static ConfigEntry<bool> startVisible;
    private static ConfigEntry<KeyboardShortcut> toggleShortcut;
    private static ConfigEntry<bool> useChapterOverride;
    private static ConfigEntry<int> previewChapter;
    private static ConfigEntry<bool> useKarmaOverride;
    private static ConfigEntry<int> previewKarma;
    private static ConfigEntry<bool> strictPlanningMode;
    private static ConfigEntry<float> tinyFactionSpawnChanceThreshold;
    private static ConfigEntry<int> uiFontSize;
    private static ConfigEntry<int> pickerFontSize;
    private static ConfigEntry<string> uiFontNames;
    private static ConfigEntry<bool> blockGameInputWhenOpen;
    private static ConfigEntry<TargetFilledSelectionMode> targetFilledSelection;
    private static ConfigEntry<string> noneButtonColor;
    private static ConfigEntry<string> mustButtonColor;
    private static ConfigEntry<string> canButtonColor;
    private static ConfigEntry<string> customerSelectedColor;

    private static bool visible;
    private static Rect windowRect = new Rect(60f, 60f, 1720f, 860f);
    private static bool randomRuleWindowVisible;
    private static Rect randomRuleWindowRect = new Rect(120f, 120f, 860f, 720f);
    private static Vector2 customersScroll;
    private static Vector2 detailsScroll;
    private static Vector2 requirementsScroll;
    private static Vector2 targetPickerScroll;
    private static Vector2 randomRuleScroll;
    private static GUISkin windowSkin;
    private static int windowSkinFontSize;
    private static string windowSkinFontKey = string.Empty;
    private static Font windowFont;
    private static string windowFontKey = string.Empty;
    private static Texture2D windowBackgroundTexture;
    private static Texture2D boxBackgroundTexture;
    private static Texture2D borderTexture;
    private static readonly Dictionary<string, Texture2D> ColorTextures =
        new Dictionary<string, Texture2D>(StringComparer.Ordinal);
    private static readonly Dictionary<string, GUIStyle> ColoredButtonStyles =
        new Dictionary<string, GUIStyle>(StringComparer.Ordinal);
    private static GUIStyle pickerButtonStyle;
    private static int pickerButtonStyleFontSize;
    private static string pickerButtonStyleFontKey = string.Empty;
    private static GUIStyle windowTitleStyle;
    private static int windowTitleStyleFontSize;
    private static string windowTitleStyleFontKey = string.Empty;
    private static GUIStyle noWrapLabelStyle;
    private static int noWrapLabelStyleFontSize;
    private static string noWrapLabelStyleFontKey = string.Empty;
    private static string hoverTooltip = string.Empty;
    private static bool targetPickerOpen;
    private static Rect targetPickerAnchorScreenRect;
    private static Rect targetPickerAnchorGuiRect;
    private static string targetPickerAnchorKey = string.Empty;
    private static string targetPickerAnchorSource = string.Empty;
    private static Rect targetPickerFallbackGuiRect;
    private static Rect targetPickerModalRect;
    private static readonly Dictionary<string, PickerAnchorCacheEntry> PickerAnchorCache =
        new Dictionary<string, PickerAnchorCacheEntry>(StringComparer.Ordinal);
    private static string targetPickerRequirementName = string.Empty;
    private static string targetPickerTitle = string.Empty;
    private static string[] targetPickerOptions = Array.Empty<string>();
    private static PickerOption[] targetPickerCachedOptions = Array.Empty<PickerOption>();
    private static PickerMode targetPickerMode;
    private static RequirementTargetKind targetPickerTargetKind;

    private static string exactInternalNameFilter = string.Empty;
    private static string textFilter = string.Empty;
    private static string mustHaveEffectFilter = string.Empty;
    private static string mustNotHaveEffectFilter = string.Empty;
    private static int selectedCustomerIndex;
    private static int selectedQuestIndex;
    private static List<RegularCustomerOption> cachedCustomers = new List<RegularCustomerOption>();
    private static List<QuestRequirementInQuest> cachedRequirements = new List<QuestRequirementInQuest>();
    private static string searchStatus = "Click Search to populate the customer list.";
    private static string actionStatus = string.Empty;
    private static readonly Dictionary<string, RequirementSelection> RequirementSelections =
        new Dictionary<string, RequirementSelection>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> RequirementTargets =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly FieldInfo IngredientCheckPotentialField =
        typeof(QuestRequirementCertainIngredient).GetField("checkPotential", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo IngredientShortDistancePotentialField =
        typeof(QuestRequirementCertainIngredient).GetField("shortDistancePotential", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo IngredientPotentialThresholdField =
        typeof(QuestRequirementCertainIngredient).GetField("potentialThreshold", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo PotionEffectIconField =
        typeof(PotionEffect).GetField("icon", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly Dictionary<string, Sprite> PickerSpriteCache =
        new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string[]> TargetOptionsCache =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, bool> IngredientCompatibilityCache =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    private static readonly NextCustomerDirector.RandomRequirementRule RandomRequirementRule =
        new NextCustomerDirector.RandomRequirementRule();
    private static readonly Dictionary<string, string> RandomRequirementWeightTexts =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static bool ShouldBlockGameInput => visible && (blockGameInputWhenOpen?.Value ?? true);

    public static void Configure(ConfigFile config, ManualLogSource logSource)
    {
        logger = logSource;
        startVisible = config.Bind(
            "Next Customer Window",
            "StartVisible",
            false,
            "Show the regular customer planner window when the mod loads.");
        toggleShortcut = config.Bind(
            "Next Customer Window",
            "ToggleShortcut",
            new KeyboardShortcut(KeyCode.F2),
            "Shortcut used to show or hide the regular customer planner window.");
        useChapterOverride = config.Bind(
            "Next Customer Window",
            "UseChapterOverride",
            false,
            "If true, the planner filters and schedules regular customers as if the configured preview chapter were current.");
        previewChapter = config.Bind(
            "Next Customer Window",
            "PreviewChapter",
            10,
            "Chapter used by the planner when UseChapterOverride is true.");
        useKarmaOverride = config.Bind(
            "Next Customer Window",
            "UseKarmaOverride",
            false,
            "If true, the planner filters regular customers as if the configured preview karma were current.");
        previewKarma = config.Bind(
            "Next Customer Window",
            "PreviewKarma",
            0,
            "Karma used by the planner when UseKarmaOverride is true.");
        strictPlanningMode = config.Bind(
            "Planning",
            "StrictPlanningMode",
            true,
            "Initial default: true. If true, the planner only schedules regular customers, quests, and requirement modes that could naturally appear with the current save chapter, karma, and difficulty. Existing config files keep their saved value.");
        tinyFactionSpawnChanceThreshold = config.Bind(
            "Planning",
            "TinyFactionSpawnChanceThreshold",
            0.001f,
            "Initial default: 0.001. Positive faction spawn chances at or below this value are marked as tiny in the customer list. Existing config files keep their saved value.");
        RegularCustomerPool.Configure(tinyFactionSpawnChanceThreshold.Value);
        uiFontSize = config.Bind(
            "Next Customer Window",
            "UIFontSize",
            16,
            "Font size used by the regular customer planner window.");
        pickerFontSize = config.Bind(
            "Next Customer Window",
            "PickerFontSize",
            16,
            "Font size used by bounded dropdown/picker rows.");
        uiFontNames = config.Bind(
            "Next Customer Window",
            "UIFontNames",
            "Microsoft YaHei, Arial",
            "Comma-separated OS font names used by the IMGUI window. The first installed font is used; include a CJK font for Chinese.");
        blockGameInputWhenOpen = config.Bind(
            "Next Customer Window",
            "BlockGameInputWhenOpen",
            true,
            "If true, most native game input commands are disabled while the planner window is open.");
        targetFilledSelection = config.Bind(
            "Next Customer Window",
            "TargetFilledSelection",
            TargetFilledSelectionMode.Must,
            "Selection applied automatically when an editable requirement target is filled. Use Must or Can.");
        noneButtonColor = config.Bind(
            "Next Customer Window",
            "NoneButtonColor",
            "#777777",
            "Selected-state color for None requirement buttons. Use an HTML color such as #555555 or #555555FF.");
        mustButtonColor = config.Bind(
            "Next Customer Window",
            "MustButtonColor",
            "#B24D35",
            "Selected-state color for mandatory requirement buttons. Default follows the game's mandatory requirement red.");
        canButtonColor = config.Bind(
            "Next Customer Window",
            "CanButtonColor",
            "#55A842",
            "Selected-state color for optional requirement buttons. Default follows the game's optional/positive green.");
        customerSelectedColor = config.Bind(
            "Next Customer Window",
            "CustomerSelectedColor",
            "#4C74B8",
            "Selected-state color for the customer list.");
        visible = startVisible.Value;
    }

    public static void Update()
    {
        if (toggleShortcut.Value.IsDown())
        {
            visible = !visible;
            if (!visible)
                ClosePicker();
        }

        if (visible)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public static void OnGui()
    {
        if (!visible)
            return;

        GUISkin originalSkin = GUI.skin;
        GUI.skin = GetWindowSkin();
        try
        {
            EnsureWindowFitsFont();
            HandlePickerOutsideClick();
            windowRect = GUILayout.Window(
                0x5E71,
                windowRect,
                DrawWindow,
                string.Empty);
            DrawWindowBorder(windowRect, focused: true);
            if (randomRuleWindowVisible)
            {
                randomRuleWindowRect = GUILayout.Window(
                    0x5E72,
                    randomRuleWindowRect,
                    DrawRandomRuleWindow,
                    string.Empty);
                DrawWindowBorder(randomRuleWindowRect, focused: true);
            }
            DrawPickerModalWindow();
            DrawHoverTooltip();
        }
        finally
        {
            GUI.skin = originalSkin;
        }
    }

    private static void HandlePickerOutsideClick()
    {
        if (!targetPickerOpen)
            return;

        Event evt = Event.current;
        if (evt.type != EventType.MouseDown)
            return;

        Rect pickerRect = RootPickerOverlayRect(U(260f));
        if (pickerRect.Contains(evt.mousePosition))
            return;

        ClosePicker();
        evt.Use();
    }

    private static void DrawWindow(int id)
    {
        hoverTooltip = string.Empty;
        DrawWindowTitle(T("window.title"), windowRect.width);

        GUILayout.BeginHorizontal();
        DrawFiltersAndCustomers();
        DrawSelectedCustomerAndRequirements();
        GUILayout.EndHorizontal();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, WindowTitleHeight()));
    }

    private static void DrawWindowTitle(string title, float width)
    {
        if (Event.current.type != EventType.Repaint || string.IsNullOrWhiteSpace(title))
            return;

        Rect rect = new Rect(U(12f), U(2f), Mathf.Max(1f, width - U(24f)), WindowTitleHeight() - U(4f));
        GUI.Label(rect, title, WindowTitleStyle());
    }

    private static void DrawFiltersAndCustomers()
    {
        GUILayout.BeginVertical(Width(390f));
        GUILayout.Label(T("left.toggle", toggleShortcut.Value));
        GUILayout.Label(T("left.searchTitle"));
        exactInternalNameFilter = LabeledTextField(T("left.exactInternal"), exactInternalNameFilter);
        textFilter = LabeledTextField(T("left.name"), textFilter);
        strictPlanningMode.Value = GUILayout.Toggle(strictPlanningMode.Value, T("left.strict"));
        bool oldEnabled = GUI.enabled;
        GUI.enabled = oldEnabled && !StrictPlanningMode();
        useChapterOverride.Value = GUILayout.Toggle(useChapterOverride.Value, T("left.chapterOverride"));
        if (useChapterOverride.Value)
            previewChapter.Value = LabeledIntField(T("left.chapter"), previewChapter.Value, 1, 999);
        useKarmaOverride.Value = GUILayout.Toggle(useKarmaOverride.Value, T("left.karmaOverride"));
        if (useKarmaOverride.Value)
            previewKarma.Value = LabeledIntField(T("left.karma"), previewKarma.Value, -100, 100);
        GUI.enabled = oldEnabled;
        if (StrictPlanningMode())
        {
            GUILayout.Label(T("left.strictUses", CurrentChapter(), CurrentKarma()));
            GUILayout.Label(T("left.tinyThreshold", tinyFactionSpawnChanceThreshold.Value.ToString("0.#########")));
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(T("left.search"), Height(28f)))
            RunSearch();
        if (GUILayout.Button(T("left.importCurrent"), Height(28f)))
            ImportCurrentCustomer();
        if (GUILayout.Button(T("left.clearResults"), Height(28f)))
            ClearSearchResults();
        GUILayout.EndHorizontal();

        selectedCustomerIndex = Mathf.Clamp(selectedCustomerIndex, 0, Math.Max(0, cachedCustomers.Count - 1));

        GUILayout.Space(U(8f));
        GUILayout.Label(LocalizedStatus(searchStatus));
        if (!string.IsNullOrWhiteSpace(actionStatus))
            GUILayout.Label(actionStatus);
        GUILayout.Label(T("left.cachedCandidates", cachedCustomers.Count));
        customersScroll = GUILayout.BeginScrollView(customersScroll, false, true, GUILayout.ExpandHeight(true));
        for (int i = 0; i < cachedCustomers.Count; i++)
        {
            bool selected = i == selectedCustomerIndex;
            GUIContent content = new GUIContent(
                cachedCustomers[i].CachedListLabel ?? cachedCustomers[i].DisplayName,
                cachedCustomers[i].CachedTooltip ?? string.Empty);
            if (GUILayout.Button(content, selected ? CustomerSelectedButtonStyle() : GUI.skin.button) && !selected)
            {
                selectedCustomerIndex = i;
                selectedQuestIndex = 0;
            }
            if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                hoverTooltip = content.tooltip;
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private static void DrawSelectedCustomerAndRequirements()
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        RegularCustomerOption selected = cachedCustomers.Count == 0 ? null : cachedCustomers[selectedCustomerIndex];
        NormalizeOptionalSelectionsForCurrentMode();

        DrawEffectFilters();
        GUILayout.Space(U(6f));

        GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
        DrawSelectedCustomerPlannerColumn(selected);
        GUILayout.Space(U(8f));
        DrawRequirementEditorColumn(selected);
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private static void DrawSelectedCustomerPlannerColumn(RegularCustomerOption selected)
    {
        GUILayout.BeginVertical(Width(680f), GUILayout.ExpandHeight(true));

        detailsScroll = GUILayout.BeginScrollView(detailsScroll, false, true, GUILayout.ExpandHeight(true));
        DrawSelectedCustomerDetails(selected);
        GUILayout.EndScrollView();

        GUILayout.Space(U(6f));
        DrawRequirementPlanStatusAndActions(selected);

        GUILayout.EndVertical();
    }

    private static void DrawSelectedCustomerDetails(RegularCustomerOption selected)
    {
        GUILayout.Label(T("details.selectedCustomer"));
        if (selected == null)
        {
            GUILayout.Label(T("details.noCustomer"));
            return;
        }

        int chapter = PreviewChapter();
        List<Quest> quests = RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(selected, chapter);
        List<Quest> matchingQuests = selected.CachedMatchingQuests ?? quests;
        selectedQuestIndex = Mathf.Clamp(selectedQuestIndex, 0, Math.Max(0, matchingQuests.Count - 1));
        Quest targetQuest = SelectedQuest(selected);
        GUILayout.Label(selected.DisplayName);
        GUILayout.Label(T("details.source", selected.Source));
        GUILayout.Label(T("details.chapter", CurrentChapter(), chapter));
        GUILayout.Label(T("details.karma", CurrentKarma(), PreviewKarma()));
        GUILayout.Label(T("details.unlockChapter", selected.Template.unlockAtChapter));
        GUILayout.Label(T("details.questCounts", matchingQuests.Count, quests.Count));
        GUILayout.Label(T("details.targetQuest", targetQuest?.name ?? "-"));
#if DEBUG
        if (GUILayout.Button(T("details.logSpawn"), Height(26f)))
            LogSpawnDiagnostics(selected, targetQuest, matchingQuests);
        if (GUILayout.Button(T("details.logWindow"), Height(26f)))
            LogWindowDiagnostics();
#endif
        for (int i = 0; i < matchingQuests.Count && i < 8; i++)
        {
            Quest quest = matchingQuests[i];
            bool selectedQuest = i == selectedQuestIndex;
            if (DrawQuestToggle(quest, selectedQuest) && !selectedQuest)
                selectedQuestIndex = i;
        }
        if (matchingQuests.Count > 8)
            GUILayout.Label(T("details.moreQuests", matchingQuests.Count - 8));
    }

    private static void DrawRequirementEditorColumn(RegularCustomerOption selected)
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUILayout.BeginHorizontal();
        GUILayout.Label(T("requirements.title"));
        if (GUILayout.Button(T("requirements.refresh"), Width(130f)))
            RefreshRequirementCache();
        if (GUILayout.Button(T("requirements.reset"), Width(130f)))
            ResetRequirementConfig();
        GUILayout.EndHorizontal();
        if (cachedRequirements.Count == 0)
            GUILayout.Label(T("requirements.noCache"));
        else
            GUILayout.Label(T("requirements.targetHint"));
        RequirementLimitInfo limits = CurrentRequirementLimits();
        if (limits.MaxOptional == 0)
            GUILayout.Label(T("requirements.onlyMandatory"));
        if (limits.MaxMandatory == 0)
            GUILayout.Label(T("requirements.onlyOptional"));
        GUILayout.Label(
            T(
                "requirements.selectedCounts",
                SelectedRequirementCount(RequirementSelection.Mandatory),
                limits.MandatoryRangeText,
                SelectedRequirementCount(RequirementSelection.Optional),
                limits.OptionalRangeText,
                SelectedRequirementTotal(),
                limits.TotalRangeText));

        Quest targetQuest = SelectedQuest(selected);
        requirementsScroll = GUILayout.BeginScrollView(requirementsScroll, false, true, GUILayout.ExpandHeight(true));
        string currentCategory = null;
        foreach (QuestRequirementInQuest requirement in cachedRequirements
            .Where(item => IsRequirementAvailableForPlanning(item, out _))
            .OrderBy(RequirementCategoryOrder)
            .ThenBy(RequirementCategoryName)
            .ThenBy(item => item.requirement.name))
        {
            string name = requirement.requirement.name;
            string category = RequirementCategoryName(requirement);
            if (!string.Equals(category, currentCategory, StringComparison.Ordinal))
            {
                DrawRequirementCategoryHeader(category);
                currentCategory = category;
            }

            RequirementSelections.TryGetValue(name, out RequirementSelection state);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, NoWrapLabelStyle(), Width(RequirementNameColumnWidth()));
            DrawRequirementSelectionButton(name, state, RequirementSelection.None, T("requirements.none"), limits);
            DrawRequirementSelectionButton(name, state, RequirementSelection.Mandatory, T("requirements.must"), limits);
            DrawRequirementSelectionButton(name, state, RequirementSelection.Optional, T("requirements.can"), limits);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            DrawRequirementTargetField(requirement, name, targetQuest);
            GUILayout.EndVertical();
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    private static void DrawRequirementPlanStatusAndActions(RegularCustomerOption selected)
    {
        Quest targetQuest = SelectedQuest(selected);
        List<PlannedRequirement> mandatory = SelectedRequirements(RequirementSelection.Mandatory);
        List<PlannedRequirement> optional = SelectedRequirements(RequirementSelection.Optional);
        string blockReason = string.Empty;
        bool valid = IsSelectedRequirementCountAllowed(targetQuest, out blockReason)
            && selected != null
            && NextCustomerDirector.IsRequirementGroupAllowed(
                selected,
                CurrentChapter(),
                CurrentKarma(),
                StrictPlanningMode(),
                targetQuest,
                mandatory,
                optional,
                out blockReason);

        GUILayout.Space(U(6f));
        GUILayout.Label(valid ? T("requirements.groupAllowed") : T("requirements.groupBlocked", blockReason));
        DrawPlannerActionButtons(selected, targetQuest, mandatory, optional, valid);
        DrawScheduledQueueSummary();
    }

    private static void DrawPlannerActionButtons(
        RegularCustomerOption selected,
        Quest targetQuest,
        IReadOnlyList<PlannedRequirement> mandatory,
        IReadOnlyList<PlannedRequirement> optional,
        bool valid)
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(T("actions.title"));

        GUILayout.BeginHorizontal();
        GUI.enabled = selected != null && valid;
        if (GUILayout.Button(T("actions.previewSelected"), Height(28f)))
            PreviewCurrentCustomer(selected, targetQuest, mandatory, optional);
        GUI.enabled = true;
        if (GUILayout.Button(T("actions.randomPreview"), Height(28f)))
            RandomizePreviewCustomer();
        if (GUILayout.Button(T("actions.editRandomRule"), Height(28f)))
            ToggleRandomRuleWindow();
        GUI.enabled = NextCustomerDirector.HasPreview;
        if (GUILayout.Button(T("actions.revertPreview"), Height(28f)))
            RevertPreviewCustomer();
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUI.enabled = selected != null && valid;
        if (GUILayout.Button(T("actions.addScheduled"), Height(28f)))
        {
            NextCustomerDirector.ScheduleResult result =
                NextCustomerDirector.Schedule(CreatePlan(selected, targetQuest, mandatory, optional));
            actionStatus = ScheduleResultText(result);
        }
        GUI.enabled = NextCustomerDirector.ScheduledCount > 0;
        if (GUILayout.Button(T("actions.clearScheduled"), Height(28f)))
        {
            NextCustomerDirector.Clear();
            actionStatus = T("state.clearedScheduled");
        }
        GUI.enabled = false;
        GUILayout.Button(T("actions.loadPreset"), Height(28f));
        GUILayout.Button(T("actions.savePreset"), Height(28f));
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private static void DrawScheduledQueueSummary()
    {
        IReadOnlyList<NextCustomerDirector.ScheduledPlanSnapshot> plans = NextCustomerDirector.ScheduledPlans;
        if (plans.Count == 0)
            return;

        GUILayout.Space(U(4f));
        GUILayout.Label(
            T(
                "schedule.summary",
                NextCustomerDirector.PendingScheduledCount,
                NextCustomerDirector.AppliedPlaceholderCount,
                NextCustomerDirector.ScheduledCount));

        foreach (NextCustomerDirector.ScheduledPlanSnapshot snapshot in plans.Take(5))
        {
            PlannedCustomer plan = snapshot.Plan;
            if (plan == null)
                continue;
            string state = snapshot.Applied ? T("schedule.stateApplied") : T("schedule.statePending");
            GUILayout.Label(
                T(
                    "schedule.entry",
                    snapshot.Index + 1,
                    state,
                    CustomerPublicLabel(plan.Customer),
                    plan.TargetQuest?.name ?? "-"));
        }

        if (plans.Count > 5)
            GUILayout.Label(T("schedule.more", plans.Count - 5));
    }

    private static string ScheduleResultText(NextCustomerDirector.ScheduleResult result)
    {
        string pruned = result.RemovedAppliedAppointments > 0
            ? $" Removed {result.RemovedAppliedAppointments} previously applied appointment(s)."
            : string.Empty;
        if (!result.Success)
            return $"Schedule blocked: {result.Reason}{pruned}";
        if (result.AppliedImmediately)
            return "Applied to current customer immediately; it will be removed from the list when you add the next appointment." + pruned;
        string reason = string.IsNullOrWhiteSpace(result.Reason)
            ? string.Empty
            : $" Current customer was not changed now: {result.Reason}";
        return "Added to scheduled list." + reason + pruned;
    }

    private static void PreviewCurrentCustomer(
        RegularCustomerOption selected,
        Quest targetQuest,
        IReadOnlyList<PlannedRequirement> mandatory,
        IReadOnlyList<PlannedRequirement> optional)
    {
        if (selected == null || targetQuest == null)
        {
            actionStatus = "Preview blocked: select a customer and quest first.";
            return;
        }
        if (!IsSelectedRequirementCountAllowed(targetQuest, out string countReason)
            || !NextCustomerDirector.IsRequirementGroupAllowed(
                selected,
                CurrentChapter(),
                CurrentKarma(),
                StrictPlanningMode(),
                targetQuest,
                mandatory,
                optional,
                out countReason))
        {
            actionStatus = $"Preview blocked: {countReason}";
            return;
        }

        PlannedCustomer plan = CreatePlan(selected, targetQuest, mandatory, optional);
        actionStatus = NextCustomerDirector.PreviewCurrentCustomer(plan, out string reason)
            ? $"Preview applied: {CustomerPublicLabel(selected)} / {targetQuest.name}."
            : $"Preview blocked: {reason}";
    }

    private static void RandomizePreviewCustomer()
    {
        if (!NextCustomerDirector.CanPreviewEditCurrentCustomer(out string reason))
        {
            actionStatus = $"Randomize preview blocked: {reason}";
            return;
        }

        bool selectedTargetMode = TryGetSelectedCustomerQuest(out RegularCustomerOption selected, out Quest targetQuest);
        if (selectedTargetMode)
        {
            if (!CanUseSelectedTargetForCurrentRandomPreview(selected, targetQuest, out string selectedTargetReason))
            {
                actionStatus = $"Randomize preview blocked: {selectedTargetReason}";
                return;
            }
        }
        else if (!TrySelectRandomCustomerAndQuest(out selected, out targetQuest, out string randomSelectionReason))
        {
            actionStatus = $"Randomize preview blocked: {randomSelectionReason}";
            return;
        }

        PlannedCustomer basePlan = CreateCurrentPlan(
            selected,
            targetQuest,
            new List<PlannedRequirement>(),
            new List<PlannedRequirement>());
        if (!NextCustomerDirector.CanPreviewCurrentCustomer(basePlan, out reason))
        {
            actionStatus = $"Randomize preview blocked: {reason}";
            return;
        }
        if (!NextCustomerDirector.TryGenerateRandomRequirements(
                targetQuest,
                CurrentChapter(),
                RandomRequirementRule,
                out IReadOnlyList<GeneratedQuestRequirement> generatedMandatory,
                out IReadOnlyList<GeneratedQuestRequirement> generatedOptional,
                out reason))
        {
            actionStatus = $"Randomize preview blocked: {reason}";
            return;
        }

        ImportCurrentRequirements(generatedMandatory, generatedOptional);
        List<PlannedRequirement> mandatory = SelectedRequirements(RequirementSelection.Mandatory);
        List<PlannedRequirement> optional = SelectedRequirements(RequirementSelection.Optional);
        if (!IsSelectedRequirementCountAllowed(targetQuest, out string countReason)
            || !NextCustomerDirector.IsRequirementGroupAllowed(
                selected,
                CurrentChapter(),
                CurrentKarma(),
                strict: true,
                targetQuest,
                mandatory,
                optional,
                out countReason))
        {
            actionStatus = $"Randomize preview blocked after import: {countReason}";
            return;
        }

        PlannedCustomer plan = CreateCurrentPlan(selected, targetQuest, mandatory, optional);
        actionStatus = NextCustomerDirector.PreviewCurrentCustomer(plan, out reason)
            ? $"{(selectedTargetMode ? "Random requirements preview" : "Random customer preview")} applied and imported: {CustomerPublicLabel(selected)} / {targetQuest.name} / requirements={generatedMandatory.Count}+{generatedOptional.Count}."
            : $"Randomize preview blocked: {reason}";
    }

    private static bool TryGetSelectedCustomerQuest(out RegularCustomerOption selected, out Quest targetQuest)
    {
        selected = null;
        targetQuest = null;
        if (cachedCustomers.Count == 0)
            return false;

        selectedCustomerIndex = Mathf.Clamp(selectedCustomerIndex, 0, cachedCustomers.Count - 1);
        selected = cachedCustomers[selectedCustomerIndex];
        targetQuest = SelectedQuest(selected);
        return selected != null && targetQuest != null;
    }

    private static bool CanUseSelectedTargetForCurrentRandomPreview(
        RegularCustomerOption selected,
        Quest targetQuest,
        out string reason)
    {
        reason = string.Empty;
        if (selected == null || targetQuest == null)
        {
            reason = "No selected customer quest is available.";
            return false;
        }

        int chapter = CurrentChapter();
        int karma = CurrentKarma();
        List<RegularCustomerOption> currentCandidates = RegularCustomerPool
            .GetAvailableRegularCustomers(chapter, karma, strict: true)
            .ToList();
        if (!currentCandidates.Any(candidate => IsSameCustomerOption(candidate, selected)))
        {
            reason = $"The selected customer is not available for the current chapter={chapter}, karma={karma}.";
            return false;
        }

        List<Quest> currentQuests = RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(selected, chapter);
        if (!currentQuests.Any(quest => IsSameQuest(quest, targetQuest)))
        {
            reason = $"The selected quest is not spawnable for the selected customer at current chapter={chapter}.";
            return false;
        }

        return true;
    }

    private static bool TrySelectRandomCustomerAndQuest(
        out RegularCustomerOption selected,
        out Quest targetQuest,
        out string reason)
    {
        selected = null;
        targetQuest = null;
        reason = string.Empty;

        List<RegularCustomerOption> candidates = RegularCustomerPool.GetAvailableRegularCustomers(
                CurrentChapter(),
                CurrentKarma(),
                strict: true)
            .Where(candidate => CacheAndMatchFilters(candidate, CurrentChapter()))
            .ToList();
        List<RegularCustomerOption> spawnableCandidates = candidates
            .Where(candidate => MatchingQuests(
                candidate.CachedMatchingQuests
                    ?? RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(candidate, CurrentChapter())).Count > 0)
            .ToList();
        if (spawnableCandidates.Count == 0)
        {
            reason = "No random customer candidate with matching quests is available.";
            return false;
        }

        int selectedIndex = RandomIndexByWeights(spawnableCandidates.Select(RandomCustomerWeight).ToList());
        if (selectedIndex < 0)
        {
            reason = "No random customer candidate has a positive weight.";
            return false;
        }

        RegularCustomerOption selectedCustomer = spawnableCandidates[selectedIndex];
        List<Quest> quests = MatchingQuests(
            selectedCustomer.CachedMatchingQuests
                ?? RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(selectedCustomer, CurrentChapter()));
        int questIndex = UnityEngine.Random.Range(0, quests.Count);
        targetQuest = quests[questIndex];

        cachedCustomers.RemoveAll(candidate => IsSameCustomerOption(candidate, selectedCustomer));
        cachedCustomers.Insert(0, selectedCustomer);
        selectedCustomerIndex = 0;
        selectedCustomer.CachedMatchingQuests = quests;
        selectedCustomer.CachedEnabledQuestCount = RegularCustomerPool
            .GetSpawnableQuestsForRegularCustomer(selectedCustomer, CurrentChapter())
            .Count;
        selectedCustomer.CachedListLabel = CustomerListLabel(selectedCustomer);
        selectedCustomer.CachedTooltip = CustomerTooltip(selectedCustomer);
        selectedQuestIndex = questIndex;
        selected = selectedCustomer;
        return true;
    }

    private static void RevertPreviewCustomer()
    {
        actionStatus = NextCustomerDirector.RevertPreview(out string reason)
            ? "Preview reverted."
            : $"Revert preview blocked: {reason}";
    }

    private static float RandomCustomerWeight(RegularCustomerOption option)
    {
        if (option == null)
            return 0f;
        if (option.Source == CustomerCandidateSource.PlotRandomClosenessQuest)
            return 1f;
        float factionWeight = Mathf.Max(0f, RegularCustomerPool.GetFactionSpawnChanceAtKarma(option, CurrentKarma()));
        float classWeight = Mathf.Max(0f, option.ClassInFaction?.spawnChance ?? 0f);
        float weight = factionWeight * classWeight;
        return weight > 0f || StrictPlanningMode()
            ? weight
            : Mathf.Max(0.0001f, classWeight);
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

    private static void ToggleRandomRuleWindow()
    {
        randomRuleWindowVisible = !randomRuleWindowVisible;
        if (randomRuleWindowVisible)
            SyncRandomRuleChanceDefaults();
    }

    private static void DrawRandomRuleWindow(int id)
    {
        DrawWindowTitle(T("window.randomRuleTitle"), randomRuleWindowRect.width);
        GUILayout.Label(T("randomRule.description"));
        GUILayout.Label(T("randomRule.weightHint"));

        SyncRandomRuleChanceDefaults();
        randomRuleScroll = GUILayout.BeginScrollView(randomRuleScroll, false, true);
        DrawRandomChanceSection(
            T("randomRule.mandatoryChances"),
            isMandatoryRequirements: true,
            () => RandomRequirementRule.OverrideMandatorySpawnChances,
            value => RandomRequirementRule.OverrideMandatorySpawnChances = value,
            value => RandomRequirementRule.MandatoryFirstChance = value,
            () => RandomRequirementRule.MandatoryFirstChance,
            value => RandomRequirementRule.MandatorySecondChance = value,
            () => RandomRequirementRule.MandatorySecondChance);
        DrawRandomChanceSection(
            T("randomRule.optionalChances"),
            isMandatoryRequirements: false,
            () => RandomRequirementRule.OverrideOptionalSpawnChances,
            value => RandomRequirementRule.OverrideOptionalSpawnChances = value,
            value => RandomRequirementRule.OptionalFirstChance = value,
            () => RandomRequirementRule.OptionalFirstChance,
            value => RandomRequirementRule.OptionalSecondChance = value,
            () => RandomRequirementRule.OptionalSecondChance);

        GUILayout.Space(U(8f));
        DrawRequirementCategoryHeader(T("randomRule.weightMultipliers"));
        if (cachedRequirements.Count == 0)
            GUILayout.Label(T("randomRule.noCache"));
        foreach (QuestRequirementInQuest requirement in cachedRequirements
            .Where(item => item?.requirement != null)
            .OrderBy(RequirementCategoryOrder)
            .ThenBy(RequirementCategoryName)
            .ThenBy(item => item.requirement.name))
        {
            DrawRandomWeightRow(requirement);
        }
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(T("randomRule.reset"), Height(28f)))
            ResetRandomRule();
        if (GUILayout.Button(T("randomRule.close"), Height(28f)))
            randomRuleWindowVisible = false;
        GUILayout.EndHorizontal();
        GUI.DragWindow(new Rect(0f, 0f, 10000f, WindowTitleHeight()));
    }

    private static void SyncRandomRuleChanceDefaults()
    {
        QuestRequirementDifficultySettings settings =
            Settings<GameDifficultyQuestRequirements>.Asset?.GetCurrentValue();
        if (settings == null)
            return;

        (int mandatoryFirst, int mandatorySecond) =
            settings.GetSpawnChances(CurrentChapter(), isMandatoryRequirements: true);
        if (!RandomRequirementRule.OverrideMandatorySpawnChances)
        {
            RandomRequirementRule.MandatoryFirstChance = mandatoryFirst;
            RandomRequirementRule.MandatorySecondChance = mandatorySecond;
        }

        (int optionalFirst, int optionalSecond) =
            settings.GetSpawnChances(CurrentChapter(), isMandatoryRequirements: false);
        if (!RandomRequirementRule.OverrideOptionalSpawnChances)
        {
            RandomRequirementRule.OptionalFirstChance = optionalFirst;
            RandomRequirementRule.OptionalSecondChance = optionalSecond;
        }
    }

    private static void DrawRandomChanceSection(
        string title,
        bool isMandatoryRequirements,
        Func<bool> getOverrideChances,
        Action<bool> setOverrideChances,
        Action<int> setFirst,
        Func<int> getFirst,
        Action<int> setSecond,
        Func<int> getSecond)
    {
        DrawRequirementCategoryHeader(title);
        QuestRequirementDifficultySettings settings =
            Settings<GameDifficultyQuestRequirements>.Asset?.GetCurrentValue();
        (int nativeFirst, int nativeSecond) = settings == null
            ? (0, 0)
            : settings.GetSpawnChances(CurrentChapter(), isMandatoryRequirements);
        bool overrideChances = GUILayout.Toggle(
            getOverrideChances(),
            T("randomRule.overrideChances", nativeFirst, nativeSecond));
        setOverrideChances(overrideChances);
        bool oldEnabled = GUI.enabled;
        GUI.enabled = oldEnabled && overrideChances;
        GUILayout.BeginHorizontal();
        GUILayout.Label(T("randomRule.firstChance"), Width(220f));
        setFirst(IntField(getFirst(), 0, 100, Width(90f)));
        GUILayout.Space(U(20f));
        GUILayout.Label(T("randomRule.secondChance"), Width(240f));
        setSecond(IntField(getSecond(), 0, 100, Width(90f)));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUI.enabled = oldEnabled;
    }

    private static void DrawRandomWeightRow(QuestRequirementInQuest requirement)
    {
        string name = requirement.requirement.name;
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, Width(360f));
        GUILayout.Label(T("randomRule.nativeWeight", requirement.requirement.spawnChance.ToString("0.###")), Width(180f));
        GUILayout.Label("x", Width(18f));
        RandomRequirementWeightTexts.TryGetValue(name, out string text);
        if (string.IsNullOrWhiteSpace(text))
            text = RandomRequirementRule.RequirementWeightMultipliers.TryGetValue(name, out float multiplier)
                ? multiplier.ToString("0.###")
                : "1";
        string edited = GUILayout.TextField(text, Width(110f));
        RandomRequirementWeightTexts[name] = edited;
        if (float.TryParse(edited, out float parsed))
        {
            if (Mathf.Approximately(parsed, 1f))
                RandomRequirementRule.RequirementWeightMultipliers.Remove(name);
            else
                RandomRequirementRule.RequirementWeightMultipliers[name] = Mathf.Max(0f, parsed);
        }
        if (GUILayout.Button(T("randomRule.resetOne"), Width(90f)))
        {
            RandomRequirementRule.RequirementWeightMultipliers.Remove(name);
            RandomRequirementWeightTexts[name] = "1";
        }
        GUILayout.EndHorizontal();
    }

    private static void ResetRandomRule()
    {
        RandomRequirementRule.OverrideMandatorySpawnChances = false;
        RandomRequirementRule.OverrideOptionalSpawnChances = false;
        RandomRequirementRule.RequirementWeightMultipliers.Clear();
        RandomRequirementWeightTexts.Clear();
        SyncRandomRuleChanceDefaults();
    }

    private static void DrawRequirementCategoryHeader(string category)
    {
        GUILayout.Space(U(6f));
        Color oldContentColor = GUI.contentColor;
        GUI.contentColor = new Color(0.95f, 0.82f, 0.56f, oldContentColor.a);
        GUILayout.Label("──────── " + LocalizedRequirementCategoryName(category) + " ────────");
        GUI.contentColor = oldContentColor;
    }

    private static int RequirementCategoryOrder(QuestRequirementInQuest requirement)
    {
        string category = RequirementCategoryName(requirement);
        switch (category)
        {
            case "Potion effects":
                return 10;
            case "Ingredient targets":
                return 20;
            case "Potion base limits":
                return 30;
            case "Ingredient count":
                return 40;
            case "Potion quality":
                return 50;
            case "No salts":
                return 60;
            case "Mod: ingredient categories":
                return 70;
            case "Mod: ingredient counts":
                return 80;
            case "Mod: other":
                return 90;
            default:
                return 100;
        }
    }

    private static string RequirementCategoryName(QuestRequirementInQuest requirement)
    {
        QuestRequirement questRequirement = requirement?.requirement;
        if (questRequirement == null)
            return "Other / external";

        if (questRequirement is QuestRequirementsAdditionalEffects)
            return "Potion effects";
        if (questRequirement is QuestRequirementCertainBase)
            return "Potion base limits";
        if (questRequirement is QuestRequirementMaxIngredients)
            return "Ingredient count";
        if (questRequirement is QuestRequirementCertainIngredient)
            return "Ingredient targets";
        if (questRequirement is QuestRequirementPotionQuality)
            return "Potion quality";
        if (questRequirement.GetType() == typeof(QuestRequirementNoSalts))
            return "No salts";

        if (RequirementTargetMetadataResolver.TryGetTags(questRequirement, out string[] tags))
        {
            if (tags.Any(tag => tag.IndexOf("broad", StringComparison.OrdinalIgnoreCase) >= 0
                || tag.IndexOf("category", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "Mod: ingredient categories";
            }

            if (tags.Any(tag => tag.IndexOf("highlander", StringComparison.OrdinalIgnoreCase) >= 0
                || tag.IndexOf("count", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "Mod: ingredient counts";
            }

            return "Mod: other";
        }

        return questRequirement.GetType().Assembly == typeof(QuestRequirement).Assembly
            ? "Other native"
            : "Other / external";
    }

    private static string LocalizedRequirementCategoryName(string category)
    {
        switch (category)
        {
            case "Potion effects":
                return T("category.potionEffects");
            case "Ingredient targets":
                return T("category.ingredientTargets");
            case "Potion base limits":
                return T("category.potionBaseLimits");
            case "Ingredient count":
                return T("category.ingredientCount");
            case "Potion quality":
                return T("category.potionQuality");
            case "No salts":
                return T("category.noSalts");
            case "Mod: ingredient categories":
                return T("category.modIngredientCategories");
            case "Mod: ingredient counts":
                return T("category.modIngredientCounts");
            case "Mod: other":
                return T("category.modOther");
            case "Other native":
                return T("category.otherNative");
            case "Other / external":
                return T("category.otherExternal");
            default:
                return category;
        }
    }

    private static void RunSearch()
    {
        cachedCustomers = RegularCustomerPool.GetAvailableRegularCustomers(
                PreviewChapter(),
                PreviewKarma(),
                StrictPlanningMode())
            .Where(candidate => CacheAndMatchFilters(candidate))
            .ToList();
        foreach (RegularCustomerOption customer in cachedCustomers)
        {
            customer.CachedListLabel = CustomerListLabel(customer);
            customer.CachedTooltip = CustomerTooltip(customer);
        }
        selectedCustomerIndex = cachedCustomers.Count == 0
            ? 0
            : Mathf.Clamp(selectedCustomerIndex, 0, cachedCustomers.Count - 1);
        selectedQuestIndex = 0;
        customersScroll = Vector2.zero;
        searchStatus = T("state.searchComplete", PreviewChapter(), PreviewKarma());
    }

    private static void ClearSearchResults()
    {
        cachedCustomers.Clear();
        selectedCustomerIndex = 0;
        customersScroll = Vector2.zero;
        searchStatus = T("state.resultsCleared");
    }

    private static void ImportCurrentCustomer()
    {
        if (!NextCustomerDirector.TryGetCurrentCustomer(
                out RegularCustomerOption current,
                out Quest currentQuest,
                out IReadOnlyList<GeneratedQuestRequirement> mandatoryRequirements,
                out IReadOnlyList<GeneratedQuestRequirement> optionalRequirements,
                out string reason))
        {
            actionStatus = T("state.importCurrentBlocked", reason);
            return;
        }

        List<Quest> quests = RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(current, PreviewChapter());
        if (currentQuest != null && !quests.Contains(currentQuest))
            quests.Insert(0, currentQuest);
        current.CachedEnabledQuestCount = quests.Count;
        current.CachedMatchingQuests = quests;
        current.CachedTooltip = CustomerTooltip(current);
        current.CachedListLabel = "[current] " + CustomerListLabel(current);

        cachedCustomers.RemoveAll(option => IsSameCustomerOption(option, current));
        cachedCustomers.Insert(0, current);
        selectedCustomerIndex = 0;
        selectedQuestIndex = currentQuest == null ? 0 : Mathf.Max(0, quests.IndexOf(currentQuest));
        ImportCurrentRequirements(mandatoryRequirements, optionalRequirements);
        customersScroll = Vector2.zero;
        searchStatus = T("state.importedCurrent");
        actionStatus =
            T(
                "state.importedCurrentDetails",
                current.Template?.name ?? "-",
                currentQuest?.name ?? "-",
                mandatoryRequirements.Count,
                optionalRequirements.Count);
    }

    private static void ImportCurrentRequirements(
        IReadOnlyList<GeneratedQuestRequirement> mandatoryRequirements,
        IReadOnlyList<GeneratedQuestRequirement> optionalRequirements)
    {
        RequirementSelections.Clear();
        RequirementTargets.Clear();

        foreach (GeneratedQuestRequirement requirement in mandatoryRequirements ?? Array.Empty<GeneratedQuestRequirement>())
            ImportGeneratedRequirement(requirement, RequirementSelection.Mandatory);
        foreach (GeneratedQuestRequirement requirement in optionalRequirements ?? Array.Empty<GeneratedQuestRequirement>())
            ImportGeneratedRequirement(requirement, RequirementSelection.Optional);

        targetPickerOpen = false;
    }

    private static void ImportGeneratedRequirement(
        GeneratedQuestRequirement generated,
        RequirementSelection selection)
    {
        string name = generated?.requirementInQuest?.requirement?.name;
        if (string.IsNullOrWhiteSpace(name))
            return;

        RequirementSelections[name] = selection;
        string target = ImportedRequirementTarget(generated);
        if (!string.IsNullOrWhiteSpace(target))
            RequirementTargets[name] = target;
    }

    private static string ImportedRequirementTarget(GeneratedQuestRequirement generated)
    {
        if (generated == null)
            return string.Empty;
        if (!string.IsNullOrWhiteSpace(generated.stringValue1))
            return generated.stringValue1;
        if (generated.requirementInQuest?.ingredient != null)
            return generated.requirementInQuest.ingredient.name;
        if (generated.requirementInQuest?.potionBase != null)
            return generated.requirementInQuest.potionBase.name;
        return string.Empty;
    }

    private static bool IsSameCustomerOption(RegularCustomerOption left, RegularCustomerOption right)
    {
        if (left == null || right == null)
            return false;
        return left.Source == right.Source
            && left.Template == right.Template
            && left.Faction == right.Faction
            && left.FactionClass == right.FactionClass;
    }

    private static bool IsSameQuest(Quest left, Quest right)
    {
        if (left == null || right == null)
            return false;
        return ReferenceEquals(left, right)
            || string.Equals(left.name, right.name, StringComparison.OrdinalIgnoreCase);
    }

    private static PlannedCustomer CreatePlan(
        RegularCustomerOption selected,
        Quest targetQuest,
        IReadOnlyList<PlannedRequirement> mandatory,
        IReadOnlyList<PlannedRequirement> optional)
    {
        return new PlannedCustomer(
            selected,
            targetQuest,
            PreviewChapter(),
            StrictPlanningMode(),
            mandatory,
            optional);
    }

    private static PlannedCustomer CreateCurrentPlan(
        RegularCustomerOption selected,
        Quest targetQuest,
        IReadOnlyList<PlannedRequirement> mandatory,
        IReadOnlyList<PlannedRequirement> optional)
    {
        return new PlannedCustomer(
            selected,
            targetQuest,
            CurrentChapter(),
            StrictPlanningMode(),
            mandatory,
            optional);
    }

    private static void RefreshRequirementCache()
    {
        cachedRequirements = QuestRequirementInQuest.allRequirements
            .Where(item => item?.requirement != null)
            .OrderBy(item => item.requirement.name)
            .ToList();
        TargetOptionsCache.Clear();
        IngredientCompatibilityCache.Clear();
    }

    private static void ResetRequirementConfig()
    {
        RequirementSelections.Clear();
        RequirementTargets.Clear();
        TargetOptionsCache.Clear();
        IngredientCompatibilityCache.Clear();
        targetPickerOpen = false;
    }

    private static bool CacheAndMatchFilters(RegularCustomerOption option)
    {
        return CacheAndMatchFilters(option, PreviewChapter());
    }

    private static bool CacheAndMatchFilters(RegularCustomerOption option, int chapter)
    {
        if (!string.IsNullOrWhiteSpace(exactInternalNameFilter)
            && !MatchesExactInternalName(option, exactInternalNameFilter))
        {
            return false;
        }

        if (!ContainsIgnoreCase(option.DisplayName, textFilter))
            return false;

        List<Quest> quests = RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(option, chapter);
        option.CachedEnabledQuestCount = quests.Count;
        option.CachedMatchingQuests = MatchingQuests(quests);
        return option.CachedMatchingQuests.Count > 0;
    }

    private static List<Quest> MatchingQuests(IEnumerable<Quest> quests)
    {
        return quests
            .Where(QuestMatchesEffectFilters)
            .ToList();
    }

    private static bool QuestMatchesEffectFilters(Quest quest)
    {
        if (!QuestHasAllEffects(quest, mustHaveEffectFilter))
            return false;
        if (QuestHasAnyEffect(quest, mustNotHaveEffectFilter))
            return false;
        return true;
    }

    private static bool MatchesExactInternalName(RegularCustomerOption option, string filter)
    {
        string needle = filter.Trim();
        return string.Equals(option.Template?.name, needle, StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.Faction?.name, needle, StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.FactionClass?.name, needle, StringComparison.OrdinalIgnoreCase);
    }

    private static bool QuestHasEffect(Quest quest, string filter)
    {
        if (quest.desiredEffects == null || string.IsNullOrWhiteSpace(filter))
            return false;

        PotionEffect exactEffect = PotionEffect.GetByName(filter.Trim(), returnFirst: false, warning: false);
        if (exactEffect != null)
        {
            return quest.desiredEffects.Any(effect =>
                effect != null
                && string.Equals(effect.name, exactEffect.name, StringComparison.OrdinalIgnoreCase));
        }

        return quest.desiredEffects.Any(effect => effect != null && ContainsIgnoreCase(effect.name, filter));
    }

    private static bool QuestHasAllEffects(Quest quest, string filter)
    {
        string[] tokens = EffectFilterTokens(filter);
        return tokens.Length == 0 || tokens.All(token => QuestHasEffect(quest, token));
    }

    private static bool QuestHasAnyEffect(Quest quest, string filter)
    {
        string[] tokens = EffectFilterTokens(filter);
        return tokens.Length > 0 && tokens.Any(token => QuestHasEffect(quest, token));
    }

    private static string[] EffectFilterTokens(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return Array.Empty<string>();
        return filter
            .Split(new[] { ',', ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string EffectsText(Quest quest)
    {
        if (quest.desiredEffects == null || quest.desiredEffects.Length == 0)
            return "(no effects)";
        return string.Join(", ", quest.desiredEffects.Where(effect => effect != null).Select(effect => effect.name).ToArray());
    }

    private static bool DrawQuestToggle(Quest quest, bool selectedQuest)
    {
        GUIContent content = new GUIContent(quest?.name ?? "-", EffectsText(quest));
        bool result = GUILayout.Toggle(selectedQuest, GUIContent.none, "Button", Height(46f));
        Rect rect = GUILayoutUtility.GetLastRect();
        if (rect.Contains(Event.current.mousePosition))
            hoverTooltip = content.tooltip;
        DrawCenteredQuestContent(rect, quest);
        return result;
    }

    private static void DrawCenteredQuestContent(Rect rect, Quest quest)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        Rect contentRect = new Rect(rect.x + U(8f), rect.y + U(4f), rect.width - U(16f), rect.height - U(8f));
        string questName = quest?.name ?? "-";
        GUIStyle labelStyle = GUI.skin.label;
        GUIContent questNameContent = new GUIContent(questName);
        Vector2 nameSize = labelStyle.CalcSize(questNameContent);
        float nameWidth = nameSize.x;
        float gap = U(10f);
        float effectsWidth = EffectMixedRowWidth(quest?.desiredEffects, includeText: false, contentRect.width - nameWidth - gap);
        float totalWidth = Mathf.Min(contentRect.width, nameWidth + (effectsWidth > 0f ? gap + effectsWidth : 0f));
        float x = contentRect.x + Mathf.Max(0f, contentRect.width - totalWidth) * 0.5f;
        float nameHeight = Mathf.Min(contentRect.height, nameSize.y);
        float nameY = contentRect.y + Mathf.Max(0f, contentRect.height - nameHeight) * 0.5f;
        GUI.Label(new Rect(x, nameY, nameWidth, nameHeight), questNameContent, labelStyle);
        if (effectsWidth > 0f)
        {
            DrawEffectMixedRow(
                quest?.desiredEffects,
                new Rect(x + nameWidth + gap, contentRect.y, effectsWidth, contentRect.height),
                includeText: false,
                alignRight: false,
                centerAsGroup: false,
                maxWidth: effectsWidth);
        }
    }

    private static float EffectMixedRowWidth(IEnumerable<PotionEffect> effects, bool includeText, float maxWidth)
    {
        if (effects == null || maxWidth <= 0f)
            return 0f;

        float iconSize = InlineIconSize();
        float spacing = InlineIconSpacing();
        float itemSpacing = spacing + U(6f);
        GUIStyle labelStyle = GUI.skin.label;
        float width = 0f;
        int count = 0;
        foreach (PotionEffect effect in effects.Where(effect => effect != null))
        {
            float itemWidth = iconSize
                + (includeText ? spacing + labelStyle.CalcSize(new GUIContent(effect.name)).x : 0f);
            float nextWidth = width + (count > 0 ? itemSpacing : 0f) + itemWidth;
            if (nextWidth > maxWidth && count > 0)
                break;
            width = Mathf.Min(nextWidth, maxWidth);
            count++;
        }
        return width;
    }

    private static void DrawEffectIconRow(IEnumerable<PotionEffect> effects, Rect rect, bool alignRight)
    {
        if (effects == null || Event.current.type != EventType.Repaint)
            return;

        List<Sprite> sprites = effects
            .Where(effect => effect != null)
            .Select(EffectSprite)
            .Where(sprite => sprite != null)
            .ToList();
        if (sprites.Count == 0)
            return;

        float size = InlineIconSize();
        float spacing = InlineIconSpacing();
        float totalWidth = sprites.Count * size + Math.Max(0, sprites.Count - 1) * spacing;
        float x = alignRight ? rect.xMax - totalWidth : rect.x;
        float y = rect.y + (rect.height - size) * 0.5f;
        for (int i = 0; i < sprites.Count; i++)
        {
            DrawSpriteInRect(sprites[i], new Rect(x + i * (size + spacing), y, size, size));
        }
    }

    private static void DrawEffectTokenIconRow(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, NoWrapLabelStyle(), Width(EffectFilterLabelWidth()));
        Rect rowRect = GUILayoutUtility.GetRect(1f, Mathf.Max(RowHeight(), InlineIconSize() + U(10f)), GUILayout.ExpandWidth(true));
        string[] tokens = EffectFilterTokens(value);
        if (tokens.Length == 0)
        {
            GUI.Label(rowRect, "-");
        }
        else
        {
            IEnumerable<PotionEffect> effects =
                tokens.Select(token => PotionEffect.GetByName(token, returnFirst: false, warning: false));
            DrawEffectMixedRow(effects, rowRect, includeText: true, alignRight: false, centerAsGroup: true);
            if (rowRect.Contains(Event.current.mousePosition))
                hoverTooltip = string.Join(", ", tokens);
        }
        GUILayout.EndHorizontal();
    }

    private static void DrawEffectMixedRow(
        IEnumerable<PotionEffect> effects,
        Rect rect,
        bool includeText,
        bool alignRight,
        bool centerAsGroup = false,
        float maxWidth = 0f)
    {
        if (effects == null || Event.current.type != EventType.Repaint)
            return;

        List<PotionEffect> effectList = effects.Where(effect => effect != null).ToList();
        if (effectList.Count == 0)
            return;

        float iconSize = InlineIconSize();
        float spacing = InlineIconSpacing();
        float itemSpacing = spacing + U(6f);
        GUIStyle labelStyle = GUI.skin.label;
        List<float> widths = new List<float>();
        float availableWidth = maxWidth > 0f ? maxWidth : rect.width;
        float usedWidth = 0f;
        foreach (PotionEffect effect in effectList)
        {
            float width = iconSize
                + (includeText ? spacing + labelStyle.CalcSize(new GUIContent(effect.name)).x : 0f);
            float nextWidth = usedWidth + (widths.Count > 0 ? itemSpacing : 0f) + width;
            if (nextWidth > availableWidth && widths.Count > 0)
                break;
            widths.Add(width);
            usedWidth = Mathf.Min(nextWidth, availableWidth);
        }
        if (widths.Count == 0)
            return;
        if (widths.Count < effectList.Count)
            effectList = effectList.Take(widths.Count).ToList();
        float totalWidth = widths.Sum() + Math.Max(0, widths.Count - 1) * itemSpacing;
        float x = centerAsGroup
            ? rect.x + Mathf.Max(0f, rect.width - totalWidth) * 0.5f
            : alignRight
                ? rect.xMax - totalWidth
                : rect.x;
        float y = rect.y + (rect.height - iconSize) * 0.5f;
        for (int i = 0; i < effectList.Count; i++)
        {
            PotionEffect effect = effectList[i];
            Sprite sprite = EffectSprite(effect);
            if (sprite != null)
                DrawSpriteInRect(sprite, new Rect(x, y, iconSize, iconSize));
            if (includeText)
            {
                Rect labelRect = new Rect(
                    x + iconSize + spacing,
                    rect.y,
                    Mathf.Max(0f, widths[i] - iconSize - spacing),
                    rect.height);
                GUI.Label(labelRect, effect.name);
            }
            x += widths[i] + itemSpacing;
        }
    }

    private static string CustomerListLabel(RegularCustomerOption option)
    {
        List<Quest> quests = option.CachedMatchingQuests
            ?? MatchingQuests(RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(option, PreviewChapter()));
        int enabledQuestCount = option.CachedEnabledQuestCount > 0
            ? option.CachedEnabledQuestCount
            : RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(option, PreviewChapter()).Count;
        string tinyMarker = TinyChanceMarker(option);
        return $"{CustomerPublicLabel(option)}{tinyMarker}\n"
            + $"  {option.Gender}  |  ch.{option.Template?.unlockAtChapter.ToString() ?? "-"}  |  quests {quests.Count}/{enabledQuestCount}";
    }

    private static string CustomerPublicLabel(RegularCustomerOption option)
    {
        if (option == null)
            return "-";
        string role = option.FactionClass?.name;
        if (string.IsNullOrWhiteSpace(role))
            role = option.Faction?.name;
        if (string.IsNullOrWhiteSpace(role))
            role = option.Source == CustomerCandidateSource.PlotRandomClosenessQuest ? "Plot customer" : "Regular customer";
        string source = option.Source == CustomerCandidateSource.PlotRandomClosenessQuest ? "Plot" : "Regular";
        return $"{role}  |  {source}";
    }

    private static string CustomerTooltip(RegularCustomerOption option)
    {
        List<Quest> quests = option.CachedMatchingQuests
            ?? MatchingQuests(RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(option, PreviewChapter()));
        int enabledQuestCount = option.CachedEnabledQuestCount > 0
            ? option.CachedEnabledQuestCount
            : RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(option, PreviewChapter()).Count;
        List<string> lines = new List<string>
        {
            CustomerPublicLabel(option),
            $"Gender: {option.Gender}",
            $"Unlock chapter: {option.Template?.unlockAtChapter.ToString() ?? "-"}",
            string.Empty,
            "Internal identifiers:",
            $"Template: {option.Template?.name ?? "-"}",
            $"Faction: {option.Faction?.name ?? "-"}",
            $"Faction class: {option.FactionClass?.name ?? "-"}",
            TinyChanceTooltipLine(option),
            $"Matching quests at chapter {PreviewChapter()}: {quests.Count}/{enabledQuestCount}",
        };

        foreach (Quest quest in quests.Take(12))
            lines.Add($"- {quest.name}: {EffectsText(quest)}");
        if (quests.Count > 12)
            lines.Add($"... and {quests.Count - 12} more quests");

        return string.Join("\n", lines.ToArray());
    }

    private static string TinyChanceMarker(RegularCustomerOption option)
    {
        if (RegularCustomerPool.HasNoPositiveFactionSpawnChance(option, PreviewKarma()))
            return "  [no chance now]";
        return RegularCustomerPool.HasTinyPositiveFactionSpawnChance(option, PreviewKarma())
            ? "  [tiny chance]"
            : string.Empty;
    }

    private static string TinyChanceTooltipLine(RegularCustomerOption option)
    {
        if (option.Source != CustomerCandidateSource.RegularFactionQuest)
            return $"Source: {option.Source}";
        float chance = RegularCustomerPool.GetFactionSpawnChanceAtKarma(option, PreviewKarma());
        if (RegularCustomerPool.HasNoPositiveFactionSpawnChance(option, PreviewKarma()))
        {
            return $"No current faction spawn chance at karma {PreviewKarma()}: raw={chance:0.#########}. "
                + "Shown only because non-strict mode lists candidates that may be possible at another karma.";
        }
        if (!RegularCustomerPool.HasTinyPositiveFactionSpawnChance(option, PreviewKarma()))
            return $"Faction raw chance at karma {PreviewKarma()}: {chance:0.#########}";
        return $"Tiny faction chance at karma {PreviewKarma()}: {chance:0.#########} <= marker threshold {RegularCustomerPool.TinyFactionSpawnChanceThreshold:0.#########}";
    }

#if DEBUG
    private static void LogSpawnDiagnostics(
        RegularCustomerOption selected,
        Quest targetQuest,
        IReadOnlyList<Quest> matchingQuests)
    {
        if (selected == null)
        {
            logger?.LogInfo("[CustomerPlanner diagnostics] No selected customer.");
            return;
        }

        List<string> lines = new List<string>();
        int chapter = PreviewChapter();
        int karma = PreviewKarma();
        lines.Add("===== Spawn diagnostics begin =====");
        lines.Add($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        lines.Add(
            $"Mode strict={StrictPlanningMode()}, chapter={chapter}, karma={karma}, "
            + $"tinyFactionSpawnChanceThreshold={FormatWeight(tinyFactionSpawnChanceThreshold.Value)}, "
            + $"selected={selected.DisplayName}, targetQuest={targetQuest?.name ?? "-"}");

        List<Faction> enabledFactions = (Faction.allFactions ?? new List<Faction>())
            .Where(faction => faction != null && faction.IsEnabled(chapter))
            .ToList();
        float factionWeightSum = enabledFactions.Sum(faction => PositiveFactionWeightAtKarma(faction, karma));
        lines.Add($"Enabled factions={enabledFactions.Count}, raw weight sum at karma={FormatWeight(factionWeightSum)}");
        foreach (Faction faction in enabledFactions.OrderByDescending(faction => FactionWeightAtKarma(faction, karma)))
        {
            float weight = FactionWeightAtKarma(faction, karma);
            float positiveWeight = Mathf.Max(0f, weight);
            float normalized = factionWeightSum <= 0f ? 0f : positiveWeight / factionWeightSum;
            lines.Add(
                $"Faction {faction.name}: raw={FormatWeight(weight)}, normalized={FormatProbability(normalized)}, "
                + $"curseWeight={FormatWeight(EffectWeightByName(faction, "Curse"))}, "
                + $"nonZeroEffects=[{FactionEffectWeightsText(faction)}]");
        }

        float selectedFactionWeight = FactionWeightAtKarma(selected.Faction, karma);
        float selectedFactionProbability = factionWeightSum <= 0f ? 0f : Mathf.Max(0f, selectedFactionWeight) / factionWeightSum;
        float classProbability = ClassProbability(selected.Faction, selected.ClassInFaction, chapter);
        lines.Add(
            $"Selected faction probability={FormatProbability(selectedFactionProbability)}; "
            + $"selected class probability within faction={FormatProbability(classProbability)}; "
            + $"combined customer slot probability before template extras={FormatProbability(selectedFactionProbability * classProbability)}");
        lines.Add($"Selected faction class weights: [{FactionClassWeightsText(selected.Faction, chapter)}]");

        LogQuestDiagnostics(lines, selected.Faction, targetQuest, "Target quest");
        lines.Add($"Matching quest diagnostics count={matchingQuests?.Count ?? 0}");
        foreach (Quest quest in matchingQuests ?? Array.Empty<Quest>())
            LogQuestDiagnostics(lines, selected.Faction, quest, "Matching quest");

        List<Quest> allSpawnableQuests = RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(selected, chapter);
        lines.Add($"All spawnable quest diagnostics count={allSpawnableQuests.Count}");
        foreach (Quest quest in allSpawnableQuests)
            LogQuestDiagnostics(lines, selected.Faction, quest, "Spawnable quest");

        LogSelectedIngredientTargetDiagnostics(lines, targetQuest);
        lines.AddRange(NextCustomerDirector.DescribeRequirementGroupDryRun(
            targetQuest,
            SelectedRequirements(RequirementSelection.Mandatory),
            SelectedRequirements(RequirementSelection.Optional)));

        lines.Add("===== Spawn diagnostics end =====");
        lines.Add(string.Empty);
        WriteSpawnDiagnostics(lines);
    }

    private static void LogQuestDiagnostics(List<string> lines, Faction faction, Quest quest, string label)
    {
        if (faction == null || quest == null)
        {
            lines.Add($"{label}: faction or quest is null.");
            return;
        }

        PotionEffect[] effects = quest.desiredEffects ?? Array.Empty<PotionEffect>();
        string effectText = effects.Length == 0
            ? "(no desired effects)"
            : string.Join(
                "; ",
                effects
                    .Where(effect => effect != null)
                    .Select(effect =>
                        $"{effect.name}: factionEffectWeight={FormatWeight(faction.GetEffectSpawnChance(effect))}, "
                        + $"questSpawnWeight={FormatWeight(faction.GetQuestSpawnChance(quest, effect))}")
                    .ToArray());
        float maxQuestWeight = effects
            .Where(effect => effect != null)
            .Select(effect => faction.GetQuestSpawnChance(quest, effect))
            .DefaultIfEmpty(0f)
            .Max();
        lines.Add(
            $"{label} {quest.name}: karmaReward={quest.karmaReward}, "
            + $"maxQuestSpawnWeight={FormatWeight(maxQuestWeight)}, effects=[{effectText}]");
    }

    private static void LogSelectedIngredientTargetDiagnostics(List<string> lines, Quest targetQuest)
    {
        lines.Add("Selected ingredient target diagnostics:");
        if (targetQuest == null)
        {
            lines.Add("- No target quest selected.");
            return;
        }

        int count = 0;
        foreach (KeyValuePair<string, RequirementSelection> pair in RequirementSelections
            .Where(pair => pair.Value != RequirementSelection.None)
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            QuestRequirementInQuest requirement = cachedRequirements.FirstOrDefault(item =>
                item?.requirement != null
                && string.Equals(item.requirement.name, pair.Key, StringComparison.OrdinalIgnoreCase))
                ?? FindRequirement(pair.Key);
            if (requirement?.requirement is not QuestRequirementCertainIngredient certainIngredient)
                continue;

            count++;
            RequirementTargets.TryGetValue(pair.Key, out string target);
            lines.Add(
                $"- Requirement {pair.Key}, selection={pair.Value}, configuredTarget={(string.IsNullOrWhiteSpace(target) ? "-" : target)}, "
                + $"wrapperIngredient={requirement.ingredient?.name ?? "-"}");
            LogIngredientRequirementRuleDiagnostics(lines, certainIngredient, targetQuest);
            if (!string.IsNullOrWhiteSpace(target))
                LogSingleIngredientCandidateDiagnostics(lines, certainIngredient, targetQuest, target, "configured target");
            if (requirement.ingredient != null)
                LogSingleIngredientCandidateDiagnostics(lines, certainIngredient, targetQuest, requirement.ingredient.name, "wrapper ingredient");
        }

        if (count == 0)
            lines.Add("- No selected QuestRequirementCertainIngredient requirements.");
    }

    private static void LogIngredientRequirementRuleDiagnostics(
        List<string> lines,
        QuestRequirementCertainIngredient certainIngredient,
        Quest targetQuest)
    {
        int chapter = PreviewChapter();
        ElementType[] dominantElements = EnabledDominantElements(targetQuest, chapter);
        bool checkPotential = GetPrivateBool(certainIngredient, IngredientCheckPotentialField, fallback: true);
        bool shortDistancePotential = GetPrivateBool(certainIngredient, IngredientShortDistancePotentialField, fallback: false);
        float potentialThreshold = GetPrivateFloat(certainIngredient, IngredientPotentialThresholdField, fallback: 0.2f);
        int unlockedCount = Ingredient.allIngredients?.Count(ingredient => ingredient != null && IsIngredientUnlocked(ingredient)) ?? 0;
        int compatibleCount = Ingredient.allIngredients?.Count(ingredient =>
            ingredient != null
            && IsIngredientUnlocked(ingredient)
            && IngredientMatchesCertainIngredientRule(certainIngredient, ingredient, targetQuest, out _)) ?? 0;

        lines.Add(
            $"  rule: chapter={chapter}, checkPotential={checkPotential}, "
            + $"shortDistancePotential={shortDistancePotential}, threshold={potentialThreshold:0.#########}, "
            + $"enabledDominantElements=[{string.Join(", ", dominantElements.Select(element => element.ToString()).ToArray())}], "
            + $"unlockedIngredients={unlockedCount}, compatibleUnlockedIngredients={compatibleCount}");
    }

    private static void LogSingleIngredientCandidateDiagnostics(
        List<string> lines,
        QuestRequirementCertainIngredient certainIngredient,
        Quest targetQuest,
        string ingredientName,
        string label)
    {
        Ingredient ingredient = Ingredient.GetByName(ingredientName, returnFirst: false, warning: false);
        if (ingredient == null)
        {
            lines.Add($"  {label}: ingredient not found: {ingredientName}");
            return;
        }

        bool compatible = IngredientMatchesCertainIngredientRule(
            certainIngredient,
            ingredient,
            targetQuest,
            out string reason);
        bool inPickerOptions = TargetOptions(
                RequirementTargetKind.Ingredient,
                targetQuest,
                new QuestRequirementInQuest(certainIngredient))
            .Any(option => string.Equals(option, ingredient.name, StringComparison.OrdinalIgnoreCase));
        lines.Add(
            $"  {label} {ingredient.name}: chapter={ingredient.chapter}, unlocked={IsIngredientUnlocked(ingredient)}, "
            + $"compatible={compatible}, inCurrentPickerOptions={inPickerOptions}, reason={reason}");
        lines.Add($"  {label} potentials: {IngredientPotentialsText(certainIngredient, ingredient, targetQuest)}");
    }

    private static string IngredientPotentialsText(
        QuestRequirementCertainIngredient certainIngredient,
        Ingredient ingredient,
        Quest targetQuest)
    {
        int chapter = PreviewChapter();
        ElementType[] dominantElements = EnabledDominantElements(targetQuest, chapter);
        if (dominantElements.Length == 0)
            return "(no enabled quest dominant elements)";

        bool shortDistancePotential = GetPrivateBool(certainIngredient, IngredientShortDistancePotentialField, fallback: false);
        ElementalPotential elementalPotential = ingredient.GetElementalPotential(shortDistancePotential);
        return string.Join(
            ", ",
            dominantElements.Select(element => $"{element}={elementalPotential.GetPotential(element):0.#########}").ToArray());
    }

    private static void WriteSpawnDiagnostics(IReadOnlyList<string> lines)
    {
        try
        {
            string directory = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            if (string.IsNullOrWhiteSpace(directory))
                directory = Environment.CurrentDirectory;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "SpawnDiagnostics.txt");
            File.AppendAllLines(path, lines);
            searchStatus = $"Spawn diagnostics written to {path}";
            logger?.LogInfo($"Spawn diagnostics written to {path}");
        }
        catch (Exception ex)
        {
            searchStatus = $"Failed to write spawn diagnostics: {ex.Message}";
            logger?.LogWarning($"Failed to write spawn diagnostics: {ex}");
        }
    }

    private static void LogWindowDiagnostics()
    {
        List<string> lines = new List<string>();
        Event evt = Event.current;
        Rect pickerRect = targetPickerOpen ? RootPickerOverlayRect(U(260f)) : Rect.zero;
        Rect anchorGuiFromScreen = targetPickerOpen ? ScreenRectToGuiRect(targetPickerAnchorScreenRect) : Rect.zero;
        Vector2 mouseGui = evt?.mousePosition ?? Vector2.zero;
        Vector2 mouseScreen = GUIUtility.GUIToScreenPoint(mouseGui);
        lines.Add("===== Window diagnostics begin =====");
        lines.Add($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        lines.Add($"eventType={evt?.type.ToString() ?? "-"}, mouseGui={Vec(mouseGui)}, mouseScreen={Vec(mouseScreen)}");
        lines.Add($"screen={Screen.width}x{Screen.height}, windowRect={RectText(windowRect)}");
        lines.Add($"targetPickerOpen={targetPickerOpen}, mode={targetPickerMode}, targetKind={targetPickerTargetKind}, requirementName={targetPickerRequirementName}");
        lines.Add($"targetPickerTitle={targetPickerTitle}, options={targetPickerOptions?.Length ?? 0}, cachedOptions={targetPickerCachedOptions?.Length ?? 0}");
        lines.Add($"anchorKey={targetPickerAnchorKey}, anchorSource={targetPickerAnchorSource}");
        lines.Add($"anchorGuiOriginal={RectText(targetPickerAnchorGuiRect)}");
        lines.Add($"anchorFallbackGui={RectText(targetPickerFallbackGuiRect)}");
        lines.Add($"anchorScreen={RectText(targetPickerAnchorScreenRect)}");
        lines.Add($"anchorGuiFromScreen={RectText(anchorGuiFromScreen)}");
        if (!string.IsNullOrEmpty(targetPickerAnchorKey)
            && PickerAnchorCache.TryGetValue(targetPickerAnchorKey, out PickerAnchorCacheEntry cachedAnchor))
        {
            lines.Add(
                $"anchorCache: gui={RectText(cachedAnchor.GuiRect)}, screen={RectText(cachedAnchor.ScreenRect)}, "
                + $"valid={IsValidAnchorRect(cachedAnchor.ScreenRect)}, event={cachedAnchor.EventType}, frame={cachedAnchor.FrameCount}");
        }
        else
        {
            lines.Add("anchorCache: -");
        }
        lines.Add($"pickerRect={RectText(pickerRect)}");
        lines.Add($"pickerContainsMouseGui={pickerRect.Contains(mouseGui)}");
        lines.Add($"anchorContainsMouseGuiViaScreen={ScreenRectContainsGuiPoint(targetPickerAnchorScreenRect, mouseGui)}");
        lines.Add($"requirementsScroll={Vec(requirementsScroll)}, targetPickerScroll={Vec(targetPickerScroll)}");
        lines.Add($"uiFontSize={uiFontSize?.Value.ToString() ?? "-"}, pickerFontSize={PickerFontSize()}, scaleUnit={U(1f):0.###}");
        PickerLayoutMetrics metrics = RootPickerLayout(U(260f));
        lines.Add(
            $"rowHeight={metrics.RowHeight:0.###}, desiredViewport={metrics.DesiredViewportHeight:0.###}, "
            + $"chromeHeight={metrics.ChromeHeight:0.###}, viewportHeight={metrics.ViewportHeight:0.###}");
        lines.Add(
            $"pickerBudget: outerHeight={metrics.OuterHeight:0.###}, innerHeight={metrics.InnerHeight:0.###}, "
            + $"padding={metrics.Padding:0.###}, titleHeight={metrics.TitleHeight:0.###}, "
            + $"titleSpacing={metrics.TitleSpacing:0.###}, bottomMargin={metrics.BottomMargin:0.###}");
        if (targetPickerOpen)
            AddPickerLayoutDiagnostics(lines, U(260f), metrics);
        lines.Add("===== Window diagnostics end =====");
        lines.Add(string.Empty);
        WriteWindowDiagnostics(lines);
    }

    private static void AddPickerLayoutDiagnostics(List<string> lines, float width, PickerLayoutMetrics metrics)
    {
        lines.Add(
            $"layout: width={width:0.###}, minHeight={metrics.MinOuterHeight:0.###}, "
            + $"maxDownHeight={metrics.MaxDownHeight:0.###}, maxUpHeight={metrics.MaxUpHeight:0.###}, "
            + $"openUp={metrics.OpenUp}, availableHeight={metrics.AvailableHeight:0.###}, "
            + $"visibleRows={metrics.VisibleRows}, desiredRows={metrics.DesiredRows}, "
            + $"rows={metrics.Rows}, computedHeight={metrics.OuterHeight:0.###}");
    }

    private static void WriteWindowDiagnostics(IReadOnlyList<string> lines)
    {
        try
        {
            string directory = Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
            if (string.IsNullOrWhiteSpace(directory))
                directory = Environment.CurrentDirectory;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "WindowDiagnostics.txt");
            File.AppendAllLines(path, lines);
            actionStatus = $"Window diagnostics written to {path}";
            logger?.LogInfo($"Window diagnostics written to {path}");
        }
        catch (Exception ex)
        {
            actionStatus = $"Failed to write window diagnostics: {ex.Message}";
            logger?.LogWarning($"Failed to write window diagnostics: {ex}");
        }
    }

    private static string RectText(Rect rect)
    {
        return $"x={rect.x:0.###}, y={rect.y:0.###}, w={rect.width:0.###}, h={rect.height:0.###}, xMax={rect.xMax:0.###}, yMax={rect.yMax:0.###}";
    }

    private static string Vec(Vector2 value)
    {
        return $"x={value.x:0.###}, y={value.y:0.###}";
    }

    private static float FactionWeightAtKarma(Faction faction, int karma)
    {
        return faction?.spawnChance == null ? 0f : faction.spawnChance.Evaluate(karma);
    }

    private static float PositiveFactionWeightAtKarma(Faction faction, int karma)
    {
        return Mathf.Max(0f, FactionWeightAtKarma(faction, karma));
    }

    private static float ClassProbability(Faction faction, FactionClassInFaction selectedClass, int chapter)
    {
        if (faction?.factionClasses == null || selectedClass == null)
            return 0f;
        List<FactionClassInFaction> enabledClasses = faction.factionClasses
            .Where(classInFaction => classInFaction != null
                && classInFaction.spawnChance > 0f
                && classInFaction.IsEnabled(faction, chapter))
            .ToList();
        float sum = enabledClasses.Sum(classInFaction => classInFaction.spawnChance);
        return sum <= 0f ? 0f : selectedClass.spawnChance / sum;
    }

    private static float EffectWeightByName(Faction faction, string effectName)
    {
        PotionEffect effect = PotionEffect.allPotionEffects?
            .FirstOrDefault(item => item != null && string.Equals(item.name, effectName, StringComparison.OrdinalIgnoreCase));
        return effect == null ? 0f : faction.GetEffectSpawnChance(effect);
    }

    private static string FactionEffectWeightsText(Faction faction)
    {
        if (faction == null || PotionEffect.allPotionEffects == null)
            return "-";

        List<string> entries = PotionEffect.allPotionEffects
            .Where(effect => effect != null)
            .Select(effect => new { Effect = effect, Weight = faction.GetEffectSpawnChance(effect) })
            .Where(entry => Math.Abs(entry.Weight) > 0f)
            .OrderByDescending(entry => entry.Weight)
            .ThenBy(entry => entry.Effect.name)
            .Select(entry => $"{entry.Effect.name}={FormatWeight(entry.Weight)}")
            .ToList();
        if (entries.Count == 0)
            return "-";
        return string.Join(", ", entries.ToArray());
    }

    private static string FactionClassWeightsText(Faction faction, int chapter)
    {
        if (faction?.factionClasses == null)
            return "-";
        List<FactionClassInFaction> enabledClasses = faction.factionClasses
            .Where(classInFaction => classInFaction != null
                && classInFaction.spawnChance > 0f
                && classInFaction.IsEnabled(faction, chapter))
            .ToList();
        float sum = enabledClasses.Sum(classInFaction => classInFaction.spawnChance);
        if (sum <= 0f)
            return "-";
        return string.Join(
            ", ",
            enabledClasses
                .OrderByDescending(classInFaction => classInFaction.spawnChance)
                .Select(classInFaction =>
                    $"{classInFaction.factionClass?.name ?? classInFaction.name ?? "-"}={FormatWeight(classInFaction.spawnChance)} ({FormatProbability(classInFaction.spawnChance / sum)})")
                .ToArray());
    }

    private static string FormatWeight(float value)
    {
        return $"{value:0.#########} / {value:E9}";
    }

    private static string FormatProbability(float value)
    {
        return $"{value:P8} / {value:E9}";
    }
#endif

    private static Quest SelectedQuest(RegularCustomerOption selected)
    {
        if (selected == null)
            return null;
        List<Quest> matchingQuests = selected.CachedMatchingQuests
            ?? MatchingQuests(RegularCustomerPool.GetSpawnableQuestsForRegularCustomer(selected, PreviewChapter()));
        if (matchingQuests.Count == 0)
            return null;
        selectedQuestIndex = Mathf.Clamp(selectedQuestIndex, 0, matchingQuests.Count - 1);
        return matchingQuests[selectedQuestIndex];
    }

    private static void DrawRequirementTargetField(QuestRequirementInQuest requirement, string name, Quest targetQuest)
    {
        if (!TryGetRequirementTargetInfo(requirement, out RequirementTargetInfo targetInfo))
            return;

        RequirementTargets.TryGetValue(name, out string value);
        GUILayout.BeginHorizontal();
        GUILayout.Label(targetInfo.Label, NoWrapLabelStyle(), Width(RequirementTargetLabelWidth()));
        Sprite targetSprite = TargetValueSprite(targetInfo.Kind, targetInfo.Editable ? value : targetInfo.FixedValue);
        GUILayout.Space(U(RequirementTargetIconColumnWidth()));
        DrawSpriteInRect(targetSprite, GUILayoutUtility.GetLastRect());
        if (targetInfo.Editable)
        {
            string editedValue = GUILayout.TextField(value ?? string.Empty, Width(RequirementTargetValueWidth()));
            SetRequirementTargetValue(name, editedValue, updateSelection: true);
            DrawTargetPickerButton(requirement, name, targetQuest, targetInfo);
        }
        else
        {
            GUILayout.Label(targetInfo.FixedValue ?? "-", Width(RequirementTargetValueWidth()));
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private static Sprite TargetValueSprite(RequirementTargetKind kind, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (kind == RequirementTargetKind.Ingredient)
        {
            Ingredient ingredient = Ingredient.GetByName(value, returnFirst: false, warning: false);
            return ingredient?.GetInventoryIcon() ?? ingredient?.smallIcon;
        }
        if (kind == RequirementTargetKind.Base)
        {
            PotionBase potionBase = PotionBase.GetByName(value, returnFirst: false, warning: false);
            return potionBase?.smallIconSprite ?? potionBase?.tooltipIconSprite ?? potionBase?.recipeMarkIcon;
        }
        return null;
    }

    private static bool TryGetRequirementTargetInfo(
        QuestRequirementInQuest requirement,
        out RequirementTargetInfo targetInfo)
    {
        targetInfo = default;
        if (requirement?.requirement == null)
            return false;

        QuestRequirement questRequirement = requirement.requirement;
        if (questRequirement is QuestRequirementCertainIngredient)
        {
            if (requirement.ingredient != null)
            {
                targetInfo = RequirementTargetInfo.FixedTarget(T("target.ingredient"), requirement.ingredient.name);
                return true;
            }

            targetInfo = RequirementTargetInfo.EditableTarget(T("target.ingredient"), RequirementTargetKind.Ingredient);
            return true;
        }

        if (questRequirement is QuestRequirementCertainBase)
        {
            if (requirement.potionBase != null)
            {
                targetInfo = RequirementTargetInfo.FixedTarget(T("target.base"), requirement.potionBase.name);
                return true;
            }

            targetInfo = RequirementTargetInfo.EditableTarget(T("target.base"), RequirementTargetKind.Base);
            return true;
        }

        if (RequirementTargetMetadataResolver.TryGetDeclaredTarget(
            questRequirement,
            out string declaredTargetDisplayName))
        {
            targetInfo = RequirementTargetInfo.FixedTarget(
                T("target.generic"),
                declaredTargetDisplayName);
            return true;
        }

        return false;
    }

    private static void DrawTargetPickerButton(
        QuestRequirementInQuest requirement,
        string name,
        Quest targetQuest,
        RequirementTargetInfo targetInfo)
    {
        string anchorKey = RequirementTargetAnchorKey(name);
        bool open = GUILayout.Button("▼", Width(RequirementTargetPickerButtonWidth()));
        Rect buttonRect = GUILayoutUtility.GetLastRect();
        CachePickerAnchor(anchorKey, buttonRect);
        if (open)
            OpenTargetPicker(requirement, name, targetQuest, targetInfo, anchorKey, buttonRect);
        if (GUILayout.Button(T("target.clear"), Width(RequirementTargetClearButtonWidth())))
            SetRequirementTargetValue(name, string.Empty, updateSelection: true);
    }

    private static void OpenTargetPicker(
        QuestRequirementInQuest requirement,
        string name,
        Quest targetQuest,
        RequirementTargetInfo targetInfo,
        string anchorKey,
        Rect buttonRect)
    {
        if (!TryResolvePickerAnchor(anchorKey, buttonRect, out Rect anchorScreenRect, out Rect anchorGuiRect, out string anchorSource))
        {
            actionStatus = $"Cannot open picker: missing valid anchor for {name}.";
            LogInvalidPickerAnchor(anchorKey, buttonRect);
            return;
        }

        targetPickerMode = PickerMode.RequirementTarget;
        targetPickerTargetKind = targetInfo.Kind;
        targetPickerRequirementName = name;
        targetPickerTitle = $"{targetInfo.Label} target - {name}";
        targetPickerOptions = CachedTargetOptions(targetInfo.Kind, targetQuest, requirement);
        RebuildPickerOptionCache();
        targetPickerScroll = Vector2.zero;
        targetPickerOpen = true;
        targetPickerAnchorKey = anchorKey;
        targetPickerAnchorSource = anchorSource;
        targetPickerFallbackGuiRect = buttonRect;
        targetPickerAnchorGuiRect = anchorGuiRect;
        targetPickerAnchorScreenRect = anchorScreenRect;
    }

    private static bool IsTargetPickerOpen(string requirementName)
    {
        return targetPickerOpen
            && targetPickerMode == PickerMode.RequirementTarget
            && string.Equals(targetPickerRequirementName, requirementName, StringComparison.OrdinalIgnoreCase);
    }

    private static void PreparePickerModalInput(Event evt)
    {
        if (evt == null)
            return;

        if (evt.type == EventType.MouseDown)
        {
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
        }
    }

    private static void DrawPickerModalWindow()
    {
        if (!targetPickerOpen)
            return;

        targetPickerModalRect = RootPickerOverlayRect(U(260f));
        targetPickerModalRect = GUI.Window(0x5E74, targetPickerModalRect, DrawPickerModalContents, GUIContent.none);
        GUI.BringWindowToFront(0x5E74);
        DrawWindowBorder(targetPickerModalRect, focused: true);
    }

    private static void DrawPickerModalContents(int id)
    {
        Event evt = Event.current;
        if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
        {
            ClosePicker();
            evt.Use();
            return;
        }

        bool oldEnabled = GUI.enabled;
        GUI.enabled = true;
        Rect localRect = new Rect(0f, 0f, targetPickerModalRect.width, targetPickerModalRect.height);
        GUI.Box(localRect, GUIContent.none);
        DrawLocalBorder(localRect, new Color(0.95f, 0.78f, 0.45f, 1f), U(2f));
        PickerLayoutMetrics metrics = RootPickerLayout(localRect.width);
        GUILayout.BeginArea(new Rect(
            metrics.Padding,
            metrics.Padding,
            localRect.width - metrics.Padding * 2f,
            localRect.height - metrics.Padding * 2f));
        GUILayout.Label(targetPickerTitle, GUILayout.Height(metrics.TitleHeight));
        GUILayout.Space(metrics.TitleSpacing);
        targetPickerScroll = GUILayout.BeginScrollView(targetPickerScroll, false, true, GUILayout.Height(metrics.ViewportHeight));
        foreach (PickerOption option in targetPickerCachedOptions)
        {
            if (GUILayout.Button(GUIContent.none, PickerButtonStyle(), GUILayout.Height(PickerRowHeight())))
            {
                ApplyPickerOption(option.Value);
                ClosePicker();
            }
            DrawPickerOptionOverlay(option);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.enabled = oldEnabled;
    }

    private static Rect RootPickerOverlayRect(float width)
    {
        PickerLayoutMetrics metrics = RootPickerLayout(width);
        Rect anchor = metrics.Anchor;
        float height = metrics.OuterHeight;
        float x = anchor.x;
        float y = metrics.OpenUp ? anchor.y - height - U(2f) : anchor.yMax + U(2f);
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        if (x + width > screenWidth - U(8f))
            x = Mathf.Max(U(8f), screenWidth - width - U(8f));
        y = Mathf.Clamp(y, U(8f), Mathf.Max(U(8f), screenHeight - height - metrics.BottomMargin));

        return new Rect(x, y, width, height);
    }

    private static string RequirementTargetAnchorKey(string requirementName)
    {
        return $"RequirementTarget:{requirementName}";
    }

    private static string EffectAnchorKey(PickerMode pickerMode)
    {
        return pickerMode == PickerMode.ExcludesEffect
            ? "ExcludesEffect"
            : "NeedsEffect";
    }

    private static void CachePickerAnchor(string key, Rect guiRect)
    {
        if (string.IsNullOrEmpty(key) || !IsValidAnchorRect(guiRect))
            return;

        PickerAnchorCache[key] = new PickerAnchorCacheEntry(
            guiRect,
            RectToScreenRect(guiRect),
            Event.current?.type.ToString() ?? "-",
            Time.frameCount);
    }

    private static bool TryResolvePickerAnchor(
        string key,
        Rect fallbackGuiRect,
        out Rect screenRect,
        out Rect guiRect,
        out string source)
    {
        if (!string.IsNullOrEmpty(key)
            && PickerAnchorCache.TryGetValue(key, out PickerAnchorCacheEntry cached)
            && IsValidAnchorRect(cached.ScreenRect))
        {
            screenRect = cached.ScreenRect;
            guiRect = cached.GuiRect;
            source = $"cached:{cached.EventType}@{cached.FrameCount}";
            return true;
        }

        if (IsValidAnchorRect(fallbackGuiRect))
        {
            screenRect = RectToScreenRect(fallbackGuiRect);
            guiRect = fallbackGuiRect;
            source = "fallback";
            return true;
        }

        screenRect = Rect.zero;
        guiRect = fallbackGuiRect;
        source = "none";
        return false;
    }

    private static bool IsValidAnchorRect(Rect rect)
    {
        return rect.width > U(4f)
            && rect.height > U(4f)
            && !float.IsNaN(rect.x)
            && !float.IsNaN(rect.y)
            && !float.IsNaN(rect.width)
            && !float.IsNaN(rect.height);
    }

    private static void LogInvalidPickerAnchor(string key, Rect fallbackGuiRect)
    {
#if DEBUG
        WriteWindowDiagnostics(new[]
        {
            "===== Invalid picker anchor =====",
            $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}",
            $"key={key}, fallbackGuiRect={RectText(fallbackGuiRect)}, fallbackValid={IsValidAnchorRect(fallbackGuiRect)}",
            $"cacheExists={PickerAnchorCache.ContainsKey(key)}",
            PickerAnchorCache.TryGetValue(key, out PickerAnchorCacheEntry cached)
                ? $"cachedGui={RectText(cached.GuiRect)}, cachedScreen={RectText(cached.ScreenRect)}, cachedValid={IsValidAnchorRect(cached.ScreenRect)}, cachedEvent={cached.EventType}, cachedFrame={cached.FrameCount}"
                : "cached=-",
            string.Empty,
        });
#endif
    }

    private static Rect RectToScreenRect(Rect rect)
    {
        Vector2 min = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMin, rect.yMin));
        Vector2 max = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMax, rect.yMax));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static Rect ScreenRectToGuiRect(Rect rect)
    {
        Vector2 min = GUIUtility.ScreenToGUIPoint(new Vector2(rect.xMin, rect.yMin));
        Vector2 max = GUIUtility.ScreenToGUIPoint(new Vector2(rect.xMax, rect.yMax));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static Rect RootScreenRectToGuiRect(Rect rect)
    {
        return rect;
    }

    private static bool ScreenRectContainsGuiPoint(Rect screenRect, Vector2 guiPoint)
    {
        Vector2 screenPoint = GUIUtility.GUIToScreenPoint(guiPoint);
        return screenRect.Contains(screenPoint);
    }

    private static void DrawLocalBorder(Rect rect, Color color, float thickness)
    {
        if (Event.current.type != EventType.Repaint)
            return;
        EnsureBorderTexture();
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), borderTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), borderTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), borderTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), borderTexture);
        GUI.color = oldColor;
    }

    private static void RebuildPickerOptionCache()
    {
        targetPickerCachedOptions = targetPickerOptions
            .Select(option => new PickerOption(option, PickerOptionContent(option), null))
            .ToArray();
    }

    private static PickerLayoutMetrics RootPickerLayout(float width)
    {
        Rect anchor = RootScreenRectToGuiRect(targetPickerAnchorScreenRect);
        float rowHeight = PickerRowHeight();
        float padding = U(6f);
        float titleHeight = PickerFontSize() + U(8f);
        float titleSpacing = U(4f);
        float bottomMargin = U(4f);
        int maxRows = Mathf.Max(1, Mathf.FloorToInt(U(300f) / rowHeight));
        int desiredRows = Mathf.Clamp(targetPickerOptions.Length, 1, maxRows);
        float desiredViewportHeight = desiredRows * rowHeight;
        float chromeHeight = padding * 2f + titleHeight + titleSpacing;
        float minOuterHeight = chromeHeight + rowHeight;
        float maxDownHeight = Mathf.Max(minOuterHeight, Screen.height - (anchor.yMax + U(2f)) - bottomMargin);
        float maxUpHeight = Mathf.Max(minOuterHeight, anchor.y - U(8f) - U(2f));
        bool openUp = maxDownHeight < desiredViewportHeight + chromeHeight
            && maxUpHeight > maxDownHeight;
        float availableHeight = openUp ? maxUpHeight : maxDownHeight;
        int visibleRows = Mathf.Max(1, Mathf.FloorToInt((availableHeight - chromeHeight) / rowHeight));
        int rows = Mathf.Min(visibleRows, desiredRows);
        float viewportHeight = rows * rowHeight;
        float outerHeight = viewportHeight + chromeHeight;
        float innerHeight = outerHeight - padding * 2f;
        return new PickerLayoutMetrics(
            anchor,
            rowHeight,
            padding,
            titleHeight,
            titleSpacing,
            bottomMargin,
            chromeHeight,
            desiredViewportHeight,
            minOuterHeight,
            maxDownHeight,
            maxUpHeight,
            openUp,
            availableHeight,
            visibleRows,
            desiredRows,
            rows,
            viewportHeight,
            innerHeight,
            outerHeight);
    }

    private static GUIContent PickerOptionContent(string option)
    {
        return new GUIContent(option);
    }

    private static void DrawPickerOptionOverlay(PickerOption option)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        Rect row = GUILayoutUtility.GetLastRect();
        Sprite sprite = option.Sprite ?? PickerOptionSprite(option.Value);
        GUIStyle style = PickerButtonStyle();
        float iconSize = sprite == null ? 0f : Mathf.Min(row.height - U(6f), PickerIconSize());
        float spacing = sprite == null ? 0f : U(6f);
        Vector2 textSize = style.CalcSize(option.Content);
        float totalWidth = iconSize + spacing + textSize.x;
        float x = row.x + Mathf.Max(0f, row.width - totalWidth) * 0.5f;
        if (sprite != null)
        {
            DrawSpriteInRect(sprite, new Rect(x, row.y + (row.height - iconSize) * 0.5f, iconSize, iconSize));
            x += iconSize + spacing;
        }

        GUI.Label(
            new Rect(x, row.y, textSize.x, row.height),
            option.Content,
            style);
    }

    private static GUIStyle PickerButtonStyle()
    {
        int fontSize = PickerFontSize();
        string fontKey = UiFontKey();
        if (pickerButtonStyle != null
            && pickerButtonStyleFontSize == fontSize
            && string.Equals(pickerButtonStyleFontKey, fontKey, StringComparison.Ordinal))
            return pickerButtonStyle;
        pickerButtonStyle = new GUIStyle(GUI.skin.button)
        {
            font = UiFont(),
            fontSize = fontSize
        };
        pickerButtonStyleFontSize = fontSize;
        pickerButtonStyleFontKey = fontKey;
        return pickerButtonStyle;
    }

    private static Sprite PickerOptionSprite(string option)
    {
        if (string.IsNullOrWhiteSpace(option))
            return null;

        string cacheKey = $"{targetPickerMode}:{targetPickerTargetKind}:{option}";
        if (PickerSpriteCache.TryGetValue(cacheKey, out Sprite cached))
            return cached;

        Sprite sprite = null;
        if (targetPickerMode == PickerMode.NeedsEffect || targetPickerMode == PickerMode.ExcludesEffect)
        {
            PotionEffect effect = PotionEffect.GetByName(option, returnFirst: false, warning: false);
            sprite = EffectSprite(effect);
        }
        else if (targetPickerMode == PickerMode.RequirementTarget && targetPickerTargetKind == RequirementTargetKind.Ingredient)
        {
            Ingredient ingredient = Ingredient.GetByName(option, returnFirst: false, warning: false);
            sprite = ingredient?.GetInventoryIcon() ?? ingredient?.smallIcon;
        }
        else if (targetPickerMode == PickerMode.RequirementTarget && targetPickerTargetKind == RequirementTargetKind.Base)
        {
            PotionBase potionBase = PotionBase.GetByName(option, returnFirst: false, warning: false);
            sprite = potionBase?.smallIconSprite ?? potionBase?.tooltipIconSprite ?? potionBase?.recipeMarkIcon;
        }

        PickerSpriteCache[cacheKey] = sprite;
        return sprite;
    }

    private static Sprite EffectSprite(PotionEffect effect)
    {
        if (effect == null || PotionEffectIconField == null)
            return null;

        try
        {
            object icon = PotionEffectIconField.GetValue(effect);
            if (icon == null)
                return null;

            MethodInfo defaultMethod = icon.GetType().GetMethod(
                "GetColoredIconWithDefaultColors",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object coloredIcon = defaultMethod?.Invoke(icon, null);
            Array colors = coloredIcon?.GetType()
                .GetField("iconColors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(coloredIcon) as Array;
            MethodInfo getSprite = icon.GetType().GetMethod(
                "GetSprite",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getSprite == null)
                return null;
            return getSprite.Invoke(icon, new object[] { null, colors, true, true }) as Sprite;
        }
        catch (Exception ex)
        {
            logger?.LogDebug($"Failed to resolve potion effect icon for {effect.name}: {ex.Message}");
            return null;
        }
    }

    private static void DrawSpriteInLastRect(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null || Event.current.type != EventType.Repaint)
            return;

        Rect row = GUILayoutUtility.GetLastRect();
        float size = Mathf.Min(row.height - U(6f), PickerIconSize());
        if (size <= 0f)
            return;
        Rect iconRect = new Rect(row.x + U(8f), row.y + (row.height - size) * 0.5f, size, size);
        DrawSpriteInRect(sprite, iconRect);
    }

    private static void DrawSpriteInRect(Sprite sprite, Rect iconRect)
    {
        if (sprite == null || sprite.texture == null || Event.current.type != EventType.Repaint)
            return;
        iconRect = FitSpriteRect(sprite, iconRect);
        Rect texCoords = SpriteTexCoords(sprite);
        GUI.DrawTextureWithTexCoords(iconRect, sprite.texture, texCoords, alphaBlend: true);
    }

    private static Rect FitSpriteRect(Sprite sprite, Rect bounds)
    {
        Rect spriteRect = sprite.rect;
        if (spriteRect.width <= 0f || spriteRect.height <= 0f || bounds.width <= 0f || bounds.height <= 0f)
            return bounds;

        float scale = Mathf.Min(bounds.width / spriteRect.width, bounds.height / spriteRect.height);
        float width = spriteRect.width * scale;
        float height = spriteRect.height * scale;
        return new Rect(
            bounds.x + (bounds.width - width) * 0.5f,
            bounds.y + (bounds.height - height) * 0.5f,
            width,
            height);
    }

    private static Rect SpriteTexCoords(Sprite sprite)
    {
        Rect rect = sprite.textureRect;
        Texture texture = sprite.texture;
        return new Rect(
            rect.x / texture.width,
            rect.y / texture.height,
            rect.width / texture.width,
            rect.height / texture.height);
    }

    private static void ApplyPickerOption(string option)
    {
        switch (targetPickerMode)
        {
            case PickerMode.NeedsEffect:
                SetEffectFilterValue(PickerMode.NeedsEffect, AddEffectToken(mustHaveEffectFilter, option));
                break;
            case PickerMode.ExcludesEffect:
                SetEffectFilterValue(PickerMode.ExcludesEffect, AddEffectToken(mustNotHaveEffectFilter, option));
                break;
            default:
                SetRequirementTargetValue(targetPickerRequirementName, option, updateSelection: true);
                break;
        }
    }

    private static string[] TargetOptions(
        RequirementTargetKind targetKind,
        Quest targetQuest,
        QuestRequirementInQuest requirement)
    {
        if (targetKind == RequirementTargetKind.Ingredient)
        {
            return Ingredient.allIngredients
                .Where(ingredient => ingredient != null
                    && IsIngredientUnlocked(ingredient)
                    && IsIngredientCompatibleWithRequirementTarget(requirement, ingredient, targetQuest, out _))
                .OrderBy(ingredient => ingredient.name)
                .Select(ingredient => ingredient.name)
                .ToArray();
        }

        if (targetKind == RequirementTargetKind.Base)
        {
            PotionBase[] bases = Settings<RecipeMapManagerPotionBasesSettings>.Asset?.potionBases
                ?? Array.Empty<PotionBase>();
            return bases
                .Where(potionBase => potionBase != null
                    && (targetQuest == null || potionBase.ContainsAnyEffect(targetQuest.desiredEffects, anyRotation: true)))
                .OrderBy(potionBase => potionBase.name)
                .Select(potionBase => potionBase.name)
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private static string[] CachedTargetOptions(
        RequirementTargetKind targetKind,
        Quest targetQuest,
        QuestRequirementInQuest requirement)
    {
        string key = string.Join(
            "|",
            targetKind.ToString(),
            targetQuest?.name ?? "-",
            requirement?.requirement?.name ?? "-",
            PreviewChapter().ToString(),
            StrictPlanningMode().ToString());
        if (TargetOptionsCache.TryGetValue(key, out string[] cached))
            return cached;

        string[] options = TargetOptions(targetKind, targetQuest, requirement);
        TargetOptionsCache[key] = options;
        return options;
    }

    private static bool IsIngredientUnlocked(Ingredient ingredient)
    {
        return ingredient != null && PreviewChapter() >= ingredient.chapter;
    }

    private static bool IsIngredientCompatibleWithRequirementTarget(
        QuestRequirementInQuest requirement,
        Ingredient ingredient,
        Quest targetQuest,
        out string reason)
    {
        reason = string.Empty;
        if (requirement?.requirement is not QuestRequirementCertainIngredient certainIngredient
            || ingredient == null
            || targetQuest == null)
        {
            return true;
        }

        string cacheKey = string.Join(
            "|",
            requirement.requirement.name,
            ingredient.name,
            targetQuest.name,
            PreviewChapter().ToString());
        if (IngredientCompatibilityCache.TryGetValue(cacheKey, out bool cached))
        {
            if (!cached)
                reason = $"Target ingredient {ingredient.name} is incompatible with {requirement.requirement.name} for quest {targetQuest.name}.";
            return cached;
        }

        bool checkPotential = GetPrivateBool(
            certainIngredient,
            IngredientCheckPotentialField,
            fallback: true);
        if (!checkPotential)
            return true;

        if (IngredientMatchesCertainIngredientRule(certainIngredient, ingredient, targetQuest, out reason))
        {
            IngredientCompatibilityCache[cacheKey] = true;
            return true;
        }

        IngredientCompatibilityCache[cacheKey] = false;
        return false;
    }

    private static bool IngredientMatchesCertainIngredientRule(
        QuestRequirementCertainIngredient certainIngredient,
        Ingredient ingredient,
        Quest targetQuest,
        out string reason)
    {
        reason = string.Empty;
        if (certainIngredient == null || ingredient == null || targetQuest == null)
            return true;

        bool checkPotential = GetPrivateBool(
            certainIngredient,
            IngredientCheckPotentialField,
            fallback: true);
        if (!checkPotential)
            return true;

        int chapter = PreviewChapter();
        ElementType[] dominantElements = EnabledDominantElements(targetQuest, chapter);
        if (dominantElements.Length == 0)
        {
            reason = $"Target ingredient {ingredient.name} cannot be validated because the target quest has no enabled effects for chapter {chapter}.";
            return false;
        }

        bool shortDistancePotential = GetPrivateBool(
            certainIngredient,
            IngredientShortDistancePotentialField,
            fallback: false);
        float potentialThreshold = GetPrivateFloat(
            certainIngredient,
            IngredientPotentialThresholdField,
            fallback: 0.2f);
        ElementalPotential elementalPotential = ingredient.GetElementalPotential(shortDistancePotential);
        if (dominantElements.Any(element => elementalPotential.GetPotential(element) >= potentialThreshold))
            return true;

        reason = $"Target ingredient {ingredient.name} does not match the elemental potential required by {certainIngredient.name} for quest {targetQuest.name}.";
        return false;
    }

    private static ElementType[] EnabledDominantElements(Quest targetQuest, int chapter)
    {
        return (targetQuest?.desiredEffects ?? Array.Empty<PotionEffect>())
            .Where(effect => effect != null && effect.IsEnabledForChapter(chapter))
            .Select(effect => effect.elementalPotential.GetDominantElementType())
            .Distinct()
            .ToArray();
    }

    private static bool GetPrivateBool(object instance, FieldInfo field, bool fallback)
    {
        try
        {
            return field != null && field.GetValue(instance) is bool value ? value : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static float GetPrivateFloat(object instance, FieldInfo field, float fallback)
    {
        try
        {
            return field != null && field.GetValue(instance) is float value ? value : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static GUISkin GetWindowSkin()
    {
        int fontSize = Mathf.Clamp(uiFontSize?.Value ?? 16, 10, 30);
        string fontKey = UiFontKey();
        if (windowSkin != null && windowSkinFontSize == fontSize && string.Equals(windowSkinFontKey, fontKey, StringComparison.Ordinal))
            return windowSkin;

        windowSkin = UnityEngine.Object.Instantiate(GUI.skin);
        windowSkinFontSize = fontSize;
        windowSkinFontKey = fontKey;
        Font font = UiFont();
        ApplyFont(windowSkin.label, font, fontSize);
        ApplyFont(windowSkin.button, font, fontSize);
        ApplyFont(windowSkin.toggle, font, fontSize);
        ApplyFont(windowSkin.textField, font, fontSize);
        ApplyFont(windowSkin.textArea, font, fontSize);
        ApplyFont(windowSkin.box, font, fontSize);
        ApplyFont(windowSkin.window, font, fontSize);
        ApplyWindowTitleMetrics(windowSkin.window, fontSize);
        ApplyBackground(windowSkin.window, ref windowBackgroundTexture, new Color(0.08f, 0.07f, 0.05f, 0.94f));
        ApplyBackground(windowSkin.box, ref boxBackgroundTexture, new Color(0.10f, 0.09f, 0.07f, 0.86f));
        pickerButtonStyle = null;
        windowTitleStyle = null;
        noWrapLabelStyle = null;
        ColoredButtonStyles.Clear();
        return windowSkin;
    }

    private static void ApplyFont(GUIStyle style, Font font, int fontSize)
    {
        if (style != null)
        {
            if (font != null)
                style.font = font;
            style.fontSize = fontSize;
        }
    }

    private static void ApplyWindowTitleMetrics(GUIStyle style, int fontSize)
    {
        if (style == null)
            return;

        int titleHeight = Mathf.CeilToInt(WindowTitleHeight(fontSize));
        style.padding = new RectOffset(
            style.padding.left,
            style.padding.right,
            Mathf.Max(style.padding.top, titleHeight),
            style.padding.bottom);
        style.contentOffset = Vector2.zero;
    }

    private static GUIStyle WindowTitleStyle()
    {
        int fontSize = Mathf.Clamp(uiFontSize?.Value ?? 16, 10, 30);
        string fontKey = UiFontKey();
        if (windowTitleStyle != null
            && windowTitleStyleFontSize == fontSize
            && string.Equals(windowTitleStyleFontKey, fontKey, StringComparison.Ordinal))
        {
            return windowTitleStyle;
        }

        windowTitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            font = UiFont(),
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            clipping = TextClipping.Clip,
            wordWrap = false
        };
        windowTitleStyle.normal.textColor = Color.white;
        windowTitleStyleFontSize = fontSize;
        windowTitleStyleFontKey = fontKey;
        return windowTitleStyle;
    }

    private static GUIStyle NoWrapLabelStyle()
    {
        int fontSize = Mathf.Clamp(uiFontSize?.Value ?? 16, 10, 30);
        string fontKey = UiFontKey();
        if (noWrapLabelStyle != null
            && noWrapLabelStyleFontSize == fontSize
            && string.Equals(noWrapLabelStyleFontKey, fontKey, StringComparison.Ordinal))
        {
            return noWrapLabelStyle;
        }

        noWrapLabelStyle = new GUIStyle(GUI.skin.label)
        {
            font = UiFont(),
            fontSize = fontSize,
            clipping = TextClipping.Clip,
            wordWrap = false
        };
        noWrapLabelStyleFontSize = fontSize;
        noWrapLabelStyleFontKey = fontKey;
        return noWrapLabelStyle;
    }

    private static float WindowTitleHeight()
    {
        return WindowTitleHeight(uiFontSize?.Value ?? 16);
    }

    private static float WindowTitleHeight(int fontSize)
    {
        return Mathf.Max(U(24f), fontSize + U(12f));
    }

    private static Font UiFont()
    {
        string fontKey = UiFontKey();
        if (windowFont != null && string.Equals(windowFontKey, fontKey, StringComparison.Ordinal))
            return windowFont;

        string[] names = (uiFontNames?.Value ?? string.Empty)
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        if (names.Length == 0)
        {
            windowFont = null;
            windowFontKey = fontKey;
            return null;
        }

        windowFont = Font.CreateDynamicFontFromOSFont(names, Mathf.Max(uiFontSize?.Value ?? 16, pickerFontSize?.Value ?? 16));
        windowFontKey = fontKey;
        return windowFont;
    }

    private static string UiFontKey()
    {
        return uiFontNames?.Value ?? string.Empty;
    }

    private static float UiScale()
    {
        return Mathf.Clamp((uiFontSize?.Value ?? 16) / 16f, 0.75f, 1.8f);
    }

    private static float U(float value)
    {
        return Mathf.Round(value * UiScale());
    }

    private static GUILayoutOption Width(float value)
    {
        return GUILayout.Width(U(value));
    }

    private static GUILayoutOption Height(float value)
    {
        return GUILayout.Height(U(value));
    }

    private static float FilterLabelWidth()
    {
        return 115f;
    }

    private static float EffectFilterLabelWidth()
    {
        return 155f;
    }

    private static float RequirementNameColumnWidth()
    {
        return 272f;
    }

    private static float RequirementSelectionButtonWidth()
    {
        return 82f;
    }

    private static float RequirementTargetLabelWidth()
    {
        return 80f;
    }

    private static float RequirementTargetIconColumnWidth()
    {
        return 24f;
    }

    private static float RequirementTargetValueWidth()
    {
        return 260f;
    }

    private static float RequirementTargetPickerButtonWidth()
    {
        return 40f;
    }

    private static float RequirementTargetClearButtonWidth()
    {
        return 70f;
    }

    private static float RowHeight()
    {
        return Mathf.Max(U(28f), (uiFontSize?.Value ?? 16) + U(12f));
    }

    private static int PickerFontSize()
    {
        return Mathf.Clamp(pickerFontSize?.Value ?? uiFontSize?.Value ?? 16, 10, 36);
    }

    private static float PickerIconSize()
    {
        return Mathf.Clamp(PickerFontSize() + U(6f), 10, 72);
    }

    private static float InlineIconSize()
    {
        return Mathf.Clamp((uiFontSize?.Value ?? 16) + U(4f), 10, 64);
    }

    private static float InlineIconSpacing()
    {
        return Mathf.Clamp((uiFontSize?.Value ?? 16) * 0.25f, 2f, 12f);
    }

    private static float PickerRowHeight()
    {
        return Mathf.Max(PickerIconSize() + U(8f), PickerFontSize() + U(12f));
    }

    private static void EnsureWindowFitsFont()
    {
        windowRect.width = Mathf.Max(windowRect.width, U(1720f));
        windowRect.height = Mathf.Max(windowRect.height, U(820f));
        randomRuleWindowRect.width = Mathf.Max(randomRuleWindowRect.width, U(860f));
        randomRuleWindowRect.height = Mathf.Max(randomRuleWindowRect.height, U(680f));
    }

    private static void ApplyBackground(GUIStyle style, ref Texture2D texture, Color color)
    {
        if (style == null)
            return;
        if (texture == null)
        {
            texture = new Texture2D(1, 1, TextureFormat.ARGB32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        }

        style.normal.background = texture;
        style.onNormal.background = texture;
        style.focused.background = texture;
        style.onFocused.background = texture;
        style.active.background = texture;
        style.onActive.background = texture;
    }

    private static void DrawWindowBorder(Rect rect, bool focused)
    {
        EnsureBorderTexture();
        Color oldColor = GUI.color;
        GUI.color = focused ? new Color(0.86f, 0.72f, 0.46f, 1f) : new Color(0.95f, 0.84f, 0.58f, 1f);
        DrawRectOutline(rect, 3f);
        GUI.color = new Color(0.16f, 0.10f, 0.04f, 1f);
        DrawRectOutline(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f), 1f);
        GUI.color = oldColor;
    }

    private static void DrawRectOutline(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), borderTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), borderTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), borderTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), borderTexture);
    }

    private static void EnsureBorderTexture()
    {
        if (borderTexture != null)
            return;
        borderTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        borderTexture.SetPixel(0, 0, Color.white);
        borderTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
    }

    private static void DrawHoverTooltip()
    {
        if (string.IsNullOrEmpty(hoverTooltip))
            return;

        Vector2 mouse = Event.current.mousePosition;
        GUIContent content = new GUIContent(hoverTooltip);
        GUIStyle style = GUI.skin.box;
        float width = Mathf.Min(560f, Mathf.Max(320f, style.CalcSize(content).x + 24f));
        float height = Mathf.Min(U(420f), style.CalcHeight(content, width) + U(20f));
        Rect rect = new Rect(mouse.x + 18f, mouse.y + 18f, width, height);
        rect.x = Mathf.Min(rect.x, Screen.width - rect.width - 8f);
        rect.y = Mathf.Min(rect.y, Screen.height - rect.height - 8f);
        GUI.Box(rect, content);
        DrawLocalBorder(rect, new Color(0.95f, 0.78f, 0.45f, 1f), U(2f));
    }

    private static List<PlannedRequirement> SelectedRequirements(RequirementSelection selection)
    {
        List<PlannedRequirement> result = new List<PlannedRequirement>();
        RequirementLimitInfo limits = CurrentRequirementLimits();
        if (selection == RequirementSelection.Optional && limits.MaxOptional == 0)
            return result;
        if (selection == RequirementSelection.Mandatory && limits.MaxMandatory == 0)
            return result;

        foreach (KeyValuePair<string, RequirementSelection> pair in RequirementSelections.Where(pair => pair.Value == selection))
        {
            QuestRequirementInQuest requirement = cachedRequirements.FirstOrDefault(item =>
                string.Equals(item.requirement.name, pair.Key, StringComparison.OrdinalIgnoreCase));
            RequirementTargets.TryGetValue(pair.Key, out string target);
            result.Add(new PlannedRequirement(
                pair.Key,
                TryGetRequirementTargetInfo(requirement, out RequirementTargetInfo targetInfo) && targetInfo.Editable
                    ? target
                    : null));
        }

        return result;
    }

    private static bool IsSelectedRequirementCountAllowed(Quest targetQuest, out string reason)
    {
        if (!AreSelectedRequirementsAvailable(targetQuest, out reason))
            return false;

        RequirementLimitInfo limits = CurrentRequirementLimits();
        int mandatory = SelectedRequirementCount(RequirementSelection.Mandatory);
        int optional = SelectedRequirementCount(RequirementSelection.Optional);
        if (mandatory > limits.MaxMandatory)
        {
            reason = $"Too many mandatory requirements selected ({mandatory}; allowed {limits.MandatoryRangeText}).";
            return false;
        }

        if (optional > limits.MaxOptional)
        {
            reason = $"Too many optional requirements selected ({optional}; allowed {limits.OptionalRangeText}).";
            return false;
        }

        if (mandatory + optional > limits.MaxTotal)
        {
            reason = $"Too many total requirements selected ({mandatory + optional}; allowed {limits.TotalRangeText}).";
            return false;
        }

        if (mandatory < limits.MinMandatory)
        {
            reason = $"Too few mandatory requirements selected ({mandatory}; allowed {limits.MandatoryRangeText}).";
            return false;
        }

        if (optional < limits.MinOptional)
        {
            reason = $"Too few optional requirements selected ({optional}; allowed {limits.OptionalRangeText}).";
            return false;
        }

        if (mandatory + optional < limits.MinTotal)
        {
            reason = $"Too few total requirements selected ({mandatory + optional}; allowed {limits.TotalRangeText}).";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool AreSelectedRequirementsAvailable(Quest targetQuest, out string reason)
    {
        reason = string.Empty;
        foreach (KeyValuePair<string, RequirementSelection> pair in RequirementSelections
            .Where(pair => pair.Value != RequirementSelection.None))
        {
            QuestRequirementInQuest requirement = FindRequirement(pair.Key);
            if (!IsRequirementAvailableForPlanning(requirement, out reason))
                return false;

            if (!IsSelectedRequirementTargetAvailable(requirement, pair.Key, targetQuest, out reason))
                return false;
        }

        return true;
    }

    private static QuestRequirementInQuest FindRequirement(string requirementName)
    {
        return cachedRequirements.FirstOrDefault(item =>
            item?.requirement != null
            && string.Equals(item.requirement.name, requirementName, StringComparison.OrdinalIgnoreCase))
            ?? QuestRequirementInQuest.GetByName(requirementName, returnFirst: false, warning: false);
    }

    private static bool IsRequirementAvailableForPlanning(
        QuestRequirementInQuest requirement,
        out string reason)
    {
        reason = string.Empty;
        if (requirement?.requirement == null)
        {
            reason = "Selected requirement is missing.";
            return false;
        }

        int chapter = PreviewChapter();
        int unlockChapter;
        try
        {
            unlockChapter = requirement.requirement.GetChapterToUnlock();
        }
        catch
        {
            unlockChapter = 1;
        }

        if (chapter < unlockChapter)
        {
            reason = $"Requirement {requirement.requirement.name} unlocks at chapter {unlockChapter}.";
            return false;
        }

        if (requirement.ingredient != null && !IsIngredientUnlocked(requirement.ingredient))
        {
            reason = $"Requirement {requirement.requirement.name} uses locked ingredient {requirement.ingredient.name}.";
            return false;
        }

        return true;
    }

    private static bool IsSelectedRequirementTargetAvailable(
        QuestRequirementInQuest requirement,
        string requirementName,
        Quest targetQuest,
        out string reason)
    {
        reason = string.Empty;
        if (!TryGetRequirementTargetInfo(requirement, out RequirementTargetInfo targetInfo) || !targetInfo.Editable)
            return true;

        RequirementTargets.TryGetValue(requirementName, out string target);
        if (string.IsNullOrWhiteSpace(target))
            return true;

        if (targetInfo.Kind == RequirementTargetKind.Ingredient)
        {
            Ingredient ingredient = Ingredient.GetByName(target, returnFirst: false, warning: false);
            if (ingredient == null || !IsIngredientUnlocked(ingredient))
            {
                reason = $"Target ingredient is not unlocked for the current chapter: {target}";
                return false;
            }

            if (!IsIngredientCompatibleWithRequirementTarget(requirement, ingredient, targetQuest, out reason))
                return false;
        }
        else if (targetInfo.Kind == RequirementTargetKind.Base)
        {
            PotionBase potionBase = PotionBase.GetByName(target, returnFirst: false, warning: false);
            if (potionBase == null)
            {
                reason = $"Target base not found: {target}";
                return false;
            }
        }

        return true;
    }

    private static int SelectedRequirementCount(RequirementSelection selection)
    {
        return RequirementSelections.Count(pair => pair.Value == selection);
    }

    private static int SelectedRequirementTotal()
    {
        return RequirementSelections.Count(pair => pair.Value != RequirementSelection.None);
    }

    private static void DrawRequirementSelectionButton(
        string requirementName,
        RequirementSelection current,
        RequirementSelection target,
        string label,
        RequirementLimitInfo limits)
    {
        Color oldContentColor = GUI.contentColor;
        bool oldEnabled = GUI.enabled;
        if (!CanSelectRequirementState(requirementName, current, target, limits, resetConflicts: true))
            GUI.enabled = false;
        string buttonLabel = label;
        GUIStyle style;
        if (current == target)
        {
            GUI.contentColor = Color.white;
            buttonLabel = "● " + label;
            style = ColoredButtonStyle(SelectionColor(target));
        }
        else
        {
            GUI.contentColor = new Color(0.82f, 0.82f, 0.82f, oldContentColor.a);
            style = GUI.skin.button;
        }

        if (GUILayout.Button(buttonLabel, style, Width(RequirementSelectionButtonWidth())))
            SetRequirementSelection(requirementName, target, resetConflicts: true);
        GUI.enabled = oldEnabled;
        GUI.contentColor = oldContentColor;
    }

    private static bool CanSelectRequirementState(
        string requirementName,
        RequirementSelection current,
        RequirementSelection target,
        RequirementLimitInfo limits,
        bool resetConflicts)
    {
        if (target == RequirementSelection.None)
            return true;
        if (target == RequirementSelection.Optional && limits.MaxOptional == 0)
            return false;
        if (target == RequirementSelection.Mandatory && limits.MaxMandatory == 0)
            return false;
        if (current == target)
            return true;

        int mandatory = 0;
        int optional = 0;
        foreach (KeyValuePair<string, RequirementSelection> pair in RequirementSelections)
        {
            if (pair.Value == RequirementSelection.None)
                continue;
            if (string.Equals(pair.Key, requirementName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (resetConflicts && AreRequirementsMutuallyExclusive(requirementName, pair.Key))
                continue;

            if (pair.Value == RequirementSelection.Mandatory)
                mandatory++;
            else if (pair.Value == RequirementSelection.Optional)
                optional++;
        }

        if (target == RequirementSelection.Mandatory)
            mandatory++;
        else if (target == RequirementSelection.Optional)
            optional++;

        return mandatory <= limits.MaxMandatory
            && optional <= limits.MaxOptional
            && mandatory + optional <= limits.MaxTotal;
    }

    private static Color SelectionColor(RequirementSelection selection)
    {
        switch (selection)
        {
            case RequirementSelection.Mandatory:
                return ConfiguredColor(mustButtonColor, new Color(0.56f, 0.29f, 0.19f, 1f));
            case RequirementSelection.Optional:
                return ConfiguredColor(canButtonColor, new Color(0.31f, 0.54f, 0.23f, 1f));
            default:
                return ConfiguredColor(noneButtonColor, new Color(0.33f, 0.33f, 0.33f, 1f));
        }
    }

    private static Color ConfiguredColor(ConfigEntry<string> entry, Color fallback)
    {
        if (entry != null
            && !string.IsNullOrWhiteSpace(entry.Value)
            && ColorUtility.TryParseHtmlString(entry.Value.Trim(), out Color color))
        {
            color.a = Mathf.Max(color.a, 0.75f);
            return color;
        }

        return fallback;
    }

    private static GUIStyle CustomerSelectedButtonStyle()
    {
        return ColoredButtonStyle(ConfiguredColor(customerSelectedColor, new Color(0.30f, 0.46f, 0.72f, 1f)));
    }

    private static GUIStyle ColoredButtonStyle(Color color)
    {
        string key = $"{windowSkinFontSize}:{UiFontKey()}:{ColorUtility.ToHtmlStringRGBA(color)}";
        if (ColoredButtonStyles.TryGetValue(key, out GUIStyle style))
            return style;

        style = new GUIStyle(GUI.skin.button);
        Font font = UiFont();
        if (font != null)
            style.font = font;
        Texture2D texture = ColorTexture(color);
        style.normal.background = texture;
        style.hover.background = texture;
        style.active.background = texture;
        style.focused.background = texture;
        style.onNormal.background = texture;
        style.onHover.background = texture;
        style.onActive.background = texture;
        style.onFocused.background = texture;
        style.normal.textColor = Color.white;
        style.hover.textColor = Color.white;
        style.active.textColor = Color.white;
        style.focused.textColor = Color.white;
        ColoredButtonStyles[key] = style;
        return style;
    }

    private static Texture2D ColorTexture(Color color)
    {
        string key = ColorUtility.ToHtmlStringRGBA(color);
        if (ColorTextures.TryGetValue(key, out Texture2D texture))
            return texture;

        texture = new Texture2D(1, 1, TextureFormat.ARGB32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, color);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        ColorTextures[key] = texture;
        return texture;
    }


    private static string EffectFilterField(string label, string value, PickerMode pickerMode)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, NoWrapLabelStyle(), Width(EffectFilterLabelWidth()));
        string anchorKey = EffectAnchorKey(pickerMode);
        bool open = GUILayout.Button(T("filters.selectEffect"), Width(180f));
        Rect buttonRect = GUILayoutUtility.GetLastRect();
        CachePickerAnchor(anchorKey, buttonRect);
        if (open)
            OpenEffectPicker(pickerMode, anchorKey, buttonRect);
        string result = value ?? string.Empty;
        result = GUILayout.TextField(result, Width(420f));
        if (GUILayout.Button(T("target.clear"), Width(55f)))
            result = string.Empty;
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        SetEffectFilterValue(pickerMode, result);
        if (pickerMode == PickerMode.NeedsEffect)
            return mustHaveEffectFilter;
        if (pickerMode == PickerMode.ExcludesEffect)
            return mustNotHaveEffectFilter;
        return result;
    }

    private static void DrawEffectFilters()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(T("filters.title"));
        mustHaveEffectFilter = EffectFilterField(T("filters.needs"), mustHaveEffectFilter, PickerMode.NeedsEffect);
        mustNotHaveEffectFilter = EffectFilterField(T("filters.excludes"), mustNotHaveEffectFilter, PickerMode.ExcludesEffect);
        DrawEffectTokenIconRow(T("filters.needsAll"), mustHaveEffectFilter);
        DrawEffectTokenIconRow(T("filters.excludesAny"), mustNotHaveEffectFilter);
        GUILayout.Label(T("filters.searchHint"));
        GUILayout.EndVertical();
    }

    private static void SetEffectFilterValue(PickerMode pickerMode, string value)
    {
        string normalized = NormalizeEffectFilterText(value);
        if (pickerMode == PickerMode.NeedsEffect)
        {
            mustHaveEffectFilter = normalized;
            mustNotHaveEffectFilter = RemoveEffectTokens(mustNotHaveEffectFilter, EffectFilterTokens(normalized));
        }
        else if (pickerMode == PickerMode.ExcludesEffect)
        {
            mustNotHaveEffectFilter = normalized;
            mustHaveEffectFilter = RemoveEffectTokens(mustHaveEffectFilter, EffectFilterTokens(normalized));
        }
    }

    private static string DisplayFilterTokens(string value)
    {
        string[] tokens = EffectFilterTokens(value);
        return tokens.Length == 0 ? "-" : string.Join(", ", tokens);
    }

    private static void OpenEffectPicker(PickerMode pickerMode, string anchorKey, Rect buttonRect)
    {
        if (!TryResolvePickerAnchor(anchorKey, buttonRect, out Rect anchorScreenRect, out Rect anchorGuiRect, out string anchorSource))
        {
            actionStatus = $"Cannot open effect picker: missing valid anchor for {pickerMode}.";
            LogInvalidPickerAnchor(anchorKey, buttonRect);
            return;
        }

        targetPickerMode = pickerMode;
        targetPickerTargetKind = RequirementTargetKind.None;
        targetPickerRequirementName = string.Empty;
        targetPickerTitle = pickerMode == PickerMode.NeedsEffect ? "Add needed effect" : "Add excluded effect";
        targetPickerOptions = PotionEffect.allPotionEffects
            .Where(effect => effect != null)
            .OrderBy(effect => effect.name)
            .Select(effect => effect.name)
            .ToArray();
        RebuildPickerOptionCache();
        targetPickerScroll = Vector2.zero;
        targetPickerOpen = true;
        targetPickerAnchorKey = anchorKey;
        targetPickerAnchorSource = anchorSource;
        targetPickerFallbackGuiRect = buttonRect;
        targetPickerAnchorGuiRect = anchorGuiRect;
        targetPickerAnchorScreenRect = anchorScreenRect;
    }

    private static void ClosePicker()
    {
        targetPickerOpen = false;
        targetPickerAnchorKey = string.Empty;
        targetPickerAnchorSource = string.Empty;
        targetPickerFallbackGuiRect = Rect.zero;
        targetPickerAnchorGuiRect = Rect.zero;
        targetPickerAnchorScreenRect = Rect.zero;
        targetPickerOptions = Array.Empty<string>();
        targetPickerCachedOptions = Array.Empty<PickerOption>();
        targetPickerTargetKind = RequirementTargetKind.None;
    }

    private static string AddEffectToken(string value, string token)
    {
        List<string> tokens = EffectFilterTokens(value).ToList();
        if (!tokens.Any(existing => string.Equals(existing, token, StringComparison.OrdinalIgnoreCase)))
            tokens.Add(token);
        return string.Join(", ", tokens.ToArray());
    }

    private static string NormalizeEffectFilterText(string value)
    {
        return string.Join(", ", EffectFilterTokens(value).ToArray());
    }

    private static string RemoveEffectTokens(string value, IEnumerable<string> tokensToRemove)
    {
        HashSet<string> removals = new HashSet<string>(
            tokensToRemove ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        if (removals.Count == 0)
            return NormalizeEffectFilterText(value);
        return string.Join(
            ", ",
            EffectFilterTokens(value)
                .Where(token => !removals.Contains(token))
                .ToArray());
    }

    private static void SetRequirementTargetValue(
        string requirementName,
        string value,
        bool updateSelection)
    {
        string normalized = value ?? string.Empty;
        bool hadPreviousTarget = RequirementTargets.TryGetValue(requirementName, out string previousTarget);
        RequirementTargets[requirementName] = normalized;
        if (!updateSelection)
            return;

        RequirementSelections.TryGetValue(requirementName, out RequirementSelection currentSelection);
        RequirementSelection nextSelection;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            nextSelection = RequirementSelection.None;
        }
        else if (currentSelection == RequirementSelection.Mandatory
            || (currentSelection == RequirementSelection.Optional && CurrentRequirementLimits().MaxOptional > 0))
        {
            nextSelection = currentSelection;
        }
        else
        {
            nextSelection = TargetFilledSelection();
        }

        bool selectionApplied = SetRequirementSelection(
            requirementName,
            nextSelection,
            resetConflicts: true);
        if (!selectionApplied)
        {
            if (hadPreviousTarget)
                RequirementTargets[requirementName] = previousTarget;
            else
                RequirementTargets.Remove(requirementName);
        }
    }

    private static bool SetRequirementSelection(
        string requirementName,
        RequirementSelection selection,
        bool resetConflicts)
    {
        selection = NormalizeSelectionForRequirementLimits(selection);
        RequirementSelections.TryGetValue(requirementName, out RequirementSelection current);
        if (!CanSelectRequirementState(requirementName, current, selection, CurrentRequirementLimits(), resetConflicts))
        {
            searchStatus = $"Cannot select {requirementName}: selected requirement count would exceed the current limit.";
            return false;
        }

        RequirementSelections[requirementName] = selection;
        if (selection == RequirementSelection.None)
        {
            RequirementTargets.Remove(requirementName);
            return true;
        }

        if (!resetConflicts || selection == RequirementSelection.None)
            return true;

        foreach (string otherName in RequirementSelections
            .Where(pair => pair.Value != RequirementSelection.None
                && !string.Equals(pair.Key, requirementName, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Key)
            .ToArray())
        {
            if (AreRequirementsMutuallyExclusive(requirementName, otherName))
                ResetRequirementSelection(otherName);
        }

        return true;
    }

    private static void ResetRequirementSelection(string requirementName)
    {
        RequirementSelections[requirementName] = RequirementSelection.None;
        RequirementTargets.Remove(requirementName);
    }

    private static bool AreRequirementsMutuallyExclusive(string firstName, string secondName)
    {
        GeneratedQuestRequirement first = GeneratedPlaceholder(firstName);
        GeneratedQuestRequirement second = GeneratedPlaceholder(secondName);
        if (first?.requirementInQuest?.requirement == null
            || second?.requirementInQuest?.requirement == null)
        {
            return false;
        }

        if (AreRequirementTagsMutuallyExclusive(
            first.requirementInQuest.requirement,
            second.requirementInQuest.requirement))
        {
            return true;
        }

        return !first.requirementInQuest.requirement.IsCompatibleWithOtherRequirements(
                new List<GeneratedQuestRequirement> { second })
            || !second.requirementInQuest.requirement.IsCompatibleWithOtherRequirements(
                new List<GeneratedQuestRequirement> { first });
    }

    private static bool AreRequirementTagsMutuallyExclusive(
        QuestRequirement first,
        QuestRequirement second)
    {
        if (!RequirementTargetMetadataResolver.TryGetRequirementTags(first, out RequirementTagsMetadata firstMetadata)
            || !RequirementTargetMetadataResolver.TryGetRequirementTags(second, out RequirementTagsMetadata secondMetadata))
        {
            return false;
        }

        return Intersects(firstMetadata.ConflictingTags, secondMetadata.Tags)
            || Intersects(secondMetadata.ConflictingTags, firstMetadata.Tags);
    }

    private static bool Intersects(string[] left, string[] right)
    {
        return left.Length > 0
            && right.Length > 0
            && left.Intersect(right, StringComparer.OrdinalIgnoreCase).Any();
    }

    private static GeneratedQuestRequirement GeneratedPlaceholder(string requirementName)
    {
        QuestRequirementInQuest source = cachedRequirements.FirstOrDefault(item =>
            item?.requirement != null
            && string.Equals(item.requirement.name, requirementName, StringComparison.OrdinalIgnoreCase))
            ?? QuestRequirementInQuest.GetByName(requirementName, returnFirst: false, warning: false);
        if (source == null)
            return null;

        RequirementTargets.TryGetValue(requirementName, out string target);
        PlannedRequirement planned = new PlannedRequirement(requirementName, target);
        QuestRequirementInQuest wrapper = planned.CreateWrapper(source);
        GeneratedQuestRequirement generated = new GeneratedQuestRequirement(wrapper);
        if (wrapper.ingredient != null)
            generated.stringValue1 = wrapper.ingredient.name;
        else if (wrapper.potionBase != null)
            generated.stringValue1 = wrapper.potionBase.name;
        return generated;
    }

    private static RequirementSelection TargetFilledSelection()
    {
        RequirementLimitInfo limits = CurrentRequirementLimits();
        if (limits.MaxMandatory == 0 && limits.MaxOptional > 0)
            return RequirementSelection.Optional;
        if (limits.MaxOptional == 0 && limits.MaxMandatory > 0)
            return RequirementSelection.Mandatory;
        if (limits.MaxMandatory == 0 && limits.MaxOptional == 0)
            return RequirementSelection.None;

        return limits.MaxOptional > 0
            && targetFilledSelection?.Value == TargetFilledSelectionMode.Can
            ? RequirementSelection.Optional
            : RequirementSelection.Mandatory;
    }

    private static void NormalizeOptionalSelectionsForCurrentMode()
    {
        foreach (string requirementName in RequirementSelections.Keys.ToArray())
        {
            if (!IsRequirementAvailableForPlanning(FindRequirement(requirementName), out _))
                ResetRequirementSelection(requirementName);
        }

        foreach (string requirementName in RequirementSelections.Keys.ToArray())
        {
            RequirementSelection normalized = NormalizeSelectionForRequirementLimits(RequirementSelections[requirementName]);
            if (normalized == RequirementSelection.None)
                ResetRequirementSelection(requirementName);
            else
                RequirementSelections[requirementName] = normalized;
        }
    }

    private static RequirementSelection NormalizeSelectionForRequirementLimits(RequirementSelection selection)
    {
        if (selection == RequirementSelection.None)
            return RequirementSelection.None;

        RequirementLimitInfo limits = CurrentRequirementLimits();
        if (selection == RequirementSelection.Optional && limits.MaxOptional == 0)
            return limits.MaxMandatory > 0 ? RequirementSelection.Mandatory : RequirementSelection.None;
        if (selection == RequirementSelection.Mandatory && limits.MaxMandatory == 0)
            return limits.MaxOptional > 0 ? RequirementSelection.Optional : RequirementSelection.None;
        return selection;
    }

    private static RequirementLimitInfo CurrentRequirementLimits()
    {
        if (!StrictPlanningMode())
            return new RequirementLimitInfo(
                minMandatory: 0,
                maxMandatory: 4,
                minOptional: 0,
                maxOptional: 4,
                minTotal: 0,
                maxTotal: 4);

        try
        {
            QuestRequirementDifficultySettings settings =
                Settings<GameDifficultyQuestRequirements>.Asset?.GetCurrentValue();
            if (settings == null)
                return new RequirementLimitInfo(
                    minMandatory: 0,
                    maxMandatory: 4,
                    minOptional: 0,
                    maxOptional: 4,
                    minTotal: 0,
                    maxTotal: 4);

            int chapter = PreviewChapter();
            RequirementSlotRange nativeMandatory =
                PossibleRequirementSlots(settings.GetSpawnChances(chapter, isMandatoryRequirements: true));
            RequirementSlotRange nativeOptional =
                PossibleRequirementSlots(settings.GetSpawnChances(chapter, isMandatoryRequirements: false));
            QuestRequirementsModeConversionType mode = settings.GetModeConversionType();
            int minTotal = nativeMandatory.Min + nativeOptional.Min;
            int maxTotal = nativeMandatory.Max + nativeOptional.Max;
            if (mode == QuestRequirementsModeConversionType.ConvertAllToMandatory)
            {
                return new RequirementLimitInfo(
                    minMandatory: minTotal,
                    maxMandatory: maxTotal,
                    minOptional: 0,
                    maxOptional: 0,
                    minTotal: minTotal,
                    maxTotal: maxTotal);
            }

            if (mode == QuestRequirementsModeConversionType.ConvertAllToOptional)
            {
                return new RequirementLimitInfo(
                    minMandatory: 0,
                    maxMandatory: 0,
                    minOptional: minTotal,
                    maxOptional: maxTotal,
                    minTotal: minTotal,
                    maxTotal: maxTotal);
            }

            return new RequirementLimitInfo(
                minMandatory: nativeMandatory.Min,
                maxMandatory: nativeMandatory.Max,
                minOptional: nativeOptional.Min,
                maxOptional: nativeOptional.Max,
                minTotal: minTotal,
                maxTotal: maxTotal);
        }
        catch
        {
            return new RequirementLimitInfo(
                minMandatory: 0,
                maxMandatory: 4,
                minOptional: 0,
                maxOptional: 4,
                minTotal: 0,
                maxTotal: 4);
        }
    }

    private static RequirementSlotRange PossibleRequirementSlots((int, int) spawnChances)
    {
        if (spawnChances.Item1 <= 0)
            return new RequirementSlotRange(min: 0, max: 0);

        int max = spawnChances.Item2 > 0 ? 2 : 1;
        int min = spawnChances.Item1 >= 100 ? 1 : 0;
        if (min == 1 && spawnChances.Item2 >= 100)
            min = 2;
        return new RequirementSlotRange(min, max);
    }

    private static bool StrictPlanningMode()
    {
        return strictPlanningMode?.Value ?? true;
    }

    private static string LabeledTextField(string label, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, NoWrapLabelStyle(), Width(FilterLabelWidth()));
        string result = GUILayout.TextField(value ?? string.Empty);
        GUILayout.EndHorizontal();
        return result;
    }

    private static int CurrentChapter()
    {
        return Managers.Goals == null ? 1 : Managers.Goals.GetCurrentChapterNumber(0);
    }

    private static int PreviewChapter()
    {
        return !StrictPlanningMode() && useChapterOverride.Value
            ? Mathf.Clamp(previewChapter.Value, 1, 999)
            : CurrentChapter();
    }

    private static int CurrentKarma()
    {
        if (Managers.Player?.karma == null)
            return 0;
        return Managers.Player.karma.Karma;
    }

    private static int PreviewKarma()
    {
        return !StrictPlanningMode() && useKarmaOverride.Value
            ? Mathf.Clamp(previewKarma.Value, -100, 100)
            : CurrentKarma();
    }

    private static bool ContainsIgnoreCase(string value, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;
        return value != null
            && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int LabeledIntField(string label, int value, int min, int max)
    {
        string text = LabeledTextField(label, value.ToString());
        if (int.TryParse(text, out int parsed))
            return Mathf.Clamp(parsed, min, max);
        return value;
    }

    private static int IntField(int value, int min, int max, params GUILayoutOption[] options)
    {
        string text = GUILayout.TextField(value.ToString(), options);
        if (int.TryParse(text, out int parsed))
            return Mathf.Clamp(parsed, min, max);
        return value;
    }

    private readonly struct RequirementTargetInfo
    {
        public string Label { get; }
        public RequirementTargetKind Kind { get; }
        public bool Editable { get; }
        public string FixedValue { get; }

        private RequirementTargetInfo(
            string label,
            RequirementTargetKind kind,
            bool editable,
            string fixedValue)
        {
            Label = label;
            Kind = kind;
            Editable = editable;
            FixedValue = fixedValue;
        }

        public static RequirementTargetInfo EditableTarget(string label, RequirementTargetKind kind)
        {
            return new RequirementTargetInfo(label, kind, editable: true, fixedValue: null);
        }

        public static RequirementTargetInfo FixedTarget(string label, string fixedValue)
        {
            return new RequirementTargetInfo(
                label,
                default,
                editable: false,
                fixedValue: fixedValue);
        }
    }

    private readonly struct RequirementLimitInfo
    {
        public int MinMandatory { get; }
        public int MaxMandatory { get; }
        public int MinOptional { get; }
        public int MaxOptional { get; }
        public int MinTotal { get; }
        public int MaxTotal { get; }
        public string MandatoryRangeText => RangeText(MinMandatory, MaxMandatory);
        public string OptionalRangeText => RangeText(MinOptional, MaxOptional);
        public string TotalRangeText => RangeText(MinTotal, MaxTotal);

        public RequirementLimitInfo(
            int minMandatory,
            int maxMandatory,
            int minOptional,
            int maxOptional,
            int minTotal,
            int maxTotal)
        {
            MinMandatory = Math.Max(0, minMandatory);
            MaxMandatory = Math.Max(0, maxMandatory);
            MinOptional = Math.Max(0, minOptional);
            MaxOptional = Math.Max(0, maxOptional);
            MinTotal = Math.Max(0, minTotal);
            MaxTotal = Math.Max(0, maxTotal);
        }

        private static string RangeText(int min, int max)
        {
            return min > 0 ? $"{min}..{max}" : max.ToString();
        }
    }

    private readonly struct PickerOption
    {
        public string Value { get; }
        public GUIContent Content { get; }
        public Sprite Sprite { get; }

        public PickerOption(string value, GUIContent content, Sprite sprite)
        {
            Value = value;
            Content = content;
            Sprite = sprite;
        }
    }

    private readonly struct PickerAnchorCacheEntry
    {
        public Rect GuiRect { get; }
        public Rect ScreenRect { get; }
        public string EventType { get; }
        public int FrameCount { get; }

        public PickerAnchorCacheEntry(Rect guiRect, Rect screenRect, string eventType, int frameCount)
        {
            GuiRect = guiRect;
            ScreenRect = screenRect;
            EventType = eventType;
            FrameCount = frameCount;
        }
    }

    private readonly struct PickerLayoutMetrics
    {
        public Rect Anchor { get; }
        public float RowHeight { get; }
        public float Padding { get; }
        public float TitleHeight { get; }
        public float TitleSpacing { get; }
        public float BottomMargin { get; }
        public float ChromeHeight { get; }
        public float DesiredViewportHeight { get; }
        public float MinOuterHeight { get; }
        public float MaxDownHeight { get; }
        public float MaxUpHeight { get; }
        public bool OpenUp { get; }
        public float AvailableHeight { get; }
        public int VisibleRows { get; }
        public int DesiredRows { get; }
        public int Rows { get; }
        public float ViewportHeight { get; }
        public float InnerHeight { get; }
        public float OuterHeight { get; }

        public PickerLayoutMetrics(
            Rect anchor,
            float rowHeight,
            float padding,
            float titleHeight,
            float titleSpacing,
            float bottomMargin,
            float chromeHeight,
            float desiredViewportHeight,
            float minOuterHeight,
            float maxDownHeight,
            float maxUpHeight,
            bool openUp,
            float availableHeight,
            int visibleRows,
            int desiredRows,
            int rows,
            float viewportHeight,
            float innerHeight,
            float outerHeight)
        {
            Anchor = anchor;
            RowHeight = rowHeight;
            Padding = padding;
            TitleHeight = titleHeight;
            TitleSpacing = titleSpacing;
            BottomMargin = bottomMargin;
            ChromeHeight = chromeHeight;
            DesiredViewportHeight = desiredViewportHeight;
            MinOuterHeight = minOuterHeight;
            MaxDownHeight = maxDownHeight;
            MaxUpHeight = maxUpHeight;
            OpenUp = openUp;
            AvailableHeight = availableHeight;
            VisibleRows = visibleRows;
            DesiredRows = desiredRows;
            Rows = rows;
            ViewportHeight = viewportHeight;
            InnerHeight = innerHeight;
            OuterHeight = outerHeight;
        }
    }

    private readonly struct RequirementSlotRange
    {
        public int Min { get; }
        public int Max { get; }

        public RequirementSlotRange(int min, int max)
        {
            Min = Math.Max(0, min);
            Max = Math.Max(Min, max);
        }
    }

    private enum RequirementTargetKind
    {
        None,
        Ingredient,
        Base,
    }

    private static string T(string key)
    {
        return CustomerPlannerLocalization.Text(key);
    }

    private static string T(string key, params object[] args)
    {
        return CustomerPlannerLocalization.Text(key, args);
    }

    private static string LocalizedStatus(string status)
    {
        return string.Equals(status, "Click Search to populate the customer list.", StringComparison.Ordinal)
            ? T("state.searchPrompt")
            : status;
    }

    private enum TargetFilledSelectionMode
    {
        Must,
        Can,
    }

    private enum PickerMode
    {
        RequirementTarget,
        NeedsEffect,
        ExcludesEffect,
    }
}
