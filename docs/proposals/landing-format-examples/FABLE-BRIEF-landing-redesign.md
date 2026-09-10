# Fable brief — EQBuddy GitHub landing redesign (Evolved)

**Opened by:** Helm on Founder ask 2026-09-10 ~12:16 CT  
**Epic:** EQ-V2  
**Role:** Planner (Fable / `claude-fable-5`) plans; Executor implements after Helm SIGN. Do not implement in the plan seat.

## Goal

Redesign and build a public **EQBuddy landing page on GitHub** (enable GitHub Pages — none today) that:

1. Leverages the **style and format** of Founder’s two HTML examples (dark Inter glassmorphism, sticky nav/TOC, progress bar, card grids, capability stacks, hero → sections).
2. Highlights key aspects of the **full Evolved featureset**, with **screenshot examples** (repo already has `docs/screenshots/`).
3. Centers the product north star from PRODUCT.md:

> EQBuddy understands **who I am playing, what I can do, what I own, what I am working on, how I actually perform, and where I have been** — then quietly helps me decide what to do next.

Landing should make a player feel that Evolved answers: **“Given who I am and what I want to accomplish, what should I do next?”** — via the guidance chain **gear → quest → mob → camp → route**, Guide surface, HUD → full app → Mobile.

## Style references (treat as HTML; originally mailed as .txt)

- `WebFormatExample1.html` — “From a business question to a trusted reporting answer” (sectioned capability stack, pill topbar, radial dark bg, Inter, cyan/blue/violet accents).
- `WebFormatExample2.html` — long-form worked example with sticky TOC, progress bar, cards, Fira Code for code.

Saved for Soft at (box): `/workspace/landing-format-examples/` and should be copied onto the PC under `docs/proposals/landing-format-examples/` (or equivalent) before planning.

**Brand note:** Standing Evolved docs/tutorial theme is **teal+grey** (Founder lock 2026-09-07). Prefer example *structure/chrome* (dark glass, Inter, sticky nav, cards) with **teal+grey accents** over Dell cyan/violet unless the plan argues otherwise and Helm signs it.

## Constraints

- Evolved is **proprietary** / All Rights Reserved — landing must not imply MIT for v2; 1.x MIT stays clear.
- Current public downloads remain **1.x** until Evolved channel opens — landing may show Evolved vision + screenshots of local Evolved, but must not fake a public Evolved download.
- Log-only, local-first, personal-not-competitive hard lines from PRODUCT.md.
- No Play Console / signing / prod secrets work in this card.
- Prefer static site (plain HTML/CSS or minimal static generator) suitable for GitHub Pages from `docs/` or `/docs` branch — Fable picks and justifies.

## Deliverables (plan must specify)

1. Information architecture + section list mapped to Evolved featureset + north-star “next action” story.
2. Visual system tokens adapted from examples ↔ teal+grey.
3. Screenshot plan (which existing `docs/screenshots/*` + what still needs Desktop shoots).
4. GitHub Pages enablement steps.
5. Sliceable Executor tickets (IA/shell first, content, screenshots, Pages wiring).
6. Success criteria: a cold visitor understands Evolved’s promise and the guidance chain without reading PRODUCT.md.

## Out of scope

- Implementing product Guide P1c / quest AI in this card (landing only).
- Rewriting PRODUCT.md / EQBuddy-Evolved.md as the primary surface (landing can link them).
