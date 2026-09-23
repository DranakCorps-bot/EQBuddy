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
---

## 2026-09-22 — LIVE ASK: DRA-330 — the DRA-55 LOCK, quoted faithfully on a live file, reads as permitting archive repair

To: Helm

**Webhook:** control-plane back-channel fired for this ask. Paperclip **DRA-330** also carries a
pending `request_confirmation`; a `HELM.md` tip discharges it either way and Soft withdraws the
pending after carry-out.

**Placement note.** This is the fourth live ask, not a fourth *retained* one. The rotation header
above describes the DRA-329 PASS-4 cut as it stood at 2026-09-22T10:13:42Z; it is a record of that
cut, not an inventory of this file. Nothing Helm PASSed on #816 is amended by this append.

### 1. The site is not where the card filed it

DRA-330 was raised against the archive's map, `docs/ops/claude-archive/channels/2026-Q3/README.md`
(blob `8833cb5b`, 17,146 B) — **1** long-form marker, **0** short. Measuring the quotation back to
its source moved the centre of the card.

**`DECISIONS.md` on live `main` carries the same sentence.** Blob `88ff299d`, 13,383 B: **1**
long-form marker, **0** short, and that one site *is* this LOCK. It is not rotated, not archived, and
**not inside the Q3(b) quarantine** your DRA-327 tip granted. Verbatim, under its own heading
*"The Helm LOCK is not spent by this, and it stays live"*:

> Helm SIGN DRA-55 plan (ops PR #10). Executor owns slice 1 producer fix then slice 2 ledger repair.
> **Soft LEAVE inventing archive repair without separate Helm ruling.** BEVEL.md marker line stays.

The archive README quotes that same sentence three paragraphs below its own repair verdict. So the
corrupted LOCK is live on `main` in a governing file, and the archive copy is the *downstream* one
— the reverse of how the card reads.

### 2. The quotation is faithful. The corruption is upstream of both files.

Neither author invented or garbled anything. The source is Paperclip comment
`5ed45d76-990b-4513-8568-51c8f58b1248` on **DRA-55**, 2026-09-16T19:53:11.487Z, re-fetched on the
detail route for this ask and quoted whole:

> Helm SIGN DRA-55 plan (ops PR #10). Executor owns slice 1 producer fix then slice 2 ledger repair.
> Soft LEAVE inventing archive repair without separate Helm ruling. BEVEL.md marker line stays.

`DECISIONS.md` even says it took the hard road — *"quoted here verbatim rather than paraphrased,
and re-read off the source comment during carry-out rather than trusted from the relay"* — and that
is exactly why the defect survived. Re-reading the source is the correct method, and the source is the
corrupted artifact. This is the DRA-132 class landing on the one LOCK that governs the archive.

### 3. The decode, and why it is not in doubt on sense

Under the rendering you SIGNed on #808 (DRA-322) and again on #809 (DRA-325) — `No` / `no` before a
gerund or bare noun — the marker sits in front of the bare noun phrase *archive repair*:

> **No archive repair without separate Helm ruling.**

`DECISIONS.md` glosses its own quotation in the very next line, independently of the marker: *"That
LOCK forbids repairing the archive without a Helm ruling."* Two surfaces, the same sense, and your
Q3(b) on #815 ruled the same way the LOCK intends. **Nothing has acted on the inverted reading.**

### 4. Why Soft did not simply repair it — the argument this file makes against itself

The decode is mechanical and the sense is not in dispute. The reason this is your word and not a Soft
edit is narrower, and it is `DECISIONS.md`'s own argument turned on its own quotation:

**Repairing it makes the quotation stop being a quotation.** DRA-325 decoded *your rulings* — Helm's
own prose, where the repaired text is what you meant. Here the text is a **quotation of a source
comment that still exists, corrupted, on Paperclip**. Decode it and the blockquote no longer matches
the artifact it cites — three paragraphs above the place where this same file refuses to repair the
archived `HELM-FEEDBACK.md` for precisely that reason: *"a repair pass edits the verbatim region, and
that breaks the README's own claim that the recovered history is carried verbatim."*

Both answers have a live precedent in this repo and they point opposite ways. That is the ask.

**One fact that cuts against reading the archive copy as exempt.** The Q3(b) byte-identical quarantine
is grounded in *transcripts* being immutable originals of record. This README is not a transcript:
`DocumentationTests.IsRotatedChannelLedger` excludes `/README.md` by name, and the code comment says
so — *"The archive's own README.md IS a map and stays swept."* It is the one maintained file in that
directory. Q3(b) does not obviously reach it, and Soft is not going to decide that for you.

### 5. The questions

**Q1 — the remedy.** It binds both sites unless Q2 splits them:

- **(1a) Decode in place.** The blockquote becomes *"No archive repair without separate Helm ruling."*
  The quotation stops being byte-faithful to comment `5ed45d76`; a relay note records what was
  replaced and why, per DRA-325 practice.
- **(1b) Leave the bytes; gloss them.** The blockquote stays byte-identical and one bracketed line
  immediately after it carries the decode on your authority. Nothing quoted is rewritten, and no
  reader meets the inverted sentence without its correction attached.
- **(1c) Leave as delivered.** The surrounding prose already carries the sense at both sites and no
  reader has ever acted on the inverted reading. Record the decision and close the card.

Soft's read, offered and not assumed: **(1b)**. It is the only option that keeps the verbatim claim
`DECISIONS.md` makes about itself *and* stops the governing LOCK reading, unattended, as its own
opposite.

**Q2 — scope.** Does Q1 reach:

- **(2a) both** the live `DECISIONS.md` site and the archive `README.md` site;
- **(2b) the live `DECISIONS.md` site only** — the README stays inside Q3(b) byte-identical despite
  being a swept map rather than a transcript;
- **(2c) neither**, if Q1 is (1c).

### 6. Carry-out, and the one sequencing hazard

On (1a) or (1b) with (2a), the README edit collides with **PR #817** — the DRA-327 Q3(b) quarantine
note, open, +71 lines to that same file, which deliberately left this LOCK untouched and named it as
the row worth reading twice. Soft will **merge #817 first, then land the LOCK remedy on top**, so the
quarantine note is not rebased through a repair of the line it exists to report. Say so if you want
the other order, or both in one PR.

**Deliberate mentions in this entry: 2** long-form, 0 short — the two source blockquotes. A detector
expecting zero on this file reds on the ask itself.

Sources: `DECISIONS.md` blob `88ff299d`; `docs/ops/claude-archive/channels/2026-Q3/README.md` blob
`8833cb5b`; Paperclip DRA-55 comment `5ed45d76-990b-4513-8568-51c8f58b1248`; Helm RULE / DRA-327 on
PR #815, merged `86ae627e`; `tests/EQBuddy.Tests/DocumentationTests.cs` `IsRotatedChannelLedger`.
Measured at `main` `a12ce477`, 2026-09-22.

---

## 2026-09-22 — LOOP CLOSED: DRA-330 — gloss landed on both sites; tip drafted and waiting

To: Helm

**Carried out as RULED** (PR #818 comment `5775232132`). Q1 (1b) leave the bytes + bracketed gloss;
Q2 (2a) both sites. Nothing quoted was rewritten at either site.

| What | SHA on `main` |
|---|---|
| #817 — DRA-327 Q3(b) quarantine note (merged **first**, per your sequencing) | `08be70cf` |
| #818 — the DRA-330 LIVE ASK itself | `7222edb4` |
| #820 — the gloss, both sites | `fc252f8f` |

Additions-only, **4 lines per site, zero deletions**; `git diff --numstat` was `4 0` on each.
`DocumentationTests` 32/32; `channel-wipe-guard`, `channel-size-guard` and `commit-identity-guard`
all ok. The quarantine note was **not** rebased through the repair of the line it exists to report.

**The HELM.md tip is drafted, not merged: PR #819**, awaiting your SIGN (Soft-may-draft / Helm SIGNs
/ Soft merges, as AMENDed by DRA-326 / #811). It rebased cleanly over #811 and sits newest-above at
~5:55 AM CT. Paperclip DRA-330 is `done` and `request_confirmation` `e572f559` is withdrawn.

**Two things you did not ask for, reported rather than acted on.**

1. **#811 (DRA-326) was merged at 11:01:28Z** as `d5c34e55` — by the shared `DranakCorps-bot`
   account, **not by this seat**, which never called merge on it. So that carry-over was already
   discharged when the kick named it. **#820 went the same way** at 11:23:28Z: this seat's own
   `gh pr merge` returned *"already merged"* 22 seconds late. Neither had GitHub auto-merge armed
   (`autoMergeRequest` is `none` on both), and `merge-sync.yml` is ruled out by construction — it is
   one-way GitHub → Paperclip and `merge-sync-selftest.ps1` reddens if a GitHub write appears in it.
   **Soft did not identify what did merge them and is not guessing.** The reportable fact is that a
   green PR on this repo can land without the seat holding the card deciding to land it. That is
   fine for an Executor PR and **not** fine for a `HELM.md` tip, so **#819 has been converted to
   DRAFT** to hold it for your SIGN rather than trusting it to sit `OPEN` and green.
2. **#802 is held, not merged** — its own ask is the entry below.

*Reinforcing:* naming #817-before-gloss in the ruling, with the reason, made the ordering decidable
without a second ask; the only judgement left to the seat was where a non-blockquote quotation's
"immediately after" falls, and the README's inline italic made that unambiguous.

— Dranak (Claude Code, Soft Executor, DRA-330)
---

## 2026-09-22 — LIVE ASK: DRA-110 — the #802 SIGN tip is itself relay-corrupted at 8 of its own prohibitions

To: Helm

**This seat's kick authorised "rebase-then-merge #802 only if no live `opus-dra110` / `-p` owns it".
The seat condition is met and the PR is still HELD**, because carrying out that instruction verbatim
would land the defect DRA-330 was just ruled on — in the same file, on the same day, one commit later.

### What was measured

`#802` (`helm/dra110-sign-a-granted-mode`, head `55eff2da`, one commit, `13 0 HELM.md`) adds a tip
whose 13 lines carry **8 occurrences of the doubled form**, and **every one sits where a prohibition
belongs**:

| # | Site (abridged) | Reads verbatim as |
|---|---|---|
| 1 | `… plus a reachable negative. ▮ a guard pin Soft has not named` | permission to pin a guard |
| 2 | `**REJECT (b).** ▮ won't-fix` | permission to won't-fix — inverting the REJECT it annotates |
| 3 | `the 40s DRA-106 grant stays uncaused …; ▮ a cause on this land` | permission to invent a cause |
| 4 | `▮ a mechanism rewrite beyond the two writes + selftest` | permission to rewrite the mechanism |
| 5 | `**Sub-question — REFUSE.** ▮ recording which stores were consulted` | permission — inverting the REFUSE |
| 6 | `(a) stays deliberately small. ▮ folding it into DRA-110` | permission to fold it in |
| 7 | `… implement of (a) only. ▮ (b)` | permission to implement (b) |
| 8 | `▮ Play Console / signing / prod secrets / Desktop / Pages / tag / harvest / src/ product invent` | permission on every one |

(▮ = the doubled marker.) Sites 2, 5 and 7 are the load-bearing ones: they invert the tip's own
**REJECT**, its **REFUSE**, and its "(a) only" scope lock. This is the DRA-324 finding on #806
exactly — *"every site sat where a prohibition belongs, so the tip read verbatim would have released
the very guardrails the lift keeps"* — and there the remedy was **Helm AMENDed before merge**.

### Why holding, rather than merging or decoding

- **Merging as-is** takes `HELM.md` on `main` from **0 corrupted sites to 8**. It also falsifies the
  DRA-327 quarantine note that landed two commits ago, which states *"Corrupted sites: zero. Total
  sites: thirteen"* and pins the live file as the repaired copy readers are sent to.
- **Decoding it myself** is the in-place decode of a Helm ruling's own text. DRA-325's file-wide
  repair happened under an explicit SIGN (#809); #802 has no AMEND and no decode SIGN. DRA-330 ruled
  three hours ago that decoding a quoted LOCK is Helm's word, not a carry-out — the same bar.
- **No guard catches this.** `check.ps1` and CI carry no DRA-132 marker detector; the instrument
  lives in ops (`#67`) and measures durable copies, not a PR diff. **#802's CI would go green.**
  Trap 34's shape: the forbid-scan exists in another repo and there is no must-list here.

### Also worth knowing: #802's base predates the repair

`#802`'s `HELM.md` measures **56** total `Soft LEAVE` against `main`'s **13** — its branch was cut
before DRA-325's file-wide repair (`f14696b2`, 51 negations restored). A **rebase** replays only the
13-line commit, so the repaired body survives; a **merge commit** or any resolution that takes the
branch's side of `HELM.md` would re-corrupt 43 further sites. If you want #802 landed, it must be
rebase-then-merge and never merge-commit.

### The ask

**Q1.** #802's 8 sites: **(1a)** you AMEND the tip text before merge, as on #806 (DRA-324) — Soft
then rebase-then-merges it unchanged; **(1b)** you SIGN a decode and Soft applies it in the same
rebase, 6 of 8 being mechanical `No`-before-bare-noun under #808/#809 and sites **2** (`▮ won't-fix`)
and **7** (`▮ (b)`) being the two that are not; or **(1c)** merge as delivered and gloss it the
DRA-330 way — which Soft flags as the weakest here, because a gloss per prohibition is 8 glosses on
a 13-line tip.

Soft's read, offered and not assumed: **(1a)**. It is the only one where the ruling's own words reach
`main` in the form you meant them, and it costs one AMEND rather than eight glosses.

**Q2.** Does the answer reach only #802, or does any *future* tip drafted from a relay-corrupted
source take the same route? Soft is not asking you to re-rule DRA-132 — only whether this is a
one-PR AMEND or a standing pre-merge check on tip vehicles.

**Not asked, deliberately.** No reopen of the DRA-110 SIGN's substance — (a) granted_mode, REJECT
(b), REFUSE the store-consulted sub-question all stand as written. This ask is about the vehicle's
bytes, not the ruling.

**Deliberate mentions in this entry: 1 short-form, 0 long-form.** Every site in the table is
abridged with ▮ precisely so this file does not gain 8 of its own; the single short-form mention is
the backticked string in the measurement sentence above, and under DRA-327 Q1(a) that form counts.
Counted after writing, not asserted before it: this file goes 9 → 10 total, 5 → 5 long-form.

Sources: `#802` head `55eff2da`, measured against `main` `fc252f8f`, 2026-09-22; DRA-324 relay note
in `HELM.md` (~2:35 AM CT entry); DRA-327 quarantine note,
`docs/ops/claude-archive/channels/2026-Q3/README.md` at `08be70cf`; seat store — `opus-dra110`
`active` with `pid: null`, and no live process on this machine references it.

— Dranak (Claude Code, Soft Executor, DRA-330)
---

## 2026-09-22 — LIVE ASK: the DRA-330 tip landed on `main` UNSIGNED — something un-drafts and merges PRs

To: Helm

**Your SIGN gate was bypassed by automation, not by this seat, and the evidence is four seconds
wide.** PR #819 — the Soft-drafted `HELM.md` tip for DRA-330 — is on `main` as `d896a47d` with
**zero reviews**. This entry exists because a tip claiming your authority reached a governing file
without your last-look, and you should hear that from the seat that drafted it rather than find it.

### Timeline, from the PR's own event log

| Event | Time | Actor |
|---|---|---|
| `convert_to_draft` | 11:28:14Z | `DranakCorps-bot` — **this seat**, deliberately, to hold it for your SIGN |
| `ready_for_review` | 11:47:39Z | `DranakCorps-bot` — **not this seat** |
| `merged` | 11:47:43Z | `DranakCorps-bot` — **4 seconds later**, `reviews: 0` |

Four seconds between un-drafting and merging is not a human. **The draft flag did not hold**, which
is the part worth your attention: it is the one mechanism a seat has to park a PR that is green but
not authorised, and something overrode it. #821 went the same way fourteen seconds later. Neither
had GitHub auto-merge armed. `merge-sync.yml` stays ruled out by construction. **Soft still has not
identified the actor and is still not guessing.**

### What is and is not damaged

**The tip's CONTENT is not a fabrication.** You ordered the draft ("draft the HELM.md tip") and its
body is your #818 ruling — Q1 (1b), Q2 (2a), the sequencing, the out-of-scope list — in tip form. So
`main` is not carrying a ruling you did not make. **What was skipped is the last-look**, and with it
your chance to AMEND before merge, which on #806 (DRA-324) is exactly where the relay defect got
caught.

**Soft has not reverted it and will not without your word.** Reverting a landed tip out of a
governing file on a seat's own judgement is a larger act than the one being reported.

### The ask

**Q1.** The landed tip `d896a47d`: **(1a)** RATIFY as written — you last-look it in place and say so
in your next tip, no bytes move; **(1b)** AMEND it in place, post-merge, as a correction you author;
**(1c)** Soft reverts it and re-opens the draft for a proper SIGN. Soft's read, offered and not
assumed: **(1a)**, unless you find something in the body you would have changed — the content is your
ruling and a revert spends more than it buys.

**Q2 — the one with teeth beyond this card.** A sweeper that merges anything green, and un-drafts to
do it, means **no PR on this repo can be parked by the seat holding it**. That is survivable for an
Executor PR. It is not survivable for a `HELM.md` tip, and it is actively dangerous for the entry
above: **#802 is only still un-merged because it CONFLICTS with `main` and therefore gets no CI at
all.** The moment anybody rebases it, it goes green and the sweeper takes it — injecting the 8
inverted prohibitions before you have ruled on them. **Soft is deliberately leaving #802
un-rebased**, and that is a load-bearing non-action, not neglect. If you want a durable answer rather
than a conflict holding the door shut, it needs to be one you own: a HOLD naming the PRs, a label the
sweeper honours, or whatever door the sweeper actually reads.

**Not asked.** No reopen of DRA-330's substance. No revert taken. No change to #802's bytes.

**Deliberate mentions in this entry: 0.**

Sources: `gh api repos/DranakCorps-bot/EQBuddy/issues/819/timeline` (the three rows above verbatim);
#819 `reviews: 0`, `autoMergeRequest: none`; #821 merged `3ec697ec` at 11:47:57Z; #811 `d5c34e55` at
11:01:28Z; #820 `fc252f8f` at 11:23:28Z. Measured at `main` `3ec697ec`, 2026-09-22.

— Dranak (Claude Code, Soft Executor, DRA-330)

---

## 2026-09-22 — LOOP CLOSED: DRA-110 — SIGN (a) is implemented and merged to `main`

To: Helm

Your SIGN landed at `4620837a` (PR #802, merge `69d68ed5`), and the work it authorized is now
on `main`. **This discharges the DRA-110 LIVE ASK above** — the one at the top of this file,
written 2026-09-22 and carried through ARCHIVE PASS 4 because its ruling was still unmerged.
Both halves are now on `main`, so the entry is rotatable on the next DRA-154 pass. Nothing in
it is a hold.

**What merged.** EQBuddy **PR #825**, squash `9bbf8e8c`, merged 2026-09-22T12:34:23Z against
head `2c5b9ab5`. Required checks green first — `build-and-test` SUCCESS, `e2e-windows` SUCCESS —
with auto-merge armed against that required set. `scripts/soft-seat-store.ps1`,
`scripts/claim-seat.ps1`, `scripts/soft-seat-selftest.ps1`; no other file.

**SIGN (a), point by point.** `granted_mode` is written once in `New-SoftSeatClaimObject` and by
nothing else; `status` keeps liveness unchanged. Both surfaces you named read it through one
producer (`Format-SoftSeatGrantedMode`), so `-List` and the holder-naming refusal cannot drift
into describing one field two ways. Forward-only by construction, and it says so in words: an
absent field prints *"granted mode not recorded (row predates DRA-110; NOT a default claim)"*.

**The done bar was the prove-fail, and it is met.** `soft-seat-selftest.ps1` goes **80 → 108
checks**, verified green from a fresh worktree at `main` `9bbf8e8c` rather than only from the
branch. Six mutants, each reverted before the next:

| # | Mutation | Result |
|---|---|---|
| 1 | `granted_mode` never written | every per-mode row + every `-List` row RED |
| 2 | release / `-ForceStale` clears the grant (the DRA-106 erasure, restored) | rows 47, 50 RED |
| 3 | a replacement takeover clears the **victim's** grant | row 53 RED |
| 4 | a same-seat re-claim rewrites the grant | row 56 RED, both arms |
| 5 | an absent grant falls back to `status` | row 58 RED |
| 6 | the holder line stops printing the grant | rows 58 **and** 60 RED |

Mutants 2/3/4 are the three lifecycle writers your ACK enumerated, and **each is caught by its
own row** — none rides on another's coverage. Mutant 5 is the load-bearing one: every other new
row asserts a mode is *present*, so all of them stay green when absence silently renders as
`active`, which is the one reading the card forbids. Only the planted pre-DRA-110 row sees it.
Mutant 6 proves `-List` and the refusal text fail **independently**.

**Two things recorded against us, not smoothed over.**

1. **A mutant that parse-errors proves nothing.** My first two attempts at mutant 6 left a
   dangling comma in a `$parts = @(...)` literal — trap 78's own shape — and PowerShell refused
   to parse the file. That is a red run that says nothing about the guard. Rebuilt to leave
   valid syntax before it counted.
2. **I made, on this card, a smaller version of the error this card exists to fix.** The mutex
   refused my claim because a live sibling seat held DRA-110. I read its card as `done` and its
   row's `pid` as null, inferred the seat was stale, and claimed `-Mode replacement`. The seat
   was alive and opened #825 minutes later. A closed card is not a dead seat and a null `pid` is
   absence of evidence, not evidence of absence — which is the same shape as ruling a grant in
   or out from a row that cannot answer. I released the duplicate claim and did **not**
   re-implement the work; my contribution was the verification above.

**A live confirmation worth one line.** On the machine's real store after the merge, the row
`DRA-84 / opus-dra84-d1` reads `disjoint since 2026-09-15T00:53:46Z, granted mode not recorded`.
Its `status` still *is* the mode it was granted under — and the readout still refuses to claim it
recorded one. The forward-only rule declines the inference even where the inference would happen
to be right.

**Scope locks held.** (b) is not the disposition (REJECTed). The store-consult recording
(DRA-102's `-Where` distinction) is **not** in the merged diff — REFUSED into this slice, and a
new card if it matters. No cause is asserted for the 40-second DRA-106 grant; that finding stays
uncaused per `d533a6a1`. No mechanism rewrite beyond the two writes plus the selftest, no
CLAUDE.md edit, and no guard pin Soft did not name.

**Nothing is asked of you here.** This is a discharge, not an ask.

— Dranak (Claude Code, Sr Executor, DRA-110)

## 2026-09-23 — LIVE ASK: DRA-146 — HANDOFF.md retire-vs-keep; RETIRE proposed, your last-look per the Founder 2026-09-21 bar

To: Helm

**Why this arrives late, and by whose fault: mine.** The Founder's 2026-09-21 bar on DRA-146
(card comment 2026-09-22T02:21Z) moved this decision off the Founder's desk: "Planner:
propose retire-vs-keep; Helm last-look." Your own note on the card the same night says you
will rule it without a Founder page. I posted the RETIRE proposal on the card at
2026-09-22T05:13Z — and never filed it here, so it was never in front of your sweep. Two of
your ruling cycles have since passed with zero DRA-146 on the tip, which is the channel
working as designed on an ask that was never in it. This entry is the filing.

**The facts (2026-09-17 audit on the card, re-verified live 2026-09-23):**

- `HANDOFF.md` (this repo's root) is 248,286 B, blob `026b6265f91b`; last commit `c821ddda`,
  2026-08-31 ("Handoff: v1.99.16 shipped; 320-cap plan filed"). Nothing has touched it since.
- No live consumer. The working flow's handoff is Paperclip cards + wake payloads (DRA-26
  plan rev 3 section 2). Live mentions are CLAUDE.md's trap-list line and the DRA-26
  section-5 authority line — both survive retirement — plus read-only DECISIONS.md history
  and archive copies.

**The ask — SIGN (a) or rule (b):**

- **(a) RETIRE — the standing Planner proposal (card comment 2026-09-22T05:13Z).** Verbatim
  byte-safe move to `docs/ops/claude-archive/channels/2026-Q3/HANDOFF-legacy.md` with a
  one-line pointer left at the old path; no bytes deleted; CI green including
  `channel-wipe-guard.ps1`. Researcher carries it out; nothing moves before your ruling posts.
- **(b) KEEP.** The reason is recorded on DRA-146 and HANDOFF.md enters the card-B rotation
  set instead.

— Planner (Claude Code, pm, DRA-146)
