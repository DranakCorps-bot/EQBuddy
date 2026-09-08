# Soft / local ops — how to use C′ and the CLAUDE split

Two 2026-09-08 operating-model experiments, kept reversible (docs + light tooling).
CI/`main` gates are unchanged and remain authoritative.

## What Soft reads first

1. **[CLAUDE.md](../../CLAUDE.md)** — live manual. Current rules, architecture
   invariants, operating contracts, compact traps. Always loaded.
2. **[verification-ladder.md](verification-ladder.md)** — how much to verify
   locally before you push. Sized by V0–V3 consequence, not by habit.
3. **[flake-ledger.md](flake-ledger.md)** — known intermittent failures.
   “Passed on rerun” is an **observation**, not a resolution.

Do **not** load the archive at session start. Open a novel only when a compact
live rule is not enough to act. `DocumentationTests` scans this directory so
the pointers stay true.

## Compact Soft rules

- **Verify to the class, then stop.** V0 is targeted unit/static. V1 adds the
  relevant unit suite and targeted E2E when the change is user-visible. V2 runs
  the affected suites plus integration (`scripts/check.ps1`). V3 is full
  discipline. Local green never waives CI.
- **A flake you have not named is still open.** Add a ledger row. Do not “fix
  product” because CI was red once and green on rerun. Do not treat a rerun
  green as closed.
- **CLAUDE.md stays the pointer set.** Incident novels, superseded mechanisms,
  and historical evidence live under
  [claude-archive/](claude-archive/README.md). Progression:
  incident → verified lesson → executable test/guard → compact live rule.
  Once a guard exists, drop the novel from always-loaded — not the rule.
- **Practice that stays (Helm-aligned):** evidence before confidence; prove-fail
  a new guard; last-look where consequence warrants; ship the instrument before
  the third theory; local greens are not CI.

Out of this experiment: no model-routing pilot, no HELM-FEEDBACK migration,
no Play Console / signing / prod secrets.
