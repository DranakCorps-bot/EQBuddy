# EQBuddy v2 — Project Guide, Requirements, and Operating Model

**Working product identity:** **EQBuddy — Your Personalized Guide to Norrath**  
**Audience:** Helm, Fable, Bevel, Scribe, Claude/execution agents, contributors  
**Owner direction:** David / Dranak75  
**Status:** Owner-approved v2 direction and project charter  
**Revision:** 1.1 — competitive-review refinements incorporated before Helm handoff  
**Date:** 2026-09-04  

> **This document defines the v2 product direction.** Where an older roadmap, theme plan, agent inbox, or implementation convention conflicts with this document, this document wins for v2 unless David explicitly changes the direction.
>
> The purpose of v2 is **not** to add another layer of features to EQBuddy 1.x. The purpose is to turn the strong capabilities already built into a coherent, accurate, highly polished product that is clean, simple, personal, and easy to navigate.

---

## 1. Executive Direction

EQBuddy has reached the point where **feature breadth is no longer the primary constraint on product quality**. The parser, combat tracking, healing, pet/proc attribution, class inference, quest data, inventory ingestion, maps, spawn tracking, session history, and mobile companion already provide a strong foundation.

The v2 job is therefore:

1. **Preserve and harden the accurate core.**
2. **Remove platform and surface duplication.**
3. **Separate the live in-game HUD from the full desktop application.**
4. **Give every major capability one obvious home.**
5. **Build durable knowledge of the character, not just the current session.**
6. **Connect gear → quest → mob → camp → route into one personalized guidance chain.**
7. **Make EQBuddy proactive where useful, but never noisy or intrusive.**
8. **Use observed facts and verified game rules; identify estimates honestly.**
9. **Reduce the support/agent process to approval by exception rather than routine gates.**
10. **Finish and polish before expanding again.**

The intended product experience is:

> EQBuddy understands **who I am playing, what I can do, what I own, what I am working on, how I actually perform, and where I have been** — then quietly helps me decide what to do next.

---

# 2. Product Principles

These principles are requirements, not aspirations.

## 2.1 Personal, not competitive

EQBuddy measures and guides **the player using it**.

It may track:

- the player's damage and healing;
- the player's pets and charm pets where attributable;
- the player's inventory, gear, quests, progression and history;
- mobs, camps and world facts observed through the player's log;
- nearby casts/effects when the player's log legitimately exposes them, such as mez timing.

It must **not** become a party/raid ranking tool, leaderboard, coaching score, or mechanism for judging other players.

## 2.2 Log-only and local-first

Existing hard lines remain:

- no game-memory reads;
- no packet inspection;
- no gameplay automation or input broadcasting;
- no hidden-information extraction;
- no required account;
- no required cloud service;
- no telemetry by default;
- personal history remains local unless the player explicitly exports or contributes it.

## 2.3 Evidence before confidence

EQBuddy must distinguish both **where a fact came from** and **how confidently it may be used**. At minimum, the product/presentation model must be able to represent:

- **Observed** — directly witnessed in the player's log/output files;
- **Verified** — calculated from a verified rule or authoritative catalog;
- **Manual** — explicitly entered or corrected by the player;
- **Estimated / Inferred** — derived from incomplete historical/community/personal evidence;
- **Assumed / Fallback** — a deliberately used default or rule that is not authoritative for EQL;
- **Unknown** — insufficient evidence to make the claim honestly.

These labels need not be shown as six badges everywhere. The UI should expose them where the distinction affects trust or a decision, and carry the provenance in the underlying model so tooltips/detail views can explain it.

Examples:

- `Spawn: 22m 30s · observed from 8 kills`
- `Class: Druid · inferred from 3 unique spells + ability evidence`
- `Travel ritual: Ring of Toxxulia · verified from character history`
- `Exaltation compatibility: assumed · game rule not yet authoritative`
- `Inventory: observed · refreshed 6 min ago`

A precise-looking wrong number is worse than a clearly identified estimate. **Missing/unmatched data must be surfaced rather than silently discarded when it could change the answer.**

## 2.4 Fewer definitions, not merely fewer screens

Consolidation succeeds only when one domain has one source of truth and one obvious path.

A feature rendered three different ways from three separately maintained definitions is still duplicated even if all three renderers happen to live in one window.

## 2.5 One click to the domain; one more to the answer

For primary workflows:

> **One navigation action reaches the domain. One additional action reaches the useful answer.**

Examples:

- Gear → Primary
- Quests → Monk Epic
- World → Camps
- Progress → Unlocks
- Search → Hierophant's Crook

A primary capability that requires hunting through a context menu, enabling a hidden card, opening a second list, then drilling into another window is a v2 UX defect.

## 2.6 Deadline information earns HUD space

The in-game HUD is for information that may change the player's next action **within seconds or minutes**.

Examples:

- mez/charm timers;
- spawn-due timers;
- watch alerts;
- important buff expiration;
- live DPS/HPS glance metrics.

Research, comparison, planning, configuration and retrospective analysis belong in the full application.

## 2.7 No modal interruptions for ordinary information

Informational events such as looting a quest item should normally use a dismissible toast/chip, not a modal window that steals attention during play.

---

# 3. Platform Strategy and Legacy Preservation

## 3.1 Supported v2 platform

**EQBuddy v2 desktop support is Windows only.**

The Windows WPF product becomes the canonical desktop application.

The browser-based EQBuddy Mobile / phone / tablet experience may continue as a **second screen hosted by the Windows application**. Mobile support does not imply Linux/macOS desktop support.

## 3.2 Legacy Linux/macOS preservation policy

Windows-only v2 must **not strand existing Linux or macOS users**.

The distinction is:

- **Preserved:** yes.
- **Actively supported for v2 development:** no.

### LEGACY-001 — Final v1 bridge release

Before v2 becomes the normal/latest release channel, ship a final **1.x legacy bridge release** containing the last supported:

- Linux x64 build;
- macOS ARM64 build;
- macOS x64 build;
- Windows 1.x artifacts as appropriate.

The exact 1.x version number is chosen when the bridge is ready; do not hard-code a version in this charter.

### LEGACY-002 — Stop non-Windows v1 from chasing v2

Current non-Windows builds use the same GitHub `releases/latest` feed as Windows. The bridge release must change legacy update behavior so a v1 Linux/macOS installation does **not** repeatedly advertise Windows-only v2 as an installable update.

Preferred behavior:

1. If a non-Windows v1 client detects that the newest public release has major version 2 or later, show a **one-time** notice:
   - v1 is the final legacy build for this platform;
   - the current installation will continue to work;
   - v2 development is Windows-only;
   - the final legacy release remains available.
2. After acknowledgement, suppress recurring v2 update nags on that installation.
3. Never present a Windows v2 installer as usable on Linux/macOS.

### LEGACY-003 — No forced migration or self-removal

No v2 release mechanism may:

- delete a legacy Linux/macOS installation;
- overwrite it with a Windows artifact;
- invalidate its local settings/history intentionally;
- require migration to v2 to continue using the already-installed v1 product.

### LEGACY-004 — Permanently retain final artifacts

Do not delete the final legacy release assets, release notes, tag, or source state from GitHub.

Existing users and future community users must remain able to download the final v1 Linux/macOS binaries.

### LEGACY-005 — Preserve source for forks/community continuation

Create a clear final legacy tag and preferably a stable source branch such as `legacy-v1` from the final cross-platform state before removing Avalonia from the v2 mainline.

The project may explicitly invite community forks or independent continuation. This is not a commitment by David to maintain those platforms.

### LEGACY-006 — Clear support matrix

README/release documentation must clearly state:

| Surface | Status after v2 |
|---|---|
| Windows desktop v2 | Supported |
| EQBuddy Mobile hosted by Windows | Supported v2 surface |
| Linux desktop v1 | Preserved legacy; no v2 feature/support commitment |
| macOS desktop v1 | Preserved legacy; no v2 feature/support commitment |

### LEGACY-007 — v2 release page must not obscure legacy access

The first v2 release notes and README must contain a visible **Legacy Linux/macOS** section linking users to the final v1 release.

This is especially important for users running pre-bridge builds that still see the generic GitHub latest-release page.

---

# 4. Target v2 Product Structure

EQBuddy v2 should behave as two coordinated product surfaces plus the optional mobile second screen.

## 4.1 Surface A — EQBuddy HUD

A small, movable, always-on-top live surface designed to remain on screen while playing.

The HUD is **not** the full application.

### Default expanded concept

```text
Dranak · WAR / MNK / DRU
DPS 412    HPS 38    XP 8.2%/hr

[ Mez · froglok shaman · 0:08 ]
[ Lord Ghiosk · respawn 0:21 ]
[ Clarity · 0:34 ]
```

### Collapsed concept

```text
Dranak · 412 DPS · 8.2%/hr
```

The exact visual design remains Bevel's responsibility, but the interaction model is fixed: **glance first, expand for live detail, full application for analysis.**

## 4.2 Surface B — Full Windows application

One coherent Windows shell with persistent primary navigation.

Recommended top-level information architecture:

1. **Home**
2. **Live**
3. **Progress**
4. **Gear**
5. **Quests**
6. **World**
7. **Search**
8. **Settings**

Search may be a global affordance rather than consuming permanent navigation space; implementation may choose the cleanest design.

The key requirement is **one shell and one navigation model**, not a collection of loosely related top-level windows.

## 4.3 Surface C — EQBuddy Mobile

Phone/tablet remains an optional LAN-only second screen hosted by Windows.

High-value mobile surfaces include:

- map/camps;
- active/tracked quest information;
- item/gear lookup;
- live meters/session totals where useful;
- route guidance.

A phone/tablet is for information worth looking away from the main monitor to inspect. It is not required to mirror every desktop control.

---

# 5. UX Requirements

## UX-001 — One canonical door per domain

Every major capability must have a single obvious home.

Do not expose the same primary action through several unrelated menus/windows unless one is a deliberate shortcut.

## UX-002 — Context menu is not primary navigation

The HUD context menu may retain system/in-play utilities such as:

- show/hide;
- lock/unlock/click-through;
- open EQBuddy;
- exit.

Primary domains such as World, Gear, Quests, History/Progress, imports or configuration must not depend on context-menu discovery.

## UX-003 — Eliminate breakout-window choreography

DPS/HPS/Pet analysis should not require users to understand a star system that creates separate independently managed breakout windows.

Live glance information belongs in the HUD. Detailed meter analysis belongs in **Live**.

Separate floating live meters may exist only if they serve a proven use case and share one configuration/positioning system; they must not be the only way to access the detail.

## UX-004 — Search Norrath globally

Provide a global search capability across available catalogs for at least:

- items;
- NPCs/named mobs;
- zones;
- quests.

Where useful, also include spells/abilities.

A result should expose relevant personal context and direct actions.

Example:

```text
Hierophant's Crook
Weapon · usable by <classes>
Drops from <mob> · <zone>
Relevant quests: <...>
[Compare to my gear] [Guide me there]
```

## UX-005 — Universal “Guide Me There” action

Wherever a destination appears — quest step, named, camp, gear upgrade, search result — the same **Guide Me There** action should be available when routing data exists.

## UX-006 — Good empty states

Every empty page must explain why it is empty and what the player can do.

Bad:

> No data.

Good:

> EQBuddy has not seen an inventory dump for Dranak yet. Type `/outputfile inventory` in game; EQBuddy will read it automatically when the game finishes writing it.

## UX-007 — Do not expose implementation vocabulary

Players should not need to understand internal concepts such as projection fingerprints, reconciliation, ledger sources, or parser channels.

## UX-008 — Keep advanced controls advanced

Power-user configuration should remain available, but common use must work without configuration.

A new player should receive value by installing EQBuddy and playing normally.

## UX-009 — No fake precision

Recommendations and estimates should express confidence appropriately.

Use ranges or labels when evidence is weak rather than displaying falsely exact values.

## UX-010 — Windows-native polish

With cross-platform parity removed, v2 should deliberately optimize for Windows:

- crisp DPI/scaling behavior;
- consistent WPF controls and typography;
- predictable Alt+Tab/taskbar behavior;
- correct focus and click-through behavior over the game;
- no Wine/CrossOver-specific settings in the v2 UI;
- keyboard accessibility for major navigation where practical.

## UX-011 — Contextual readiness instead of setup friction

EQBuddy should quietly identify missing/stale inputs that materially limit a page and offer the shortest action needed to fix them.

Examples:

- logging is off / no live log found;
- inventory has never been observed or is stale;
- achievement/class-unlock history is missing;
- a catalog required for an answer is unavailable/out of date.

Requirements:

- show only inputs relevant to the current character/workflow;
- explain exactly what the player should do in game (for example `/outputfile inventory`);
- automatically clear the suggestion when the requirement is satisfied;
- allow dismissal when the input is optional;
- never make a permanent onboarding checklist another navigation destination.

The goal is **install → play → value**, with EQBuddy asking for additional evidence only when it enables a useful answer.

---

# 6. Functional Requirements — Live HUD and Combat

The following systems are perceived as substantially complete and should be **preserved, validated and integrated**, not rewritten merely to make v2 feel new.

## LIVE-001 — Accurate personal DPS

Track personal damage accurately across all observable sources, including where supported by the log:

- melee skills;
- combat abilities;
- spells/direct damage;
- DoTs;
- songs/invocations where attributable;
- pets;
- charm pets;
- procs;
- other verified player-owned damage sources.

## LIVE-002 — Compact aggregated DPS

Collapsed HUD must display a clear aggregated DPS metric.

The aggregation definition must be documented in-app/tooltip and remain consistent. Preferred total: player-owned damage including attributable pet/charm-pet damage, with transparent breakdown available.

## LIVE-003 — Expanded DPS breakdown

Expanding live DPS must show the current/live breakdown without opening a different application concept.

At minimum expose:

- skill/ability/spell source;
- proc contribution;
- pet contribution;
- current fight and session context where useful.

## LIVE-004 — Accurate personal HPS

Track outbound healing attributable to the player across observable healing sources.

Expanded detail should break healing down by spell/ability/source and separate verified healing from estimates such as configured regeneration when applicable.

## LIVE-005 — Encounter analysis

The **Live** application page should provide detailed combat analysis including:

- current/last encounter;
- session combat;
- damage breakdown;
- healing breakdown;
- pet breakdown;
- timeline/encounter history where useful;
- explicitly labeled DPS denominator/model.

## LIVE-006 — Mez countdowns

Display mez countdown chips when the player's log provides sufficient evidence that a mez landed, including mez cast by a nearby player when observable.

The system must not imply knowledge outside log visibility/range.

## LIVE-007 — Spawn timer chips

Named/important mob respawn timers remain deadline information and may appear as HUD chips.

Preserve:

- learned timers from personal evidence;
- source/provenance;
- manual correction/edit capability;
- conservative behavior when timing evidence is insufficient.

## LIVE-008 — Unified HUD edit mode

HUD and chip placement should be managed through one obvious **Edit HUD** / unlock mode.

Users should not have to open unrelated options pages to discover where live overlays can be moved.

---

# 7. Functional Requirements — Persistent Character Profile

A major v2 addition is a durable profile that represents the character across sessions.

## PROFILE-001 — Character identity

Persist character state by stable character/server identity.

## PROFILE-002 — Active class trio

Preserve current automatic class inference from observed skills, abilities, songs and spells.

Support up to the EQL class-combination limit already used by EQBuddy.

The UI must clearly distinguish **inferred active classes** from verified/unlocked historical classes.

## PROFILE-003 — Historical unlocked classes

Persist classes known to have been unlocked by the character from authoritative player-specific evidence such as achievement imports.

This list must survive session/class changes.

## PROFILE-004 — Travel capabilities

Persist known character travel capabilities such as verified Druid/Wizard portal/ritual abilities when EQBuddy has evidence they are unlocked/usable.

The route engine may use these historical abilities even when the corresponding class is not currently equipped if the game allows the ritual to remain usable.

## PROFILE-005 — Durable level state

Persist:

- current known level;
- last known position/progress within the level where supportable;
- XP accumulated since the last reliable baseline;
- level-up history;
- provenance/confidence of the progress state.

A new EQBuddy session must not reset the conceptual question “how far am I into this level?” to zero.

## PROFILE-006 — Inventory and equipment snapshot

Persist the newest trusted inventory/equipment snapshot per character, including timestamp and source.

## PROFILE-007 — Personal history inputs

The profile may reference/query durable history needed for recommendations, including:

- historical fight results;
- kills by mob/zone;
- loot history;
- death history;
- XP rates;
- visited zones/camps;
- named kills;
- quest progress.

Do not duplicate full session data unnecessarily; this may be a query layer over existing history.

---

# 8. Functional Requirements — Progress

The **Progress** page is the canonical home for character development and session earning rates.

## PROGRESS-001 — Current level progression

Display clearly separated concepts:

- level and percent complete;
- XP gained this session;
- XP/hour;
- active XP/hour where useful;
- projected time to next level;
- average XP per eligible/attributable kill where honestly calculable.

Do not label “XP seen this session” as though it were absolute level completion.

## PROGRESS-002 — Resume accurately across sessions

Projected time to next level must use durable current-level progress, not merely assume the current session began at 0% into the level.

## PROGRESS-003 — Current-level unlocks

Show abilities/spells/skills unlocked or available at the current level, grouped by the character's detected/selected classes.

## PROGRESS-004 — Next-level unlocks

Show what becomes available at the next level, grouped by class.

## PROGRESS-005 — Session skill-ups

Track and display actual skill-ups observed this session separately from catalog-based “available at this level” unlocks.

## PROGRESS-006 — Motes/hour

Display:

- total motes gained this session;
- motes/hour;
- breakdown by mote type;
- totals by type.

## PROGRESS-007 — Money/hour

Display at minimum:

- net/total money gained this session under the product's defined model;
- money/hour;
- coin gained from kills where observable;
- value/coin gained from loot sold where observable.

Every category must be based on events EQBuddy can legitimately distinguish.

## PROGRESS-008 — Level history

Retain durable level-up history and make it easy to inspect without hiding it behind multiple folds.

## PROGRESS-009 — Personal leveling history by zone

Where the log provides sufficient evidence, let the player inspect where they have actually leveled, including useful historical measures such as:

- active play time in zone;
- observed XP gained;
- observed XP/hour;
- kills/hour;
- deaths or other meaningful risk signals.

This history is primarily an input to Home recommendations and World/camp guidance, not a requirement for another permanent top-level dashboard.

Do not treat wall-clock gaps/AFK time as productive zone time. The active-time definition must be explicit and tested.

---

# 9. Functional Requirements — Gear

Gear is one of the largest v2 product opportunities.

## GEAR-001 — Paper-doll equipment view

The Gear landing page should show the character's current equipment by actual EQL equipment slot.

Use the game's real slot identities from inventory/output data. Paired/multiple slots such as ears/rings/any slots must be represented distinctly where the data permits.

Do not invent slot semantics that cannot be mapped to the player's actual inventory dump.

## GEAR-002 — Click a slot to evaluate upgrades

Selecting an equipment slot should show:

- equipped item;
- owned alternatives;
- recommended attainable upgrades not currently owned;
- item source/acquisition path;
- usability for the player's class combination;
- enhancement-tier comparison;
- direct actions such as Compare / Track / Guide Me There.

## GEAR-003 — Exact +0 through +10 modeling

The current conservative behavior that refuses to compare enhanced items without trustworthy enhancement math is correct for v1.

For v2, enhancement-aware recommendations are required, but implementation must use **verified EQL enhancement rules** and regression tests — never a guessed linear model.

For a candidate item, EQBuddy should be able to answer:

> My equipped +10 item is better than this candidate at +2, but the candidate becomes an upgrade at +4.

## GEAR-004 — Show crossover tier

Where one item overtakes another at a particular enhancement tier, state the crossover directly.

Example:

```text
Current: Baron's Blade +5
Candidate: <Item>

+0  worse
+1  worse
+2  worse
+3  roughly even
+4  upgrade
...
+10 +<verified delta>
```

## GEAR-005 — Whole-character comparison where justified

When evaluating gear, prefer showing the effect on the character's aggregate relevant stats rather than only asserting that one item's raw row dominates another.

Do not collapse different build priorities into a mysterious single score unless the scoring model is explicit and user-selectable.

## GEAR-006 — Class usability

Never recommend an item the character cannot use under verified class restrictions.

If active classes are uncertain, use historical verified classes where appropriate and label the assumption.

## GEAR-007 — Achievable upgrade ordering

Recommend upgrades in ascending order of practical acquisition difficulty for each slot.

Difficulty vocabulary should be understandable rather than falsely numeric. Suggested classes:

- Owned / Ready
- Easy
- Moderate
- Group
- Raid
- Unknown

The exact model may evolve, but its inputs and uncertainty must remain explainable.

## GEAR-008 — Acquisition evidence

Difficulty/recommendation inputs may include:

- mob/content level;
- whether the character has killed the mob before;
- historical fight duration and survival;
- zone accessibility;
- spawn rarity/timer confidence;
- quest-chain requirements;
- items already owned for the quest;
- raid classification;
- tracked personal history.

## GEAR-009 — Immediate loot upgrade notice

When the player loots an item that is clearly better than currently equipped gear — or becomes better at a known enhancement tier — EQBuddy may show a lightweight toast:

```text
Potential upgrade: <Item>
Beats <equipped> at +3
[Compare] [Dismiss]
```

The notice must not fire when the comparison is uncertain.

## GEAR-010 — Full owned-gear context

Gear evaluation should understand the player's **owned copies**, not only the currently worn row. Where the inventory export exposes them, include relevant items in:

- worn slots;
- bags;
- bank;
- other character-owned storage represented reliably by the output format.

For duplicate items, preserve the actual +N tier and location so EQBuddy can answer questions such as:

> You are wearing this item at +3, but you already own a +5 copy in the bank.

Do not count ambiguous keyring/trailing sections as equipped unless the output format proves that meaning.

## GEAR-011 — Exaltation ownership and placement intelligence

Where the game/output data and verified catalogs permit, Gear should understand owned exaltations/effects separately from the host item. Useful answers include:

- which focus/proc/worn/click effects the character owns;
- where an effect is currently socketed/active;
- duplicate or stronger owned versions;
- compatible candidate hosts **only when compatibility is verified**;
- potential moves when rules are incomplete, clearly labeled **assumed** rather than presented as fact.

This capability belongs inside Gear/item analysis rather than becoming a new top-level v2 domain.

## GEAR-012 — Personal acquisition memory

When EQBuddy has personally observed an item being looted, preserve enough history to answer:

- where did I get this?
- what mob/source dropped it?
- what zone was I in?
- have I obtained it more than once?

This evidence should enrich item detail, upgrade recommendations, Search and Guide Me There. It must not override verified catalog source data; personal observations are complementary evidence.

---

# 10. Functional Requirements — Quests

Quests should become an action-oriented personal workflow rather than a large catalog the player must mentally interpret.

## QUEST-001 — Unified quest home

Keep one **Quests** domain with clear subtypes such as:

- General
- Epic
- Plane of Sky

Do not re-create separate top-level cards/windows for each quest category.

## QUEST-002 — Default class lens

Epic/Sky views should default to the character's currently detected class combination where applicable.

Allow the player to manually multi-select from relevant known classes.

## QUEST-003 — Multiple useful sort/group lenses

Support at least:

- **By progression/content level** — what is realistic now versus later;
- **By zone** — what can be advanced while in/near a zone;
- **By required sequence** — prerequisite/order-of-operations where the quest requires it.

## QUEST-004 — Show rewards prominently

Quest details must show reward(s), class relevance and useful item comparison where catalog data supports it.

## QUEST-005 — Quest-item loot suggestion

When an item is looted that is known to belong to a quest relevant to the character's selected/current classes, show a non-modal toast/chip.

Preferred actions:

- **Track**
- **View**
- **Not now**
- **Don't suggest this quest**

Do not force a modal popup during combat.

## QUEST-006 — Tracked quest summary

A tracked quest should surface the **next actionable step**, not permanently occupy HUD space with the entire quest chain.

## QUEST-007 — Quest step routing

Any tracked step with a known destination should expose **Guide Me There**.

## QUEST-008 — Inventory-aware completion

Continue using inventory/output evidence to reconcile quest item ownership and completion where logically valid.

Automatic changes must remain explainable and reversible where the evidence is not inherently permanent.

---

# 11. Functional Requirements — World and Guide Me There

## WORLD-001 — Unified World domain

World remains the home for:

- Map
- Camps/spawn timers
- Path/route guidance
- travel/death/history context where useful

## WORLD-002 — Map and spawn integration

The map should show learned/known spawn positions and active timers without requiring a separate spawn application concept.

## WORLD-003 — Capability-aware route graph

Travel routing must model paths as graph edges with requirements/costs where necessary.

Examples may include:

- normal zone connections;
- boats;
- verified teleport/portal/ritual routes;
- other known travel mechanics.

## WORLD-004 — Historical class capability matters

The router must use durable character capabilities, not merely classes currently equipped.

Example requirement:

> If the player is in Butcherblock, wants to reach The Hole, and has previously unlocked a usable Ring of Toxxulia ritual, EQBuddy should be able to route through Toxxulia/Paineel rather than assuming the player must take a purely physical route.

## WORLD-005 — Explain route choice

When a special capability changes the recommended route, make that visible.

Example:

> Fastest route uses your unlocked Ring of Toxxulia ritual.

## WORLD-006 — Universal destination model

Items, quests, named mobs, recommendations and search results should all resolve to the same destination/routing model where possible rather than each implementing routing separately.

---

# 12. Functional Requirements — Home and Recommendations

Home is where EQBuddy becomes a personalized guide rather than a collection of trackers.

## HOME-001 — Answer “what should I do next?”

Home should summarize the character and surface a small number of high-value next actions.

Recommended default categories:

- Level faster
- Upgrade gear
- Continue quests
- Farm motes
- Make money

The UI may present these as goals/filters rather than a permanent wall of sections.

## HOME-002 — Prefer three strong recommendations to thirty weak ones

Default recommendation lists should be concise and ranked.

A recommendation should explain **why it is being shown**.

Example:

```text
Lower Guk — Bedroom / Assassin

Expected XP: 7–9%/hr
Your avg kill: 47 sec
Downtime: low in your recent sessions
3 possible gear upgrades
2 tracked quest steps nearby

[Guide me there]
```

## HOME-003 — Personal evidence outranks generic advice

Where sufficient personal history exists, recommendations should use actual observed performance.

Useful evidence may include:

- DPS;
- incoming damage;
- deaths;
- average fight duration;
- healing received;
- avoidance;
- pull/kill cadence;
- downtime;
- observed XP/kill or XP/hour;
- prior kills of the same/similar mobs.

## HOME-004 — Generic fallback must be labeled

When EQBuddy has insufficient personal evidence, it may recommend from verified catalog/community information, but must identify that it is an estimate rather than “your expected rate.”

## HOME-005 — Recommendations connect domains

A useful recommendation should be able to combine multiple reasons:

> Good XP for your observed performance + a gear upgrade + a tracked quest step in the same area.

This cross-domain chain is a key EQBuddy differentiator.

## HOME-006 — Do not overclaim survivability

Do not claim a camp is safe merely because the player's DPS is high.

Use actual survival/history signals where available. If insufficient, say so.

---

# 13. Functional Requirements — Search

## SEARCH-001 — Search from anywhere

Provide a global search affordance available from the primary application shell.

A keyboard shortcut such as `Ctrl+K` is recommended if it fits the final Windows design.

## SEARCH-002 — Unified result object

Search results should connect to the same domain detail views used elsewhere.

Do not build a second item-detail implementation just for search.

## SEARCH-003 — Personal context on results

Where useful, search should answer questions such as:

- Do I own it?
- Am I wearing it?
- Is it an upgrade for me?
- Is it used in one of my quests?
- Have I killed the mob before?
- Is there an active timer?
- Can EQBuddy route me there?

## SEARCH-004 — Personal-history answers

When local history supports it, search/detail should expose remembered personal facts without forcing the player into Session History.

Examples:

- `Last looted from: a frenzy ghoul · Lower Guk`
- `Observed drops: 3`
- `You have killed this named 11 times`
- `You previously averaged 8.1% XP/hr in this zone`

These are **Observed** personal-history facts and should remain distinguishable from catalog/wiki claims.

---

# 14. Functional Requirements — Mobile / Second Screen

## MOBILE-001 — Windows-hosted LAN-only model remains

EQBuddy Mobile remains optional, local/LAN-focused and disabled until the player enables it.

## MOBILE-002 — Device chooses useful surfaces

A paired phone/tablet may choose which supported surfaces it shows rather than being forced to duplicate the desktop layout.

## MOBILE-003 — High-value v2 mobile surfaces

Priority:

1. World map/camps/routes
2. Tracked quests
3. Gear/item lookup
4. Live/session metrics where useful

## MOBILE-004 — No desktop-platform parity requirement

“Mobile is supported” must never be interpreted as a requirement to maintain a second desktop UI toolkit.

---

# 15. Accuracy, Diagnostics, and Test Requirements

Accuracy should become a visible v2 quality differentiator.

## ACCURACY-001 — Real-log golden corpus

Create a curated regression corpus of **literal real EQL log specimens** covering major event families.

At minimum:

| Domain | Cases |
|---|---|
| Melee | hits, misses, crits, specials, riposte/avoidance as applicable |
| Spells | casts, DD, DoT, AoE, resist/failure cases |
| Procs | weapon/exaltation/clicky ambiguity and known patterns |
| Pets | summon, permanent pet, damage, kill credit, charm/break/reclaim |
| Healing | direct, HoT, regeneration, group patterns where observable |
| XP | solo, party, bonus-event variants, level-up |
| CC | mez/charm/slow/fade patterns |
| Loot/Money | loot, coin, sale/trade events that feed supported stats |
| Progress | skill-ups, AA, level |
| Output files | inventory, achievements, factions |
| World | zone, loc, named kill, spawn evidence |

## ACCURACY-002 — Every parser escape becomes a permanent fixture

When a real support report exposes a parser/attribution defect, the reporter's literal evidence must become a regression test when legally/practically appropriate.

Support should continuously improve the compatibility corpus.

## ACCURACY-003 — Unrecognized relevant-line diagnostics

Provide local diagnostics that can identify suspicious/unparsed lines associated with known domains.

Example:

> 7 lines looked related to XP/progression but were not recognized.

Offer a user-controlled **Copy diagnostic sample** action.

No telemetry or automatic upload is required.

## ACCURACY-004 — No silent parser degradation

Major catalog/parser harvest changes must have sanity guards that fail visibly when event/catalog coverage collapses unexpectedly.

## ACCURACY-005 — Provenance on learned world facts

Spawn timers/locations and other learned facts should continue carrying enough provenance to distinguish:

- personal observation;
- manually entered values;
- wiki/catalog values;
- estimates.

## ACCURACY-006 — Release blockers

A known high-confidence defect that materially corrupts DPS, HPS, XP, class detection, quest completion, gear recommendations or route guidance is a release blocker unless David explicitly accepts the risk.

## ACCURACY-007 — Coverage failures must be visible

When an import/parser/catalog operation cannot place meaningful input, EQBuddy should prefer:

> parsed 241 rows · 3 unrecognized

over silently returning 241 rows as though coverage were complete.

This applies particularly to:

- output-file imports;
- quest/catalog harvests;
- item/stat joins;
- compatibility/routing prerequisites;
- any source whose omissions could change a recommendation.

The ordinary player should see a concise caveat; diagnostics may carry the exact rejected records.

## ACCURACY-008 — Revisioned, idempotent historical backfill

When EQBuddy learns a new log shape or adds a new derived-history capability, it should be possible to improve existing local history from the player's retained log without resetting the profile or duplicating prior facts.

Use an explicit parser/derivation revision or equivalent migration contract so a version can determine which historical facts need reprocessing.

Requirements:

- backfills are idempotent;
- additive facts are deduplicated by stable identity/evidence;
- interruption is safe to resume;
- the UI may show a temporary `Updating history…` state rather than presenting partial data as complete;
- reprocessing should target the required event families where practical rather than blindly rebuilding everything;
- original logs/output files remain source evidence and are never modified.

This is especially valuable for progression, loot/source history, zones, quests and future parser compatibility fixes.

---

# 16. Architecture Direction

## 16.1 Do not rewrite the proven engine for aesthetics

v2 is **not** permission to rewrite Core because a new architectural pattern is fashionable.

Preserve the proven parser/aggregation/data model where behavior is correct.

Refactor when doing so:

- deletes duplication;
- creates a necessary v2 seam;
- improves testability of meaningful behavior;
- removes a known hotspot/responsibility;
- makes the new shell practical.

## 16.2 Remove Avalonia from the v2 mainline

After the final legacy bridge/tag/branch is created:

- remove `EQBuddy.Avalonia` from v2 mainline;
- remove Avalonia desktop parity obligations;
- remove Linux/macOS CI/release jobs from v2;
- remove Avalonia-only tests from v2;
- remove Wine/CrossOver UI accommodations from v2 Windows UI;
- update documentation to match the support matrix.

This is deliberate scope reduction, not a temporary parity gap.

## 16.3 Preserve framework-free presentation logic

The current Core / UI.Shared separation has substantial value.

Logical target:

```text
EQBuddy.Core
    parsing, events, aggregation, catalogs, history, character/profile rules

EQBuddy.Presentation (current UI.Shared may be evolved/renamed)
    framework-free presentation models, row builders, text, sorting, recommendations

EQBuddy.Companion
    Windows-hosted LAN/mobile projection + web surface

EQBuddy.Windows
    one WPF application shell + HUD
```

Renaming projects is optional if churn outweighs clarity. **The boundary matters more than the namespace.**

## 16.4 MainWindow should lose responsibility, not just lines

Do not make v2 a project whose success is measured by slowly shaving a monolithic widget while preserving all of its conceptual responsibilities.

The new HUD should need only the responsibilities required for a live HUD.

The full application shell owns navigation/pages.

## 16.5 Avoid architecture for architecture's sake

Do not introduce a large DI framework, message bus, plugin architecture, or new UI framework merely because v2 is a major version.

Use the simplest boundaries that keep domains testable and understandable.

## 16.6 One domain presentation model

Where desktop, HUD and mobile show the same fact, they should consume a shared domain/presentation definition rather than separately reproducing business rules.

---

# 17. Data Migration and Rollback Safety

## DATA-001 — Preserve current Windows users' history/settings where meaningful

v2 should migrate useful 1.x state rather than treating users as new installs.

Examples:

- character/session history;
- spawn knowledge;
- quest progress;
- gear/inventory state where still valid;
- watch rules;
- class/unlock history;
- useful UI preferences that map cleanly to v2.

## DATA-002 — Do not preserve obsolete complexity merely for settings compatibility

Old settings that exist only because v1 had duplicate surfaces may be translated into sensible v2 defaults instead of recreating the old surface.

## DATA-003 — Pre-v2 backup

Before the first destructive/schema-changing v2 migration, create a local pre-v2 backup sufficient to recover the v1 data store/settings.

## DATA-004 — Idempotent migration

Migration should be safe to rerun after interruption and must not duplicate history/ledger entries.

## DATA-005 — Legacy platform isolation

Windows v2 migration changes must not alter or invalidate preserved Linux/macOS v1 release artifacts.

## DATA-006 — Freshness is part of character state

Any recommendation based on a snapshot-like source must know when that source was last observed.

Examples:

- inventory/equipment snapshot time;
- achievement/unlock import time;
- faction/other optional character exports if retained;
- catalog revision/source date where relevant.

A stale source may still be useful, but EQBuddy should not present stale inventory as unquestionably current when the distinction could change an upgrade/quest answer.

---

# 18. v2 Non-Goals / Explicit Deferrals

Unless David explicitly changes direction, v2 does **not** need:

- Linux/macOS desktop parity;
- party/raid DPS ranking;
- leaderboards;
- gameplay automation;
- cloud accounts;
- required online sync;
- a new plugin ecosystem;
- a replacement for eqlwiki;
- automatic publishing of learned world data;
- a standalone tradeskill-management domain merely for competitor parity;
- expansion of faction tracking into a separate major product domain unless it directly completes a defined v2 workflow;
- feature-for-feature parity with another EQL companion;
- every community feature suggestion received during the v2 build;
- a proliferation of new floating widgets;
- a new UI framework rewrite.

A new capability should normally be admitted to v2 only if it either:

1. completes one of the defined v2 domains, or
2. removes a meaningful user step/friction point.

Otherwise backlog it until after the v2 polish gate.

---

# 19. Recommended v2 Delivery Sequence

The phases below describe dependency order, not calendar commitments.

## Phase 0 — Protect v1 users before cutting platforms

**Goal:** v2 can move Windows-only without harming current Linux/macOS users.

Deliverables:

- final 1.x cross-platform bridge release;
- non-Windows v2 update suppression/one-time notice;
- final legacy tag;
- recommended `legacy-v1` branch;
- retained release artifacts;
- support matrix and legacy links;
- documented transition.

**Gate:** A Linux or Mac user on the final bridge build can continue using v1 after v2 ships without being asked to install an incompatible Windows build.

## Phase 1 — Subtract platform and surface debt

Deliverables:

- remove Avalonia from v2 mainline;
- simplify CI/release pipeline to Windows + relevant Core/mobile tests;
- remove obsolete platform UI options/docs;
- inventory current v1 features and classify each:
  - Keep
  - Merge
  - Replace
  - Advanced
  - Remove

**Gate:** No v2 requirement is blocked on non-Windows desktop parity.

## Phase 2 — Build the v2 shell and HUD

Deliverables:

- one Windows application shell;
- Home / Live / Progress / Gear / Quests / World navigation structure;
- global search affordance;
- Settings as settings, not a window launcher directory;
- new small HUD with minimized/expanded state;
- unified HUD/chip placement/edit mode;
- current live metrics integrated into HUD/Live.

**Gate:** A user can find every retained primary feature without the legacy context-menu maze.

## Phase 3 — Accuracy and CharacterProfile foundation

Deliverables:

- real-log golden corpus;
- diagnostics for suspicious/unparsed relevant lines;
- explicit provenance/freshness presentation contract;
- revisioned/idempotent historical backfill mechanism;
- durable CharacterProfile;
- persistent level progress baseline/state;
- active vs historical class model;
- durable travel capability model;
- safe v1→v2 data migration/backup.

**Gate:** Core personal identity/progress survives restart and parser regressions have strong fixture coverage.

## Phase 4 — Finish the major guidance domains

### Gear

- paper doll;
- verified +0→+10 math;
- crossover-tier comparison;
- full owned-copy context (worn/bags/bank where reliable);
- exaltation ownership/placement context with explicit assumption labels;
- personal acquisition/drop memory;
- attainable upgrades;
- class usability;
- direct routing/quest actions.

### Quests

- class defaults/multi-select;
- level/zone/sequence views;
- rewards;
- quest-item toast;
- next actionable tracked step.

### World

- capability-aware route graph;
- universal Guide Me There;
- map/camp integration.

**Gate:** Gear → quest → mob/camp → route is a real connected workflow.

## Phase 5 — Personalized recommendations and search depth

Deliverables:

- Home recommendations using personal evidence;
- clearly labeled generic fallback estimates;
- XP/gear/quest/motes/money intent filters;
- global search with personal context/actions.

**Gate:** EQBuddy can answer “what should I do next?” with a concise, explainable recommendation.

## Phase 6 — v2 polish/stabilization

Feature intake is heavily restricted during this phase.

Focus:

- navigation friction;
- empty states;
- text/terminology consistency;
- DPI/scaling;
- focus/Alt+Tab/click-through;
- migration;
- performance;
- accessibility;
- documentation/screenshots;
- live-game smoke testing;
- bug corpus.

**Gate:** Definition of Done below.

---

# 20. v2 Product Definition of Done

EQBuddy 2.0 is ready when all of the following are true.

## Discoverability

- A new player can install EQBuddy and get meaningful value without reading a manual.
- Every major capability has one obvious home.
- Primary tasks satisfy the one-click-to-domain / one-more-to-answer rule in normal use.
- No important feature exists only in the right-click menu.

## Live play

- HUD is small, movable, stable and nonintrusive.
- Time-sensitive chips are actionable and do not steal focus.
- DPS/HPS/pet detail is easy to expand and easy to understand.

## Accuracy

- Golden corpus covers major log/event domains.
- Known high-confidence parser/accounting regressions are resolved or explicitly accepted by David.
- Numbers distinguish observed/verified/manual/estimated/assumed state where it matters.
- Meaningful unmatched/unparsed input is reported rather than silently discarded.
- Historical backfill/migration can safely improve derived data without duplicate facts.
- No known false gear upgrade claims from +N uncertainty.

## Personal continuity

- Character profile survives restart.
- Current level progress remains meaningful across sessions.
- Historical unlocked classes/travel capabilities are durable.
- inventory/quest/gear state has clear freshness/source.
- missing high-value inputs are surfaced contextually and disappear when resolved.

## Guidance

- Gear recommendations are class-usable and enhancement-aware.
- Quest next steps are actionable.
- Guide Me There works consistently across supported destination types.
- Home can provide concise, evidence-backed next-action recommendations.

## Migration

- Existing Windows 1.x users retain useful history/state.
- A pre-v2 backup is made before destructive migration.
- Legacy Linux/macOS final builds remain downloadable and usable.

## Product finish

- no stale screenshots describing retired UI;
- no documentation pointing to windows that no longer exist;
- no duplicate primary navigation doors;
- no unexplained empty pages;
- no unnecessary modal interruptions;
- Windows scaling/focus behavior has been tested on representative systems;
- update/install path has been tested end to end.

---

# 21. Structural Support and Agent Operating Model Changes

The existing multi-agent process has demonstrated strong discipline, especially around evidence and refusal to guess. Keep those strengths.

The problem is that routine work can now accumulate **more governance state than the product change warrants**.

v2 should move to **approval by exception** operationally, not only in principle.

## 21.1 Canonical work-state rule

> **GitHub is the canonical operational work state. Markdown is for durable knowledge.**

Use issues/discussions/project state for:

- open/waiting/ready/in-progress status;
- reporter evidence;
- reproduction;
- ownership;
- PR linkage;
- resolution.

Use repository markdown for:

- product rules;
- architecture;
- enduring decisions;
- test strategy;
- v2 requirements;
- lessons/traps worth carrying forward.

Avoid replicating the same routine work state across Scribe/Helm/Fable/Bevel inbox and feedback files.

## 21.2 Recommended role model

### David — Product owner / consequence decisions

David retains the established consequence-list decisions, including:

- values/product line;
- release go;
- public posture;
- money/licensing;
- roadmap direction;
- deliberate departure from eqlwiki as game-truth tie-breaker;
- third-party policy;
- player privacy.

Routine implementation does not require David's approval.

### Helm — Orchestrator and exception governor

Helm should stop behaving as the normal sign-off hop for every routine item.

Helm owns:

- consequence-list routing;
- conflicting-agent rulings;
- scope/priority conflicts;
- release readiness coordination;
- unresolved security/privacy questions;
- explicit holds;
- ensuring v2 gates are satisfied.

Helm does **not** need to sign:

- a factual thank-you;
- a request for one missing log line;
- an evidence-ready low-risk bug fix;
- ordinary test additions;
- routine internal refactors that stay inside existing architecture/product rules.

### Scribe — Community intake and evidence quality

Scribe should continue to:

- capture the reporter's words/source;
- determine whether the current release already addresses the report;
- inspect obvious code context before burdening the reporter;
- request literal logs/screenshots when actually needed;
- deduplicate;
- classify the issue;
- acknowledge the reporter without making scheduling promises.

Scribe may do those actions **without Helm pre-approval** when the response is factual, bounded, non-committal and within established public-posture rules.

### Fable — Planner / architecture reviewer by complexity

Fable should be invoked when:

- work crosses several domains;
- a v2 product workflow changes materially;
- a meaningful architecture seam is introduced/removed;
- the executor identifies competing approaches with nontrivial tradeoffs;
- the change is V2/V3-level consequence rather than a local correction.

An obvious parser fix with literal evidence should not require a planning ceremony merely because Fable exists.

### Bevel — UX/product-surface reviewer where visible

Bevel should review:

- new workflows;
- visible layout/navigation changes;
- terminology with product implications;
- alerts/toasts/chips that affect live-play interruption;
- major screen consolidation;
- v2 polish gates.

Bevel need not sign invisible parser corrections or mechanical internal work.

### Executor / Claude — Evidence-ready implementation

The executor may proceed directly on low-risk work when:

- evidence is sufficient;
- the behavior is already defined by product/architecture rules;
- no consequence-list decision is involved;
- tests can prove the correction;
- no live hold specifically blocks the area.

The executor must escalate rather than guess when evidence is insufficient.

---

# 22. Recommended Support Intake States

Use a small number of states rather than bespoke agent prose for every transition.

## A. `bug:evidence-ready`

Criteria:

- reproducible or literal evidence supplied;
- current version checked;
- likely failure surface identified.

Flow:

> Scribe → issue/work item → executor → tests/PR → review as appropriate.

No routine Helm gate.

## B. `bug:waiting-evidence`

Criteria:

- report plausible;
- one or more necessary facts missing;
- code cannot answer the missing fact.

Flow:

> Scribe asks for the exact evidence and waits.

Do not invent variants or start speculative implementation.

## C. `product:suggestion`

Criteria:

- valid idea, but behavior/scope is not already approved.

Flow:

> Scribe dedupes and records → product backlog/theme → periodic product review.

Do not turn every suggestion into an immediate Fable plan.

## D. `data:wiki`

Criteria:

- shared game/world truth that belongs in eqlwiki.

Flow:

> verify → prefer wiki correction/contribution path → refresh EQBuddy catalog through normal pipeline.

## E. `legacy-platform`

Criteria:

- Linux/macOS v1 question after v2 transition.

Response model:

> identify the final legacy release and preserved documentation; be clear that v2 desktop development/support is Windows-only. Do not create implicit parity commitments.

## F. `needs-owner`

Only for actual consequence-list decisions or a disagreement Helm cannot resolve inside existing owner direction.

---

# 23. Release Process Recommendations for v2

## RELEASE-001 — Separate “code is green” from “product is ready”

CI is necessary but not sufficient.

Release readiness should include:

- automated gates;
- real-log corpus;
- migration test;
- relevant screenshots/UI review for visible work;
- live-game smoke test;
- release-note accuracy.

## RELEASE-002 — Fewer micro-releases during major v2 construction

During active v2 development, prefer coherent milestone/beta releases over shipping every internal structural move to users.

v1 maintenance may continue only as needed while v2 is under construction.

## RELEASE-003 — Stabilization freeze before 2.0

Before the 2.0 tag, impose a deliberate stabilization period in which new feature intake is strongly restricted.

Only accept:

- blockers;
- correctness fixes;
- migration defects;
- UX friction/polish required by Definition of Done;
- documentation/release fixes.

## RELEASE-004 — Release go remains David's

Agents may declare **release candidate ready**, but final release go remains an owner consequence decision.

---

# 24. Suggested Repository Documentation After v2 Cleanup

Aim for a small, durable document set.

Recommended long-lived files:

```text
README.md
PRODUCT.md                 # product identity, values, hard lines
V2-PROJECT-GUIDE.md        # this document while v2 is active
ARCHITECTURE.md
TESTING.md
DECISIONS.md
CONTRIBUTING.md
SECURITY.md
LEGACY-V1.md               # final Linux/macOS status and links
```

Agent-specific working files should be reduced or archived when GitHub state can represent the work better.

Historical records need not be deleted; they simply should stop acting as parallel active queues.

---

# 25. Helm's Immediate Actions From This Charter

Helm should treat the following as the first v2 coordination actions.

1. **Record Windows-only v2 as owner-approved roadmap direction.**
2. **Create the legacy-preservation work item before approving removal of Avalonia.**
3. **Require the final v1 bridge/update-channel behavior before v2 becomes latest.**
4. **Establish a v2 workstream/project board organized by the phases in this document.**
5. **Freeze disconnected feature expansion unless David explicitly admits it to v2.**
6. **Ask Fable for the technical decomposition of Phase 0–2, not a fresh debate over the product direction.**
7. **Ask Bevel to treat the HUD + one-shell information architecture as the v2 UX destination.**
8. **Update Scribe's routing rules so routine factual acknowledgements/evidence requests no longer require Helm sign-off.**
9. **Move ordinary open-work state toward GitHub instead of multiplying agent markdown state.**
10. **Preserve the existing accuracy/no-guessing discipline as a non-negotiable product strength.**
11. **Add provenance/freshness and revisioned-backfill requirements to the Phase 3 technical plan.**
12. **Ensure Phase 4 Gear covers owned copies and exaltation context without creating a new top-level domain.**

---

# 26. Decision Summary

For avoidance of ambiguity, the following are settled directions unless David changes them.

### Settled

- v2 desktop = **Windows only**.
- existing Linux/macOS v1 users must be **preserved, not forcibly migrated or abandoned without access to their build**.
- mobile/tablet may remain as a Windows-hosted second screen.
- v2 is primarily a **consolidation + personalization + polish** effort, not a feature-count race.
- the current accurate combat/HPS/pet/proc/class systems should be preserved and hardened, not casually rewritten.
- the current “widget as the application” model should evolve into **small HUD + full Windows shell**.
- primary navigation should be domain-based, not context-menu/window based.
- CharacterProfile is a core v2 concept.
- gear must become enhancement-aware and attainable-upgrade focused.
- Guide Me There must be universal and character-capability aware.
- Home should produce concise, explainable personalized recommendations.
- support/agents move to **approval by exception**.
- GitHub becomes canonical routine work state; markdown holds durable knowledge.
- accuracy and evidence discipline remain core values.
- v2 facts/recommendations carry meaningful provenance, confidence and freshness rather than flattening all sources together.
- newly understood historical log events should improve existing local history through safe revisioned backfill.
- useful competitor ideas may be adopted only when they strengthen the defined v2 workflows; v2 will not chase feature parity.

### Still implementation decisions

The following may be decided by agents within the above constraints unless they trigger the consequence list:

- exact WPF layout and component structure;
- whether `UI.Shared` is physically renamed;
- exact navigation control style;
- exact HUD visual composition;
- exact recommendation weighting model, provided provenance/uncertainty rules are honored;
- exact local database/schema implementation;
- exact legacy branch name;
- exact final 1.x bridge version;
- exact GitHub Project/label names;
- exact test fixture organization.

---

# 27. Competitive Review Addendum — EQLegendsAssistant (2026-09-04)

A review of the newly released `Cujef/EQLegendsAssistant` identified several good ideas that reinforce the v2 direction. They are incorporated above so Helm does not need a separate competitor-response workstream.

## Adopt / strengthen in v2

### A. Explicit provenance and caveats

Good behavior observed elsewhere: clearly distinguishing computed/manual/fallback/assumed values and reporting incomplete joins instead of hiding them.

**EQBuddy response:** strengthen its existing evidence-first rule into a first-class provenance/confidence/freshness contract. Do not mechanically copy another product's labels or UI.

### B. Contextual readiness prompts

A local companion can reduce support burden by detecting which high-value inputs are absent and offering the exact next action.

**EQBuddy response:** contextual, self-clearing readiness assistance — not a permanent setup dashboard.

### C. Owned-copy and exaltation awareness

Inventory becomes more useful when it understands copies across worn/bags/bank and separates socketed effects from host items.

**EQBuddy response:** fold this into the v2 Gear domain so it directly improves upgrade decisions.

### D. Personal acquisition / leveling memory

Questions such as “where did I get this?” and “where did I actually level efficiently?” are useful because they turn raw logs into personal memory.

**EQBuddy response:** make those facts available as supporting evidence to Search, Gear, World and Home rather than creating additional top-level dashboards.

### E. Revisioned historical reprocessing

When parser coverage expands, old append-only logs can often fill newly supported history.

**EQBuddy response:** formalize idempotent/revisioned backfill so compatibility improvements benefit existing users without destructive resets.

## Explicitly not added to v2 from this review

The review does **not** change the v2 scope to add:

- a standalone Tradeskills product area;
- a broader standalone Factions product area;
- a browser-only desktop architecture;
- unlimited per-page dashboard customization as a product goal;
- CSV/JSON export for every table merely to match another tool;
- feature-by-feature parity with EQLegendsAssistant or any other companion.

Those ideas may be considered later if player evidence shows they solve a meaningful EQBuddy problem. For v2, they would dilute the finish/polish goal.

## Competitive principle

> **Borrow good product behavior, not another product's roadmap.**

EQBuddy's differentiation remains the connected personalized guidance chain:

> character identity/capabilities → personal performance/history → gear/quest opportunity → mob/camp → realistic recommendation → route/Guide Me There.

---

# 28. Product North Star

A finished EQBuddy v2 should make the following experience feel ordinary:

> I log into my level 45 character. EQBuddy recognizes the classes I am playing and remembers the classes and rituals I unlocked previously. It knows where I am in my level, what gear I am actually wearing, what quests I have underway, what mobs and camps I have personally fought, and how those fights went.
>
> During combat, the HUD stays small: DPS/HPS and only the timers or alerts that matter now.
>
> When I want more, I open EQBuddy. It can tell me which attainable weapon upgrades are meaningful for my build, exactly when an enhanced candidate becomes better than what I have, which mob or quest provides it, whether that content is realistic based on my own history, and how to get there using travel capabilities my character has already unlocked.
>
> I should not have to understand EQBuddy's internal windows, cards, menus, files, or data pipelines to get that answer.

That is the v2 product.

