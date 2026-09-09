# Guided progression — Founder locks (2026-09-08)

Working locks from David + Helm review of `GUIDED-PROGRESSION-REQUIREMENTS.md`. Binding for Fable planning. Not a Constitution amendment.

## Founder locks

1. **MVP proof:** All **Warrior, Monk, and Druid** quests in **Plane of Sky** — David can test and verify those end-to-end.
2. **Content sources:** eqlwiki and other online resources, plus **user-shared updates/corrections**.
3. **Runtime:** Local catalogs; **weekly wiki sync** (not live fetch every session).
4. **Incomplete steps:** Show **stubs** with commentary that wiki info is incomplete. Users must be able to **update and 1-click share back** to the EQBuddy repo (same family as the existing mail / log-an-issue button).
5. **Architecture:** Evolve **Evolved Quests in place** (same guide engine; progressive cutover). Do not build a parallel incompatible Quests surface.
6. **Fable:** Plan from the requirements doc **and** these locks; Fable may extend and improve the plan.

## Helm assessment (carry into plan)

- Prefer a **thin required schema core** first (identity, prerequisites, objectives with who/where/what/next, rewards, source metadata, manual progress). Optional fields earn their keep in PoS content.
- **Incomplete/uncertain** must be first-class UI states — never ship a “complete” badge on a hollow guide.
- **Manual progress first**; auto-detect from logs is later (Phase 5-shaped), not an MVP gate.
- Explicit **migrate vs replace** for existing `QuestCatalog`, Sky defaults, Home/Setup readiness surfaces.
- **Gear recommendation** stays Phase 6 — keep hooks in the model; do not bake a recommendation engine into Phase 1.
- Difficulty ★ ratings: UI collapse later; do not require authors to invent stars early.

## Out of band

- Play Console OFF. Evolved local-only until Founder says push wide.
- No memory reading. No fabricated certainty.
- Community share-back must not upload personal logs/secrets by default — opt-in scoped patches only (Fable to design).

## Ask for Fable

Produce a V2/V3 plan sized so Phase 1 foundation supports the PoS WAR/MNK/DRU MVP, Phase 2 authors those guides, including weekly sync + incomplete stubs + 1-click share-back. `needs-david:` only for a true consequence-list door.

## Founder amendment — 2026-09-09 ~11:50 AM CT (in session, Fable planning seat)

**Lock 1 scope widens; the proof set does not move.** David: *"I want to implement all class
quests for PoS with the new guided model."* Helm's lock 1 named Warrior, Monk and Druid as the
MVP proof — the classes David can verify end to end. That stays the proof set and the order
Phase 2 lands in. Phase 2's SCOPE is every class's Plane of Sky quests on the guided model:
sixteen classes, 95 rewards, 222 turn-in rows in `SkyQuestDefaults` at the time of writing.
Plan: `docs/quests/WEEKEND-SHIP-BAG-2026-09-12.md` §1 (M4 = the proof set, STRETCH batches =
the other thirteen, one PR per class, same authoring rules for all).

A class is either fully on the guided model or exactly as it was before — never half. Lock 5's
progressive cutover is by class, and the must-list test that enforces it lands with the first
Warrior authoring PR.

**Delivery shape, same session:** *"I would like your plan for Claude Code Opus to be the
development and delivery of the entire Quests rewrite. We can iterate changes if needed post
delivery."* The plan above is therefore a delivery plan for the whole rewrite (Deliveries 1–4),
the weekend is Delivery 1's checkpoint, and Bevel's faces for the active-step card become a
post-delivery critique rather than a gate before P1d. Helm's "LEAVE P1d until Bevel faces"
posture (#472 ruling) is superseded by this direction; Helm is told in the same LIVE ASK.
