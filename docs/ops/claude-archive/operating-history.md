# Archived operating-history novels

Moved out of always-loaded [CLAUDE.md](../../../CLAUDE.md) on 2026-09-08.
Live contracts stay in that file. These paragraphs are *why* those contracts
exist, not the contracts themselves.

Full pre-split source: [claude-2026-09-08.md](claude-2026-09-08.md).

---

## Scribe is on two machines

Answered by Scribe itself (`SCRIBE-TESTING.md`, 2026-08-20), when CLAUDE.md's
flat claim that “it can run commands on that PC” was questioned:

- Its agent runs on a Linux VM with **no checkout** of this repo, and it will
  not clone one. The Avalonia headless sheet captures that used to be the
  argument for asking it to photograph the product went with the lane (E-2c,
  2026-09-04). Everything that photographs this product now needs Windows.
- David's Windows PC **is** reachable, per-command, with David approving each
  one. `scripts/shoot.ps1 -List` has been run there. Every command costs him a
  click.

`SCRIBE-TESTING.md` asked for output in `dist/scribe-shots/<date>/`; `dist/` is
line 3 of `.gitignore`, so a perfect PNG could never have reached the repo.
Scribe declined to write into `docs/screenshots/` because that is ours —
correctly. The shots never arrived because of **our instruction**, not Scribe.

Live rule: ask for findings as text in `SCRIBE-TESTING.md`, not files. Treat
them as evidence, never as a green tick on something only the game can verify.
(`DocumentationTests` still holds the live file to real paths.)

**Diagnoses of code are unreliable; channel work is excellent.** Four for four
of its guesses about what the codebase contains were wrong, each one a single
`grep` from being right. The fifth was **right** (#239, 2026-08-25, verified
08-26): after expand, `MiniRoot` hides and `NormalRoot`'s title bar is wider,
so the same cursor spot is no longer over Minimize. It was still labelled a
hypothesis; verifying it took one read. A Scribe hypothesis about source is a
place to look, never a fact.

---

## A hold names a prevention, not a vibe

On 2026-08-22 all three described holds had stopped being true (David: *“that
shouldn't be a hold then, that should be an already done”*):

- #228 said “do not tell players motes are back” when the reporter had been
  answered the day before and 1.99.0's What's-new had announced it.
- #226 said “do not reply” for four hours after its reporter had replied to
  *us*.
- #208's “do not open” was about starting the **work** and read as an embargo
  on talking to the reporter.

A stale line here does not merely mislead — it **suppresses**. Before you
describe what a reporter has or has not been told, **open the thread**. One
`gh` call. A whole session went out claiming we had a fix and were being held
back from telling someone, built entirely from the hold text and Scribe's item
— both of which describe an intention, never the state of a thread.

A shipped fix does **not** lift a hold. #226 and #228 both had their fixes
released while held. Helm's #228 note said the restore was “still-wrong” — a
product judgement about what players should be told, not an oversight.

On 2026-08-21 two replies went out against holds that had landed ninety minutes
earlier, because the working tree was behind. Re-read `HELM.md` before every
thread reply. On 2026-08-19 Scribe answered #215 at 20:45 and Claude offered
the same reply at 20:48 — one account, two voices, three minutes. Read the
last comment's signature before replying.

---

## The question-tool gap was the closing paragraph

David, 2026-08-20: ask questions directly; CLI outputs are long and the ask
gets missed. Restated unprompted 2026-08-23 (*“if a call is needed by me on
anything, please always frame it to me in the question / answer way”*). Said
a third time the same day (*“I'm getting a bit tired of saying that. Please
don't bury asks of me in text”*).

Three statements of one rule means the rule was never the gap. The gap is
that an offer buried in a summary does not **feel** like a question while you
write it: *“two things sitting for you whenever you want them”*, *“neither
urgent”*, *“whenever you want”*. A message that ends with “nothing is pending
you” and then lists two things is a message that ends with two things pending
him.

Live rule: if the last paragraph names something he might do, want, review,
or decide — that is the question, and it goes in the question tool.

The wiki re-check plan put eqlwiki request-rate numbers in front of him “to
adjust at approval” (2026-08-21). That was a decision dressed as a question.
Run the two tests on the consequence list first.

---

## First release-review mailbox miss

On 2026-08-22 the first Fable release review was answered, committed, and
sitting in the working tree while the session reported it as outstanding,
because the scan checked `git status`, the three inboxes, and GitHub, and
never opened `FABLE-FEEDBACK.md`. David had to say so. When you are waiting
on any agent, the file you asked in is the first thing you re-read.

H4 earned the release-review gate: one last-look of an already-shipped diff
found a player-facing defect the entire suite could not reach (the 1.99.1
re-check losing a ✦ with the wiki down), at no cost in Founder time.

---

## Illustration-lock debt (2026-09-04)

`docs/screenshots/` held 111 committed captures and **42 had no `shoot.ps1`
recipe** (Bevel's inventory). That number is why `options-cards.png` could
sit listing a card the World fold had deleted. Live rule is unchanged: an
illustration of our own UI is a capture with a recipe, or it does not ship;
if the surface cannot be staged, write the italic caveat, do not invent a
picture nobody can check.

David, 2026-08-19: character names in shots are fine. The defect is the
wrong, non-repeatable **state** (a live profile photographed as a fixture).

---

## Avalonia lane (deleted E-2c, 2026-09-04)

Preserved at `v1.99.18` and on `legacy-v1` ([LEGACY-V1.md](../../../LEGACY-V1.md)).
When a live trap says “both lanes”, it is telling you what the bug **cost**,
not what the repo contains. Headless capture sheets went with the lane. The
surviving capture surface is `scripts/shoot.ps1` (Windows-only).
