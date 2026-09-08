# EQBuddy Evolved — Quests & Guided Progression Requirements

**Document purpose:** Product and engineering requirements for transforming EQBuddy's Quests experience from a checklist/tracker into a comprehensive, personalized **Guide to Norrath** that can support normal quests, Epic quests, Plane of Sky progression, key/access quests, class quests, and future gear-upgrade recommendations.

**Primary design goal:** A player should be able to use EQBuddy as the single point of reference for completing a quest or progression path without needing to open a browser, cross-reference a wiki, or infer missing steps.

---

## 1. Product Vision

EQBuddy Quests should evolve from:

> “Here is a list of things you need to do.”

into:

> “Here is what you should do next, exactly how to do it, why it matters, what you will get, and what to do if something does not go as expected.”

The same level of thoroughness must apply to:

- Normal quests
- Multi-step quests
- Epic quests
- Plane of Sky quests and island progression
- Keying/access quests
- Class-specific quests
- Gear/reward quests
- Repeatable/farm-oriented objectives where applicable

The quest system must also be structured so that future EQBuddy functionality can answer higher-level player intent such as:

> “I want to upgrade my gear.”

and then recommend a ranked set of realistic options based on:

- Character level
- Observed/selected classes
- Current equipment
- Potential item enhancement levels
- Quest prerequisites
- Quest difficulty
- Required group/raid effort
- Travel/progression requirements
- Mob drops and farm locations
- Relevant zone accessibility
- Current quest/key progress

The underlying quest model must therefore be designed as reusable, structured game knowledge rather than display-only quest text.

---

# 2. Core Product Principles

## 2.1 Single Point of Reference

A complete EQBuddy quest guide should answer every practical question a reasonable player might have before, during, or after a step.

A step is not considered fully authored if the player could still reasonably ask:

- Who do I interact with?
- Where do I go?
- What exactly do I need to do?
- When should I do this?
- How do I do it?
- Why am I doing it?
- What reward or progression does this produce?
- What do I need before I start?
- How do I know I completed it correctly?
- What happens if something goes wrong?
- What should I do next?

These questions do **not** need to appear as literal headings in the UI. The content should answer them naturally and concisely.

---

## 2.2 Action Before Reference

The default view should prioritize:

1. What the player should do next
2. Where to do it
3. How to do it
4. Important warnings
5. What completing it accomplishes

Reference details should remain available through progressive disclosure.

EQBuddy should not make the player read an encyclopedia entry in order to identify the next action.

---

## 2.3 Dependency Order Before Difficulty Order

Where quest steps have mandatory dependencies, the required progression order always wins.

Where multiple optional quests/objectives are simultaneously available, EQBuddy should present them from **easiest to hardest** by default.

Example:

### Available Now
1. ★ Easy — Visit class quest NPC
2. ★★ Moderate — Farm optional quest component
3. ★★★ Challenging — Required progression boss

### Later
4. ★★ Moderate — Reward quest on a later island
5. ★★★★★ Raid — End-of-zone reward

Do not recommend an “easy” quest that the player cannot currently access ahead of a harder prerequisite that must first be completed.

---

## 2.4 Progressive Disclosure

The system must support a large amount of useful information without making the primary UI cluttered.

Default active step:

- Objective
- Location
- Concise directions
- Required action
- Immediate warning
- Reward/progression result
- Completion state

Expandable detail:

- Exact directions
- Map
- Spawn mechanics
- Combat strategy
- Required items
- Relevant drops
- Why this matters
- Troubleshooting/recovery
- Full reference information
- Sources

---

## 2.5 Personalization

EQBuddy should prioritize information relevant to the current character.

Examples:

- Show quest rewards relevant to observed/selected classes first.
- Suppress irrelevant class quests by default.
- Highlight items that are likely upgrades over currently equipped gear.
- Prioritize objectives the player can currently access.
- Adjust recommendations based on known keying/progression state.
- Resume the player at the current relevant quest step.

A `Show All` option should always allow broader exploration.

---

# 3. Quest and Guide Types

Every quest-like record should have a structured `GuideType`.

Required initial types:

- `NormalQuest`
- `EpicQuest`
- `PlaneOfSkyQuest`
- `ZoneProgression`
- `KeyingAccess`
- `ClassQuest`
- `GearQuest`
- `RepeatableQuest`
- `FarmObjective`

Additional types may be added later without requiring schema redesign.

A Plane of Sky walkthrough should use the same underlying engine as other guides, with zone-specific extensions only where genuinely necessary.

---

# 4. Guide-Level Data Model

Every quest or progression guide should support the following attributes.

## 4.1 Identity

- `GuideId`
- `Name`
- `ShortName`
- `GuideType`
- `Description`
- `Summary`
- `ZoneIds[]`
- `ExpansionOrEra`
- `IsRepeatable`
- `IsClassSpecific`
- `ApplicableClasses[]`
- `MinimumLevel`
- `RecommendedLevelMin`
- `RecommendedLevelMax`
- `MaximumUsefulLevel` where meaningful
- `Tags[]`

Example tags:

- Gear
- Epic
- Keying
- Access
- PlaneOfSky
- Solo
- Group
- Raid
- Haste
- Weapon
- Armor
- Clicky
- Utility
- ClassQuest

---

## 4.2 Difficulty and Effort

- `DifficultyRating`
- `DifficultyLabel`
- `RecommendedPartySize`
- `ContentScale`
  - Solo
  - Duo
  - SmallGroup
  - FullGroup
  - MultiGroup
  - Raid
- `EstimatedComplexity`
- `TravelComplexity`
- `SpawnComplexity`
- `CombatComplexity`
- `FailurePenalty`
- `RareDropDependency`
- `TimeGateDependency`
- `PrerequisiteComplexity`

Difficulty should be derived from structured factors where possible rather than being a single arbitrary number.

The UI may collapse these into a simple:

- ★ Easy
- ★★ Moderate
- ★★★ Challenging
- ★★★★ Hard
- ★★★★★ Very Hard / Raid

---

## 4.3 Prerequisites

- `PrerequisiteGuideIds[]`
- `PrerequisiteObjectiveIds[]`
- `RequiredLevel`
- `RequiredClasses[]`
- `RequiredItems[]`
- `RequiredKeys[]`
- `RequiredFlags[]`
- `RequiredFaction[]`
- `RequiredZoneAccess[]`
- `RecommendedItems[]`
- `RecommendedResists[]`
- `RecommendedAbilities[]`
- `RecommendedGroupComposition[]`

Prerequisites should distinguish:

- Hard requirement
- Recommended preparation
- Optional convenience

---

## 4.4 Rewards

Every guide must link to structured reward records.

- `RewardItemIds[]`
- `ExperienceReward`
- `CurrencyReward`
- `FactionRewards[]`
- `KeyUnlocks[]`
- `ZoneUnlocks[]`
- `AbilityUnlocks[]`
- `ProgressionUnlocks[]`
- `OtherRewards[]`

Rewards should be visible near the top of a quest and never buried only at the end.

---

## 4.5 Progress

- `Objectives[]`
- `CurrentObjectiveId`
- `CompletedObjectiveIds[]`
- `SkippedObjectiveIds[]`
- `OptionalObjectiveIds[]`
- `DetectedProgress[]`
- `ManualOverrides[]`
- `LastUpdated`
- `CompletionPercent`

Progress must survive application restart and normal game sessions.

---

# 5. Objective / Step Data Model

The **Objective** is the fundamental unit of guided progression.

Every actionable objective must support enough structured information to answer the player questions defined above.

## 5.1 Objective Identity

- `ObjectiveId`
- `GuideId`
- `StageId`
- `Title`
- `ShortInstruction`
- `ObjectiveType`
- `SequenceOrder`
- `IsRequired`
- `IsOptional`
- `IsSkippable`
- `DifficultyRating`
- `DifficultyLabel`

Objective types should include:

- TalkToNpc
- Travel
- Explore
- Kill
- Loot
- Farm
- TurnIn
- Combine
- UseItem
- Activate
- ObtainKey
- UnlockAccess
- SurviveEncounter
- SpawnNamed
- TriggerEvent
- Collect
- ChooseReward
- ReturnToNpc
- Verify
- Custom

---

## 5.2 Who

- `PrimaryNpcIds[]`
- `SecondaryNpcIds[]`
- `TargetMobIds[]`
- `QuestNpcIds[]`
- `TurnInNpcIds[]`
- `SpawnedNpcIds[]`

For each relevant NPC/mob, the system should know when possible:

- Name
- Zone
- Location
- Role
- Faction requirements
- Spawn conditions
- Respawn information
- Placeholder relationships
- Relevant drops
- Related objectives

---

## 5.3 Where

- `ZoneId`
- `SubArea`
- `LocationName`
- `Coordinates`
- `MapTarget`
- `MapMarkerIds[]`
- `StartingPoint`
- `Destination`
- `Directions`
- `Landmarks[]`
- `ArrivalConfirmationClues[]`
- `ExitDirections`
- `ReturnRoute`

Directions should never rely solely on coordinates.

A strong direction record should include, where known:

- Starting location
- Compass direction
- Landmark
- Coordinates
- Destination
- Visual/NPC confirmation
- Hazard warnings

Poor:

> Go to Gorgalosk.

Better:

> From the island arrival point, move toward the large structure near the center of the island. Stay away from the outer edge while traveling. Gorgalosk begins inside with two nearby NPCs. You will know you are in the correct location when you see the three mobs sharing the structure.

---

## 5.4 What

- `Action`
- `DetailedInstructions[]`
- `RequiredKills[]`
- `RequiredItems[]`
- `RequiredQuantity`
- `RequiredDialogue`
- `RequiredTurnIns[]`
- `RequiredCombine`
- `RequiredInteraction`
- `SuccessCondition`

The player should never have to infer the actionable requirement from flavor text.

---

## 5.5 When

- `TimingGuidance`
- `DoNow`
- `DoBeforeLeaving`
- `DoBeforeBoss`
- `DoAfterBoss`
- `DoAfterLoot`
- `DoAfterUnlock`
- `CanDoLater`
- `RecommendedTiming`
- `Missable`
- `ReturnCost`
- `DependencyNotes`

Examples of player-facing timing guidance:

- **Do this now** — required to continue.
- **Do this before leaving** — returning is inconvenient.
- **Optional — come back later** — significantly harder than surrounding content.
- **Don't worry about this yet** — another component is not available until later.
- **Complete after killing the boss** — the next event depends on the kill.
- **Use this key before taking unnecessary risks** — activation permanently records access.

---

## 5.6 How

- `Strategy`
- `DetailedProcedure[]`
- `SpawnProcedure[]`
- `PullStrategy`
- `CombatNotes`
- `Mechanics[]`
- `RecommendedApproach`
- `AlternativeApproaches[]`
- `GroupNotes`
- `ClassNotes[]`
- `Warnings[]`

“Kill NPC X” is not sufficient when the real task requires a spawn chain, placeholder cycle, phrase, turn-in order, special mechanic, or unusual pull.

---

## 5.7 Why

- `WhyItMatters`
- `ProgressionReason`
- `UnlockReason`
- `RewardReason`
- `DependencyReason`
- `OptionalValue`

The system should explain practical significance, not simply lore.

Example:

> Killing this boss provides the key required to reach the next island and may also provide class quest components relevant to your current build.

---

## 5.8 Reward / Result

Every objective should state what completing it produces.

- `ProducedItemIds[]`
- `RewardItemIds[]`
- `Unlocks[]`
- `KeyUnlocks[]`
- `ProgressionFlags[]`
- `NextObjectiveIds[]`
- `OptionalRewardIds[]`

The active step should be able to answer:

> “What do I get from doing this?”

even if the result is progression rather than gear.

---

## 5.9 Completion Detection

- `CompletionTriggers[]`
- `LogPatterns[]`
- `NpcKillTriggers[]`
- `LootTriggers[]`
- `InventoryTriggers[]`
- `ZoneTriggers[]`
- `DialogueTriggers[]`
- `ManualCompletionAllowed`
- `CompletionConfidence`
- `CompletionEvidence`

Completion may be:

- Automatically detected
- Inferred with confidence
- Manually confirmed
- Manually overridden

EQBuddy must never fabricate certainty.

---

## 5.10 Failure / Recovery

Every meaningful objective should support:

- `FailureScenarios[]`
- `RecoveryInstructions[]`
- `BossNotUpInstructions`
- `WrongSpawnInstructions`
- `LostItemInstructions`
- `DiedInstructions`
- `LeftZoneInstructions`
- `MissedTriggerInstructions`
- `ManualRecovery`
- `ResetConditions`

A complete guide must answer:

> “That did not happen. What do I do now?”

---

# 6. Stage / Chapter Model

Long quests and zone progressions should be divided into `Stages`.

Examples:

Plane of Sky:

- Preparation
- Entry / Island 1
- Quest Chamber
- Noble Island
- Island 2
- Island 3
- Island 4
- Island 5
- Island 6
- Island 7
- Island 8
- Final Turn-Ins / Cleanup

Epic quest:

- Prerequisites
- Initial NPC
- Early collection
- Faction work
- Mid-stage boss
- Rare component
- Final encounter
- Turn-in

Each stage supports:

- `StageId`
- `Name`
- `Description`
- `SequenceOrder`
- `Prerequisites[]`
- `ArrivalInstructions`
- `ImmediateWarnings[]`
- `Objectives[]`
- `RelevantRewards[]`
- `RelevantDrops[]`
- `ExitInstructions`
- `DoNotLeaveBefore[]`
- `RecoveryNotes`

---

# 7. Structured NPC, Mob, Spawn, and Farm Knowledge

To support future gear recommendations, quest records cannot exist in isolation.

EQBuddy should maintain linkable structured entities for:

- NPCs
- Named mobs
- Spawn locations
- Spawn cycles
- Placeholders
- Respawn ranges
- Loot tables
- Drop rates where reliable
- Zone locations
- Farm camps
- Required party scale
- Recommended level range
- Access prerequisites
- Relevant quests

## 7.1 Mob / NPC Record

Suggested fields:

- `NpcId`
- `Name`
- `ZoneId`
- `NpcType`
- `LevelMin`
- `LevelMax`
- `Class`
- `Faction`
- `AggroBehavior`
- `LocationRecords[]`
- `SpawnRecords[]`
- `DropTable[]`
- `RelatedGuideIds[]`
- `RelatedObjectiveIds[]`
- `Notes`
- `Sources[]`

---

## 7.2 Spawn Record

- `SpawnId`
- `NpcId`
- `ZoneId`
- `Location`
- `Coordinates`
- `CampName`
- `RespawnMin`
- `RespawnMax`
- `PlaceholderNpcIds[]`
- `SpawnConditions`
- `TriggerChain[]`
- `KnownVariance`
- `Confidence`
- `Sources[]`

Where EQBuddy learns spawn timing from observed logs, learned values must remain distinct from canonical/source-backed values and remain manually editable.

---

## 7.3 Farm Location Record

A farm location should be able to support future recommendations such as:

> “This item is a reasonable upgrade and the mob that drops it is farmable by your current character.”

Fields:

- `FarmLocationId`
- `ZoneId`
- `CampName`
- `TargetNpcIds[]`
- `ItemIds[]`
- `RecommendedLevelMin`
- `RecommendedLevelMax`
- `RecommendedPartySize`
- `DifficultyRating`
- `TravelDifficulty`
- `AccessRequirements[]`
- `RespawnCharacteristics`
- `CompetitionNotes`
- `SafetyNotes`
- `Directions`
- `MapTargets[]`
- `Sources[]`

---

# 8. Structured Item and Reward Knowledge

Gear rewards must be first-class data, not text embedded inside a quest description.

Each reward should link to an item record with sufficient attributes for comparison and recommendation.

Required item attributes where available:

- `ItemId`
- `Name`
- `Slot`
- `ItemType`
- `UsableClasses[]`
- `RequiredLevel`
- `RecommendedLevel`
- `BaseStats`
- `EnhancedStatsByRank`
- `Resists`
- `AC`
- `HP`
- `Mana`
- `Endurance`
- `PrimaryAttributes`
- `SecondaryAttributes`
- `Haste`
- `Proc`
- `ClickEffect`
- `FocusEffect`
- `SkillModifiers`
- `WeaponDamage`
- `WeaponDelay`
- `WeaponRatio`
- `Restrictions`
- `Lore`
- `NoDrop`
- `QuestIds[]`
- `DropSources[]`
- `VendorSources[]`
- `CraftSources[]`
- `Sources[]`

The system should support item versions/enhancements such as +1 through +10 where those exist in EQL.

---

# 9. Gear Upgrade Recommendation Foundation

The quest redesign must intentionally enable a later feature:

> **“I want to upgrade my gear.”**

This should eventually produce an ordered set of practical recommendations from both quests and drops.

## 9.1 Player Inputs

Recommendation logic may use:

- Character level
- Observed classes
- Manually selected classes
- Current equipment
- Current item enhancement ranks
- Known inventory
- Known quest progress
- Known key/access state
- Completed quests
- Current zone
- User-selected acceptable level range
- User-selected effort preference
- Solo/group/raid preference
- Desired slot
- Desired stat/role objective

Examples:

- “Upgrade my weapons.”
- “Show me armor upgrades I can get within 5 levels.”
- “What haste items can I realistically obtain?”
- “What can I solo?”
- “What should I work toward next?”

---

## 9.2 Recommendation Candidate Types

The engine should be able to recommend:

- Quest reward
- Epic step/reward
- Plane of Sky class reward
- Named mob drop
- Farmable common/rare drop
- Crafted item
- Vendor item where relevant
- Progression unlock leading to future item

---

## 9.3 Candidate Scoring

A future upgrade recommendation should be rankable using structured dimensions such as:

### Upgrade Value
- Stat improvement
- Weapon DPS improvement
- AC improvement
- Haste improvement
- Proc/click utility
- Role relevance
- Enhancement potential

### Attainability
- Current level
- Class compatibility
- Zone access
- Quest prerequisites
- Key requirements
- Group size
- Boss difficulty
- Spawn rarity
- Travel burden
- Estimated farming burden
- Number of required steps

### Progress Efficiency
- Already completed prerequisites
- Already owned quest components
- Already unlocked zone/key
- Nearby/current zone
- Multiple useful rewards from same path
- Quest overlaps with other tracked objectives

A powerful upgrade that requires extensive raid progression should not automatically outrank a modest but immediately attainable upgrade unless the user asks for “best possible.”

---

## 9.4 Recommendation Explanation

Every recommendation should explain **why EQBuddy is recommending it**.

Example:

> **Recommended: Azure Ruby Ring**
>
> Strong upgrade for your Warrior build.
>
> **Why this is practical now**
> - You are already high enough level.
> - You have Plane of Sky access.
> - Its quest component drops on your current progression route.
> - You are already 2 steps away from the relevant island.
>
> **Difficulty:** ★★★ Challenging
>
> `Track Quest` `Guide Me`

Another candidate might rank lower:

> Better raw stats, but requires later raid progression and several unmet prerequisites.

---

# 10. Difficulty Ordering

Quest lists should support default ordering from easiest to hardest where practical.

Difficulty must account for more than NPC level.

Suggested composite inputs:

- Encounter level
- Required party size
- Raid requirement
- Number of prerequisite steps
- Spawn rarity
- Drop rarity
- Travel complexity
- Key/access requirements
- Faction work
- Failure penalty
- Time-gated elements
- Number of zones involved
- Mechanical complexity
- Return/recovery cost

The stored model should preserve the component values so difficulty can be recalculated later.

---

# 11. Recommended Ordering Logic

When displaying available quests/objectives:

1. Remove impossible/inapplicable candidates.
2. Respect hard dependencies.
3. Prioritize required progression.
4. Prioritize objectives relevant to the player's classes.
5. Prioritize objectives with meaningful upgrades.
6. Prefer objectives whose prerequisites are already substantially complete.
7. Prefer objectives geographically/progression-wise near the player's current work.
8. Within comparable candidates, sort easiest to hardest.
9. Let the user override the sort.

Supported views should eventually include:

- Recommended
- Easiest First
- Best Reward
- Closest to Completion
- Progression Order
- By Zone
- By Slot
- By Class
- All

---

# 12. Active Quest UX

The active quest experience should answer the player's immediate needs without overwhelming them.

Suggested active card:

> # NEXT
>
> **Kill Gorgalosk**
>
> **Where:** Plane of Sky — Island 3, central structure
>
> Move from the arrival point toward the large central structure. Stay away from the outer edge. Gorgalosk begins inside with two nearby mobs.
>
> **What to do:** Defeat Gorgalosk and loot the progression key.
>
> **Why:** The key unlocks the next island.
>
> **While you're here:** Relevant Warrior / Monk / Druid quest components may also drop here.
>
> ⚠ **Before leaving:** Activate the progression key.
>
> ☐ Boss defeated  
> ☐ Key obtained  
> ☐ Next island unlocked
>
> `Directions` `Map` `Fight Details` `Relevant Loot` `Troubleshooting`

This model should work identically for a normal quest NPC conversation, an Epic quest step, or a Plane of Sky boss.

---

# 13. Quest Overview UX

Every quest should provide an `At a Glance` summary.

Required display information:

- Quest name
- Quest type
- Reward(s)
- Relevant class(es)
- Recommended level
- Difficulty
- Zone(s)
- Required group scale
- Major prerequisites
- Current completion state
- Current/next step
- Number of remaining steps

Example:

> ## Monk — Test of Tranquility
>
> **Reward:** Golden Sash of Tranquility
>
> **Difficulty:** ★★★★ Hard  
> **Content:** Plane of Sky progression  
> **Status:** 1 of 2 components obtained
>
> **You need**
> - ✓ Component A
> - ☐ Component B
>
> **Next:** Continue to the island containing Component B.
>
> `Resume Guide`

---

# 14. Quests & Guides Navigation

The existing Quests section should evolve toward **Quests & Guides**.

Supported content labels:

- QUEST
- EPIC
- ZONE GUIDE
- PROGRESSION
- KEYING
- CLASS QUEST
- GEAR

Suggested default card:

> **Plane of Sky**
> Zone Progression Guide
>
> **63% complete**
>
> Current: Island 5
> Next: Spawn and defeat the progression boss
>
> 6 relevant class rewards
> 4 obtained

One click should resume the current actionable step.

Do not bury “resume guide” behind multiple navigation layers.

---

# 15. Guide Modes

Complex guides should support multiple views over the same underlying data.

## Recommended

Default.

Combines:

- Dependencies
- Current progression
- Difficulty
- Relevant classes
- Known rewards
- Known inventory
- Player level
- Upgrade value

## Progression

Shows the strict required sequence.

Useful for:

- Plane of Sky
- Epic quests
- Keying
- Access chains

## Rewards

Shows relevant reward objectives, preferably easiest to hardest.

Useful when a player asks:

> “What gear can I get here?”

## Full Guide

Shows the complete authored reference.

---

# 16. Automatic Context and Detection

Where supported by legitimate game-log information, EQBuddy should use observed context to reduce user work.

Potential automatic signals:

- Zone entered
- NPC encountered
- Named mob killed
- Quest item looted
- Key looted
- Relevant spell/ability/class observed
- Inventory output reconciled
- Quest component acquired
- Boss spawn observed
- Known progression event observed

Examples:

- Enter Plane of Sky → offer/resume Plane of Sky guide.
- Loot a quest item → show what quest/reward it belongs to.
- Kill a relevant boss → advance the objective where confidence is high.
- Detect a class → prioritize relevant quests.
- Inventory reconciliation shows required component → update quest state.

No prohibited memory reading or unsupported telemetry should be introduced.

---

# 17. Manual Control and Corrections

Automation must never make the quest system brittle.

Users must be able to:

- Mark a step complete
- Mark a step incomplete
- Skip an optional step
- Reset quest progress
- Correct an incorrectly detected item
- Track/untrack a quest
- Choose a different branch
- Edit learned spawn information where current EQBuddy behavior already allows it
- View why EQBuddy believes a step is complete

Manual user state should take precedence over weak inference.

---

# 18. “While You're Here” Intelligence

One of the highest-value features of the guide engine should be contextual consolidation.

Whenever a player is at a zone, island, camp, NPC, or boss, EQBuddy should surface other relevant objectives that can efficiently be completed at the same time.

Example:

> ### While you're here
>
> **Required**
> - Kill Gorgalosk for progression
>
> **Relevant rewards**
> - Monk quest component
> - Warrior quest component
> - Druid quest component
>
> **Optional**
> - Farm another component from nearby mobs

This should be driven by shared structured entity references rather than separately authored duplicate text.

---

# 19. “Do Not Leave Yet” Checks

Before a meaningful progression transition, EQBuddy should be able to identify unresolved objectives associated with the current location.

Example:

> ### Ready to leave Island 5?
>
> ✓ Required progression complete  
> ✓ Key obtained  
> ⚠ One tracked class quest component is available on this island and has not been acquired.
>
> `Continue Anyway` `Stay and Farm`

This behavior should also apply to ordinary zones when returning later would be expensive or inconvenient.

---

# 20. Map and Direction Integration

Quest objectives should be able to reference the existing/future EQBuddy map system.

Required capabilities:

- Highlight quest NPC
- Highlight target mob/camp
- Highlight destination
- Highlight route where supported
- Show relevant spawn locations
- Show teleport/transition point
- Show next progression location

Directions and maps are complementary.

A map pin alone is not sufficient documentation, and prose directions alone should not prevent a map marker from being available.

EQBuddy should not imply real-time player location unless the location is legitimately known.

---

# 21. Plane of Sky Requirements

Plane of Sky should be the first high-complexity implementation of this generic engine.

It must include:

- Entry/preparation
- How to reach Plane of Sky
- How to leave
- Key/keyring explanation
- Fall/death/recovery behavior
- Quest chamber directions
- Relevant class quest NPCs
- Island-by-island progression
- Exact key requirements
- Boss spawn procedures
- Boss mechanics
- Travel between islands
- Immediate island hazards
- What not to miss before leaving
- Class quest components by island/boss
- Reward relationships
- Optional vs required objectives
- Noble/optional branch relationships
- Bee/spawn-chain mechanics
- Final-island progression
- Turn-ins
- Troubleshooting

Plane of Sky content must not be implemented as a one-off hardcoded screen.

It should prove the generic quest/guide engine.

---

# 22. Epic Quest Requirements

Epic quests require the same depth.

For every Epic step, EQBuddy should know:

- Required previous step
- NPC
- Location
- Directions
- Required dialogue
- Required item
- Required faction
- Spawn procedure
- Placeholder
- Respawn
- Fight mechanics
- Group/raid recommendation
- Loot
- Turn-in sequence
- Expected response/result
- Failure/recovery
- Next step

Epic quest guides should support multi-zone dependency graphs and long-term persistence.

A player returning after several days should be able to open the Epic and immediately see:

> **You are here. This is what you have already completed. This is the next thing you need to do.**

---

# 23. Normal Quest Requirements

“Normal” quests should not receive lower-quality documentation just because they are simpler.

A basic quest should still identify:

- Starting NPC
- Starting location
- How to reach the NPC
- Minimum/recommended level
- Required dialogue
- Required items
- Where each item comes from
- Mob/camp locations
- Drop/farm details
- Turn-in NPC
- Exact turn-in requirements
- Reward
- Difficulty
- Relevant classes
- Whether the reward is likely useful
- Troubleshooting
- Completion detection

Simple quests may have fewer stages, but not missing information.

---

# 24. Content Source and Accuracy Requirements

Quest content is only useful if players can trust it.

Every material game fact should support source metadata.

Suggested fields:

- `SourceId`
- `SourceType`
- `SourceUrl`
- `SourceTitle`
- `RetrievedAt`
- `LastVerifiedAt`
- `GameVersionOrEra`
- `Confidence`
- `Notes`

Preferred authority order for EQL-specific mechanics should be explicitly defined by the project.

Where EQL behavior conflicts with historical EverQuest behavior:

- EQL-specific data wins when reliably sourced.
- Historical data must not silently overwrite EQL data.
- Contradictions should be flagged for review.
- Unverified mechanics should be labeled as such rather than presented as fact.

Do not fill data gaps with guesses.

---

# 25. Canonical vs Learned Data

The system should distinguish:

## Canonical / Curated
Source-backed quest mechanics, NPCs, rewards, keys, known spawn rules.

## Learned
Information derived from the user's own observed logs, such as measured spawn timing.

## User Override
Manual user corrections or preferences.

These should not be silently merged into one confidence level.

---

# 26. Validation Rules for Authored Content

Before a quest guide is considered complete, automated/manual validation should check:

### Guide completeness
- Has reward data
- Has level/class applicability
- Has difficulty
- Has prerequisites
- Has at least one objective
- Has source metadata

### Objective completeness
Where applicable, each objective answers:
- Who
- Where
- What
- When
- How
- Why
- Result/reward
- Completion condition
- Next step
- Recovery

### Entity integrity
- Referenced NPC exists
- Referenced item exists
- Referenced zone exists
- Referenced reward exists
- Referenced prerequisite exists

### Dependency integrity
- No accidental dependency cycles
- No inaccessible step recommended before its prerequisite
- Optional steps cannot block progression unless explicitly intended

---

# 27. Search Requirements

Quests should be discoverable by more than quest name.

Search should eventually support:

- Quest name
- Reward item
- NPC
- Mob
- Zone
- Class
- Slot
- Item stat
- Key
- Epic
- Level range
- Camp
- Objective text

Examples:

> “haste”

> “warrior chest”

> “Plane of Sky monk”

> “Gorgalosk”

> “Sebilis key”

These should resolve to relevant quests, items, mobs, and guides.

---

# 28. Future Natural-Language Intent Layer

The data model should support higher-level user requests without redesign.

Examples:

> “I want to upgrade my gear.”

> “What should I work on at level 45?”

> “What quests can I solo?”

> “What Plane of Sky rewards matter for my classes?”

> “What can I get in this zone?”

> “What is the easiest upgrade for my offhand?”

> “I only have an hour. What should I work on?”

The initial implementation does not need a full conversational AI layer, but the schema must contain the attributes required to answer these questions deterministically.

---

# 29. Recommended Implementation Architecture

Conceptually:

```text
Quest / Guide Knowledge
        │
        ├── Guides
        │    ├── Stages
        │    └── Objectives
        │
        ├── NPCs / Mobs
        │    └── Spawns
        │
        ├── Zones / Maps
        │
        ├── Items / Rewards
        │
        ├── Keys / Access
        │
        └── Sources
             │
             ▼
      Dependency Graph
             │
             ▼
      Player State Model
      ├── Level
      ├── Classes
      ├── Gear
      ├── Inventory
      ├── Quest Progress
      └── Zone / Access
             │
             ▼
      Recommendation Layer
      ├── What can I do?
      ├── What should I do next?
      ├── What is easiest?
      └── What upgrades my gear?
             │
             ▼
      Quests & Guides UI
```

Do not create separate incompatible data silos for:

- Quests
- Epic quests
- Plane of Sky
- Gear recommendations
- Mob drops

They should share entities and relationships.

---

# 30. Suggested Core Domain Relationships

The data model should make relationships explicit:

```text
Class
  └── can use → Item

Item
  ├── rewarded by → Quest
  ├── dropped by → NPC
  ├── found at → Farm Location
  └── compares to → Equipped Item

Quest
  ├── consists of → Objectives
  ├── requires → Item / Key / Quest / Access
  ├── interacts with → NPC
  ├── occurs in → Zone
  └── rewards → Item / Access / Progression

Objective
  ├── occurs at → Location
  ├── targets → NPC
  ├── requires → Item
  ├── produces → Item
  └── unlocks → Objective / Access

NPC
  ├── spawns at → Spawn
  ├── drops → Item
  └── participates in → Quest

Player
  ├── has → Class / Level / Gear / Item
  ├── completed → Objective
  └── can access → Zone / Key
```

This graph is the foundation for future intelligent guidance.

---

# 31. Recommendation Example: “Upgrade My Gear”

Future expected flow:

> **Player:** I want to upgrade my gear.

EQBuddy knows:

- Level 45
- Warrior / Monk / Druid
- Current gear
- Enhancement ranks
- Current keys
- Completed quests

EQBuddy evaluates:

### Candidate A
Quest reward
- +18% improvement
- Current zone access
- Two steps remaining
- Group content
- ★★★ difficulty

### Candidate B
Named mob drop
- +12% improvement
- Farmable immediately
- 12-minute spawn
- Small group
- ★★ difficulty

### Candidate C
Plane of Sky reward
- +28% improvement
- Requires three additional islands of progression
- Raid content
- ★★★★★ difficulty

Default recommendation might be:

1. Candidate B — easiest meaningful immediate upgrade
2. Candidate A — stronger, still practical
3. Candidate C — best long-term target

The player can switch to:

`Best Possible`

and receive:

1. Candidate C
2. Candidate A
3. Candidate B

This is why quest, reward, drop, mob, level, class, access, and difficulty attributes must all be structured now.

---

# 32. Acceptance Criteria — MVP Guide Engine

A first implementation is successful when:

1. A normal quest can be fully authored in the new schema.
2. An Epic quest can use the same schema.
3. Plane of Sky progression can use the same schema without hardcoded special-case UI.
4. Each objective can present:
   - who
   - where
   - what
   - when
   - how
   - why
   - reward/result
   - next step
5. Quest progress can be automatically detected where logs provide reliable evidence.
6. Every step can also be manually corrected.
7. Relevant rewards can be filtered by class.
8. Optional objectives can be sorted easiest to hardest.
9. Dependencies prevent impossible ordering.
10. A reward item can link back to:
    - quest
    - source mob
    - source location
11. A mob drop can link to:
    - item
    - spawn
    - farm location
12. A future recommendation engine can query the same data without extracting facts from prose.

---

# 33. Acceptance Criteria — Content Quality

A guide should not ship as “complete” if a tester completing the quest must leave EQBuddy to answer a routine mechanical question that should reasonably be known.

For each guide, testing should explicitly ask:

> Could a player unfamiliar with this quest complete it using only EQBuddy?

If not, document the information gap and fix the guide.

This does not mean EQBuddy must know unknowable/dynamic information.

Where certainty is unavailable, it should clearly say so and provide the best supported guidance available.

---

# 34. Non-Goals

This project should **not**:

- Introduce prohibited memory reading.
- Pretend EQBuddy knows exact player position when it does not.
- Replace uncertainty with guessed mechanics.
- Create separate incompatible systems for Plane of Sky, Epics, and normal quests.
- Turn the UI into a giant static wiki page.
- Require users to manually maintain progress that EQBuddy can reliably infer.
- Remove manual controls in favor of automation.
- Optimize only for experienced players who already know the quest.

---

# 35. Implementation Priority

## Phase 1 — Foundation

- Shared guide schema
- Stage/objective model
- NPC/mob/item/zone references
- Dependency graph
- Source metadata
- Manual progression
- Basic active-step UI

## Phase 2 — Plane of Sky

Use Plane of Sky as the stress test.

- Full island progression
- Quest Chamber
- Keys
- Class rewards
- Spawn chains
- Hazards
- Recovery
- Relevant loot
- Do-not-leave checks

## Phase 3 — Normal Quest Conversion

Convert existing tracked quests into complete walkthroughs.

Do not simply wrap existing checklist text in the new UI.

## Phase 4 — Epic Quest Conversion

Implement end-to-end Epic guides using the same engine.

## Phase 5 — Contextual Intelligence

- Automatic quest-item recognition
- Auto-progress from logs
- Relevant-class filtering
- While-you're-here objectives
- Easier-to-harder ordering
- Map integration

## Phase 6 — Gear Recommendation Layer

Expose structured data as:

- Upgrade candidates
- Quest reward candidates
- Mob/drop candidates
- Farm-location candidates
- Attainability ranking
- Best-next-upgrade ranking

---

# 36. Product-Level Definition of Done

The evolved Quests system is complete when EQBuddy can credibly function as:

> **Your personalized guide to Norrath.**

A new player should be able to select a quest and follow it from beginning to end.

An experienced player should be able to skip the explanation and see the next action immediately.

A returning player should know where they left off.

A player finding a quest item should immediately understand what it is for.

A player seeking an upgrade should eventually be able to ask EQBuddy what to pursue, and EQBuddy should be able to recommend both:

- structured quest rewards
- structured mob-drop/farm alternatives

based on what is actually attainable for that character.

The critical architectural requirement is therefore:

> **Quest text is presentation. Structured game knowledge is the product.**

Build the knowledge model so that quests, mobs, drops, locations, rewards, access, progress, and player state can all be queried and recombined into personalized guidance without redesigning the system later.
