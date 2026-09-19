# DRA-180 — `WhatsNew.json` entries, drafted

**Status: DRAFT, and deliberately NOT in `WhatsNew.json`.** Plan D3 asks for these to be
written now and shipped by whatever release carries the card's slices; that release does not
exist yet and is not this plan's go. The `dra149-whatsnew-draft.md` and
`dra181-whatsnew-draft.md` files beside this one are the same idiom and the same reason.

**They are not appended to the `2.0.0` entry.** `2.0.0` is the build the Founder smoked and
failed (`2.0.0+275cc215`); putting these under it would tell every player that the thing they
are reading about shipped in the build that did not have it. The entry they belong to is the
NEXT version's, created when `<Version>` in `Directory.Build.props` moves.

**To ship:** bump `<Version>`, add a new object at the head of `WhatsNew.json` with that
version, that day's date, and the strings below as its `highlights`. `release.ps1` refuses a
tag without both halves.

**Reporter credit:** both of these come from the **Founder's own Desktop smoke** on
2026-09-17, not from a public discussion, so there is no name or number to credit and no
thread to link. Said here so the release slice does not go looking for one.

**Highlight 2 is HELD until D5 sets the world era.** Ship highlight 1 in any release that
carries D2+D3; shipping highlight 2 before `WorldEra.Current` has a value would describe a
gate that stands down on every machine. That is the one ordering constraint in this file.

---

## Highlight 1 — the empty upgrade list finally says what it found (D2 + D3) — READY

> **FIXED — "NO UPGRADES FOR THIS ITEM" NOW TELLS YOU WHAT IT ACTUALLY FOUND.** If you asked
> the Helper about something you are wearing and got nothing back, EQBuddy gave you the same
> blank answer whether it had found nothing at all or found several and ruled every one of them
> out — and those are completely different facts. **IT NOW SAYS WHICH.** Where every better
> item it read about was removed by one of its own rules, it names the worn item and accounts
> for all of them: *"EQBuddy has read about 2 better base items and left every one out. 2 drop
> only where eqlwiki lists creature levels outside yours. Nothing in reach beats this item's
> base."* THAT SENTENCE IS OFTEN THE CORRECT ANSWER and it was always the correct answer — the
> bug was never telling you. WHAT IT IS NOT is a claim that your item is the best in the game.
> EQBuddy compares against the item pages it ships and nothing else, and it says so; an item it
> has never read about cannot be compared at all. The numbers are eqlwiki's own creature levels
> for the zones those items drop in, next to your level, so you can disagree with them. This
> shows on the PC and on your phone, in the same words.

## Highlight 2 — camps in content the server has not opened (D2 + D5) — HOLD FOR D5

> **FIXED — THE HELPER COULD SEND YOU TO A ZONE THAT IS NOT IN THE GAME YET.** EQBuddy decided
> whether a camp suited you by reading eqlwiki's creature levels for it, which is a good rule
> with one blind spot: **a level range cannot say which expansion a place belongs to.** Kael
> Drakkel's page lists its giants at 30–60, so a level-29 was offered Kael Drakkel — and the
> giants really are level 30, in Velious, in content the world has not reached. The same went
> for Icewell Keep and Veeshan's Peak. **EQBUDDY NOW READS THE ERA EACH ZONE PAGE GIVES
> ITSELF** and leaves out anything dated later than where the world actually is, saying so with
> the page's own words: *"3 places EQBuddy has upgrades for sit in content eqlwiki dates later
> than Classic."* IT IS A SEPARATE SENTENCE FROM THE LEVEL ONE on purpose — being told to come
> back at 30 for a zone that does not exist is worse advice than no advice. Quests are checked
> the same way: a quest in unopened content cannot be started either. WHAT THIS DOES NOT DO is
> guess. The era comes from the zone pages EQBuddy ships and from one hand-written value saying
> where the world is; where a page does not date itself, nothing is assumed and nothing is
> removed.

---

## What is deliberately NOT in either entry

- **The band gate and the who rule.** They shipped in DRA-84 and were credited there. This
  card changed the WORDS around them, not the rules, and re-announcing a rule because its
  sentence improved would make the list untrustworthy for the reader who remembers it.
- **`ZoneEras.json` and the transform** (D1). A committed data table with no player-visible
  behaviour of its own; highlight 2 is what it is FOR, and it is credited there.
- **Anything about the Founder's smoke as an event.** The entries describe what a player will
  see, not our process.
