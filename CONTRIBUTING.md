# Contributing to EQBuddy

Small project, fast cadence, two UIs, humans working with AI coding agents on both
sides — these conventions keep that combination smooth. Agents: treat this file as
binding instructions for work in this repository.

## Project values: your numbers, not other people's

EQBuddy measures **your own play** — your damage, your pet, your loot, your progress.
It deliberately does not rank, compare, or expose **other players'** performance, and
PRs that add that (party/raid DPS meters, member rankings, "who's slacking" views)
won't be merged regardless of code quality. The maintainer watched performance
scoreboards curdle other MMO communities — most visibly WoW's — and EQBuddy's answer
is to not build the scoreboard. This has come up more than once, so it's written down
here rather than re-litigated per PR.

The MIT license means you're genuinely welcome to build that tool **on top of** this
codebase — fork it, keep the attribution, godspeed. It just won't ship as EQBuddy.
(Group-related features that don't score people — a roster, group kill counts, party
XP — are fine; the line is measuring and comparing individuals' performance.)

## Layout & ownership

| Area | Owner | Notes |
|---|---|---|
| `src/EQBuddy.Core` | maintainer (David) | Parser, aggregation, journal, SQLite, settings. Ships with tests, always. |
| `src/EQBuddy.UI.Shared` | shared | ViewModels + presentation. Framework-neutral: **no WPF or Avalonia references, ever.** Logic added here needs tests. |
| `src/EQBuddy` (WPF) | maintainer (David) | The primary Windows app. Thin views over UI.Shared where migrated. |
| `src/EQBuddy.Avalonia` | Don Thompson | The Linux/cross-platform app. Thin views over UI.Shared where migrated. |

Don't edit the other lane's UI directory except by agreed handoff; cross-lane
changes go through a PR the owner reviews.

## The shared-first rule

New features land with their **logic in Core or UI.Shared** (tested), and UIs get
thin views. If you find yourself writing a mapping, format string, or workflow in
a code-behind file, it probably belongs in UI.Shared. This is what keeps the
cross-platform port cost near zero and is the road to a single UI (issue #6).

## Workflow

- **Maintainer lane:** works on `main`. Changes are field-tested locally FIRST via
  `scripts/install-local.ps1` (builds + silently installs on this machine only);
  releases happen deliberately, bundled, when the maintainer says so, via
  `scripts/release.ps1 -Tag vX.Y.Z` (version comes from `Directory.Build.props` at the repo root — single-sourced for BOTH apps, issue #30; `EQBuddy.csproj` carries a comment saying so, and the
  Avalonia csproj inherits the same property — there is nothing to keep in sync). Every release needs a matching entry in
  `src/EQBuddy.Core/Data/WhatsNew.json` — release.ps1 refuses without one, because
  that entry is what the in-app "What's new" popup shows users after they update.
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
- **Every change a player can notice gets a `WhatsNew.json` entry in the release
  that ships it.** The What's-new popup is how an update explains itself; a fix
  that lands silently is indistinguishable from the app behaving differently for
  no reason — and the player who reported it never learns it was fixed. Write the
  entry from the player's side (what changed for them, and where), name the
  reporter and the discussion, and be honest about limits. If a user-visible fix
  lands after a tag, it gets its own release rather than riding along unannounced.
  Internal-only work (dependency bumps, refactors, test infrastructure) needs no
  entry — but "invisible" means invisible to the player, not merely uninteresting
  to us. `scripts/release.ps1` refuses to publish a version with no entry at all;
  that guard is the floor, not the standard.

## Log privacy

Real logs contain other players' names and chat. Anything committed to
`tests/fixtures/` must be sanitized: no chat/tell lines, character names replaced
(see the existing fixture for the pattern).
