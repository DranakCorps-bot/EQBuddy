# DRA-149 — Founder re-smoke checklist

**What this is.** You failed "Upgrade what I wear" / Farm Gear on Desktop 2.0.0+`275cc215`
(2026-09-16, ~8:50 PM CT) with three items. Five slices answer them. This page says **what each
screen should SAY** when you open it, so you can tell a fix from a failure without reading any
code — and, more importantly, so that **the one screen that looks like a failure and is not**
arrives predicted rather than as a surprise.

Every number below is measured against your own committed dump
(`tests/fixtures/inventory/dranak.txt`) and the shipped catalog, and pinned in
`tests/EQBuddy.Tests/FounderResmokeTests.cs`. If a weekly wiki refresh moves one, that file goes
red rather than this page going quietly wrong.

**Nothing here marks DRA-84 PASS.** That is still your call, and it is a different card.

---

## Before you start

1. A build with all five slices in it. `Help → About` should show a commit at or after D5.
2. `/outputfile inventory` in game, once. Nothing below needs a played session — every answer
   comes from the catalog, which is the point.
3. Tell EQBuddy your level if it does not know: **Character room**. The gear answers change a
   great deal with it, and the room says so rather than guessing.

---

## FAIL 1 — "the bow does not appear"

> *Deteriorated Ancient Faydark Longbow +8 does not appear, so he cannot work upgrades for it.*

**Where:** Helper room → Farm Gear → *Upgrade what I wear*, the worn picker.

**What it should say:** the bow is **in the list**. The game prints it `Deterioriated Ancient
Faydark Longbow` (two `i`s in the middle) and eqlwiki titles the page `Deteriorated Ancient
Faydark Longbow`; a hand-written alias row joins the two, and it is the only reason that anchor
exists. Your dump has **21 worn rows** and all 21 are readable.

**What you should NOT see:** the sentence *"EQBuddy has never read about: …"*. That caption is
real and it ships — it is what would have told you the bow had vanished, instead of it going
silently. On YOUR dump it is correctly absent, because nothing is unreadable any more.

> If you see that caption with the bow named in it, the alias row has been lost. That is a
> one-line fix and `FounderResmokeTests.TheBowIsAnAnchorAndIsNotInTheUnreadList` should have
> caught it.

---

## FAIL 2 — "no recommendations for where to go, who to kill, what quests"

> *For items Helper does see: no recommendations… "doesn't work at all".*

**Where:** Helper room → Farm Gear → *Upgrade what I wear*, with nothing in the worn picker
(which means "all of them").

### What was actually wrong

The comparison asked whether the catalog item's **"+N" tier** was at least your worn item's.
**0 of the catalog's 11,196 names carry a "+N"**, and every row in your dump carries `+2`..`+9`.
So the rule answered before it read its inputs: zero candidates, for every item you own, forever.
The sentence you got was true and the feature was broken.

The Helper's sweep now compares **base item against base item** and says so: *"a better BASE item
than yours — at the same +, it wins"*, with one block-level line noting that your "+N" raises
yours by an amount the wiki does not state. The Gear Locker is unchanged — there both names come
off one dump and both carry their "+N", so the old rule is still right.

### What the screen should say at level 29

| What | Expected |
|---|---|
| Worn anchors | **21** |
| Catalog candidates the sweep found | **~147** (was **0**) |
| Zones the band gate refused | **~18**, each named with eqlwiki's own band |
| Rows actually drawn | **3** — measured: **Kael Drakkel**, **Great Divide**, **Clan Runnyeye** |
| Drop offers withheld for naming no creature | **8**, said in its own sentence |

### ⚠️ THE SCREEN THAT LOOKS LIKE A FAILURE AND IS NOT

You will see a sentence like:

> *"18 zones EQBuddy has upgrades for are not listed at your level 29: Chardok (50 and up),
> Dragon Necropolis (45–60 and up), … and 13 more. Those are eqlwiki's own creature levels —
> EQBuddy leaves a zone out of this list when its band starts 5 or more levels over you."*

**That is the fix working.** Temple of Veeshan, Sleeper's Tomb, Plane of Fear, Plane of Hate and
Veeshan's Peak really are where your best upgrades drop, and a level-29 character cannot farm
them. The old build would have shown you those camps with no warning; the middle build showed you
nothing at all and did not say why.

**The test that tells the two apart is the pair of numbers**: candidates ≈ 147 and rows = 3 means
"found plenty, refused the places". Candidates = 0 would mean FAIL 2 is back. If you ever want to
check, that pair is `helperCandidates` and `helperGearWhy` in the `EQBUDDY_EXPAND` dump.

### The bow specifically

Your bow has **four** base-better items in the catalog, and the room's behaviour on them is the
whole story in miniature:

| Item | Where | At level 29 |
|---|---|---|
| Priceless Velium Reinforced Bow | Sleeper's Tomb (`55+`) | refused, band quoted |
| Primal Velium Reinforced Bow | Sleeper's Tomb (`55+`) | refused, band quoted |
| Bow of the Silver Fang | Temple of Veeshan (`60+`) | refused, band quoted |
| **Rune Shafted Harpoon** | quest: *The Mighty Snowfang Hero* | **shown, with the toggle on** |

**Turn on "include quests"** to see the fourth. A quest row is not a camp, so the band gate and
the creature rule do not touch it — the quest IS the path. That is the *"what quests drop
improvements"* half of your item 2.

### Who to kill

Every drop row names its creatures — up to three from the item's page, in the page's own order,
with the rest counted (*"and 4 more on its page"*). A row that **cannot** name anyone is
**withheld**, and the count says so. On your dump that is 8 offers.

### If you ding

Try the same screen at 60. It inverts: Temple of Veeshan and Sleeper's Tomb become your top rows,
and what gets refused is a zone you have **outgrown** — e.g. Great Divide (`30-45`), *"tops out 10
or more levels under you"*. Same dump, same catalog, opposite answers. That is
`AtSixtyTheSameDumpRefusesWhatHeHasOutgrownInstead`.

---

## FAIL 3 — tradeskills, and it has two halves

> *Tradeskills (example jewelcrafting): should recommend zones/creatures where gems drop more
> commonly, OR named vendors + where they are when shopping vendors.*

You asked for either; you get both, and they are deliberately different surfaces because they are
different plans for an evening.

**Where:** Helper room → tick **Farm Materials** (or tick nothing, which weighs everything).

### 3a — where the materials DROP

The park is gone. It said *"14 of 11,197 item pages say which profession an ingredient belongs
to"*, which was true and was a survey of the **wrong column**: that counted `[[Category:…]]`
tags. The `Recipes` column carries the association on **1,276** pages, with all eight professions
appearing in it as headings.

**What the screen should say:** camps, ranked, each naming the material, the profession, the zone
and the creatures — with the same band gate and the same creature rule as the gear rows, and
their counts kept separate so one sentence never explains two lists.

**Jewelcrafting** is answered: all the classic gem and bar materials are in the catalog with
zones, and most with creatures.

**Fletching is the named gap.** 33 materials, **zero** drop zones — an arrow shaft is bought,
foraged or crafted, not killed for. It draws its own sentence rather than an empty list.

### 3b — where the VENDORS are

Item pages mention a vendor on 3 of 11,196. **Zone pages** name one in the map key under their
map image, and **50 of the 118** do — **359 lines** transcribed word for word.

**What the screen should say:** under each profession row, up to three shops, in the wiki's own
words, one per zone, with the zone in front. For example:

> **Jewelcrafting**
> Ak'Anon — Bank of Ak'Anon, and Merchants selling Jewelry supplies (Gems)
> Cabilis — Merchant selling Gems of all types, Metals and Jewelry Kit
> Erudin — Sothure's Fine Gems - Merchants selling Gems and Metals and all Jewelry Supplies
> *and 14 more zones — eqlwiki's zone pages have the rest.*

Where the page named the vendor, the name is in the line — *"Everhot Forge - Merchants selling …
(Bndainy Everhot), Jewelry Metal and Rare Gems"* in Kaladim, *"Hut with Merchant Darfumpel
Zirubbel who sells Gems nearby"* in the Rathe Mountains. The `eqlwiki` link beside each opens the
page the line came from, which carries the **map** showing where in the zone the shop is — the
one thing this room cannot draw. EQBuddy never fetches it; you open it.

### ⚠️ THIS BLOCK NEEDS NO PLAY HISTORY, AND THAT MAKES AN EMPTY ONE A REAL FAILURE

Everything else in this room gets better the more you play. The vendor list does not: it is a
catalog fact, so **all eight professions have at least one shop on screen the moment the room
opens, on any character, including one that has never logged in**. So unlike every other block
here, "it is empty because I have not played enough" is not available as an explanation.

If a profession shows *"No zone page's map key names a &lt;trade&gt; shop"*, that is a gap in
eqlwiki's maps and the sentence says so — it is never a claim that nobody in Norrath sells the
stuff. On the current catalog **no profession draws it**, so seeing it at all is worth reporting.

---

## Quick reference — which slice answers what

| Your item | Slice | The mechanism that was wrong |
|---|---|---|
| 1 — the bow is missing | **D2** | the game and the wiki spell it differently, and the drop was silent |
| 2 — no recommendations | **D1** (+ D2) | the "+N" tier rule could never admit a catalog item |
| 3a — where gems drop | **D3** | the park measured `Categories`; `Recipes` is the column that answers |
| 3b — named vendors | **D4** | the data is on the ZONE pages, not the item pages |
| this page | **D5** | — |

## What to report back

- Any screen whose numbers do not match the table above, **with the number you saw**.
- Any sentence that reads as a failure and is not covered by the two ⚠️ boxes.
- Anything that calls a camp safe, easy or survivable. Nothing should; a sweep forbids that
  vocabulary, and a word getting past it is worth a line.
