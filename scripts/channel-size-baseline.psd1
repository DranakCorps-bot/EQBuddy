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
    # Rotated 2026-09-20 by DRA-231 (DRA-144 F6): 676,431 B -> 85,238 B, into
    # channels/2026-Q3/DECISIONS.md. A date cut at 2026-09-17 with a hand-triaged floor:
    # of the 30 older blocks a coarse LIVE ASK / PARK / HOLD / STANDING sweep flagged, 24
    # were dispositioned as closed and moved, and 6 that carry something genuinely open
    # were kept at any age. The first cut of this rotation landed 81,798 B and was wrong by
    # 3,440: that coarse sweep has no LIVE ASK / PARK / HOLD / STANDING marker to flag an
    # `exo-experiment:` tag, so all six of them rotated out from under a generator and three
    # tests that read them from the LIVE file. The tags are re-pinned verbatim, which is what
    # the never-rotate floor asks for and why this row is 85,238 and not 81,798.
    # Still 1.3x over policy, so this row is LOWERED and KEPT: check C discharges a row only
    # at 64 KiB or less, and no legal cut reaches the ceiling while that floor stands. The
    # 10% band this buys is 8,523 B - about a quarter of one median day's append
    # (32,231 B/day measured 2026-09-10..19).
    # That the ceiling and the band cannot both hold this file is DRA-144 F8's subject.
    'DECISIONS.md'       = 85238
    # 7x over. Never rotated. Shrinks for real once plans move to docs/plans/DRA-nn.md.
    'FABLE.md'           = 478113
    # BEVEL.md left this table on 2026-09-20, discharged by the DRA-258 rotation
    # (DRA-144 F9): 237,542 B -> 60,352 B, into channels/2026-Q3/BEVEL.md. It was 4x over
    # and had never rotated, because 68% of it was a single UNDATED container heading
    # holding 20 dated h3 pre-designs: a date cut that reads h2 headings sees one undated
    # block and walks past it, which is how this file survived every prior pass. The cut
    # was made at h3 INSIDE the container; the container heading stays live with a pointer
    # to the archive, and the orientation notes under it are undated and still current.
    # This row is DELETED rather than lowered, which is the difference from SCRIBE.md and
    # DECISIONS.md above: check C discharges a row the moment the file reaches 64 KiB or
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
    'SCRIBE.md'          = 114715
}
