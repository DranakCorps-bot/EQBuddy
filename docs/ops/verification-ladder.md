# Consequence-sized local verification (C′)

Soft / local executor loops verify **to the class of the change**, then stop.
`scripts/check.ps1` plus CI on `main` (`build-and-test` and `e2e-windows`) stay
the authoritative gates. This ladder does **not** weaken them, skip them, or
add a `-Skip` path.

Helm practice this file preserves: evidence before confidence; prove-fail a new
guard; last-look where consequence warrants; local greens are not CI.

## Ladder

| Class | What the change looks like | Local Soft loop | Still not this |
|---|---|---|---|
| **V0** | Cosmetic, mechanical, localized. Docs, a comment, a one-file helper, a catalog pin. | Targeted unit or static check on the files you touched. Docs: `DocumentationTests` / `DocumentationSizeTests` if you edited a scanned doc. | Full unit suite, E2E, `scripts/shoot.ps1`. |
| **V1** | Straightforward product or wiring. Most inbox items. | Relevant unit project (`dotnet test tests/EQBuddy.Tests/EQBuddy.Tests.csproj -c Release` when Core/UI.Shared/tests moved) **plus targeted E2E if user-visible** — the dump key or the one `EQBuddy.E2E` class that covers the surface. | Full E2E assembly, illustration batch, release scripts. |
| **V2** | Cross-cutting, migration, privacy/profile, ambiguous root cause, two hosts of one surface. | Affected suites **and** integration: `pwsh -NoProfile -File scripts/check.ps1`, then the E2E classes that share the state. Prove-fail a new guard. | Waiving CI because local was green. Skipping the Fable plan if the work is still V2. |
| **V3** | Architecture, security/privacy/migration that can undo a player, release-shaped, or “the obvious fix is wrong for a reason you can only see with the whole system in view.” | Full discipline: `check.ps1`, `dotnet test tests/EQBuddy.E2E/EQBuddy.E2E.csproj -c Release` after `dotnet build EQBuddy.slnx -c Release`, `shoot.ps1` when the UI is the acceptance criterion, prove-fail, last-look ask. | Inventing a local-only merge bar. Touching Play Console / signing / prod secrets. |

Class is about **consequence and reach**, not effort — the same test as
[CLAUDE.md](../../CLAUDE.md): *if David answered one question right now, could
I finish this as V1?*

## Commands (unchanged)

```bash
dotnet build EQBuddy.slnx -c Release
dotnet test tests/EQBuddy.Tests/EQBuddy.Tests.csproj -c Release
pwsh -NoProfile -File scripts/check.ps1
# Windows session, after the build — CI runs this on every push/PR:
dotnet test tests/EQBuddy.E2E/EQBuddy.E2E.csproj -c Release
```

`check.ps1` is the fast local pass (what's-new, legacy notice, evolved-channel,
build, unit). It deliberately does **not** launch E2E. CI does.

## Rules that do not move

- **CI/`main` is the merge bar.** Local green is evidence for the seat, not a
  waiver. Helm has said this in as many words: *local greens are not CI*.
- **E2E asserts relationships, never the SCREEN.** A hosted runner is
  1024×768; a test that needs a taller monitor is asserting the desk it was
  written on.
- **Build the app before an E2E run that depends on a `src/EQBuddy` edit**
  (trap 64). `tests/EQBuddy.E2E` does not reference the exe it launches.
- **A new guard is proved by failing without it.** Green-only is the shape
  this repo treats as vacuous coverage (trap 34).
- **Do not “fix product” because a named flake went red once.** File or update
  a [flake-ledger](flake-ledger.md) row. “Passed on rerun” is observation.

## What this is not

- Not a second CI.
- Not permission to skip `e2e-windows` on a PR that would have needed it.
- Not a model-routing change.
- Not a release, signing, or Play Console path.
