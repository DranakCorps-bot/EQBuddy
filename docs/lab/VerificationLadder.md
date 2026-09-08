# C′ — Consequence-sized local verification (lab)

**Lab experiment, 2026-09-08. Not a Corps-wide standard yet.**
`CLAUDE.md` carries a compact pointer. This file is the ladder.

Soft / local loops are the checks a seat runs **before** it pushes or asks for
a last-look. They exist so a V0 typo does not pay for a desktop E2E run, and so
a V3 settings-migration does not stop at a filtered test of one class.

**CI on the pull request, and `main` after merge, stay authoritative.** A green
local Soft loop is an observation that the change is worth putting in front of
CI. It is never a substitute for `build-and-test` + `e2e-windows` on the PR,
and it never waives a Helm last-look or David's release go.

## Why a ladder, not one command

`scripts/check.ps1` is the whole *local* gate set (What's-new, legacy notice,
Evolved channel, Release build, unit suite). E2E is deliberately not in it —
it launches the real app and needs a desktop. CI runs both jobs on every push
and PR (`.github/workflows/ci.yml`, since 2026-09-04).

Paying that whole bill on every Soft loop taught the wrong lesson: seats either
skipped verification or burned the expensive loop on cosmetic work. The class
in `CLAUDE.md` is already about **consequence**, not effort. Verification
follows the same cut.

## The ladder

Match the loop to the class of the change. Climb when the cheapest loop cannot
see the failure you would actually ship.

| Class | What the change is | Local Soft loop (minimum) | Climb when | Still does not replace |
|---|---|---|---|---|
| **V0** | Cosmetic, mechanical, docs-only, a comment, a compact trap line. No behaviour. | Targeted: the suite that would fail if the claim is wrong (`DocumentationTests`, a single `[Fact]`, or nothing beyond the existing docs check if you only moved prose into the archive). | The edit cites a file, a test class, or a number the docs pin. | CI on the PR. |
| **V1** | Localized behaviour. One module, one decision, an obvious default. | `dotnet test tests/EQBuddy.Tests/EQBuddy.Tests.csproj -c Release` after `dotnet build EQBuddy.slnx -c Release`. Filter to the touched class only when the full unit suite has already been green in this session and the edit cannot reach another producer of the same value (trap 33 / 64). | Window / ingest / dump wiring, or a second producer of a value you already have a test for. | CI. E2E if the widget or a satellite is in the diff. |
| **V2** | Cross-cutting, migration, privacy-adjacent, two hosts of one surface, a settings write path. | `pwsh -NoProfile -File scripts/check.ps1` | Player-visible surface, `EQBUDDY_EXPAND` facts, two hosts open, or a profile JSON write. Then named E2E and/or `shoot.ps1` for that surface. | CI. Helm last-look. A Fable plan if the class itself is V2. |
| **V3** | Architecture, ambiguous root cause, security / privacy / the values line, a release-shaped change. | `scripts/check.ps1` **and** the named E2E / `shoot.ps1` rows that can see the risk. | Anything that can destroy a profile, phone home, or ship unsigned. Stop and ask — that is the consequence list, not a bigger test run. | CI + Fable release review + David's ship. |

E2E is Windows-only and takes the screen lock (`tests/EQBuddy.E2E`, trap 61).
Do not start it on a machine that is already shooting, and do not assert the
screen size — the hosted runner is 1024×768.

```bash
# V0 / cheap V1 after a green full suite this session
dotnet test tests/EQBuddy.Tests/EQBuddy.Tests.csproj -c Release --filter FullyQualifiedName~DocumentationTests

# V1 default
dotnet build EQBuddy.slnx -c Release
dotnet test tests/EQBuddy.Tests/EQBuddy.Tests.csproj -c Release

# V2 default — every local gate, one command
pwsh -NoProfile -File scripts/check.ps1

# Climb: real exe, isolated profile. After the app project has been rebuilt
# (trap 64 — this suite does not reference EQBuddy.exe).
dotnet test tests/EQBuddy.E2E/EQBuddy.E2E.csproj -c Release
```

## What a local green is allowed to mean

- **Allowed:** "the Soft loop for this class passed; the change is ready for CI."
- **Not allowed:** "CI can be skipped." "Helm can last-look from my laptop."
  "`main` is fine because check.ps1 was green." "It passed on rerun, so it is
  not a flake."

A Soft-loop failure is a stop. A Soft-loop pass is a ticket to the next rung,
not a verdict.

## Flakes

A red that goes green on the next run is an **occurrence**, not a resolution.
Log it on [FlakeLedger.md](FlakeLedger.md). Disposition stays `watched` or
`open` until a cause is named and a guard holds it — or until the surface that
produced it is gone (retired-with-lane). Helm's standing line still applies:
re-run a known product flake; do not "fix" product code to silence it.
