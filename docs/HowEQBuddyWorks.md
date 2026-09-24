# How EQBuddy works — the long form

*This is the long-form home of the deep dive, once the landing page's streamlining pass
(DRA-373 D2) lands; until then the landing's section 07 carries the same text, and this file
is the one that gets kept. Relocated in DRA-373 D1, with one correction: the two questions
below are no longer "coming for v2" — the Helper answers them live.*

Two real questions a player asks, walked through the product step by step, with the
screenshots as evidence and an honest account of where each answer comes from.

- [How to read this](#how-to-read-this)
- ["How do I upgrade my main hand?"](#how-do-i-upgrade-my-main-hand)
- ["Where should I hunt next for EXP?"](#where-should-i-hunt-next-for-exp)
- [Where each answer comes from](#where-each-answer-comes-from)
- [The honest limits](#the-honest-limits)

---

## How to read this

EQBuddy's own rule is **evidence before confidence**, so this page holds itself to it.

Everything marked **observed** is a number EQBuddy counted in your log — your kills, your
drops, your xp. Everything marked **estimated** is inference and says so. The Helper draws the
same line in its own words: a sentence from your play reads as yours, and a sentence from the
item pages EQBuddy ships ends *"From EQBuddy's own catalog — not a measurement of your
play."* Every screenshot on this page is a real capture from the repo's screenshot harness
(`scripts/shoot.ps1`) against a staged test profile — no mockups, no composites. Character
names are test fixtures.

> **The shape of every answer**
> Who you are playing → what you own → what you are working on → **what to do next**. Both
> walkthroughs below are that one shape.

## "How do I upgrade my main hand?"

The question every player actually has, and the one a generic "best in slot" list answers
worst — because it doesn't know what you own, what you can reach, or what you've already
half-finished.

**Ask it directly.** The Helper's **Farm Gear** goal answers this question in one place:
choose *Upgrade what I wear*, and it reads your inventory dump, compares what you are wearing
against the wiki's base stats, and names a better base item for the slot — the creature that
drops it, the zone it drops in, and a Map door to go there. It is never a "best in slot": it
can only compare what it has read about, and it says so. When EQBuddy knows your level, a camp
outside your level band is left out and counted, not silently dropped; an offer whose item page
names nothing that drops it is withheld, because EQBuddy will not send you to a camp it cannot
tell you what to kill at. The same answers ride to EQBuddy Mobile, read-only.

![The Helper room with the Farm Gear goal picked: three zones — Temple of Veeshan, Kael
Drakkel, Dragon Necropolis — each naming a better base item than the Cloth Gloves being worn,
the stat it improves, and the creature that drops it, every line marked as coming from
EQBuddy's own catalog; below, counted sentences for the upgrades held back and
why](screenshots/shell-helper-gear-who.png)

*Each answer anchored on what this character wears, naming who drops it and where — and every
line saying it is the catalog talking, not your play. Recipe:
`pwsh -NoProfile -File scripts/shoot.ps1 -Shot shell-helper-gear-who`.*

The steps below are what that answer is built from, and each is still its own surface you can
open.

### 1 · Start from what you own

The Gear Locker reads your inventory dump and compares every wearable you own, slot by slot,
against the wiki's base stats. When something you are carrying is at least as good on every
stat and better on one, the weaker item is marked *outclassed* — "a dump candidate by
arithmetic, not taste."

### 2 · Name the upgrade and find its source

The Quest tracker's search takes a reward, an item, a quest or an NPC. Type the weapon you want
and you land on the quest that provides it — with your own progress against it already counted,
because EQBuddy imported your achievements and scanned your bags. (The Helper's *Include quest
rewards* toggle puts those quests into its gear answer too.)

![The Gear & Loot window's Wishlist tab holding a named gear push: five slots from head to
neck, each target item listed with its source zone, one already ticked as acquired, and a copy
button for the in-game /outputfile inventory command that ticks the rest from your
bags](screenshots/gearloot-gear.png)

*A named gear push, slot by slot, each target with its source — one already ticked, and the
/outputfile command that ticks the rest shipped as a one-click copy.* **observed**

### 3 · Let the Guide run the middle

From there it is the Guide: the reward breaks into steps, **NEXT** names the one that matters
now — *kill The Spiroc Lord on Isle 5 and loot the Spiroc Battle Staff* — and looting the item
ticks the box from the log by itself. (Picture: `docs/screenshots/shell-quests-sky-guide.png`,
recipe `-Shot shell-quests-sky-guide`.)

### 4 · Camp on your own clock

If the source is a named mob, the World map shows the camp with a spawn timer learned from
*your* kills — counting down next to the map, distinct from wiki averages, and honest about
being an estimate until it has seen the cycle. **Track** an upgrade in the Helper and the map
rings the spawn points you have already archived for the creature that drops it.

![The World window's map of Befallen with "Going after" on: a dashed ring around Priest
Amiaz's spawn point with a 4:22 countdown, and a side panel naming the Blackened Wand, the
creature that drops it, and that 1 of 1 archived spawn points here is one of
these](screenshots/zone-map-target.png)

*The tracked Blackened Wand, ringed at a spawn point this character has killed Priest Amiaz at
— EQBuddy does not know where anything spawns, so a zone with no ring only means you have not
archived a kill in it yet. Recipe: `-Shot zone-map-target`.* **observed**

### 5 · Get there with what you have

The route uses travel you have actually unlocked — not the teleport network of a character you
don't play.

> **Why this beats a spreadsheet**
> Each step is fed by the previous surface: the Locker knows your slots, the tracker knows your
> turn-ins, the Guide knows your loot, the map knows your kills. You never re-enter a fact
> EQBuddy already read from the log.

## "Where should I hunt next for EXP?"

EQBuddy won't pretend there is one global best answer. What it has — and a forum thread
doesn't — is what *this character* actually earns, camp by camp.

**Ask it directly.** The Helper's **Level Up** goal ranks the zones you have played by your own
stored evidence: the xp per hour each one paid you, the damage you put out there, how long your
fights ran against your own average, and the level of the creatures you conned there against the
level you are now. A camp you have outgrown is marked down and says why. Under a quarter hour of
evidence, a zone gets no rate at all — unknown is never zero.

![The Helper room with the Level Up goal picked, weighed at level 30 from the log's ding lines:
Kithicor Forest at 14.4% an hour from one stored session and West Commonlands at 13.2% an hour
across two, each with its damage per second, fight length, and — for West Commonlands — the
conned range L5–11 against level 30](screenshots/shell-helper-throughput.png)

*Two zones ranked from this character's own sessions, every number with the evidence behind it.
Recipe: `-Shot shell-helper-throughput`.* **observed**

The steps below are the evidence that answer is made of.

### 1 · What is this camp paying right now?

Kills & Drops keeps the running answer during the sitting: kills per hour, and per creature the
average kill time, the coin, and the experience each one is worth to you.

![The Kills and Drops window: 74.6 kills per hour this session, a per-creature kill list, and a
farming section showing each creature's average kill time, coin and experience alongside
observed drop rates](screenshots/creature-kills.png)

*One session's evidence: 74.6 kills an hour, and what each creature actually pays — orc pawns
die in 2 seconds for coin, pumas take 5 and pay in pelts at this character's own observed rates.
That is a hunting decision, made from your own numbers.* **observed**

### 2 · Compare against your own history

Progress keeps every stored sitting — xp/hr, levels, AA — so "was the new zone actually better
than the old camp?" is a chart, not a feeling. The World page's Drops tab remembers what each
creature paid across sessions, not just tonight. (Pictures: `docs/screenshots/shell-progress-history.png`
and `docs/screenshots/shell-world-drops.png`, recipes `-Shot shell-progress-history` and
`-Shot shell-world-drops`.)

### 3 · Cross-check the shared truth

Zone level ranges and spawn mechanics are the community's shared knowledge, and eqlwiki is its
home. EQBuddy links you to the page rather than paraphrasing it — and when your log shows
something the wiki doesn't know, it drafts the page edit for you to paste.

> **This-character, not best-character**
> A druid who roots and rots clears a camp at a different rate than the wiki's median visitor.
> Your kills/hr and xp-per-creature are the only numbers that were measured on *you* — that is
> why they are the ones EQBuddy shows.

## Where each answer comes from

An honest scorecard. EQBuddy is not a replacement for the wiki — it is the tool that connects the
wiki's shared truth to your private evidence, and sends corrections back.

| The question | A wiki tab alone | EQBuddy over your log | Net |
|---|---|---|---|
| **What does the quest need?** | The source of truth | Mirrors eqlwiki item-for-item, re-harvested weekly; the wiki stays the tie-breaker | ✓ Same truth, in the tool |
| **What have I already done?** | — | Achievements import + bag scan + the log since; turn-ins and half-collected steps counted | ✓ Only your machine knows |
| **What should I upgrade, and who drops it?** | Lists every item | The Helper: a better base item than the one you wear, the creature and zone that drop it, catalog-marked | ✓ Anchored on what you own |
| **What actually drops here, how often — for me?** | Lists what drops | Observed rates with your kill count as the denominator | ✓ And unknown drops become paste-ready wiki edits |
| **When is my camp due?** | Respawn notes | Timers learned from your own kills, counting down beside the map | ✓ Your clock, not an average |
| **Where should I hunt next?** | Zone level ranges | The Helper: your camps ranked by what they paid you, marked down when you have outgrown them | ✓ Measured on you |
| **Was tonight better than last week?** | — | Stored session history with level and AA charts | ✓ Memory a browser doesn't have |
| **Endgame mechanics nobody has confirmed** | Sometimes silent | A stub that says what is missing and asks the player who knows | ◐ Honesty, not coverage |
| **How good are the other players?** | — | Never — by principle, not by gap | ✗ Not a feature. Not ever. |

## The honest limits

> **A precise-looking wrong number is worse than none**
> Curated game data — spawn timers, quest catalogs — is never auto-written from a scrape. The
> weekly wiki refresh *flags* conflicts for a person to resolve, and where quest data conflicts
> and can't be resolved, EQBuddy matches the wiki on purpose: being wrong the same way as the
> community's own reference is recoverable; being uniquely wrong is not.

The same rule shapes the Guide's stubs (a step without a source says so instead of inventing
one), the Locker's "stats not fetched yet" bucket, the Helper's counted sentences for every answer
it held back, and the observed/estimated labels you saw above. If EQBuddy tells you something
about Norrath, you should be able to ask it how it knows — and get a real answer.
