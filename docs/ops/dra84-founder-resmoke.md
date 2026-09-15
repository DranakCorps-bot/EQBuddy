# DRA-84 Farm Gear — Founder re-smoke checklist

**What this is.** The Founder FAILED Helper → Farm Gear on Desktop 2.0.0+`3174aa5f`
with four acceptance items. D1–D4 answered them; this is the list of what to look at
to confirm that on a real profile, and what would count as a FAIL rather than a
preference. Written in DRA-84 D5 (plan §3, P6).

**Before you start.** Build is a local Release build — *no tag, no release, no Play
Console, no Pages* (acceptance 4; nothing in the DRA-84 plan touches them). Open the
EQBuddy window → **Helper**, pick the goal **Farm Gear**, and run `/outputfile
inventory` in game if the room says it has never been told what you are wearing.

---

## 1 · Farm Gear uses the full item/zone catalog, not this session's loot

**Look at:** any Farm Gear answer for a zone you have never farmed.

**Expect:** item lines for places you have never been — each one reading
*"<item> beats the <worn item> in your slot — +N <stat>. <creature> drops it. From
EQBuddy's own catalog — not a measurement of your play."* The catalog clause is the
tell: it is the sweep reading all ~11k item records, not your loot.

**FAIL if:** every line names an item you have personally looted, or the room says it
has nothing for a slot you know has upgrades in the game.

**Where your own evidence still wins:** a zone you HAVE looted the item in says so
with your own kill count instead, and carries no catalog clause. That is one producer
per fact, not a second opinion.

---

## 2 · Every rec names item + source mob(s) and/or quest + zone

**Look at:** the creature half of each drop line, and the note under the answers.

**Expect:**

- Up to **three** creatures per item, in the page's own order, with the rest counted
  as the page's: *"…and 4 more on its page"*. Never one where the page names six.
- A line with **no** creature is not shown at all. Under the answers, a sentence says
  how many were left out and why: *"N more drop offers are not listed: their item
  pages name nothing that drops them in those zones, and you have not looted one
  there."*
- **Quest** rows (the ± quests toggle) are exempt and name the quest instead — the
  quest IS the path, so nothing has to drop it.

**FAIL if:** an offered camp names no creature, or the count of what was left out is
missing while the list is visibly shorter.

**Picture:** `docs/screenshots/shell-helper-gear-who.png` (desktop),
`docs/screenshots/mobile-helper-gear.png` (phone — the same three sentences).

---

## 3 · No zones with no viable upgrade path for your intent/level

Two different failures, two different mechanisms — check both.

**3a · The Crushbone class (a band your level is outside).** Needs EQBuddy to know
your level: the Character room sets one, or a `/ding` in the log.

**Expect:** a zone is dropped when eqlwiki's own creature band tops out **10 or more**
levels under you, or starts **5 or more** levels over you, and a line under the
answers names each one with its band and your level: *"2 zones EQBuddy has upgrades
for are not listed at your level 28: Temple of Veeshan (60 and above), Veeshan's Peak
(60 and above)."*

**FAIL if:** the zones vanish with nothing said, or a zone whose page gives no band is
dropped (not knowing is never a reason to hide a camp), or the sentence calls a place
tough/easy/dangerous rather than quoting two numbers and eqlwiki.

**Known and deliberate:** with **no** level the gate stands down completely and the
room says so. Crushbone stays refused even if you personally farmed it at 12 — that is
the default flagged in `DECISIONS.md` as the one most worth your veto.

**3b · The Rathe class (a band that cannot refuse it).** Rathe Mountains is 13–45, so
a level rule can never remove it; what was wrong with that row is that it named no
creature and no path. It is removed by item 2's rule, not by the band gate. If you see
Rathe Mountains offered with a creature named on every line, that is the rule working,
not the old bug.

**Picture:** `docs/screenshots/shell-helper-gear-band.png`.

---

## 4 · Nothing public moved

No tag, no release, no `release.ps1`, no signing, no Pages, no Play Console. The
re-smoke is a local Release build.

---

## What is knowingly NOT fixed

- **The promoter still writes zones that are not places.** 75 wearable (item, zone)
  pairs in the shipped catalog carry a `DropZones` string like `}}` or `:* Dread` —
  one bulleted wiki line read as five locations. The Helper no longer offers any of
  them as a camp (a string that is not a place has no creature under it either), but
  **the Gear room, item lookups and the wishlist still read `DropZones` directly**, so
  looking an item up can still show you `}}` as a zone. That is a transform change
  plus a catalog rebuild — V2, filed in `FABLE.md`, not fixed here.
- **Per-item level requirements are not read.** The gate reads the ZONE's band;
  exactly one wearable record in 11,196 carries a level key, so there is nothing to
  read on the item side.
- **Farm to Sell is not band-gated.** It ranks what you have already looted; there is
  no camp to band.

## The committed numbers behind the pictures

Each of these is asserted against the launched app or the real projection, so a
disagreement on your machine is a finding rather than a fixture:

| Claim | Where it is pinned |
|---|---|
| A warrior in cloth gloves gets 3 zones, 6 named creatures, 5 offers withheld, 89 capped | `ShellHostTests.AnUpgradeNothingCanNameADropperForIsWithheldAndTheRoomSaysSo` |
| At level 28 the gate refuses exactly Temple of Veeshan and Veeshan's Peak, both `60+`, on the BOTTOM arm | `ShellHostTests.TheBandGateRefusesTheZonesOutsideYourLevelAndSaysSo` |
| The phone draws the same three zones, the same six creatures and all three caption sentences | `ScreenshotFixtureTests.WriteHelperGearSnapshot` + `HelperSurfaceParityTests` |
| 98.2% of wearable (item, zone) pairs can name a creature | `ItemCatalogWhoCoverageTests` (floor armed at 50%) |
