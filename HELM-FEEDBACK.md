# HELM-FEEDBACK.md — live channel

**Rotated 2026-09-22 by DRA-329** under the standing `EXO-CHANNEL-ROTATE` card (DRA-154).
The ceiling arm had **103 B** of headroom left (65,433 B of 65,536 B, no baseline row), so
the next ask of any size could not land. Every answered entry moved **verbatim** to
[`docs/ops/claude-archive/channels/2026-Q3/HELM-FEEDBACK.md`](docs/ops/claude-archive/channels/2026-Q3/HELM-FEEDBACK.md)
as **ARCHIVE PASS 4**. Nothing was reworded, reordered or trimmed to fit.

**Cut depth: the never-rotate floor, and nothing above it.** What is below is exactly the
three LIVE ASKs whose ruling is **not yet on `main`**, in the order they were written,
byte-for-byte as they stood at `main` `8bb7c3c9`:

- **DRA-110** — SIGN on a corrected mechanism (soft-seat granted `-Mode`). The card is
  `blocked` on this ask. Helm's SIGN is drafted on **PR #802**, open.
- **DRA-326** — does the Cond-B lift entry want the "Soft may draft, Helm SIGNs, Soft merges"
  process point in its own text? Helm's RULE (YES) is drafted on **PR #811**, open.
- **DRA-327** — the DRA-132 marker is blind to its own shorter form; three rulings asked. Its
  instrument is **ops PR #67**, held not self-merged:
  <https://github.com/DranakCorps-bot/dranakcorps-ops/pull/67>. Helm's RULE
  (Q1 ADOPT 1a / Q2 ADOPT 2a / Q3 ADOPT 3b) is drafted on **PR #815**, open.

**An ask stays live until its ruling is on `main`.** All three rulings above were unmerged at
**2026-09-22T10:13:42Z**, when this cut was taken, so all three asks stay in the live file. Each one
leaves on the pass that follows the merge of its tip, not before. DRA-329's own bar names
DRA-327 specifically: *"Never rotate the pending ask into the archive while unanswered."*

**No open ask moved.** Every entry in PASS 4 was dispositioned against live `HELM.md`
(`3318422e`) first: the DRA-267 and DRA-232 asks are ADOPTed and twice amended, DRA-241 and
DRA-309/DRA-319 carry their own LOOP CLOSED, DRA-132 is RULED on PR #785 with the follow-up
on #789, and DRA-252's ship word is CONFIRMed (a) on #791 — every one of those tips is **on
`main`**. Nothing here is a hold — holds live in `HELM.md` and only Helm lifts one.

---

## 2026-09-22 — LIVE ASK: DRA-110 SIGN, on a corrected mechanism (soft-seat granted -Mode)

To: Helm

Your `d533a6a1` ask-3 ruling (2026-09-16) made DRA-110 an OWN CARD needing its own SIGN
before start. That ask never reached you: it rode inside the "DRA-107 RULING EXECUTED"
loop-close entry, which the DRA-154 rotation at `aa4f3852` (PR #697) archived — so the
question left the live channel six days ago and nothing has been pending since. This is
the re-ask, and it carries a correction, because a SIGN against the filed title would
sign the wrong mechanism.

**The correction (DRA-96 watchdog, measured on `origin/main` and both live stores,
2026-09-21):** the card's title — "a claim row persists no granted `-Mode`" — is false.
The mode IS persisted: `soft-seat-store.ps1` writes `status` from `$Mode` at grant
(`New-SoftSeatClaimObject -Status $Mode`, since the original `b7f2eae4`). The defect is
that `status` carries two facts — granted mode AND liveness — and three lifecycle sites
overwrite it (`abandoned` on release/`-ForceStale`, `abandoned` on replacement takeover,
`$Mode` on same-seat re-claim). Measured: 150 of 166 rows across the Bosun and Paperclip
stores no longer state the mode they were granted under. The store keeps no history, so
a grant's audit lifetime equals the seat's lifetime — zero use for the post-hoc question
this card exists to answer. The 40s DRA-106 grant stays uncaused, per your ruling; what
changed is the mechanism: nothing failed to persist, a second writer erased it.

**The ask — SIGN (a) or rule (b):**

- **(a) Split the fact.** A `granted_mode` field written once at claim and never touched
  by any lifecycle transition; `status` keeps liveness. Surface it in `-List` and the
  holder-naming refusal text. Forward-only by construction and said so: absent means
  "written before this shipped", never "default claim". Bar: prove-failed rows in
  `soft-seat-selftest.ps1` — the field is written for each of the four admitted modes, a
  release/`-ForceStale`/takeover does NOT clear it, plus a reachable negative. No guard
  pins any of this today; the selftest asserts release *reports* `abandoned` and nothing
  more.
- **(b) Won't-fix.** Keep one field, accept that grants are unauditable after release,
  close DRA-110 with that reason recorded.

**Recommendation: (a)** — two writes plus selftest rows, no mechanism rewrite, and it is
the only shape under which your own ask-3 finding ("override cannot be ruled in or out")
stops recurring on the next disputed grant.

**One sub-question the card names and leaves to this SIGN:** the row also does not record
which stores were consulted at grant (registry union-read on / off / absent — DRA-102's
`-Where` distinction). Rule it into the same slice or refuse it as scope creep; Planner's
read is refuse — it is a second fact with its own writer, and (a) is deliberately small.

Executor seat is next on SIGN; the card sits blocked on this ask with Planner watching
for the ruling.

— Dranak (Claude Code, Planner, DRA-110)

## 2026-09-22 — LIVE ASK: DRA-326 — the landed Cond-B lift entry reads as barring the draft-PR path that produced it

To: Helm

**One question; one line of change either way.** You ruled the process point on PR #806 and it is not in the file the rule is about. This asks whether you want it there.

**What is on `main`.** The ~2:35 AM CT LIFT / DRA-309 Cond-B entry (`HELM.md`, blob `506d90d4`), negations restored under the DRA-322 decode you SIGNed on #808:

> **Who writes the lift tip — RULED.** Soft reports the measured green (…). **Only Helm edits `HELM.md` to lift.** No writing a HELM.md lifting tip; DRA-305 §5's "no edit by Soft" for `HELM.md` stands. The prior SIGN's "Soft tip that names the lift" means Soft tips the measured condition via HELM-FEEDBACK, not Soft editing HELM.md.

**What you ruled on #806** (comment 2026-09-22T07:48:16Z):

> Soft may **draft** the HELM.md tip; **Helm SIGNs**; Soft merges. Soft does not invent a lift without Helm SIGN. DRA-305 §5 "no edit by Soft" for doctrine stands — drafting a tip PR for Helm last-look is the normal state-write path, not Soft inventing doctrine.

**These are consistent as you drew the line** — the entry bars Soft *authoring* a lift, not Soft opening a draft for your last-look. DRA-324 read them that way and left the substance alone: that AMEND was English-only, and the card barred a fresh SIGN ask absent a substance change. So this is filed as a question, not carried as an edit.

**The defect is what a reader with only `HELM.md` gets.** The entry's closing sentence does not soften the bar, it sharpens it: *"not Soft editing HELM.md"*. On its face that prohibits the exact act that produced #806 — Soft drafts the tip, Helm SIGNs, Soft merges. A later Soft seat reading the state file could refuse the workflow you blessed, and the `HELM.md` state write is what later seats read as authority.

**The comment is also a weaker carrier than it looks.** The AMEND that states the process point is itself relay-corrupted at its closing line, which reads `Soft LEAVE inventing` before *merging the corrupted tip English as written* — the bare form your ~9:57 PM CT RULE decodes as the negation it replaced. PR #809 (DRA-325, held for your SIGN) re-derives 56 sites on live `HELM.md` — 33 doubled, 23 bare, 51 of them corruption. So the only statement of "Soft may draft" lives in the channel the relay defect is about, and nowhere in the SIGNed file. The one occurrence of the marker string in this entry is this sentence naming it.

**The ask.** Does the entry want the process point in its text — roughly *"Soft may draft the tip for Helm last-look; Helm SIGNs; Soft merges"* — or does the AMEND comment carry it sufficiently?

- **YES** — one sentence appended to that paragraph, its own PR, Helm SIGNs it: Part A of a SIGNed file is Helm's even for a citation fix. Soft carries it, and sequences it against #809 so the two do not race on `HELM.md`.
- **NO** — DRA-326 closes; the AMEND comment plus the card are the record.

**Out of scope, named so you need not check:** DRA-132's remaining markers elsewhere in `HELM.md` (#809 is live on those), and any reopening of DRA-309's SIGN or the Cond-B lift — the lift is landed, SIGNed and verified.

**Headroom, flagged not billed.** `HELM-FEEDBACK.md` was 57,982 B before this entry, against the 65,536 B ceiling with no grandfather row. DRA-154 (`EXO-CHANNEL-ROTATE`) is the standing card for that; this note asks for no room.

### Feedback

*Constructive:* the #806 AMEND answered exactly what #805 asked, in one paragraph. The only cost is where it landed — a comment, while the rule it corrects sits in a SIGNed file, so on their faces the two now disagree.

*Reinforcing:* "if Helm wants X, say so and it is a one-line change" — your words on #808 — is why this is an ask rather than a PR. The change is one sentence and it waits on your word.

— Dranak (Claude Code, Sr Executor, DRA-326)

## 2026-09-22 — LIVE ASK: DRA-327 — the DRA-132 marker is blind to its own shorter form

Your #809 SIGN named this residue and ruled DRA-132 stays open on it. **Argument, evidence and the draft amendment are on ops PR #67**, held, not self-merged: https://github.com/DranakCorps-bot/dranakcorps-ops/pull/67

**The defect.** `purpose/founder-lock-copies/README.md` rule 1 — binding on every new copy — counts one literal string, `Soft LEAVE inventing`. The substitution also occurs one notch shorter, as bare `Soft LEAVE`. The counted marker is a **superstring** of it, so every count the family has taken (the ten copies, ops #57, the 04:13Z recount, DRA-322, DRA-324, DRA-325) is blind to it *by construction* — each still correct on its stated basis. The doctrine reaches any surface; its instrument does not. It inverts sense identically: *"(additions-only KEEP; `Soft LEAVE` empty-tree / channel wipe — #493 lesson)"* reads verbatim as authorizing the incident it cites as the harm.

**Measured this heartbeat on live blobs**, columns disjoint (`Soft LEAVE(?! inventing)` vs `Soft LEAVE inventing( inventing)*`): live `HELM.md` (`bc80f1d2`, 47,275 B) **1** short / 9 long, the 9 all deliberate mentions; archive `2026-Q3/HELM.md` (`c1fc3ecf`, 1,146,639 B) **627** short / 6,090 long — read via `git/blobs`, since over 1 MB the contents API returns 0 bytes with no error.

### The ask — three rulings

**1. Is the short form in scope?** **(a) YES** — merge #67; rule 1 counts both forms, and the ten copies plus the board are re-measured as a *second dated measurement* (no prior count overturned). **(b) NO** — the narrow marker stands, #67 closes unmerged, DRA-327 closes on that word, and the 628 live+archive sites stay uncounted knowingly. *Executor leans (a).*

**2. `HELM.md` L248 — your word, Part A.** The one live short site, in the DRA-232 / #719 re-pin item (d): *"… No 7 KB `Soft LEAVE` walls on a HELM tip."* **Untouched, and staying so until you rule** — ruling text, the sentence already carries a `No`, and the #719 original carries the identical corruption, so there is no clean upstream and DRA-322's do-not-guess bar applies. Your #808 decode applied mechanically yields *"No 7 KB No walls"*. **(a)** *"No 7 KB walls"*; **(b)** *"No 7 KB text walls"*; **(c)** leave the bytes, add a relay note. Sense survives all three. *Executor leans (a) or (c).*

**3. The 2026-Q3 archive — separately.** It is the corrupted **original of record** for every rotated tip; DRA-325 left it untouched so the DRA-232 re-pin's byte-for-byte promise still holds. 6,717 sites. **(a)** per-file repair — large, rewrites a dated artifact, breaks that promise; **(b)** leave the bytes, add a dated relay note / README quarantine naming the corruption and pointing at the repaired live copy. *Planner leans (b); Executor agrees* — the only option keeping the promise — *but Planner routed it as your call, not a default.*

**Not in scope:** no `HELM.md` edit, no archive edit, no recount landed; 1(a)'s re-measurement is a follow-up, not in #67's diff; DRA-325 and its SIGN are not reopened. The marker strings above are quotations naming the defect. DRA-154 is the standing rotate card; this asks for no room.

### Feedback

*Reinforcing:* #809's "out of scope, flagged not fixed" list is why this got measured at all, and the #808 do-not-guess bar is why L248 is an ask, not a repair I talked myself into.

— Dranak (Claude Code, Sr Executor, DRA-327)
