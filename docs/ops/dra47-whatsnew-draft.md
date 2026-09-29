# DRA-47 — `WhatsNew.json` entry, drafted

**Status: DRAFT, and deliberately NOT in `WhatsNew.json`.** The same idiom as
[dra181-whatsnew-draft.md](dra181-whatsnew-draft.md): the release that ships this does not
exist yet and is not this slice's go. Every player-noticeable change carries an entry **in the
release that ships it**, so it is written now, while the change is fresh, and appended when
`<Version>` moves.

**Reporter credit:** none to give. The per-character move is Delivery 2's own plan (Fable,
2026-09-09 ~10:15 PM CT, N3), and the Quests-tab un-mark defect was found by this slice while
moving the code, not reported. Said here so the release slice does not go looking for a thread.

---

## Highlight — your Sky and Epic boxes belong to the character now

> **YOUR PLANE OF SKY AND EPIC 1.0 CHECKLISTS ARE NOW PER CHARACTER.** They used to be one
> set of boxes for everyone on the PC: tick a Wind Rune on your main and your alt had it too.
> Each character now keeps its own ticks, its own turned-in rewards and its own "Mark as
> complete" epics, on the desktop and on your phone alike. **NOTHING YOU TICKED IS LOST.** The
> first time each of your characters logs in after the update it starts from exactly the boxes
> you could see before — so nobody's list changes on the day you update — and from then on
> they are separate. Clear a box on one character and it stays ticked on the others until you
> clear it there too. A copy of the old shared list is kept beside your settings as
> `quest-ticks.pre-ledger.json`, untouched.
>
> **FIXED — UN-MARKING A SKY TEST ON THE QUESTS TAB DID NOTHING.** Marking a "Sky Test" quest
> done on the Quests tab and then un-marking it left it done: the reward reopened on the Sky
> tab, but the Quests tab kept showing it completed. Un-marking now un-marks it on both.
>
> **FIXED — THE BEASTLORD'S WINDHOWL/SPIRIT RENDER TEST NOW SHOWS WHAT IT PAYS.** Every other
> Plane of Sky reward shows the item's own window when you hover its heading (or tap the reward
> line on your phone). This one pays TWO items for one hand-in, and it showed a sentence
> instead. It now shows both windows, Windhowl and then Spirit Render, each under its name.

---

## Check before shipping

- The file name in the first paragraph is the one `QuestTickMigration.FileName` writes.
- "on your phone alike" is true only if the phone build shipped with it — the phone reads the
  same settings lists the desktop does, so it is, but read the release diff for it.
