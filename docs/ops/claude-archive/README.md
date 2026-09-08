# CLAUDE.md archive — novels off the always-loaded path

[CLAUDE.md](../../../CLAUDE.md) is loaded at the start of every agent session.
It exists so a seat does not rediscover the codebase. A 170 KB always-loaded
file is a tax: the live rules drown in incident novels the guard already
replaced.

**This directory is the other half.** Historical evidence stays true and
linkable. It is not loaded automatically.

## Split

| Live (`CLAUDE.md`) | Archive (here) |
|---|---|
| Current rules, architecture invariants, operating contracts | Incident novels, superseded mechanisms, “what it cost” |
| Compact trap: the surviving sentence + guard name | Full trap entry with evidence, false paths, reporter quotes |
| Pointers | Snapshots |

Progression, and it is the only reason a novel is allowed to leave always-loaded:

> incident → verified lesson → executable test/guard → compact live rule

Once a guard exists, drop the novel from `CLAUDE.md`. Keep the compact rule.
Do not gut a rule that is still needed just to hit a size. There is no word-count
ratchet; truth outranks brevity.

## Files

| File | What it is |
|---|---|
| [claude-2026-09-08.md](claude-2026-09-08.md) | Byte-exact pre-split `CLAUDE.md` (171,685 bytes). Reversible. |
| [traps.md](traps.md) | Traps 1–68 with `### Trap N` anchors for live links. |
| [operating-history.md](operating-history.md) | Scribe two-machines, stale-hold examples, question-tool closing-paragraph novel, illustration-lock debt. |

## How to archive the next novel

1. Keep the compact rule in `CLAUDE.md` (what to do, what the guard is named).
2. Move the incident write-up here, under the matching file, with an anchor.
3. Link from the live rule. Do not leave a live sentence that only makes sense
   if the reader has the novel in context.
4. If you bury a file or a test suite, do not backtick a path that no longer
   exists — `DocumentationTests` scans this directory too.

## What this is not

- Not permission to delete a live contract because it is long (consequence list,
  three ways back, holds, “never measure other players”).
- Not a second HELM.md.
- Not a word-count gate.
