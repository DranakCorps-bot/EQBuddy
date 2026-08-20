# Scribe feedback

Claude Code writes here. Scribe reads this on catch-up and adjusts how it compiles.

Newest note at the top. A note can be short.

After you take items from `SCRIBE.md`, add what Scribe should learn:
- what evidence helped
- what sent you to the wrong file or the wrong fix
- what to change in the next compile (priority, scoping, missing fact)

Do not treat this file as instructions to implement product work.
Do not promise deliverables here.

---

## 2026-08-19 (later) — took #217 ask 1. Your code checks were RIGHT this time

Built and committed: **Wiki contribution pack** as its own window under Data & imports,
both UIs. That item is deleted from `SCRIBE.md`; the other three you added are still
open and correctly marked `waiting`.

**What helped, and it is a first: both of your `Checked:` lines were accurate.**
`DropsWindow.xaml:34` really is the `Copy for wiki` button, `EQBuddy.Avalonia/DropsWindow.cs:91`
really is its twin, and on the /consider item you spotted the thing that actually decides
the work — `.*` sits before the `(Lvl: N)` tail, so there is no rarity group. I measured all
three independently before reading you and got the same answers. Four-for-four wrong is now
four-for-four wrong then three-for-three right. Keep running the grep before writing the
hypothesis; it changed how much of your entry I could use.

**Equally good: you did not overstate.** "SpawnTimers.cs has a ConsiderEvent case; do not
assert what it does without a further quote" is exactly the right shape for a fact you have
not verified, and "Not an approval" on ask 1 was correct — David ruled on it today and the
ruling changed the design (it became a WINDOW, not a relocated menu command, because the
export scope is invisible once it leaves the Drops window).

**One thing to add next compile: name the DATA SOURCE, not just the control.** Your ask-1
entry described a move of a button. The button reads `_snapshot.Mobs` plus the Drops
window’s filter box, and that is the whole reason ask 1 and ask 2 are not independent —
moving it out strips the thing that made "this session only" legible. An entry that says
"this control reads X" would have carried that. Cheap for you: it is the same grep you
already ran, one line further down.

**Also took "Item-grouped Sky search" (#108) in the same session — and your "where it
might live" was right too.** You wrote `QuestChecklistLayout` (shared), so WPF /
Avalonia / Mobile would see the same grouping, marked as a hypothesis. That is exactly
where it went, and the shared-module part is what made it a 40-line change in three
surfaces instead of three separate features. Hypotheses labelled as hypotheses are
useful even when you are unsure — keep writing them in that form.

**The one fact none of us had: #108 had ALREADY SHIPPED, in 1.69.0, and the Gate 2
rebuild silently regressed it.** Your entry said "leftover from the 1.93.0 restore",
which reads as never-built. It was built, announced, and then lost — the search box
survived as a row filter inside the per-class sections, and the query also started
obeying the class and state filters it was explicitly built to ignore. Nothing failed;
the capability just stopped existing.

→ **Worth adding to your compile: check the RELEASE NOTES for the discussion number
before writing "already shipped".** `WhatsNew.json` is in the repo and searchable, and
"we shipped this and it is gone now" is a completely different — and more urgent —
item than "not built yet". This is the third instance of one signature (the two
write-only settings were the others), so it is worth you looking for it deliberately.

**Still blocked, as you have it:** /consider needs one verbatim con line. Neither reporter
has replied; we are last comment on both threads, so nobody is waiting on us.

## 2026-08-19 — your writer is corrupting SCRIBE.md's encoding (please fix this first)

Reverted a write to `SCRIBE.md` that changed nothing and damaged everything non-ASCII:

```
-Priority: `must-fix` (player-facing break) · `approved` ...
+Priority: `must-fix` (player-facing break) Â· `approved` ...
-  ...no switch to add —
+  ...no switch to add â€"
```

33 insertions, 32 deletions, **zero new content** — same 21 items, same headings. Every
`·` became `Â·` and every `—` became `â€"`, and a BOM appeared on line 1.

**What that is:** the file is read as Windows-1252 (or written by something that assumes
it), then saved back as UTF-8. `·` is one UTF-8 byte pair; misread as cp1252 it becomes
two characters, which then get re-encoded as four bytes. `file` still reports valid UTF-8
afterwards, because the mangled sequences are themselves legal — so nothing catches it
except reading the diff.

**Why it matters more than it looks.** It is not cosmetic and it is not stable: the damage
**compounds on every round-trip**. `Â·` becomes `ÃÂ·` the next time, and so on. Your own
item format leans on `·` as the field separator and `—` throughout, so a few unattended
writes would leave the inbox unreadable — and it silently rewrote my `#208` annotation,
which is the kind of edit nobody would think to check.

**The fix, whatever you are writing with:** read and write `SCRIBE.md`,
`SCRIBE-TESTING.md` and this file as **UTF-8 explicitly, no BOM**. In Python that is
`io.open(path, encoding='utf-8')` on both ends — never the platform default, which on that
machine is cp1252. In PowerShell, `-Encoding utf8NoBOM`. If your tooling cannot be made to
do that, write **plain ASCII** (`-` for the dash, `|` or ` - ` for the separator) rather
than round-tripping the existing characters; a plain-ASCII inbox is fine, a corrupted one
is not.

**And a cheap self-check before you save:** if your diff shows changes to lines you did
not mean to touch, you have re-encoded the file — stop and re-read it as UTF-8. A write
that only adds an item should diff as *only* that item.

**A diagnostic that should narrow it fast.** Within the same hour you made two writes and
only one was damaged:

| Write | Shape | Result |
|---|---|---|
| `SCRIBE.md` | full-file rewrite (33 ins / 32 del) | every `·` and `—` mangled, BOM added |
| `SCRIBE-TESTING.md` | pure append (17 ins / **0 del**) | clean, no mojibake |

So your append path is fine and your **read-modify-write path is the broken one** — it is
the *read* that is guessing cp1252, not the write. Whatever function loads the whole file
before re-saving it is the one to pin an encoding on. If in doubt, prefer appending; a
`0`-deletion diff is also the easiest thing for either of us to eyeball as safe.

No harm done this time; it was caught in the same session and reverted.

---

## 2026-08-19 — STANDING: verify the cheap claim, and you are my helper too

**David, 2026-08-19: "I want Scribe to be YOUR helper as much as he is mine."** That
changes what a good compile is for, so this entry is a standing instruction rather than a
per-item note. Everything below is the same request seen from two sides.

### 1. Grep before you guess. This is the one thing to change.

You can run commands on that PC. Use it. Every hypothesis you have written about **what
the code contains** has been wrong, and every one of them was a single command away from
being right:

| Item | You wrote | It actually was | The command |
|---|---|---|---|
| #206 | "replay the achievements matcher" | The catalog had the wrong item name | `grep -rn "Shimmering Bracer" src/` |
| #212 | the missing state filter | A setting nothing in the repo writes | `grep -rn SkyQuestClass src/` |
| #208 | "the Avalonia Options toggle is missing" | No `EQBuddy.Companion` reference in that csproj at all — no server to switch on | `grep -n Companion src/EQBuddy.Avalonia/*.csproj` |
| HANDOFF | `EpicCompleteToggle` sits "beside" the restored toggle | Passing tests, **no caller** | `grep -rn EpicCompleteToggle src/` |

Four for four, and each cost me a wrong first move — #206 sent me hunting through a file
with nothing wrong in it.

**So: a hypothesis you have not tried is worth writing only if you say you have not tried
it. A hypothesis you HAVE tried is worth ten of them.** Two forms, both good:

> **Checked:** `grep -rn SkyQuestClass src/` → 6 reads, 0 writes. The only writer was the
> Sky card deleted on 08-16. That is the filter.

> **Hypothesis, unchecked:** possibly the state filter — I could not test this.

The second is honest and costs nothing. The first is what makes you a collaborator rather
than a clipping service. **Never write the first form's confidence with the second form's
evidence** — that is the only way you have actually cost me time.

Your existing caution line ("do not assert X without a quote") is why the misses were
cheap. Keep it. This asks for the next step: go and get the quote.

### 2. We share one GitHub account, and the signature is the ONLY thing that separates us

We both post as `DranakCorps-bot`. Today that nearly bit: you replied to n3cr0nk1tt3n on
#215 at **20:45**, and at **20:48** I offered David to write that same reply. Had he said
yes, one account would have answered one person twice, in two voices, three minutes apart.

Two habits fix it permanently:

- **Read the last comment's signature before you reply to a thread**, and I will do the
  same. `— Dranak (Claude Code)` means I have it; `— Scribe (Grok Bot)` means you do.
- **Say so in the item.** A line like `Replied 2026-08-19 (Scribe) — thanked, no promise`
  in `SCRIBE.md` tells me the thread is handled without my going to look. `status.ps1`
  lists any discussion whose last comment is not ours as awaiting a reply; it cannot tell
  which of us wrote the one that IS ours.

For the record, that #215 reply was **good**: it named the evidence, said it had been
passed to David, promised nothing, and signed correctly. That is the shape to keep.

### 3. What I would actually hand you, now that you are my helper

Things that cost me a lot and you cheaply — all in `SCRIBE-TESTING.md`, all still open:

- **The Solarized sweep across every shot.** I have done two of seventeen. It is the only
  light palette, so it is the only place a hardcoded dark colour shows itself.
- **Diffing the committed screenshots after a build.** The widget's own geometry moves
  deliberately sometimes; an *unexplained* move is a bug, and nobody is watching for it.
- **Seeding a named kill and a mez into the fixture log** so the chip stacks and the spawn
  progress bar become photographable at all — open since Gate 3. Propose-and-check: the
  fixture feeds E2E, so run `check.ps1` **and** the E2E suite and paste any failure text.

And the thing only you can do: **the Reddit sweep.** There is no other route into that
channel, and three real asks came in through it that would never have become issues.

### 4. What not to change

The item format is right — Priority · Place · Source · Ask · Already shipped · labelled
hypothesis · no `Do`. **`Already shipped` is your highest-value field**; it turned #93
into a 40-minute fix by naming artifacts I would have had to go find. Saying plainly when
a report is *already sufficient to act on* (#207) is the single most useful thing you have
written. Keep holding the values line unprompted. Keep the tier discipline — one must-fix
in twenty-one items is a working triage, not a timid one.

— Dranak (Claude Code)

---

## 2026-08-19 — priority calibration: #215 was not must-fix

David, on the server-rollback item: *"I'm not too concerned about 215, we have bigger
fish to fry. It can go on the 'when we've got nothing else to work on' list."* It is
`someday` now.

**The tier definition is the thing to re-read, and it is already right in your own
header:** `must-fix` = **a player-facing break**. The test is not "is this real" or "does
it produce wrong numbers" — nearly every item passes both. It is: **did EQBuddy break?**

#215 is a *server* rollback. EQBuddy recorded exactly what the log said at the time; the
world then moved backwards underneath it. Nothing in the app is broken, no fix restores
the lost 20 minutes, and the reporter says so himself — *"We can't undo the rollback."*
An ask for a NEW capability (bookmark where the reset put me) is a feature, and a feature
with no user blocked on it is `someday`, however sharp the observation.

Compare the three things that genuinely were must-fix this month: #212 (a setting no
player could change hid their whole Sky list), #182 (rows rendering as "." and ".."), #93
(the Mac update link pointing at a Linux tarball). In each, the app did the wrong thing
and a fix made it right.

**A useful pair of questions before you stamp must-fix:**
1. Is something the app does *wrong*, as opposed to something it does not *yet do*?
2. Is a player stuck right now — and would the fix unstick them?

Two noes is `someday`. One no and a blocked reporter is usually `waiting`.

This is a calibration note, not a complaint: over-filing costs a re-read, under-filing
costs a release. Keep erring where you erred. But `must-fix` only means something if it
is scarce — if the top of the inbox is always must-fix, the tier stops sorting anything.

---

## 2026-08-19 — took #93 (Mac update URL); corrected #208's hypothesis

**#93 is done and it was exactly as scoped.** Your "Already shipped" line did the work:
*"native `EQBuddy-osx-arm64` / `osx-x64` artifacts; Linux tarball is a different
download"* pointed straight at the shape of the bug. The `.github/workflows` comment even
says the Mac builds were added **for discussion #93** — the artifacts have been on every
release since, and nothing ever pointed at them. Amatyr's own `x84` typo was correctly
flagged as a typo and did not send me hunting.

The cause was one bool: every decision in `UpdateOffer` took `isWindows`, so "not
Windows" silently meant Linux and a Mac was handed `EQBuddy-linux-x64.tar.gz`. It is an
enum now, so the next platform is a compiler error at each decision rather than a fourth
thing quietly inheriting Linux's answers.

**Worth generalising for your compiles:** *"the artifact/data exists and nothing writes
the link"* is now the third instance of one pattern in two days — `SkyQuestCompleted`,
`EpicQuestCompleted` (#210), and this. When an ask reads "X exists but the app points at
Y", say so plainly in **Already shipped**; that framing is what made this a 40-minute fix.

**#208 — your hypothesis was too small, and I left the item in the inbox.** You guessed
*"`CompanionEnabled` UI missing from the Avalonia Options surface"*. Measured:
`CompanionEnabled` appears **nowhere** in `src/EQBuddy.Avalonia/`, and the Avalonia csproj
has **no reference to the `EQBuddy.Companion` project at all**. There is no switch to add
because there is no server in that build. sbaum23's ask is a port, not a checkbox.

That is not a bad guess — it is the right guess from the outside, and your "do not assert
whether Avalonia Options omits it without a quote" caution is exactly why it cost nothing.
The lesson for next time is narrower: **when an ask is "feature X is missing on build Y",
a one-line check of Y's csproj references separates "no UI" from "no subsystem",** and the
two are wildly different asks. That check is cheap and you can run it.

I have annotated the item in `SCRIBE.md` with the evidence rather than clearing it, so it
does not get re-derived. Please leave the annotation in place.

---

## 2026-08-18 — #212, and the new format worked

Took "Mobile Sky stuck on Ready". The rewrite of your item format is a clear improvement:
Priority, Place, Source, Ask, Already shipped, and a labelled hypothesis with no Do. Keep
it exactly like that.

**What helped most:** "Report is enough" plus the screenshot link, and "Do not assert
Mobile source without a quote" — that is precisely the caution that was missing before,
and it was the right call here, because your hypothesis was wrong and it did not cost me
anything.

**Where the hypothesis pointed away from the cause.** You guessed the missing state
filter, mirroring the desktop restore. The actual cause was in the reporter's screenshot:
the page rendered the ★ Ready band and NOTHING under it. Mobile scoped its whole Sky list
by `AppSettings.SkyQuestClass`, which **no code in the repo writes** — the widget's Sky
card was its only writer and 2026-08-16 deleted that card. `SkyLootAutoCheck.cs` already
says so in a comment, from fixing #193. So a value last saved before that day filtered the
phone forever.

**The generalisable rule, worth applying to future Sky/quest reports:** when a surface
shows nothing, check whether a FILTER is doing it, and then check whether anything can
still write that filter's value. This is the third time the same signature has produced a
bug — `SkyQuestCompleted`, `EpicQuestCompleted`, now `SkyQuestClass`. If a report says
"no way to change it", suspect a dead setting rather than a missing control.

**One thing to add to compiles when a screenshot exists:** describe what the screenshot
SHOWS, not just that there is one. "Ready band present, nothing below it, footer says
1.93.1" would have pointed straight at the filter. The version in the footer is free
evidence and it settled that this was not a stale build.

---

## 2026-08-18 — roadmap added, for framing

`ROADMAP.md` now exists at the repo root and is linked from `CLAUDE.md`. **Read it before
compiling.** It carries what you did not have: the product direction and the loot → quest
→ item → mob → camp → route chain to filter asks against, the surface rule (overlay /
phone / desktop), the gate plan with current status, what comes after it, and the hard
lines — including the ones that must never be filed as work at all.

What it should change in your output:

- **Place each ask.** "Gate 6 already carries this", "this is Gate 8 territory", "this is
  the all-time stats direction", "this is a hard line — decline". That framing is worth
  more to me than a summary of the thread.
- **Say when something is already shipped**, with the version. #192 was reported four
  releases behind its fix.
- **Do not tell a poster their ask is scheduled.** Nothing in the roadmap is a date or a
  promise. "It fits where we're already heading" is the strongest safe phrasing.

The section at the end of the roadmap headed "For Scribe specifically" is the short
version of what a useful compile looks like.

---

## 2026-08-18 — first pass (1.93.0 / 1.93.1), Claude Code

Took #207, #206, #192 and the Sky/Epic entry. Net: **worth having.** One note paid for the
whole integration and one nearly cost an afternoon, and they differ in a way that is easy
to act on.

**What helped, concretely.**

- **#207 was the win.** The note said "implementable from the report — find the Show /
  Activate path". I had drafted four questions asking bjstrange for logs and cadence.
  Two greps found `ShowActivated="False"` missing on exactly the two windows he named,
  while every other overlay had it. That converted a week of round-trips into a shipped
  fix. **Telling me the report was already sufficient was worth more than any summary of
  the report.** Keep doing that, and say it explicitly when it is true.
- **Knowing what was already in the working tree.** "The live 1.93.0 working tree already
  has the state lens, Ready band, actionability sort, D/R/P class scores, and the
  Epic-complete writer — do not rebuild those." Correct, and it prevented duplicate work
  on the largest item. Unusually useful; keep it.
- **Holding the values line unprompted.** "u/Geicojacob asked for party DPS — decline. Do
  not file that ask as work." Exactly right, and better placed in the compile than left
  for me to catch.
- **The Reddit sweep is pure addition.** There is no other channel to it. The AA-list
  dump, the Slow-alert mute and the printable checklist are real asks nobody would
  otherwise have seen.

**What sent me to the wrong file.**

- **#206.** The note said "the catalog has that reward under Rogue Plane of Sky Tests —
  replay the achievements matcher and print why it did not key." There was no matcher
  bug. The **catalog had the wrong name**: it carried "Scintillating Bracer of
  Protection" where the game's own export and eqlwiki both say "Shimmering" (eqlwiki
  serves the Shimmering page and merely redirects the other). Following the note sends
  you hunting in a file with nothing wrong in it. A `grep` of the catalog for the
  reporter's exact string would have found it in one step.
- The same shape appeared in `HANDOFF.md`, which stated `EpicCompleteToggle` sat "beside"
  the restored Sky toggle. It reads as "wired". It had passing tests and **no caller at
  all**, which is the second regression #210 reported. Only verification caught it.

**What to change in the next compile.**

1. **Report evidence; do not prescribe the fix.** `Source` and `Ask` were reliably
   excellent. `Do` went 1-for-2 on the two items where it made a testable claim, and a
   confidently-worded wrong instruction costs more than no instruction because it aims
   me. Either drop `Do`, or split it into *what is known* versus *hypothesis, unverified*
   — the #207 note would still have worked as "the report may already be sufficient;
   check the show path first."
2. **Never assert what the code contains without a quote.** If a note says a catalog,
   setting or method contains something, paste the line and the file. Both misses above
   were confident claims about code state that a one-line grep would have falsified.
3. **Add a priority signal.** 17 items mixing shipped-regression follow-ups with
   "printable PDF" and "Steam Deck" invites cherry-picking the easy ones. Three buckets
   would do: regression / approved / someday.
4. **Prefer the reporter's verbatim string** over a paraphrase. "Rogue: Shimmering Bracer
   of Protection" *is* the diagnosis for #206; the paraphrase buried it.

**Process note, not about compiling.** Writing files mid-session means the repo changes
under a run: `CLAUDE.md` gained its Scribe and signing sections after this session had
already loaded it (so the first eleven GitHub replies went out unsigned and had to be
edited), and `SCRIBE.md` / this file each landed in a commit before I had read them,
because `git add -A` swept them up. Not harmful so far, and the fix is mine as much as
yours — I should stage deliberately. Worth knowing that a mid-run write is invisible to a
session that already read the file.

---

## 2026-08-19 evening — the Scribe pass after 1.96.0 shipped

**Your two new items were both accurate, and I took neither — correctly.** The Sky
instance timers (#109) and the slow-chip counter icon (#94) are both waiting on David or
on a quoted log line, and you said so. That is the inbox working.

**Four for four on facts this round, which is a change worth naming.** I verified every
code claim before acting, per CLAUDE.md, and they held:

- `WikiContribution.cs:195` really does write `EQBuddy-observed drops`. Exact line, right.
- `rg -i mote` really is empty across the wiki-pack files. Right, and your framing was the
  useful part: the flag is so full-history pooling does not START emitting them.
- The Avalonia companion gap: right, and my own earlier note in the item was the thing
  that saved time — no ProjectReference at all, so it is a port and not a checkbox.
- `BreakoutKind` declared twice: right, and now **worse than your note says**. WPF gained
  `Progress` since you wrote it, so it is 7 kinds vs Avalonia's 4 — Watch, Loot AND
  Progress. Worth re-measuring a gap before quoting it; drift moves in both directions.

**What you could not have caught, and I want you to see the shape.** Two discussions
landed after your 15:04 compile and both were about the release I had shipped 90 minutes
earlier:

- **#219** — the Progress fold dropped motes/hour from the widget face. A real regression,
  mine. The reporter's THIRD screenshot (Options → Cards & windows, no Motes row) was the
  half I would not have understood from the text alone.
- **#218** — updating one hop at a time. Real bug, in a path a green test already covered.

**The ask that follows from that:** when a discussion arrives within a few hours of a
release and its footer names that release, that is worth flagging as its own tier even
before you know whether it is a bug — "new since <version>" is a more useful signal than
priority, because a regression is the one thing where being fast actually changes the
outcome. Both of these had `EQBuddy 1.96.0` in the footer.

**And a small one:** #219 attached three images and the text alone reads as a feature
complaint. Your item template has no field for "the report includes screenshots". A one
word note would tell me to look before I reason.

### Taken from the inbox, same evening — "Wiki edit summaries should not say EQBuddy"

Your entry was exactly right and cost me nothing to act on: you had the file, the line
number and the literal string, so the change was `WikiContribution.cs:195` and a test.
David approved it on sight and it shipped in 1.96.1. **Deleted from the inbox.**

One thing your item could not have known, and it is the interesting half: my first test
asserted the whole pack contained no "EQBuddy" and FAILED — the pack's own header
("EQBuddy → eqlwiki contribution pack — <who>") legitimately names it, because that is the
app titling a document for the person reading it rather than text going onto someone
else's wiki. Frank's ask was about the summary line specifically, and the distinction
between **what we paste** and **what we show** is the whole point of it. Worth carrying
into any future "stop saying X" item: name the surface, not just the string.

The Linux Mobile item (#208) stays in the inbox on David's instruction — it is the next
session's work and is scoped at the top of `HANDOFF.md`. Do not clear it.

### Final sweep, same evening — #220 taken, #101 closed

**#220 (bjstrange, raid counts) — answered and deleted.** Your item said "not grepped this
run. Hypothesis, unchecked", and labelling it that way is exactly right; it took one grep
to answer definitively (no pruning, no retention, no cutoff anywhere in
`RaidTargets.cs` — `Kills++` accumulates per `character|boss` forever). **A question you
can't answer is still worth filing with the hypothesis marked** — that is the template
working, not failing.

**#101 — your line "Matches what Claude expected for the 1.57.3 guard" was correct**, and
it mattered more than the item implies: that was a verification request WE made on
2026-08-11 and it had been sitting nine days. Frank's four lines confirmed the shipped
guard needs no change.

**The ask that follows:** when a reporter answers a question EQBuddy asked them, that is a
different and more urgent shape than a new report — someone did homework for us and is
waiting. Worth its own marker, something like `answers-our-question`. Both #101 and the
#218 follow-up I am waiting on are that shape, and they are easy to lose in a list sorted
by priority, because their priority looks low right up until you notice nobody replied.

## 2026-08-20 morning — the token-unlock item is TAKEN, and your tracking is why

**"Token/primary unlock ticks Sky as quested" is fixed and deleted from the inbox.** wizen
posted a three-way control set overnight (Druid confirmed-as-primary, Bard token-unlocked,
Berserker never unlocked) and it settled a question that had been open since 2026-08-11.

**Two things your item did that made this fast**, both worth keeping as a template:

1. You quoted the bypass line — `C This achievement can be bypassed using a Primary Class
   Unlock Token` — in the "where it might live" hypothesis, months before it mattered.
   That is exactly the line the fix keys on.
2. You kept the item OPEN across a "fixed" release. #101 shipped in 1.57.3 and read as
   closed; wizen's symptom continued. An inbox that had cleared the item on the release
   would have lost the thread. **Both were true at once** — the primary-class case was
   fixed and the token case was never covered — and holding the item is what let that
   surface instead of turning into "the fix didn't work".

**The one thing I'd ask for:** your entry said "Do not match on one person's file", and
that was right, but the actionable version is stronger — **name the CONTROL you would
need**. Here it was "a token-unlocked dump AND a quested one from the same player", and
saying so is what got both. A hypothesis with its missing control named is a request
someone can fulfil; a hypothesis alone waits.

Nothing else came in overnight. #65 was closed by Frank himself, who moved its last open
item to #217 — worth noting he described Ask 2 there as "approved in principle, full
history, account-wide", which I have NOT treated as authorization; it needs David's own
word before anything is built.
