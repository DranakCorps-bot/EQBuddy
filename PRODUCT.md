# EQBuddy Evolved — Product identity

**Working name:** EQBuddy Evolved  
**Tagline:** Your Personalized Guide to Norrath  
**Status:** Direction for the next major version (v2). Current public releases remain the 1.x line until the v2 channel opens.

This document is the public product identity for EQBuddy Evolved. Where older roadmap language conflicts with it, this document wins for v2 unless the owner changes the direction.

---

## What EQBuddy Evolved is

EQBuddy Evolved turns the strong capabilities already built in 1.x into a coherent, accurate, highly polished product: clean, simple, personal, and easy to navigate.

It is **not** a feature-count race. Feature breadth is no longer the primary constraint. The job is to:

1. Preserve and harden the accurate core.
2. Remove platform and surface duplication.
3. Separate the live in-game HUD from the full desktop application.
4. Give every major capability one obvious home.
5. Build durable knowledge of the character, not just the current session.
6. Connect gear → quest → mob → camp → route into one personalized guidance chain.
7. Stay proactive where useful, never noisy or intrusive.
8. Use observed facts and verified game rules; identify estimates honestly.
9. Finish and polish before expanding again.

### The experience we are aiming for

> EQBuddy understands **who I am playing, what I can do, what I own, what I am working on, how I actually perform, and where I have been** — then quietly helps me decide what to do next.

---

## Product principles (requirements)

### Personal, not competitive

EQBuddy measures and guides **the player using it**. It does not become a party/raid ranking tool, leaderboard, coaching score, or a way to judge other players.

### Log-only and local-first

- no game-memory reads  
- no packet inspection  
- no gameplay automation or input broadcasting  
- no hidden-information extraction  
- no required account  
- no required cloud service  
- no telemetry by default  
- personal history stays local unless you explicitly export or contribute it  

### Evidence before confidence

Precise-looking wrong numbers are worse than clearly identified estimates. Where trust or a decision depends on it, EQBuddy distinguishes observed, verified, manual, estimated/inferred, assumed/fallback, and unknown — in the model first, in the UI where it matters.

### One click to the domain; one more to the answer

Primary workflows: **one navigation action reaches the domain; one additional action reaches the useful answer.** A capability that only lives behind nested menus and breakout windows is a UX defect.

### Deadline information earns HUD space

The in-game HUD is for information that may change the next action within seconds or minutes (mez/charm, spawn-due, watch alerts, important buff expiry, live glance metrics). Research, comparison, planning, configuration, and retrospective analysis belong in the full application.

### No modal interruptions for ordinary information

Informational events (for example looting a quest item) should normally use a dismissible toast/chip, not a modal that steals attention during play.

---

## Product structure

EQBuddy Evolved behaves as two coordinated desktop surfaces plus an optional mobile second screen:

| Surface | Role |
|---|---|
| **EQBuddy HUD** | Small, movable, always-on-top live glance while playing. Not the full app. |
| **Full Windows application** | One coherent shell with persistent primary navigation: Home, Live, Progress, Gear, Quests, World, Search, Settings. |
| **EQBuddy Mobile** | Optional LAN-only second screen hosted by the Windows app (map/camps, tracked quests, item/gear lookup, live meters where useful, route guidance). |

Interaction model: **glance first → expand for live detail → full application for analysis.**

---

## Platform support

| Surface | Status |
|---|---|
| Windows desktop (Evolved / v2) | **Supported** product line |
| EQBuddy Mobile hosted by Windows | **Supported** second screen |
| Linux desktop 1.x | **Preserved legacy** — final builds remain downloadable; not actively developed for Evolved |
| macOS desktop 1.x | **Preserved legacy** — final builds remain downloadable; not actively developed for Evolved |

See [LEGACY-V1.md](LEGACY-V1.md) for how we preserve existing Linux/macOS installations without taking them down.

---

## What Evolved is not (for this major version)

- not a party/raid meter or ranking product  
- not a browser-only desktop rewrite  
- not a feature-parity chase against other companions  
- not a requirement that Linux/macOS users migrate or lose their installed 1.x build  

Useful ideas from elsewhere may be adopted only when they strengthen the Evolved workflows above.

---

## North star

A finished Evolved release should make this feel ordinary:

> I log into my character. EQBuddy recognizes the classes I am playing and remembers what I unlocked before. It knows where I am in my level, what I am wearing, what quests I have underway, what I have fought, and how those fights went.  
>  
> During combat the HUD stays small: DPS/HPS and only the timers or alerts that matter now.  
>  
> When I want more, I open EQBuddy. It can tell me which attainable upgrades are meaningful, when an enhanced candidate becomes better than what I have, which mob or quest provides it, whether that content is realistic from my own history, and how to get there with travel I already unlocked.  
>  
> I should not have to understand EQBuddy's internal windows, cards, menus, files, or data pipelines to get that answer.

That is EQBuddy Evolved.
