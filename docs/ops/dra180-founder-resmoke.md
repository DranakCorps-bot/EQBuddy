# DRA-180 — Founder re-smoke checklist (the bow and the Baron's Blade)

**What this is.** You failed Farm Gear on 2026-09-17: the worn **bow** and the worn **Baron's
Blade** both came back with no upgrades and no reason, and *Replace* named places that are not
in the game yet. Five slices answer that. The last one, D5, turned on the era gate with the
world at **Classic** (Helm ruled on 2026-09-23; `WorldEra.Source` cites your P4 answer).

This page says **what each of those two screens should SAY** now, so you can tell a fix from a
failure without reading any code. Both screens are still *empty lists*. That is correct, and
the difference from the FAIL is that **each one now says why, with numbers.**

Every number is measured by running the real engine over your committed dump
(`tests/fixtures/inventory/dranak.txt`) and the shipped catalog. It uses the same inputs the app
assembles (`HelperSources`): level **29**, classes **Warrior / Paladin / Cleric**, the world at
Classic. The numbers are pinned in `tests/EQBuddy.Tests/FounderResmokeTests.cs`, in the
*DRA-180 D5* section. If a wiki refresh moves one, that file goes red rather than this page going
quietly wrong.

**Nothing here marks DRA-180 PASS.** That is your call. There is no release in this slice and
no republish; the smoke route is Helm's and yours (DRA-164 item 8).

---

## Before you start

1. A build that carries D5. `Help → About` must show a commit with **`3ef46a40` as an
   ancestor**:

   ```
   git merge-base --is-ancestor 3ef46a40 <the commit About shows>
   ```

   Exit 0 means the era gate is live in that build. A later commit is not a wrong build.
2. `/outputfile inventory` once, wearing the gear you want answers for.
3. Your level is known to EQBuddy (Character room). The predictions below are at **29**, and
   they change with it.
4. Helper room → Farm Gear → *Upgrade what I wear*. **Include quest rewards** can be on or off;
   both are predicted below.

---

## Screen 1 — the bow

**Where:** the Farm Gear block, under *Upgrade what I wear*, in the list of worn items that
got nothing.

**What it should say, word for word:**

> Deterioriated Ancient Faydark Longbow +2 — range: EQBuddy has read about 2 better base items
> and left every one out. 2 come from content eqlwiki dates later than the era EQBuddy has been
> told the world is at. Nothing in reach beats this item's base.

(Your live bow is `+8`; the committed dump says `+2`. Only the `+N` in the name moves. The
count and the reason do not, because the comparison is always base item against base item.)

**Why that is the true answer.** Among RANGE items that Warriors, Paladins and Clerics can use,
exactly **two** beat this bow's base: the Priceless and the Primal Velium Reinforced Bow. Both
drop only in **Sleeper's Tomb**, which eqlwiki dates to **Velious**.

**What changed since D3.** D3's sentence said *"2 drop only where eqlwiki lists creature levels
outside yours"*. That was the level-band rule. It was true, but it was the second reason. The
era rule now runs first, so the same two bows are counted as later content and the level clause
is gone. **If you see the level clause instead of the era clause, the era gate is off** (wrong
build, or `WorldEra.Current` emptied).

**Quest toggle:** no difference. The one quest bow that beats it, Rune Shafted Harpoon, is
Shaman-only.

## Screen 2 — the Baron's Blade

**Where:** the same list, in the PRIMARY row.

**What it should say, word for word** (quest rewards **off**):

> The Baron's Blade +6 — primary: EQBuddy has read about 11 better base items and left every one
> out. 11 come from content eqlwiki dates later than the era EQBuddy has been told the world is
> at. Nothing in reach beats this item's base.

With **Include quest rewards on**, the same sentence reads **13** in both places.

**Why that is the true answer.** Every one-handed weapon that beats the Blade's base and that
your classes can use is Kunark or Velious loot:
- **9** drop in Sleeper's Tomb (Velious): the Velium war lance, battlehammers, spears and
  warswords, plus Sceptre of Destruction and Smolder.
- **Katana of Endurance** drops in Veeshan's Peak (Kunark).
- **Blade of Carnage** drops in Kael Drakkel (Velious).

The two extra quest rewards are the Priceless and Primal Velium Knight's Sword from *Garath's
Weapons to Trade*, a Velious quest. Some of these places have level bands a 29 passes, so only the era
can refuse them. **Kael Drakkel is `30-60+`**, and that is how *Blade of Carnage* reached your
screen in the FAIL.

**The number is 11, not 21, on purpose.** Another **10** better weapons are **two-handed**, and
your off-hand is holding a morning star. They are left out before they are counted (the off-hand
rule, DRA-222). The sentence does not claim them, and the Helper's off-hand caption counts them
separately.

**What the FAIL looked like, for contrast.** Measured with the era gate off: the Blade got **no
sentence at all**, because Blade of Carnage survived the level rule. Its row sat outside the
three zones on screen. An unexplained nothing is exactly what you failed.

> Fixture note: no committed inventory dump wears the Baron's Blade. The plan pointed at
> `hateborne.txt` line 578, but that row is `Equipment`, a stored item; that character's Primary
> is Blade of Abrogation. So this prediction is **your own dump with the Primary row changed to
> `The Baron's Blade +6`**. That single change still goes through the real parser and the real
> catalog. A fresh `/outputfile inventory` from you, wielding the Blade, would let us replace
> that one-cell change with the real thing. It is not needed for this smoke.

## Around those two rows — the rest of the block at 29

- **An era caption comes first:** *"45 places EQBuddy has upgrades for sit in content eqlwiki
  dates later than Classic: Chardok (Kunark), City of Mist (Kunark), Cobalt Scar (Velious),
  and 42 more. …"* With quest rewards on the count is much larger (146), because every
  later-era quest is refused too.
- **A much shorter level-band caption:** **9** zones (was 30 with the era gate off), e.g.
  *Erud's Crossing (5–15), Greater Faydark (1–12), Nagafen's Lair (40–55), and 6 more.* The
  high planes left through the era rule before the level rule ever saw them.
- **Three zones drawn, all ones a 29 can go to:** *Blackburrow, Mistmoore Castle, Lower Guk.*
  Lower Guk's wiki page carries **no era banner at all**. EQBuddy does not assume a missing era
  means Classic, so that zone is shown because nothing refused it, not because it was dated
  Classic.
- **Seven worn items** get the per-item sentence (four did with the era gate off).

## What WOULD be a failure

- Either anchor with **no sentence** and no row. That is the FAIL shape, back again.
- A drawn row naming **Kael Drakkel, Sleeper's Tomb, Veeshan's Peak** or any Kunark/Velious
  zone. That means the era gate is not live.
- The bow sentence with the **level** clause instead of the era clause (same cause).
- A count that differs from the one above, **on a build that has `3ef46a40`**. That is worth a
  note on DRA-180 with the number you saw. `FounderResmokeTests` pins these counts, so a wiki
  refresh that moved one would already have reddened CI.

**Not measured in this pack:** the *Replace a slot* intent. It goes through the same era, level
and creature-name rules, and they are covered by `RecommendationsEraGateTests`. This page does
not predict its rows.
