# EQBuddy v1 → Evolved transition — Bevel UX one-pager

**Status:** Follow-on to `FABLE.md`'s signed/merged transition plan (2026-09-07 ~2:00 PM CT,
PR #399, tip `e08c6f0a`). Sequenced by the owner (PR #397) and handed to this seat in
`BEVEL-FEEDBACK.md` (~2:05 PM CT): *"the copy is yours to adjust without reopening the
plan."*
**Author:** Bevel.
**Not implement. Not a `FABLE.md` stub. Does not reopen §0–§8 or decide D1–D5.**

This is copy and surface shape only, over the four things the plan named as deliberately
Bevel's: the first-run import consent screen, the Windows-only gate messaging, the
dual-install detection page, and the release-page transition section. It assumes TR-1/TR-2's
mechanism exactly as signed — empty-target-only import, refuse-while-running, stage-verify-
commit, separate download under a new AppId. Where the plan named a decision as David's
(D1–D5), this file writes copy for the assumption the plan builds *now*, not for a decision
it might become later.

---

## 1. First-run import consent — the screen, before any copy runs

**Where:** the first-run Setup room (OE-6), painted before `ApplyMigrations`'s first save
(trap 47 — consent must sit ahead of the slowest path, not just the intended one). This is
mechanism the plan already owns; the requirement on the copy is that nothing below may be
worded in a way that reads as already-decided or reversible-by-accident.

**The offer**, default-checked (TR-1's stated assumption — D2 is David's; this wording does
not change if he flips it, see §5):

> ☑ **Bring my EQBuddy 1.x settings and history**
> Copies your characters, session history, and saved rules into EQBuddy Evolved. Your
> EQBuddy 1.x install and its data are untouched — nothing is moved, and 1.x keeps
> working exactly as it does today.

Two things that sentence is doing on purpose: it says **copy, not move**, in the player's
own words before the technical "your data is yours" reassurance, because "bring my
settings" reads as a move to anyone who has not read `LEGACY-V1.md`; and it names 1.x
staying untouched *in the same sentence* as the offer, not as a footnote below it — the
thing a player is likeliest to worry about is the thing most likely to go unread if it is
the fourth line.

**Decline is a plain, equally-sized alternative, not a smaller link:**

> ☐ Start fresh instead

No modal, no second "are you sure" — the plan's own model has no merge path and no
destructive step on this branch (an unchecked box copies nothing), so there is nothing here
that needs a confirm.

**The import report**, once the copy finishes (the plan's own requirement —
`ImportReportReachesASurfaceTests` — this is its voice): the inventory-dump pattern from
`docs/BEVEL-v2-staging-critique.md` §4, applied to an import instead of a missing dump —
name what came over, in player words, not file names:

> **Brought over from EQBuddy 1.x:** 3 characters, 41 sessions, 6 saved Watch rules.
> Nothing else changed on this PC.

**Two refusal states, named by name (the plan's own §2 rule), not a generic error:**

- v1 still running: *"Close EQBuddy 1.x first — its settings file is open and can't be
  copied safely."* Names the reason, not just the ask.
- Evolved profile already has data (re-run of first-run, or a manual copy): state what is
  there before asking — *"EQBuddy Evolved already has its own settings. Importing now would
  need to replace them — pick one:"* with **Keep what's here** / **Replace with 1.x's
  import**, never a silent merge and never the word "merge."

---

## 2. Windows-only gate messaging — never shown to a reader it doesn't apply to

Evolved is Windows-only; v1/legacy stays cross-platform. Every surface this plan touches
gates on that, not just says it:

- **Release page:** the §5 transition section (below) only renders under a Windows OS
  detection on the page, or — if the page can't detect the OS — leads with "Windows" in
  its own heading rather than in body text, so a scanning non-Windows reader self-excludes
  in one glance instead of reading three lines to find out it doesn't apply to them. This
  mirrors how `LegacyPlatformUpdatePolicy`'s own one-time notice already names the platform
  first (`docs/BEVEL-v2-staging-critique.md`'s addendum calls that shipped copy correct and
  final — this is the same discipline applied one level up, at the release page rather than
  in-app).
- **In-app:** nothing about Evolved's installer, its AppId, or the dual-install page (§3)
  ever appears to a non-Windows v1 install. That's mechanism the plan already isolates
  (`LegacyPlatformUpdatePolicy` rules 2–3, untouched); the copy obligation is just to never
  invent a Windows-only sentence outside a Windows-gated surface.
- **Never say "upgrade."** Evolved is a new, separate product line running beside 1.x, not
  a version bump 1.x users are behind on. "Moving to" / "trying" / "install alongside" —
  never "upgrade to," which misstates the relationship the plan (§3, §4) went out of its way
  to build as parallel, not sequential.

---

## 3. Dual-install page — names what's found, enforces nothing

Per the plan's §5.4: *"Evolved's first-run page detects a running/installed v1 … and SAYS
so — information, not enforcement."* The copy has to hold that line exactly:

> **We found EQBuddy 1.x on this PC.** It won't be touched — Evolved runs from its own
> folder (`EQBuddy Evolved`) with its own settings. You can bring your 1.x settings over
> (below) or start fresh; either way, 1.x keeps working.

Order matters: state the fact, then the reassurance, then the offer — reassurance before
the ask, because a player mid-way through installing a second copy of an app they already
trust is the moment "will this break my other one" is loudest.

No "recommended" language anywhere on this page. Not "we recommend importing" — that reads
as a nudge away from 1.x, which is exactly the promise `LEGACY-V1.md` and the values line
rule out. Both paths (import / start fresh) get equal visual weight, matching §1's decline
treatment.

**If v1 is not found:** say nothing about it. A dual-install page that mentions v1 to
someone who has never had it is describing the app's internals for no player reason —
`docs/BEVEL-v2-staging-critique.md` §4's terminology-ban logic ("a player who has to learn
our architecture to use the product is paying a trust tax we invented") applies here to
"detected v1" as much as to "card key."

---

## 4. Release-page "Moving from EQBuddy 1.x on Windows" section — shape for TR-3

This section doesn't exist yet — TR-3 builds the `legacy-notice-guard.ps1` requirement that
a 2.x release's notes carry it before it can ship. This is the shape the copy should take
when that seat writes it, so TR-3 has a template rather than a blank requirement:

> ## Moving from EQBuddy 1.x on Windows
>
> **EQBuddy Evolved is a new, separate app** — it installs beside EQBuddy 1.x, not over it.
> Your 1.x install keeps working exactly as it does today; nothing about it changes when you
> install Evolved.
>
> **What's different:** [one or two lines, release-specific — what Evolved adds that 1.x
> doesn't have, filled in per release, never boilerplate]
>
> **Your settings and history:** the first time you open Evolved, it offers to bring over
> your 1.x settings and session history. Your 1.x profile is copied, never moved or edited —
> say no and start fresh, or say yes and pick it up where 1.x left off.
>
> **EQBuddy 1.x isn't going away.** It's still MIT-licensed, still gets fixes on its own
> track, and this release doesn't change that.

Four short paragraphs, same order every time it ships (what it is, what's new this release,
what happens to your data, what happens to 1.x) — the #233 "X is now Y" discipline applied
to a product line rather than a moved feature: name the old thing and the new thing in the
same section, every time, so a returning reader never has to hunt across releases to find
out whether 1.x still works.

---

## 5. The one door this file deliberately does not touch

**D2 (import consent: opt-in default-checked vs. auto-import-with-notice) stays David's.**
Every checkbox and sentence above is written for the opt-in model TR-1 builds as its stated
assumption. If David chooses the auto-import model instead, the change is mechanical, not a
rewrite: §1's checkbox becomes a one-time notice ("EQBuddy Evolved brought over your 1.x
settings — [what came over]. [Undo / start fresh instead]"), stated *after* the copy instead
of asked *before* it, and the refusal states move from consent-gates to plain conditions
that must resolve before the notice can be true. Nobody should read this file as a case
either way; it is here so the wording is ready the moment David answers.

D1, D3, D4, D5 are untouched and not this file's business.

---

## 6. HELM lock — capture theme for anything this file's copy gets staged into (teal + grey)

**`HELM-FEEDBACK.md`'s OWNER LOCK (2026-09-07 ~3:45 PM CT, standing):** Evolved screenshots,
tutorial pages, What's-new entries and `shoot.ps1` captures use **teal + grey** going forward —
not `ParchmentBrass`, the tone every other capture in this repo has used to date. This binds
every future staging of §1–§4's surfaces (the first-run import screen, the dual-install page,
the release-page section) the moment any of them is captured for docs, a tutorial, or a
What's-new entry.

Nothing here is staged or re-shot in this pass — this file is copy and shape only, no
committed images. The note exists so the next seat that stages one of these surfaces sets the
theme **before** capture rather than after (trap 31 — a capture surface must pin its own
theme). As of this writing `UI.Shared/DesignTokens.cs`'s `ThemePalettes` has no palette named
for teal + grey; whoever stages first should check there and flag Helm if none exists yet
rather than guessing at the nearest dark theme.

---

## What this file is not

- **Not a hold.** Nothing here restrains TR-1 or TR-2, which are `ready` on Helm's sign
  independent of this copy landing.
- **Not needs-david.** D2 is named above and left open; nothing here answers it.
- **Not implement.** No `src/`, no installer, no release-note text committed to a real
  release — this is the shape TR-1's first-run surface and TR-3's release-note requirement
  should take when those seats write the real strings.
- **Not a re-decision of §1/§2/§3/§4/§5 mechanism.** Empty-target-only, refuse-while-running,
  copy-never-move, separate download under a new AppId — all exactly as signed.

— Bevel, 2026-09-07
