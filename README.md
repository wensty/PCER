# The source of all this evil mod

![](img/1.png)

# Potion Craft Extra Requirements / Customer Planner

这个仓库现在包含两个可独立加载的 BepInEx 模组：

- `PotionCraftExtraRequirements`：为《药剂工艺：炼金模拟器》增加可扩展的顾客额外要求。
- `PotionCraftCustomerPlanner`：顾客定制器/下一个常规顾客规划窗口。

两个模组可以单独使用。若同时安装，Customer Planner 会读取 Extra Requirements 暴露的
要求目标元数据，以正确展示本 Mod 的素材类别限制等固定语义目标。

## 构建

默认游戏目录在 [Directory.Build.props](Directory.Build.props) 中配置。构建 solution 会
直接写入两个插件目录：

```text
$(PotionCraftPath)\BepInEx\plugins\PotionCraftExtraRequirements\
$(PotionCraftPath)\BepInEx\plugins\PotionCraftCustomerPlanner\
```

也可以覆盖游戏路径：

```powershell
dotnet build -c Debug -p:PotionCraftPath="X:\Games\Potion Craft"
```

## 当前要求

| 要求                  |    解锁 |       价格倍率 |
| --------------------- | ------: | -------------: |
| 禁用/仅用草药         | 第 7 章 |        ×2 / ×3 |
| 禁用/仅用蘑菇         | 第 7 章 |        ×2 / ×3 |
| 禁用/仅用矿石         | 第 9 章 |        ×2 / ×3 |
| 每种素材最多 1/2/3 个 | 第 4 章 | ×3 / ×2 / ×1.5 |

当前冲突规则：

- 广谱素材限制之间互相冲突，并与 Highlander、无盐、以及不相容的指定素材/
  主料要求冲突。
- Highlander 之间互相冲突，并与广谱素材限制、原生主料要求、原生最大素材种类
  要求冲突。

各要求的启用状态、章节、生成权重、价格倍率和人气奖励会写入
`BepInEx/config/cn.potioncraft.extra-requirements.cfg`。

## 本地化

- 默认文本：英语。
- 当前翻译：简体中文（游戏 locale `zh`）。
- 其它语言暂时回退到英语。

Mod 文本通过游戏的 `LocalizationManager` 查询入口提供。要求列表继续使用原生
`GeneratedQuestRequirement` 渲染，因此感叹号、加号、勾号图标、颜色、字体及
富文本格式均来自游戏自身资源。

每项要求分别提供少量必要文本、可选偏好文本及对应的失败反应。具体句子由
游戏原生要求文本池随机选择，而不是由 Mod UI 自行拼接。

## Customer Planner：下一个常规顾客窗口

该功能已拆分到独立工程
`src/PotionCraftCustomerPlanner/PotionCraftCustomerPlanner.csproj`。给其它要求模组的
接入约定见 [Customer Planner README](src/PotionCraftCustomerPlanner/README.md)。

默认快捷键：`F2`。可在
`BepInEx/config/cn.potioncraft.customer-planner.cfg` 的 `Next Customer Window`
段落中修改。已有配置文件不会自动覆盖旧值；如果想使用新默认值，可以删除该配置项
或手动改成 `ToggleShortcut = F2`。窗口字号可用同一段落下的 `UIFontSize` 调整，
默认值为 `16`。`BlockGameInputWhenOpen = true` 时，窗口打开期间会尽量屏蔽游戏
自身快捷键输入。

同一段落还可配置 UI 颜色：

- `NoneButtonColor`
- `MustButtonColor`
- `CanButtonColor`
- `CustomerSelectedColor`

颜色使用 `#RRGGBB` 或 `#RRGGBBAA`。

窗口替换的是游戏队列中的普通 faction/class 顾客槽位；如果队首是商人、额外商人、
一次性 NPC 或已经替换好的特殊 NPC，会保留 pending 计划并等待下一个普通顾客槽位。
候选包括普通 `Faction -> FactionClass -> NpcTemplate` 随机生成路径上的顾客，以及可重复
plot NPC 的随机亲密度需求；不会主动选择一次性顾客或商人队列。候选会按当前章节、
karma、派系权重、职业启用状态、NPC 解锁条件和可用 quest 过滤。

窗口支持：

- 搜索需要手动点击 `Search`，打开窗口不会自动枚举顾客池。
- 按精确内部名搜索单个顾客/派系/职业：
  - `NpcTemplate.name`
  - `Faction.name`
  - `FactionClass.name`
- 按顾客、派系、职业、模板名称搜索。
- 效果过滤移到右侧 `Quest effect filters` 区域；可用短下拉框选择效果，选中后立即
  加入过滤文本。单项移除可直接编辑文本，`Clear` 清空整组过滤。多个 `Needs` 效果
  需要同时存在，任一 `Excludes` 效果存在则排除。
- 隐藏窗口时会自动关闭当前选择列表；选择列表以内联方式展开在对应按钮下方，避免
  IMGUI 浮动窗口坐标偏移。
- 顾客列表项支持鼠标悬停提示，显示完整内部名、章节/karma 信息和更多 quest/effect
  摘要。
- 用 karma override 预览不同 karma 下的常规顾客池；这不会修改游戏实际 karma。
- 用 chapter override 预览并排程不同章节下的常规顾客池；排程项会按该预览章节
  生成 quest。
- 筛选后的顾客列表只展示顾客身份和匹配 quest 数量；完整 effect 摘要保留在悬停提示
  和右侧详情区，避免左侧列表过载。
- 在选中顾客的匹配 quest 中指定目标 quest；排程后会锁定为该 quest。
- 为计划顾客指定强制/可选额外要求，并按要求自身数据处理目标：
  - 无目标要求不显示目标行，例如额外效果、多重效果、Highlander 等。
  - 原版 wrapper 已带有 `Ingredient` / `PotionBase` 时，目标只读显示，保持原版固定
    目标行为。
  - 原版 wrapper 未带固定目标但要求类型需要目标时，可输入 `Ingredient.name` 或
    `PotionBase.name`；目标框旁的 `▼` 会打开短下拉列表。
  - 本 Mod 的素材类别限制有固定语义目标，例如 herbal/mushroom/crystal；规划器只读
    显示该目标，不把它写成原版字符串参数。
  - `None` / `Must` / `Can` 是普通按钮，已选中的要求可点 `None` 取消
  - 需求表会按原版类型和可反射到的模组 tags 分组；未接入 metadata 的外部需求会显示
    在 `Other / external`。
  - 点选 `Must` / `Can` 时会按原版互斥规则自动重置冲突项，例如 lowlander 1/2/3、
    基底限制类以及其它原生互斥组合。
- `Refresh List` 只刷新已加载的额外要求列表；`Reset Config` 会清空当前
  `None/Must/Can` 选择和需求目标。
- 额外要求列表需要手动点击 `Refresh List` 刷新，避免窗口打开时反复扫描。
- 在确认前检查指定要求组是否至少能在该顾客预览章节的某个可用 quest 上生成；
  被原生或 Mod 冲突规则禁止的组合不能排程。

排程后，Mod 会在下一次实际生成顾客时替换队列首位，并在 NPC spawn 后应用指定的
额外要求。如果实际随机到的 quest 与指定要求组仍不兼容，Mod 会保留原生随机生成的
要求并写入 BepInEx 日志。

目标原料/药基目前需要输入内部名；如果内部名不存在或与 quest/其他要求冲突，窗口会
把该要求组标记为 blocked。
