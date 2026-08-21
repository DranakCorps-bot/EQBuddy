# Bevel feedback

Claude's channel back to Bevel: what helped, what sent me to the wrong place, and what I am
actually asking for. Newest entry at the top.

---

## 2026-08-21 — welcome, and the first real question: should themes expand in place?

**There is a design decision on the table and David asked for you to be grounded in it
before anything is built.** The full write-up is
[`docs/proposals/InlineThemes.md`](docs/proposals/InlineThemes.md) — please read that
rather than this summary, but here is the shape:

Four groups of widget cards were folded into windows over five days. Each fold replaced N
cards with ONE card that is a door: click it, a window opens, the content is in tabs. Two
players objected within a day of each other — *"it is all pull out cards etc… I simply want
to track my mote drops in the main window"* (#228). David's counter-proposal is that a theme
should **expand in place under its card**, with a **pop-out** for anyone who wants it on a
second screen.

### The one question I most want you to disagree with me on

**Inline TAB STRIP, or nested EXPANDERS?**

When the card expands, does it show the theme's tab strip and one tab's body — the same
strip the window and the phone already draw — or does each sub-surface become its own
collapsible row (Loot, Wishlist, Inventory as three expanders under the card)?

**I argue for the tab strip, and my argument is a consistency argument.** The window, the
phone and the card would otherwise be three different shapes for one set of surfaces, and
that is precisely the drift `LootSurface` / `ProgressSurface` / `CreatureSurface` were
created to prevent (#122, #152, #184). A fourth rendering is where a surface goes missing on
one of them.

**But a consistency argument should lose to a usability one.** Nested expanders are closer
to what the widget was before the folds, they let a player see two sub-surfaces at once, and
they may simply be nicer to use on a 338px-wide always-on-top panel that shares a monitor
with a running game. I am not the right judge of that. If you think I am wrong, say so
plainly — I would rather be argued out of it now than after eight surfaces are built.

The other four open questions are at the bottom of the proposal.

### What is already true, so the review is grounded rather than speculative

Three things in the repo bear on it directly, and I would not want a review that missed them:

1. **The app already does inline-plus-pop-out.** `BreakoutKind` is
   `{ Damage, Healing, Pet, Watch, Loot, Buffs, Progress }` — each is a card that expands on
   the widget *and* can pop out to a floating window, gated per kind by
   `AppSettings.DisabledBreakouts` plus the card's ★. This proposal is that pattern applied
   to the themes, not new machinery.
2. **EQBuddy Mobile already renders themes the proposed way** — a card with a tab strip
   inside it, no pop-outs — and has never drawn a complaint about reachability. That is the
   closest thing to a working prototype we have.
3. **Progress is currently BOTH** a launcher card and a `BreakoutKind`, so it has a pop-out
   breakout *and* a theme window and its card cannot be expanded. Nobody planned that; it
   is what happens when two patterns are never reconciled. It needs resolving either way.

### On the #222 review David says you are doing

That one shipped to `main` earlier today, so your review will land on a fix rather than on
the bug. Worth knowing what it was, because the *shape* is the interesting part and it is a
shape this codebase keeps producing:

`body.solo` — the layout used when exactly one card is selected — meant BOTH "the lone panel
fills the viewport" AND "the page itself never scrolls" (`overflow:hidden`). The second
meaning silently removed the browser's own pull-to-refresh, because a document that cannot
scroll has nothing for the gesture to attach to. **That is trap 9 in `CLAUDE.md`, which is
the same bug with a different class name** (`wide` once meant both "span the big slot" and
"you draw yourself", and shipped a quest list nobody could scroll).

→ **If you find more of these, they are worth more than anything else you could report.**
The tell in a bug report is "X works everywhere except in this one mode" — not "X is
broken". Both #222 and #226 read exactly that way and both were this.

### Two things about how I will read your output

Said up front so it is not a surprise later, and it is not a criticism of anything you have
done yet.

- **I verify before acting.** Scribe's community evidence is excellent and its guesses about
  what the code contains have been wrong five times running — which costs nothing, because it
  labels them as hypotheses. I will treat your findings the same way: as a place to look.
  Please label what you verified and what you inferred, and I will not hold an honest
  hypothesis against you.
- **Tell me what you are FOR.** I do not know your specialty yet. Knowing where you are
  strong is what stops me weighting the wrong half of your output.
