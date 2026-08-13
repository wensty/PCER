# Potion Craft Customer Planner

This project is the standalone customer-planning half of the original
Potion Craft Extra Requirements mod.

It can run by itself and does not compile-reference
`PotionCraftExtraRequirements`. When both mods are installed, it discovers
extra requirement metadata at runtime.

## User guide

This section is for players using the planner in game. The later sections are
mostly implementation and compatibility notes for mod authors.

### Opening the planner

- Default toggle key: `F2`.
- The toggle key can be changed in the BepInEx config file.
- While the planner window is open, the mod tries to keep the system cursor
  visible and block most native game input.

### Search, browse, and lock a target

The left column searches customer candidates. Search results are only a browser
until you explicitly lock a target.

- `Search` fills the customer list using the current filter fields.
- `Internal` can match a customer/template/faction/class internal name, or an
  exact quest internal name such as `RandomQuest_00495`.
- `Name` filters the displayed customer text.
- The effect filters on the top-right filter quests by required or excluded
  potion effects.
- Clicking a customer only browses that customer and shows its quests in the
  middle column.
- Clicking a specific quest in the middle column locks the target to that
  customer + quest.
- `Import Current` imports the active in-game customer, its active quest, and
  its current extra requirements. This also locks the target.
- `Unlock Target` removes the target lock but keeps the browsed customer visible
  in the middle column. Random preview will again choose a natural customer and
  quest.

When the target is imported from the current customer, the middle column shows
only that locked quest. When you browse a searched customer, the middle column
shows its matching quest list. If more quests are hidden, use the `Show N more
quests` button to expand the list.

### Random preview

`Random preview` directly modifies the active current customer as a preview.

- If no target is locked, it chooses a customer and quest using the current
  chapter and karma, following the game's natural repeatable-customer pool.
- It does not use the current search results as the random pool.
- It generates extra requirements, imports them into the requirement editor, and
  applies the preview to the current customer.
- After a random preview, the generated customer and quest are written back to
  the panel like an import, so you can inspect or edit the result. If you want
  to unlock it and randomize freely again, click `Unlock Target`.

### Editing planned requirements

The right column edits extra requirements for the current plan.

- `Refresh List` rebuilds the requirement list after a save/game data is loaded.
- `Reset Config` clears selected requirements and targets.
- `None`, `Must`, and `Can` choose whether a requirement is absent, mandatory,
  or optional.
- In strict difficulty modes where the game only allows mandatory requirements,
  `Can` is disabled.
- Editable target fields accept internal names such as `Ingredient.name` or
  `PotionBase.name`; the small arrow opens a bounded picker when options are
  available.
- Filling an editable target automatically selects that requirement according
  to the configured target-filled mode.
- Requirement conflict checks run before applying or scheduling a plan.

### Applying, scheduling, and reverting

The bottom middle action buttons operate on the active in-game customer.

- `Preview selected` applies the currently browsed/locked customer + quest +
  edited requirements as a preview.
- `Random preview` generates and applies a random preview as described above.
- `Revert preview` restores the previous current customer state when possible.
- `Add scheduled` adds the selected plan to the schedule. The planner first
  tries to apply it to the current customer; if it cannot, it remains pending
  and is tried again when a new current customer appears.
- `Clear scheduled list` removes pending scheduled plans.
- `Load preset` and `Save preset` are reserved UI entries and are not implemented
  yet.

The planner avoids modifying merchants and other special non-modifiable NPCs.

### Diagnostics

Debug builds include diagnostic buttons:

- `Log spawn diagnostics` writes customer/quest spawn information to
  `BepInEx/plugins/PotionCraftCustomerPlanner/SpawnDiagnostics.txt`.
- `Log window diagnostics` writes UI layout and picker diagnostics to
  `BepInEx/plugins/PotionCraftCustomerPlanner/WindowDiagnostics.txt`.

These files are useful when a customer appears unexpectedly, a quest cannot be
found, or a UI row is clipped.

## Mod engineer guide

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
