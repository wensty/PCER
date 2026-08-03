# Potion Craft Extra Requirements

为《药剂工艺：炼金模拟器》增加可扩展的顾客额外要求。

## 构建

默认游戏目录在 [Directory.Build.props](Directory.Build.props) 中配置。构建输出会
直接写入：

```text
$(PotionCraftPath)\BepInEx\plugins\PotionCraftExtraRequirements\
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
