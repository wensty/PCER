# Potion Craft Customer Planner

This project is the standalone customer-planning half of the original
Potion Craft Extra Requirements mod.

It can run by itself and does not compile-reference
`PotionCraftExtraRequirements`. When both mods are installed, it discovers
extra requirement metadata at runtime.

## What the planner controls

- Searches repeatable customer candidates from the game's normal
  regular-customer pool and repeatable plot-NPC random closeness quest pools.
- Modifies the active current customer only. Trader, extra-trader, one-shot,
  and other non-modifiable NPCs are left alone.
- Filters by internal name, display/name text, chapter, karma, and quest
  effects.
- Imports the currently active customer into the panel. Adding a scheduled
  customer automatically tries the current customer first: matching current
  customers are edited in place; different current customers are rebuilt only
  when they are allowed by the current strict/non-strict mode. If there is no
  current customer, or the current customer cannot be modified, the schedule is
  kept and tried again when the next current customer arrives.
  Importing the current customer also imports its current mandatory/optional
  requirements and editable string targets into the requirement editor.
- Maintains a small FIFO scheduled-customer list. Adding a schedule first tries
  to apply it to the current customer; if that is not possible, it is kept and
  checked again when new NPCs become current. If a schedule is applied
  immediately, the panel reports that feedback and the applied entry remains
  marked until the next schedule add, then is pruned.
- Locks scheduled customers to a selected template/faction/class when applicable
  and a selected quest.
- Adds selected mandatory/optional quest requirements to that planned customer.

`StrictPlanningMode` is enabled by default for newly generated config files. In
strict mode the planner uses the
current save's real chapter, karma, and quest-requirement difficulty mode; it
only schedules customers and quests that could naturally spawn, and disables
optional `Can` requirements when the current difficulty converts all
requirements to mandatory. It also reads the game's chapter-specific
mandatory/optional requirement spawn chances to enforce the number of
requirements that could naturally appear, including guaranteed minimum counts
when a difficulty sets a requirement slot chance to 100%. Requirement entries
must be unlocked
for the current chapter, ingredient targets must satisfy the ingredient's
chapter unlock and, for native particular-ingredient requirements, the same
elemental-potential compatibility filter that the game uses when choosing a
main/particular ingredient automatically. Potion base targets are not
pre-filtered by save unlock state;
base compatibility is left to the final generation/reachability validation,
matching cases where the game can ask for a base the player has not obtained
yet. Disable strict mode to use chapter/karma overrides for future-spawnable
customers; non-strict mode still excludes mechanically impossible customers,
but ignores current karma blockers and allows up to four total planned
requirements. Strict mode still lists positive faction spawn chances exactly as
the game does, but tiny positive values at or below
`TinyFactionSpawnChanceThreshold=0.001` are marked as `[tiny chance]` in the
customer list for newly generated config files. This calls out likely Unity
`AnimationCurve` endpoint residuals without hiding them.

BepInEx config defaults are initial values only: if the config file already
exists, changing these defaults in code will not overwrite the saved values.
Edit the generated config file or delete the relevant entries to adopt new
defaults.

It does not create new requirements. Requirement mods remain responsible for
injecting their own `QuestRequirementInQuest` entries into
`QuestRequirementInQuest.allRequirements`.

## Customer / quest sources

The planner currently supports repeatable quest sources:

- `RegularFactionQuest`: normal faction/class customers whose final quest comes
  from `FactionClass.GetRandomQuest`.
- `PlotRandomClosenessQuest`: plot NPC templates that can repeat after cooldown
  and whose repeatable quest comes from `NpcTemplate.randomClosenessQuests`.

The planner intentionally excludes one-shot NPCs and trader-only requests from
this repeatable quest pool.

The planner does not patch or write quest/template cooldown state. Scheduling
does not replace entries in the not-yet-spawned queue and does not mutate
already-spawned waiting NPCs. A scheduled customer is applied only when an NPC is
the active current customer. In strict mode, current-customer rebuilds are
limited to normal faction/class customers. In non-strict mode, current-customer
application skips most natural-spawn checks but still avoids merchants and
customers already reserved by another schedule.

When a schedule modifies the active current customer, the planner requests an
instant refresh of the game's dialogue box so the changed quest and requirement
text is visible immediately. If a potion is already on the scales, it also asks
the game's scales display to re-check potion suitability, then recalculates the
deal cost and refreshes trade buttons/text so price, acceptance state, and
requirement completion markers follow the modified quest.

## Requirement target model

The planner classifies requirement targets in this order:

1. Fixed native wrapper targets
   - `QuestRequirementInQuest.ingredient != null` is shown as a read-only
     Ingredient target.
   - `QuestRequirementInQuest.potionBase != null` is shown as a read-only Base
     target.

2. Editable native target types
   - `QuestRequirementCertainIngredient` with no wrapper ingredient is editable
     as `Ingredient.name`.
   - `QuestRequirementCertainBase` with no wrapper potion base is editable as
     `PotionBase.name`.

3. External fixed metadata target
   - If a loaded requirement mod exposes metadata that the planner can discover,
     that target is shown read-only.

4. No target
   - All other requirements are treated as targetless.

This matches the current game shape: some native requirements are targetless
(for example additional effects), some have fixed targets through the
`QuestRequirementInQuest` wrapper, and some can be represented by filling a
wrapper target field.

## Optional metadata integration for requirement mods

Requirement mods do not need to reference this planner. The planner currently
supports reflection-based discovery of metadata exposed by loaded requirement
mods, including `PotionCraftExtraRequirements`.

For compatibility, a requirement mod may expose a public static catalog with a
method shaped like:

```csharp
public static bool TryGet(
    QuestRequirement requirement,
    out YourRequirementDefinition definition)
```

The returned definition should expose a public instance property:

```csharp
public YourTargetMetadata DeclaredTarget { get; }
public IReadOnlyCollection<string> Tags { get; }
public IReadOnlyCollection<string> ConflictingTags { get; }
```

The target object should expose at least:

```csharp
public string DisplayName { get; }
```

If `DisplayName` is absent or empty, the planner will also try an
`IngredientCategory` property and use its `ToString()` value.

The metadata target is considered fixed/read-only. Do not use this for
requirements that should be edited by typing an `Ingredient.name` or
`PotionBase.name`; those should inherit/use the game's native
`QuestRequirementCertainIngredient` or `QuestRequirementCertainBase` behavior
with an empty wrapper target.

`Tags` is optional but helps the planner group external/modded requirements in
the UI. Unknown or untagged external requirements remain compatible and are
shown under `Other / external`.

`ConflictingTags` is optional but lets the planner auto-reset explicitly
declared mod conflicts in the UI. When a newly selected requirement has a
conflicting tag that intersects another selected requirement's `Tags` (or vice
versa), the older selection is reset to `None`.

The planner does not synthesize tags for native requirements. If no tag conflict
is declared or found, it falls back to the requirement's own
`IsCompatibleWithOtherRequirements` implementation in both directions. This
keeps native/native conflicts on the game's original path, while still allowing
mod/mod conflicts to use lightweight metadata when useful.

Target-dependent or stateful rules, such as ingredient-category restrictions
conflicting only with a specific ingredient target, or base/salt/effect
reachability checks, should still be implemented by the requirement's own
`UpdateGeneratedRequirement` / `IsCompatibleWithOtherRequirements`; the planner
also calls those rules during generation validation.

Current useful tag conventions:

- Ingredient category restrictions: include words such as `broad` or
  `category`.
- Ingredient-count restrictions: include words such as `highlander` or `count`.

For example, two external requirement mods can agree on a tag such as
`ingredient-count-limit`; either mod can then expose that tag in
`ConflictingTags` to make the planner auto-reset the other requirement. Conflicts
with native requirements should generally be represented by the requirement's
own compatibility/generation methods instead of planner-specific native tags.

## Build output

The project writes directly to:

```text
$(PotionCraftPath)\BepInEx\plugins\PotionCraftCustomerPlanner\
```
