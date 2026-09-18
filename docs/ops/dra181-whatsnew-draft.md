# DRA-181 — `WhatsNew.json` entry, drafted

**Status: DRAFT, and deliberately NOT in `WhatsNew.json`.** The same idiom as
[dra149-whatsnew-draft.md](dra149-whatsnew-draft.md): the release that ships this does not
exist yet and is not this slice's go. The standing rule is that every player-noticeable
change carries an entry **in the release that ships it** — so it is written now, while the
change is fresh, and appended when `<Version>` moves.

**Not appended to the `2.0.0` entry.** `2.0.0` is the build the Founder smoked, and this is
one of its two FAIL items — filing the fix under that version would tell every player the
thing they are reading about was in the build that did not have it.

**Reporter credit:** the Founder's own Desktop smoke (2026-09-17), not a public discussion.
There is no name or number to credit and no thread to link. Said here so the release slice
does not go looking for one.

---

## Highlight — the quest window's class chips

> **FIXED — THE CLASS CHIPS UNDER THE QUEST WINDOW'S CLASS PICKER IGNORED WHAT YOU PICKED.**
> The row of chips under the picker — `Any · WAR · PAL · CLR` — is the lens that narrows the
> whole window to one of your classes. It was built from the classes EQBuddy has decided you
> ARE, which is not the same list as the classes you ticked: picking Warrior and Paladin left
> a Cleric chip sitting there. **And it was a dead button.** The list itself had already
> narrowed to what you picked, so clicking the leftover chip lensed the window to a class it
> was no longer showing and the next repaint quietly dropped the lens again — a click that
> did nothing, with nothing on screen saying so. THE CHIPS NOW ARE YOUR SELECTION: tick three
> classes and there are three chips, untick one and its chip goes with it. WITH NOTHING
> PICKED NOTHING CHANGES — the chips are the classes your character holds, exactly as before.
> AND A CLASS YOU PICKED THAT ISN'T YOURS NOW GETS ONE TOO: the picker offers all sixteen on
> purpose, because you may be looking something up for a friend, and that was the one case
> where the class you had chosen was the one class you could not lens to. The line naming who
> your character is has not moved and still shows every class EQBuddy knows about — picking a
> class narrows the view, it never edits your identity.

---

## Check before shipping

- Every claim above is TRUE of the build being tagged — re-read after any further slice.
- **No surface moved**, so the "X is now Y" rule has nothing to say here: no card was folded,
  subtracted or relocated, and the classic Sky class view is unchanged (KEEP).
- No reporter to credit (Founder smoke, not a discussion). Do not invent one.
