# Grandfather baselines for scripts/channel-size-guard.ps1.
#
# THIS TABLE IS A DEBT REGISTER, NOT A POLICY. The policy is 64 KiB (65,536 bytes) and it
# lives in the guard. Every row below is a file that was ALREADY over that limit on the day
# the ratchet shipped, recorded so the ratchet could be turned on without deadlocking the
# nine ledgers that have never rotated (DRA-26 plan rev 3 section 0.2).
#
# WHY A GRANDFATHER LIST EXISTS AT ALL. A ratchet that simply refused growth over 64 KB
# would have gone red on its first day for 9 of the 11 rostered files, and it would have
# gone red at HELM.md's rate: 32 of the 80 commits before this one touched HELM.md, each
# adding ~13 KB. So roughly two of every five pull requests would have failed on a file the
# author was appending to correctly. Worse, the remedy - rotation - is NOT the Executor
# seat's to perform (DRA-26 rev 3 section 5, "Executor never trims"), so the agent holding
# the red had no legal move. A guard whose only remedy is out of reach of whoever trips it
# is not a gate; it is a stall.
#
# WHAT A ROW BUYS. The named number plus 10% - the tolerance the repo's other ratchet
# already uses (docs/Architecture.md, "Hotspot ratchet": `ArchitectureTests` fails the
# build if these grow more than 10% past their baseline). DRA-73 plan rev 2 section 4.2
# asked for this check in exactly those words - "same idiom as the hotspot ratchet" - so
# the tolerance is borrowed from a calibrated guard rather than invented here.
#
# WHAT A ROW DOES NOT BUY: forever. At HELM.md's measured rate the 10% band is about seven
# appends. Running out of band is the ratchet WORKING - the file is 14x over policy and the
# answer is to rotate it into docs/ops/claude-archive/channels/2026-Q3/, which is DRA-144's
# card, not a number to raise here.
#
# HOW A ROW LEAVES. Rotation. When a rotation brings the file to 64 KiB or less, DELETE its
# row in the same commit; the guard fails that pull request until you do, for the same
# reason the hotspot table insists the lift and the re-baseline land together. Rows only
# ever leave. The guard refuses a pull request that RAISES a number here, and refuses one
# that ADDS a key - either would be a self-granted exemption written by the change it
# exempts (trap 52), and the whole value of a ratchet is that its ceiling can only fall.
#
# MEASURED at EQBuddy `main` 275cc215 (2026-09-17), in LF-normalised UTF-8 bytes - the same
# unit the guard measures, so these numbers are comparable to what CI prints.
#
# NOT HERE, ON PURPOSE:
#   CLAUDE-FEEDBACK.md (18,283 B) and SCRIBE-TESTING.md (15,419 B) are under the limit and
#   need no grandfathering; they are governed by the ceiling arm with zero headroom.
#   HANDOFF.md (248,286 B) is over the limit but is NOT a channel ledger and is not in the
#   guard's roster - it is a retirement candidate under DRA-26 card D, and ratcheting a file
#   nobody appends to (untouched since 2026-08-31) would be coverage theatre.

@{
    # HELM.md and HELM-FEEDBACK.md left this table on 2026-09-18, discharged by the DRA-154
    # rotation that took them to 25,411 B and 4,730 B - the first rotation of the HELM.md
    # class. Rows only ever leave, and they leave in the pull request that earns it.
    # DECISIONS.md left this table on 2026-09-21, discharged by the DRA-281 rotation
    # (DRA-144 F8 / DRA-246 re-seat): 86,981 B -> 55,411 B, into channels/2026-Q3/DECISIONS.md.
    # DRA-231's 2026-09-20 cut (row LOWERED to 85,238, not deleted - see the BEVEL.md note
    # below for why a lowered row differs from a deleted one) kept a date-cut floor of
    # 2026-09-17 plus 6 older blocks (2026-09-11..09-16) its coarse sweep called genuinely
    # open. DRA-281 re-triaged those 6 by hand rather than trusting the label: DRA-57's LIVE
    # ASK reads answered in its own text, DRA-106's LIVE ASK to Helm closed per the archived
    # `HELM-FEEDBACK.md` LOOP CLOSE entry, DRA-71's and DRA-65's PARKs are tracked live in
    # `FABLE.md` (not in this file, so archiving the DECISIONS.md report of them loses
    # nothing the never-rotate floor protects), and DRA-84's D3-scoped un-PARK note is
    # superseded now that D4/D5 already ran. All 6 (31,937 B) moved; this row is DELETED
    # rather than lowered, same as BEVEL.md below - 55,411 B is under the ceiling so the
    # ceiling arm governs this file now with no band at all.
    # FABLE.md left this table on 2026-09-21, discharged by the DRA-259 rotation
    # (DRA-144 F10): 501,593 B -> 39,850 B, into channels/2026-Q3/FABLE.md. It was 7x over
    # and had never rotated - the last of DRA-144's nine to get a card at all. A plain date
    # cut would have been wrong twice over, and both traps are the never-rotate floor rather
    # than the arithmetic. (1) 5,259 B of the file was an UNDATED operating charter - "When
    # this file is in play" / "How Fable reaches Helm" / "How Claude calls Fable" / "Item
    # shape", explicitly "standing process, not a V2-V3 plan item" - sitting at line 2,676
    # where every date cut takes it; it was MOVED to the top of the live file, not archived.
    # (2) Three `###` sections are cited BY SECTION NUMBER from ten places in scripts/,
    # tests/, installer/ and site/ - `FABLE.md` section 4 (the SCREEN mutex, six citers, two
    # of them inside runtime error strings a user reads), section 3 (TR-2, three citers) and
    # "plan section 3, DRA-48" (the landing page's visual tokens, cited by the shipped
    # site/assets/css/landing.css). The card named two of those anchors and seven citers; the
    # third anchor and the other three citers were found by re-deriving the set from the repo.
    # All three are re-pinned verbatim in the live file with provenance labels, which is what
    # the floor asks for and is 4,054 B of the 39,850.
    # This row is DELETED rather than lowered, the same call as BEVEL.md below: check C
    # discharges a row the moment the file reaches 64 KiB or less, and 39,850 B is under the
    # ceiling, so the ceiling arm governs FABLE.md now and it has no tolerance band at all.
    # That is a known cost, not an oversight - at the measured 13.7 KB/day the 25,686 B of
    # headroom is about 1.9 days, and check B refuses any pull request that adds this
    # key back. The durable answer is DRA-73's - FABLE.md becomes a short index and plans
    # move to docs/plans/DRA-nn.md - and it is filed as a follow-on rather than bought with
    # a number here (trap 52).
    # Successor note to the DRA-281 paragraph above, written in the pull request that
    # falsifies one of its sentences rather than leaving it to go stale: DRA-281 discharged
    # DRA-71's and DRA-65's PARKs from DECISIONS.md on the ground that they are "tracked
    # live in FABLE.md", and this rotation archives the three `### 4. PARKED` plan
    # inventories that were that live copy. Both are still right, because the live residence
    # for every row of those inventories is SOURCE, not a ledger, and that was re-derived
    # here rather than assumed:
    #   - per-class levels: src/EQBuddy.Core/CharacterLevel.cs restates the park AND its
    #     reopen condition in the doc comment ("Per-class levels are PARKED ... the reopen
    #     condition is a game dump or log line that states it").
    #   - generic camp/XP catalog: src/EQBuddy.Core/Recommendations.cs ("the plan PARKS that
    #     until somebody asks for it"), with RecommendationsTests.cs holding it.
    #   - item-to-profession arithmetic and the recipe/ingredient model: SUPERSEDED, not
    #     moved - DRA-149 D3 un-parked Farm Materials on the finding that "the park measured
    #     the wrong COLUMN", live in docs/TestPlan.md, with the floor kept open by a live
    #     test (EveryProfessionMatchesAZoneAndJewelcraftingClearsTheParkFloor in
    #     ZoneMerchantsTests.cs) and restated in live DECISIONS.md.
    #   - catalog copper item value: SUPERSEDED - DRA-84 P9 met its reopen condition and the
    #     code shipped (EqlWikiItems.ParseMerchantValue, ItemInfoWindow).
    #   - DRA-65's harvested "ways to raise": a new harvest shape, refused outright by live
    #     HELM.md's standing PARK S8/S9 (no eqlwiki harvest authorized in this program).
    # So no unexpired park lost its last live home in the pair of rotations, and the ledger
    # copies that archived were reports of rules resident elsewhere - the residence test the
    # never-rotate floor actually asks for.
    # BEVEL.md left this table on 2026-09-20, discharged by the DRA-258 rotation
    # (DRA-144 F9): 237,542 B -> 60,352 B, into channels/2026-Q3/BEVEL.md. It was 4x over
    # and had never rotated, because 68% of it was a single UNDATED container heading
    # holding 20 dated h3 pre-designs: a date cut that reads h2 headings sees one undated
    # block and walks past it, which is how this file survived every prior pass. The cut
    # was made at h3 INSIDE the container; the container heading stays live with a pointer
    # to the archive, and the orientation notes under it are undated and still current.
    # This row is DELETED rather than lowered, which is the difference from SCRIBE.md
    # below: check C discharges a row the moment the file reaches 64 KiB or
    # less, and 60,352 B is under the ceiling, so the ceiling arm governs BEVEL.md now and
    # it has no tolerance band at all. That is a known cost, not an oversight - at the
    # measured 16.4 KB/day this file is back over policy in well under a week, and check B
    # refuses any pull request that adds this key back. The successor rotation is filed
    # rather than bought with a number here (trap 52).
    # Rotated 2026-09-20 by DRA-229 (DRA-144 F4b): 213,675 B -> 114,715 B, into
    # channels/2026-Q3/SCRIBE.md. The cut is a TRIAGE by the entries' own Priority field,
    # not a date cut - SCRIBE.md is an inbox and 42 of its 92 entries are still open at
    # any age, so a date cut would have been silent closure. Still 1.7x over policy, so
    # this row is LOWERED and KEPT: check C discharges a row only at 64 KiB or less, and
    # the open-ask floor is ~105 KB on its own, so no legal cut reaches the ceiling.
    # Rotated again 2026-09-23 on the #710+#690 intake (PR #733), which had spent the
    # 10% band (116,999 B -> 126,424 B against a cap of 126,186 B). Pass 2 appended
    # 26 blocks / 47,257 B into the same archive: waiting blocks dated before
    # 2026-09-01, undated waiting blocks, the terminal BUILT /consider entry, and the
    # Avalonia breakout note. Kept live: the two new intakes, every must-fix /
    # approved / authorized / authorized-next / open block at any age, and every
    # waiting block dated 2026-09-01 or later. 126,424 B -> 79,897 B. Still over
    # 64 KiB, so this row is LOWERED to that measured size and KEPT. Check C
    # deletes a row only at 64 KiB or less; deleting it here would be the HELM.md
    # mistake named above (no band left, and check B refuses putting the key back).
    'SCRIBE.md'          = 79897
}
