# EQBuddy Evolved

**Your Personalized Guide to Norrath**

EQBuddy 1.x grew a lot of capability, fast. **EQBuddy Evolved** is the next major version: the same private, log-only companion, finished into one coherent product — clean, simple, personal, and easy to navigate.

This is the player-facing vision. The product identity — principles, surfaces, and the north star in full — lives in [PRODUCT.md](PRODUCT.md). **Current public downloads remain 1.x.** Evolved is the direction, not a download yet.

---

## Why Evolved

1.x already reads your log, tracks your session, remembers camps, compares your bags, and follows your quests. The next job is not “more cards.” It is to make those capabilities feel like one companion:

- one obvious home for each major thing you do
- a small live HUD while you play, and a full Windows app when you want the answer
- durable knowledge of *your* character, not only tonight’s session
- honest numbers — observed facts and verified game rules, with estimates labeled as estimates

Feature breadth is no longer the primary constraint. Finish and polish before expanding again.

---

## Two surfaces (plus an optional phone)

```
  ┌──────────────────────────────────────┐
  │  EQBuddy HUD                         │
  │  small · movable · always-on-top     │
  │  glance metrics, timers, alerts      │
  └──────────────────┬───────────────────┘
                     │  expand for live detail
                     ▼
  ┌──────────────────────────────────────┐
  │  Full Windows application            │
  │  Home · Live · Progress · Gear       │
  │  Quests · World · Search · Settings  │
  └──────────────────┬───────────────────┘
                     │  optional second screen
                     ▼
  ┌──────────────────────────────────────┐
  │  EQBuddy Mobile                      │
  │  LAN-only, hosted by the Windows app │
  └──────────────────────────────────────┘
```

**Glance first → expand for live detail → full application for analysis.**

The HUD earns space only for information that may change the next action within seconds or minutes. Research, comparison, planning, and review belong in the full app. The phone remains a second screen you look *away* to — map, tracked quests, item and gear lookup, live meters where they help, route guidance — never a required account or cloud service.

---

## The guidance chain

Evolved connects what you already own and have done into one personalized path:

**gear → quest → mob → camp → route**

Not a generic “best in slot” list. The useful answer is the upgrade you can actually get, from content that is realistic given *your* history, reached with travel you already unlocked.

Primary workflows aim for **one click to the domain, one more to the answer.** A capability that only lives behind nested menus and breakout windows is a defect.

---

## Hard lines (these do not move)

- **Personal, not competitive.** EQBuddy measures the player using it. It is not a party or raid ranking tool, a leaderboard, or a way to judge other people.
- **Log-only and local-first.** No game-memory reads, no packet inspection, no gameplay automation, no hidden-information extraction, no required account, no required cloud, no telemetry by default.
- **Evidence before confidence.** A precise-looking wrong number is worse than a clearly identified estimate.
- **No modal interruptions for ordinary information.** Looting a quest item should be a dismissible toast or chip, not a dialog that steals the fight.

---

## Platform honesty

| | Status |
|---|---|
| **Supported** | Windows Evolved desktop, and EQBuddy Mobile hosted by that Windows app |
| **Preserved** | Final Linux/macOS/Windows **1.x** builds — they stay downloadable and usable. We are **not** removing them. |

Linux and macOS players keep the 1.x build they have. Evolved does not require them to migrate, and it will not offer a Windows v2 installer as “the update” on those platforms. Details and the Phase 0 bridge are in [LEGACY-V1.md](LEGACY-V1.md) and [issue #275](https://github.com/DranakCorps-bot/EQBuddy/issues/275).

---

## North star

A finished Evolved release should make this feel ordinary: you log in, EQBuddy already knows the character you are playing, and when you want more than a glance you open the app and get a useful next step — without having to understand EQBuddy’s internal windows, cards, menus, files, or data pipelines.

The full north-star paragraph is in [PRODUCT.md](PRODUCT.md). That is EQBuddy Evolved.
