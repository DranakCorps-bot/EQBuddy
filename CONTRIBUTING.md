# Contributing to EQBuddy

Small project, fast cadence, two UIs, humans working with AI coding agents on both
sides — these conventions keep that combination smooth. Agents: treat this file as
binding instructions for work in this repository.

## Layout & ownership

| Area | Owner | Notes |
|---|---|---|
| `src/EQBuddy.Core` | maintainer (David) | Parser, aggregation, journal, SQLite, settings. Ships with tests, always. |
| `src/EQBuddy.UI.Shared` | shared | ViewModels + presentation. Framework-neutral: **no WPF or Avalonia references, ever.** Logic added here needs tests. |
| `src/EQBuddy` (WPF) | maintainer (David) | The Windows app the family runs. Thin views over UI.Shared where migrated. |
| `src/EQBuddy.Avalonia` | Don Thompson | The Linux/cross-platform app. Thin views over UI.Shared where migrated. |

Don't edit the other lane's UI directory except by agreed handoff; cross-lane
changes go through a PR the owner reviews.

## The shared-first rule

New features land with their **logic in Core or UI.Shared** (tested), and UIs get
thin views. If you find yourself writing a mapping, format string, or workflow in
a code-behind file, it probably belongs in UI.Shared. This is what keeps the
cross-platform port cost near zero and is the road to a single UI (issue #6).

## Workflow

- **Maintainer lane:** works on `main`; releases via `scripts/release.ps1 -Tag vX.Y.Z`
  (version comes from `src/EQBuddy/EQBuddy.csproj`; keep the Avalonia csproj version
  in sync). Releases can land daily — rebase feature branches often.
- **Contributor lane:** feature branches → PRs. Every PR gets CI (build both UIs +
  tests) plus, for Avalonia changes, a **Windows smoke-run** by the maintainer
  against a fixture profile, with findings posted in the PR.
- **Issues are the only sync channel.** Design-before-code for shared features.
  Decisions live in issue threads, not in chat memories.

## Testing without the game

You don't need Windows, the game, or an account:

- `EQBUDDY_APPDATA=<dir>` runs any build against an isolated profile.
- `pwsh scripts/make-test-session.ps1 -Out <dir>` turns the sanitized fixture log
  (`tests/fixtures/eqlog_Testchar_fixture.txt`) into a live-looking session;
  append lines to simulate play. Full recipe: `docs/FeatureGuide.md`.
- `EQBUDDY_EXPAND=1` expands all cards (screenshots); WPF also has
  `EQBUDDY_OPTIONS/HISTORY/MENU/TUTORIAL`-style hooks — see `MainWindow` ctor.
- `docs/FeatureGuide.md` is the per-feature manual-verification list; if your
  change touches a feature there, re-run its verification and update the text.

## Quality bar

- `dotnet build EQBuddy.slnx -c Release` and `dotnet test` green before every PR/push.
- Parser changes include the raw log line as a test fixture.
- UI-visible changes update `docs/FeatureGuide.md` (and screenshots when the look
  changes — regenerate via the fixture recipe, don't hand-mock them).
- Commit messages explain *why*, release-notes style; user-facing changes get a
  version bump and a line in the release.

## Log privacy

Real logs contain other players' names and chat. Anything committed to
`tests/fixtures/` must be sanitized: no chat/tell lines, character names replaced
(see the existing fixture for the pattern).
