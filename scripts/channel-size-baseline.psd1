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
    # 14x over. Never rotated. The busiest file in the repo by commit count.
    'HELM.md'            = 942417
    # 5x over even AFTER DRA-75 rotated 390 entries out of it (1,504,146 -> 311,088).
    'HELM-FEEDBACK.md'   = 311088
    # 9x over. Never rotated. Append-only by design, which is why it only ever grows.
    'DECISIONS.md'       = 614986
    # 7x over. Never rotated. Shrinks for real once plans move to docs/plans/DRA-nn.md.
    'FABLE.md'           = 478113
    # 4x over. Never rotated.
    'BEVEL.md'           = 237551
    # 3x over. Never rotated.
    'SCRIBE.md'          = 177198
}
