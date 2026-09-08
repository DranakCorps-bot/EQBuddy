# CLAUDE.md archive

**Lab experiment Context, 2026-09-08. Not a Corps-wide standard yet.**

`CLAUDE.md` is the live manual, loaded every session. This directory holds the
retrospectives that used to live inside it.

## Progression (incident → lesson → executable guard → compact live rule)

1. **Incident** — a player report, a red CI run, a minidump, a screenshot that
   disproves the code.
2. **Lesson** — the sentence that would have caught it, written while the
   evidence is still in hand. This is the novel: who, when, which hypothesis
   died, what the instrument said.
3. **Executable guard** — a test, a `scripts/*.ps1` stage, a dump key. If the
   lesson cannot fail the build, it is not yet a guard.
4. **Compact live rule** — the trap line in `CLAUDE.md`: title, the rule that
   still binds, the guard's name. The novel moves here.

A trap that has no guard yet keeps enough of the lesson in the live file that
the next seat does not have to rediscover the mechanism. A tombstone (guard
gone with its surface) keeps the *rule* live and parks the named files in the
snapshot so `DocumentationTests` is not forced to cite a path that no longer
exists.

## Snapshot

| File | What it is |
|---|---|
| [claude-2026-09-08.md](claude-2026-09-08.md) | Byte-for-byte `CLAUDE.md` immediately before the live/archive split (~172 KB). Traps 1–68 in novel form, the Scribe/Helm hold narratives, the "fifth was RIGHT" record, and the tooling stories. |

Do not edit the snapshot to "fix" encoding or to keep it current. A new
incident lands as a compact live rule; if it needs a novel, append a dated
note in this directory rather than rewriting the snapshot.

`CLAUDE.md` must keep every still-needed rule. If a compact line is not enough
to act on, the snapshot is the next place to read — not a second live manual.
