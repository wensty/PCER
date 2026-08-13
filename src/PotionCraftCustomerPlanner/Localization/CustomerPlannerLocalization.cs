using System;
using System.Collections.Generic;
using PotionCraft.LocalizationSystem;

namespace PotionCraftCustomerPlanner;

internal static class CustomerPlannerLocalization
{
    private static readonly Dictionary<string, LocalizedLine> Entries =
        new Dictionary<string, LocalizedLine>(StringComparer.Ordinal)
        {
            ["window.title"] = new LocalizedLine(
                "Potion Craft Customer Planner - Regular Customer Planner",
                "药剂工艺顾客规划器 - 常规顾客规划器"),
            ["window.randomRuleTitle"] = new LocalizedLine(
                "Randomize Requirement Rule",
                "随机需求规则"),

            ["left.toggle"] = new LocalizedLine("Toggle: {0}", "切换：{0}"),
            ["left.searchTitle"] = new LocalizedLine("Search customer candidates", "搜索顾客候选"),
            ["left.exactInternal"] = new LocalizedLine("Internal", "内部名"),
            ["left.name"] = new LocalizedLine("Name", "名称"),
            ["left.strict"] = new LocalizedLine("Strict: only naturally spawnable results", "严格：仅自然可生成结果"),
            ["left.chapterOverride"] = new LocalizedLine("Preview with chapter override", "使用章节预览覆盖"),
            ["left.karmaOverride"] = new LocalizedLine("Preview with karma override", "使用声望预览覆盖"),
            ["left.chapter"] = new LocalizedLine("Chapter", "章节"),
            ["left.karma"] = new LocalizedLine("Karma", "声望"),
            ["left.strictUses"] = new LocalizedLine(
                "Strict uses current chapter={0}, karma={1}.",
                "严格模式使用当前章节={0}，声望={1}。"),
            ["left.tinyThreshold"] = new LocalizedLine(
                "Tiny threshold={0}; no-chance/tiny customers are marked, not hidden.",
                "微概率阈值={0}；无概率/微概率顾客会标记而非隐藏。"),
            ["left.search"] = new LocalizedLine("Search", "搜索"),
            ["left.importCurrent"] = new LocalizedLine("Import Current", "导入当前"),
            ["left.clearResults"] = new LocalizedLine("Unlock Target", "取消目标"),
            ["left.cachedCandidates"] = new LocalizedLine("Cached customer candidates: {0}", "已缓存顾客候选：{0}"),

            ["details.selectedCustomer"] = new LocalizedLine("Selected customer", "选定顾客"),
            ["details.noCustomer"] = new LocalizedLine("No matching customer.", "没有匹配顾客。"),
            ["details.source"] = new LocalizedLine("Source: {0}", "来源：{0}"),
            ["details.chapter"] = new LocalizedLine("Actual chapter: {0}, Preview chapter: {1}", "实际章节：{0}，预览章节：{1}"),
            ["details.karma"] = new LocalizedLine("Actual karma: {0}, Preview karma: {1}", "实际声望：{0}，预览声望：{1}"),
            ["details.unlockChapter"] = new LocalizedLine("Unlock chapter: {0}", "解锁章节：{0}"),
            ["details.questCounts"] = new LocalizedLine("Matching quests: {0} / Enabled quests: {1}", "匹配任务：{0} / 已启用任务：{1}"),
            ["details.targetQuest"] = new LocalizedLine("Target quest: {0}", "目标任务：{0}"),
            ["details.logSpawn"] = new LocalizedLine("Log spawn diagnostics", "输出生成诊断"),
            ["details.logWindow"] = new LocalizedLine("Log window diagnostics", "输出窗口诊断"),
            ["details.moreQuests"] = new LocalizedLine("... and {0} more matching quests", "……以及另外 {0} 个匹配任务"),
            ["details.showMoreQuests"] = new LocalizedLine("Show {0} more quests", "展开另外 {0} 个任务"),
            ["details.showFewerQuests"] = new LocalizedLine("Show fewer quests", "收起任务"),

            ["requirements.title"] = new LocalizedLine("Planned extra requirements", "计划额外需求"),
            ["requirements.refresh"] = new LocalizedLine("Refresh List", "刷新列表"),
            ["requirements.reset"] = new LocalizedLine("Reset Config", "重置配置"),
            ["requirements.noCache"] = new LocalizedLine(
                "No requirement cache yet. Click Refresh List after a save is loaded.",
                "尚无需求缓存。读取存档后点击刷新列表。"),
            ["requirements.targetHint"] = new LocalizedLine(
                "Targets can be typed manually or chosen with Select. Max uses a number.",
                "目标可手动输入或通过选择器选择。最大数量使用数字。"),
            ["requirements.onlyMandatory"] = new LocalizedLine(
                "Current strict difficulty mode allows only mandatory requirements; Can is disabled.",
                "当前严格难度模式仅允许强制需求；可选已禁用。"),
            ["requirements.onlyOptional"] = new LocalizedLine(
                "Current strict difficulty mode allows only optional requirements; Must is disabled.",
                "当前严格难度模式仅允许可选需求；强制已禁用。"),
            ["requirements.selectedCounts"] = new LocalizedLine(
                "Selected requirements: Must {0} allowed {1}, Can {2} allowed {3}, Total {4} allowed {5}",
                "已选需求：强制 {0} 允许 {1}，可选 {2} 允许 {3}，总计 {4} 允许 {5}"),
            ["requirements.groupAllowed"] = new LocalizedLine("Requirement group: allowed", "需求组：允许"),
            ["requirements.groupBlocked"] = new LocalizedLine("Requirement group: blocked - {0}", "需求组：阻止 - {0}"),
            ["requirements.none"] = new LocalizedLine("None", "无"),
            ["requirements.must"] = new LocalizedLine("Must", "强制"),
            ["requirements.can"] = new LocalizedLine("Can", "可选"),
            ["category.potionEffects"] = new LocalizedLine("Potion effects", "药水效果"),
            ["category.ingredientTargets"] = new LocalizedLine("Ingredient targets", "素材目标"),
            ["category.potionBaseLimits"] = new LocalizedLine("Potion base limits", "药水基底限制"),
            ["category.ingredientCount"] = new LocalizedLine("Ingredient count", "素材数量"),
            ["category.potionQuality"] = new LocalizedLine("Potion quality", "药水品质"),
            ["category.noSalts"] = new LocalizedLine("No salts", "无盐"),
            ["category.modIngredientCategories"] = new LocalizedLine("Mod: ingredient categories", "模组：素材类别"),
            ["category.modIngredientCounts"] = new LocalizedLine("Mod: ingredient counts", "模组：素材数量"),
            ["category.modOther"] = new LocalizedLine("Mod: other", "模组：其他"),
            ["category.otherNative"] = new LocalizedLine("Other native", "其他原生"),
            ["category.otherExternal"] = new LocalizedLine("Other / external", "其他 / 外部"),

            ["actions.title"] = new LocalizedLine("Customer actions", "顾客操作"),
            ["actions.previewSelected"] = new LocalizedLine("Preview selected", "预览选定"),
            ["actions.randomPreview"] = new LocalizedLine("Random preview", "随机预览"),
            ["actions.editRandomRule"] = new LocalizedLine("Edit random rule", "编辑随机规则"),
            ["actions.revertPreview"] = new LocalizedLine("Revert preview", "还原预览"),
            ["actions.addScheduled"] = new LocalizedLine("Add scheduled", "添加预约"),
            ["actions.clearScheduled"] = new LocalizedLine("Clear scheduled list", "清空预约列表"),
            ["actions.loadPreset"] = new LocalizedLine("Load preset", "载入预设"),
            ["actions.savePreset"] = new LocalizedLine("Save preset", "保存预设"),

            ["schedule.summary"] = new LocalizedLine(
                "Scheduled queue: pending {0}, applied placeholders {1}, total {2}",
                "预约队列：等待 {0}，已应用占位 {1}，总计 {2}"),
            ["schedule.entry"] = new LocalizedLine("{0}. [{1}] {2} / {3}", "{0}. [{1}] {2} / {3}"),
            ["schedule.stateApplied"] = new LocalizedLine("applied/current", "已应用/当前"),
            ["schedule.statePending"] = new LocalizedLine("pending", "等待"),
            ["schedule.more"] = new LocalizedLine("... and {0} more scheduled entries", "……以及另外 {0} 个预约项"),

            ["randomRule.description"] = new LocalizedLine(
                "Override used only by this mod's Random preview; it does not change native game spawning.",
                "覆盖项仅用于本模组的随机预览；不会改变游戏原生生成。"),
            ["randomRule.weightHint"] = new LocalizedLine(
                "Unset weight multipliers behave as 1.0 and keep the native requirement weight.",
                "未设置的权重倍率按 1.0 处理，保留原生需求权重。"),
            ["randomRule.mandatoryChances"] = new LocalizedLine("Mandatory requirement chances", "强制需求概率"),
            ["randomRule.optionalChances"] = new LocalizedLine("Optional requirement chances", "可选需求概率"),
            ["randomRule.weightMultipliers"] = new LocalizedLine("Requirement weight multipliers", "需求权重倍率"),
            ["randomRule.noCache"] = new LocalizedLine("No requirement cache yet. Click Refresh List in the main window.", "尚无需求缓存。请在主窗口点击刷新列表。"),
            ["randomRule.reset"] = new LocalizedLine("Reset random rule", "重置随机规则"),
            ["randomRule.close"] = new LocalizedLine("Close", "关闭"),
            ["randomRule.overrideChances"] = new LocalizedLine("Override native chances (native {0}% / {1}%)", "覆盖原生概率（原生 {0}% / {1}%）"),
            ["randomRule.firstChance"] = new LocalizedLine("First requirement chance %", "第一个需求概率 %"),
            ["randomRule.secondChance"] = new LocalizedLine("Second requirement chance %", "第二个需求概率 %"),
            ["randomRule.nativeWeight"] = new LocalizedLine("native weight {0}", "原生权重 {0}"),
            ["randomRule.resetOne"] = new LocalizedLine("Reset", "重置"),

            ["filters.title"] = new LocalizedLine("Quest effect filters", "任务效果筛选"),
            ["filters.needs"] = new LocalizedLine("Needs", "需要"),
            ["filters.excludes"] = new LocalizedLine("Excludes", "排除"),
            ["filters.needsAll"] = new LocalizedLine("Needs all", "需要全部"),
            ["filters.excludesAny"] = new LocalizedLine("Excludes any", "排除任意"),
            ["filters.searchHint"] = new LocalizedLine("Click Search after changing filters.", "修改筛选器后点击搜索。"),
            ["filters.selectEffect"] = new LocalizedLine("Select effect ▼", "选择效果 ▼"),

            ["target.ingredient"] = new LocalizedLine("Ingredient", "素材"),
            ["target.base"] = new LocalizedLine("Base", "基底"),
            ["target.generic"] = new LocalizedLine("Target", "目标"),
            ["target.clear"] = new LocalizedLine("Clear", "清除"),

            ["state.searchPrompt"] = new LocalizedLine("Click Search to populate the customer list.", "点击搜索填充顾客列表。"),
            ["state.resultsCleared"] = new LocalizedLine("Selected target unlocked. Random preview will choose a customer and quest naturally.", "已取消选定目标。随机预览将按自然机制选择顾客和任务。"),
            ["state.searchComplete"] = new LocalizedLine("Search complete. Chapter={0}, Karma={1}.", "搜索完成。章节={0}，声望={1}。"),
            ["state.importedCurrent"] = new LocalizedLine("Imported current customer.", "已导入当前顾客。"),
            ["state.importCurrentBlocked"] = new LocalizedLine("Import current blocked: {0}", "导入当前顾客被阻止：{0}"),
            ["state.importedCurrentDetails"] = new LocalizedLine(
                "Imported current customer: {0} / quest={1} / requirements={2}+{3}",
                "已导入当前顾客：{0} / 任务={1} / 需求={2}+{3}"),
            ["state.clearedScheduled"] = new LocalizedLine("Cleared scheduled plan.", "已清空预约计划。"),
            ["state.previewReverted"] = new LocalizedLine("Preview reverted.", "预览已还原。"),
        };

    public static string Text(string key)
    {
        return TryGetLine(key, out LocalizedLine line)
            ? line.Text(CurrentLanguage())
            : key;
    }

    public static string Text(string key, params object[] args)
    {
        string format = Text(key);
        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private static bool TryGetLine(string key, out LocalizedLine line)
    {
        return Entries.TryGetValue(key, out line);
    }

    private static Language CurrentLanguage()
    {
        try
        {
            return LocalizationManager.CurrentLocale == LocalizationManager.Locale.zh
                ? Language.Zh
                : Language.En;
        }
        catch
        {
            return Language.En;
        }
    }

    private enum Language
    {
        En,
        Zh,
    }

    private readonly struct LocalizedLine
    {
        private readonly string en;
        private readonly string zh;

        public LocalizedLine(string en, string zh)
        {
            this.en = en;
            this.zh = zh;
        }

        public string Text(Language language)
        {
            if (language == Language.Zh && !string.IsNullOrWhiteSpace(zh))
                return zh;
            return string.IsNullOrWhiteSpace(en) ? zh ?? string.Empty : en;
        }
    }
}
