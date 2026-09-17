# DRA-149 — `WhatsNew.json` entries, drafted

**Status: DRAFT, and deliberately NOT in `WhatsNew.json`.** Plan P6 asks for these to be written
now and shipped by whatever release carries the five slices; that release does not exist yet and
is not this plan's go.

**They are not appended to the `2.0.0` entry, and that is not tidiness.** `2.0.0` is the build the
Founder smoked and failed (`2.0.0+275cc215`). Putting these highlights under it would tell every
player that the thing they are reading about was in the build that did not have it. The entry
they belong to is the NEXT version's, created when `<Version>` in `Directory.Build.props` moves.

**To ship:** bump `<Version>`, add a new object at the head of `WhatsNew.json` with that
version, today's date, and the three strings below as its `highlights`. `release.ps1` refuses a
tag without both halves.

**Reporter credit:** every one of these is the **Founder's own smoke report**, not a public
discussion, so there is no name or number to credit and no thread to link. Saying that here so
the release slice does not go looking for one.

---

## Highlight 1 — the gear sweep (D1, and it needs FAIL 2's mechanism said plainly)

> **FIXED — "UPGRADE WHAT I WEAR" FOUND NOTHING, FOR EVERYONE, AND IT WAS NOT YOUR GEAR.** If you
> asked the Helper for upgrades to what you are wearing and it told you the catalog had nothing
> better, that answer was wrong for almost everybody — and the reason is worth knowing, because
> it explains why it looked so convincing. EQBuddy compared the "+N" on your item against the
> "+N" on the catalog item. **No item page on eqlwiki carries a "+N" at all** — the wiki
> describes the base item — so that test compared your `+4` against nothing and refused every
> candidate before looking at a single number on it. If any part of your gear was plussed, and in
> Legends everything is, the list could only ever come back empty. IT NOW COMPARES BASE ITEM
> AGAINST BASE ITEM and says so on the block: *"a better BASE item than yours — at the same +, it
> wins"*, with one line noting that your "+N" raises yours by an amount the wiki does not state.
> EQBuddy does not invent that amount and does not pretend to. ON THE GEAR LOCKER NOTHING
> CHANGES: there both items come off your own dump and both carry their "+N", so comparing them
> is the right thing to do and it still happens. WHAT YOU WILL SEE INSTEAD OF THE EMPTY LIST is
> real camps, and — if you are low for the places your best upgrades drop — a sentence naming
> those places, quoting eqlwiki's own creature levels for each one and your level beside them.
> That sentence is not the old bug wearing a new coat: it means EQBuddy found the upgrades and is
> telling you that Temple of Veeshan is not a level-29 plan.

## Highlight 2 — the unread worn rows and the alias (D2)

> **FIXED — AN ITEM YOU ARE WEARING COULD VANISH BETWEEN YOUR BAGS AND THE HELPER, SILENTLY.** If
> EQBuddy could not match something in your inventory dump to an item page, it dropped the row
> and said nothing, so the Helper would show twenty of your twenty-one worn items with no hint
> that the twenty-first existed. NOW IT SAYS SO: *"EQBuddy has never read about: …"*, naming up
> to three of them with the count, and a one-click eqlwiki search beside each name. THE USUAL
> CAUSE IS A SPELLING, AND YOU CAN HELP. The game and the wiki do not always agree — the
> Deteriorated Ancient Faydark Longbow is spelled `Deterioriated`, with an extra `i`, everywhere
> the game prints it. EQBuddy now carries a hand-written list of those, one row per case somebody
> has actually measured, and that bow is the first entry. **The matching is never fuzzy**: a
> near-match would describe an item you are not wearing, which is worse than saying nothing. So
> if the search finds the real page, you have found a row for that list; if it finds no page at
> all, you have found something eqlwiki is missing — and its edit link is right there. IF EVERY
> ROW IN YOUR DUMP IS UNREADABLE the Helper says that instead, and does NOT ask you to run
> `/outputfile inventory` again — the command has already run and would produce the same rows.

## Highlight 3 — the two tradeskill answers (D3 + D4)

> **NEW — EQBUDDY NOW ANSWERS "WHERE DO I GET THIS PROFESSION'S MATERIALS", BOTH WAYS.** Pick Farm
> Materials in the Helper and tick the professions you are raising — or tick none, which shows all
> eight. WHERE THEY DROP: camps ranked by how many of your professions' materials they feed, each
> naming the material, the zone and the creatures the wiki named, with the same level check and
> the same "who drops it" rule the gear answers use. THIS WAS PARKED AND THE PARK WAS ABOUT THE
> WRONG COLUMN: EQBuddy had measured how many item pages file themselves under a profession
> CATEGORY (14 of 11,197) and concluded there was nothing to work with. The pages carry the answer
> somewhere else — 1,276 of them list the RECIPES the item is used in, with the profession as the
> heading — so the work that was parked is simply done. WHERE TO BUY THEM: under each profession,
> up to three shops, in eqlwiki's own words, taken from the map key under each zone page's map.
> *"Everhot Forge - Merchants selling … (Bndainy Everhot), Jewelry Metal and Rare Gems"* in
> Kaladim; *"The Bauble - Merchant selling Gems for Jewelcraft"* in Neriak. **Those sentences are
> the wiki's, not ours** — nothing is re-worded, and where the page named the vendor the name is
> in the line. The eqlwiki link beside each opens the page it came from, which has the MAP showing
> where in the zone the shop is; EQBuddy never fetches it for you, you open it. ONE PROFESSION HAS
> NO DROP LIST AND SAYS SO: fletching materials are bought, foraged or crafted rather than killed
> for, so that half draws a sentence rather than an empty list. AND IF A PROFESSION HAS NO SHOP ON
> THIS LIST, the sentence says the gap is in eqlwiki's maps — it is never a claim that nobody in
> Norrath sells the stuff.

---

## Check before shipping

- Every claim above is TRUE of the build being tagged — re-read after any further slice.
- Nothing above promises a surface that moved. No card was folded, subtracted or relocated by
  DRA-149, so the "X is now Y" rule has nothing to say here.
- No reporter to credit (Founder smoke, not a discussion). Do not invent one.
