# Fable inbox

Plans for Claude, not a work order. **Claude: take a `ready` item, then delete it**
(or leave only what is still planned).

EQBuddy is the incubation lab. We refine the finished state here. The organization
iterates the same way as the software (observe → diagnose → change → verify).

## When this file is in play

**V2–V3 only.** Cross-cutting architecture, significant refactor, ambiguous root cause,
security/privacy/migration, complex parallel decomposition.

Fable 5 writes the plan. Helm last-looks. **Claude executes it** — unless the plan carries a
`needs-david:` line, which names a decision from the consequence list in `CLAUDE.md`
("What needs David, and what does not") and waits for him to answer THAT. David reads this
file as a digest he can veto; the release gate is where anything he dislikes is caught.

**Approval by exception, not by gate** (David, 2026-08-22). The old shape — Fable plans,
David marks `approved`, Claude executes — had him reading every plan in full to say yes to
work the release gate already protected him from. The first two plans through here were
approved without a word changed.

**V0–V1 does not belong here.** Cosmetic, mechanical, localized, straightforward work
stays one Claude loop. Do not pay a planning-handoff tax without reason. The test before
stubbing: *if David answered one question right now, could this be V1?* If yes, ask the
question instead.

This is not a fourth gate on Scribe intake or Bevel critique. Those files stay their
own inboxes. Org-level proposals do not go in this file.

There is no Fable Grok Bot. Point Fable 5 at this file.

## How Fable reaches Helm

**You reach Helm by webhook, not by David** (David, 2026-08-24). After you write
or change `HELM-FEEDBACK.md` and push it (a LIVE ASK or a loop-close Helm must
see), trigger the private wake:

`gh workflow run helm-back-channel.yml --repo DranakCorps-bot/dranakcorps-control-plane`

Optional: `-f reason="HELM-FEEDBACK.md changed"`. File writes are not a wake. A
push alone is not. The URL and key are Actions secrets on that private repo,
never in this file. Do not paste them here.

Helm last-looks, then pages Dranak to run `claude -p` if the executor needs a
kick. David is not the courier. Page him only for a consequence-list door.

This is standing process, not a V2–V3 plan item. Do not stub it as a work item.

## How Claude calls Fable

Claude does not start you (David, 2026-08-24). Claude files a `To: Fable` note
(this file or `FABLE-FEEDBACK.md`), pushes, and wakes Helm with the same
`gh workflow run` command above. Helm last-looks and pages Dranak to start a
Fable-shaped `claude -p` in this repo. You plan; Claude executes. Do not wait
for David to carry the ask.

## Item shape

- **Priority:** `ready` (plan written; Claude may take it) · `needs-david: <the decision>`
  (names ONE consequence-list decision; waits for his answer, never for a generic "approve") ·
  `someday`. David may still write `approved` as an explicit mark; it means `ready`.
- **Class:** `V2` or `V3` (if you cannot say why it is not V0–V1, it does not go here)
- **Source:** discussion/issue, Bevel/Scribe item, or David's words
- **Plan:** architecture, risks, decomposition, verification, what is out of scope
- **Bevel pre-design: yes / no, because…** — required on any plan with a presentation PR.
  Fable plans the architecture; Bevel judges whether the player can still do the job. The
  executor treated a plan as the design pass once (2026-08-22) and should not have had to guess.
- **Shot offline: yes / no** — for any staged screenshot. `shoot.ps1` is NOT offline by
  default, so a "not read yet" prediction for an unseeded wiki page is wrong before it runs.
- **Column budgets: <the fixed widths this touches>** — for any plan that puts a new string
  into an existing surface. The Sky glance overflowed a fixed 150 px column and was found from
  a screenshot after it was built; measure before writing the string.
- **Guards run eight times** — a new test that guards a fix is not green until it has passed
  eight consecutive runs. `SettingsClobberTests` was flaky one run in three from the hour it
  shipped and would have passed any single review.
- **What clamps it: <the stored setting's other readers>** — for any formula that takes a
  persisted value as an input. The 320-cap plan named `ContentHeight` as "what the player
  dragged" when `SectionMaxHeight` clamps it to the work area first, so the body could claim
  room the stack was never granted (executor, 2026-08-31). One grep for the setting's readers
  before it becomes a plan input.
- **Must-list rows on this surface: <the `GameCommandsTests` / `ImportReportReachesASurfaceTests`
  rows>** — for any plan that reshapes a surface carrying one. "Defer to the window's scroller"
  read as "delete the inner scroller" until the executor found the ⧉ that scroller pins is a
  trap-34 row on that exact tab (2026-08-31). Name the row and the plan cannot un-pin it.
- **Already shipped:** what exists that this must not fight
- **Checked:** what Fable actually read. Hypotheses labeled as such.
- **Decided without asking:** the implementation calls the plan made that could have gone the
  other way, one line each — these go to `DECISIONS.md` when the item is taken.

After Claude takes an item, write a short note in `FABLE-FEEDBACK.md`. Fable last-looks the
executed diff (H4) and answers in the same file; a defect found there is a V1 item for the
next loop, not a reopening of the plan.

---

## EQBuddy v2 Phase 0–2 — technical decomposition (charter §25 item 6, issue #275)

- **Priority:** `ready` — **for Phase 0 only.** Phase 1 is written here in full and is
  **BLOCKED**: Helm's 2026-09-04 11:50 AM CT ruling says Avalonia removal waits on the
  Phase 0 gate, and charter LEGACY-005 says the legacy tag and branch exist *before*
  Avalonia leaves the mainline. Phase 2 is an **architecture sketch for Bevel's parallel
  staging**, not buildable work — its first PR waits on Bevel's IA.
  **Not `needs-david`.** Every call below is inside the owner-approved charter; the two
  places a line could have been drawn differently are marked **→ Helm** and named, not
  guessed. Nothing here touches the values line, the release go, public posture, money,
  roadmap direction, eqlwiki policy, third-party policy or player privacy.
- **Class:** `V3` — a platform line is being cut under live users, the update channel is
  the mechanism, and the mechanism is a one-way door: once a `v2.x` release is `latest`,
  every un-bridged Linux and macOS install is already being offered a Windows installer
  and nothing we ship afterwards can reach it. Not V0–V1 because no single answer finishes
  it and the obvious version ("stop building the tarball") is wrong for a reason only
  visible with the whole system in view — see P0-2 rule 4.
- **Source:** `docs/v2/EQBuddy-v2-Project-Guide-Requirements.md` rev 1.1 (owner-approved
  2026-09-04) §§3 / LEGACY-001…007 / 16.2–16.6 / 19 / 25 item 6; `HELM.md` §"EQBuddy v2
  Phase 0 / #275 — Windows-only posture", signed Helm 2026-09-04 11:50 AM CT;
  `HELM-FEEDBACK.md` same stamp; issue #275.
- **Bevel pre-design: Phase 0 YES but small; Phase 2 YES and it gates everything.**
  Phase 0's only player-facing surface is the LEGACY-002 notice — its wording, and the one
  question below about what "acknowledged" means. That is a real design ask and it is the
  last thing Linux and macOS players will ever be shown by EQBuddy, so it is worth Bevel's
  pass even though it is three sentences. Phase 2's IA is Bevel's own parallel track
  (Helm, 11:50 AM CT); the sketch below is seams, not screens, and does not pre-empt it.
- **Shot offline: N/A for Phase 0.** The notice appears only when the live feed reports a
  major-2 release, which `shoot.ps1` has no way to stage — the fixture profile has no
  update state and `UpdateChecker` reaches a hardcoded URL (see P0-2 verification). Do not
  add a shot that pretends to cover it; the honest coverage is the unit policy plus one
  real prerelease. Phase 2 shots are Bevel's to specify.
- **Column budgets: 320 px, wrapping — so the budget is HEIGHT, not width.**
  `MainWindow.xaml:87` `NormalRoot Width="320"` and `MainWindow.cs:115`
  `_normalRoot = new() { Width = 320 }` — both lanes. Both banners already wrap
  (`MainWindow.xaml:191` `TextWrapping="Wrap"`; `MainWindow.cs:121`
  `TextWrapping = TextWrapping.Wrap`), so a long string grows the widget **downwards**.
  **Trap 12 is live here:** both widgets are `SizeToContent`, so a string that fails to
  wrap is a geometry change on a transparent always-on-top window over a fullscreen game —
  which is #173. Keep the notice to two or three short sentences and never let it produce
  a single unbreakable token (a bare URL is exactly that; put the link behind the click).
- **Guards run eight times.** The new policy tests are pure functions with no clock, no
  network and no file, so flake is unlikely — but `LegacyPlatformUpdatePolicyTests` and the
  source scanner both run eight consecutive times before the PR is called green, same as
  every other guard here.
- **What clamps it:** `AppSettings.LegacyFinalNoticeAcknowledged` is a **new** setting with
  exactly one reader (the policy) and one writer (the policy's application in both lanes);
  nothing else clamps it. The values it competes with are `UpdateFolder` (read by
  `UpdateChecker.FindUpdateFolder`, Windows-only channel, unaffected) and the in-memory
  `_lastUpdateCheck`, which is **not** persisted — see the epoch note in P0-2.
- **Must-list rows on these surfaces:** none on the update banner today —
  `GameCommandsTests.SurfacesNeedingACommand` and
  `ImportReportReachesASurfaceTests` carry no row for it, because it names no in-game
  command and reports no import. **P1-5 is where those two lists matter**, and they matter
  a great deal: a feature-disposition pass is a fold of the whole product, and traps 20,
  26 and 43 are all the same event happening during a fold.
- **Already shipped — what this must not fight:**
  - `UpdateChecker` (Core) already merges two channels and refuses to stage an installer
    with no published SHA-256. Do not touch the staging path; LEGACY-003 is satisfied by
    construction on the non-Windows lanes because `UpdateOffer.CanAutoInstall` requires
    `IsWindows`.
  - `UpdateOffer` (Avalonia) already models **four** platforms as an enum rather than a
    bool, precisely because a bool made every Mac user a Linux user (#93). Extend that
    enum's home; do not re-introduce a bool.
  - `release-assets.yml` already builds and attaches all three non-Windows artifacts on
    every published release. The bridge needs no new packaging.
  - `SingleInstance.cs` means one process per profile, so the new setting has no second
    writer and trap 13's clobber does not apply.
  - `docs/Themes.md` already contains four BUILT themes. Phase 2 is not a new construction
    (see the sketch).

---

### PHASE 0 — protect v1 users before cutting platforms (`ready`)

Charter §19 Phase 0. Four PRs, in this order. **P0-4 is last and it is irreversible in the
useful direction: once `legacy-v1` exists at the bridge commit, Phase 1 is unblocked.**

#### P0-1 — Bridge release mechanics (LEGACY-001, LEGACY-004)

**Finding: the bridge needs almost no new mechanics.** `scripts/release.ps1 -Tag v1.99.N`
already publishes, signs, installs and tags; `release-assets.yml` fires on
`release: published`, checks out `github.event.release.tag_name`, and publishes
`EQBuddy-linux-x64.tar.gz`, `EQBuddy-osx-arm64.zip` and `EQBuddy-osx-x64.zip` from
`src/EQBuddy.Avalonia`. The bridge is an ordinary release whose *contents* are special.

What actually has to change or be proved:

1. **`release.ps1` gains `-Prerelease`** (passes `--prerelease` to `gh release create`).
   Today the script always publishes a `latest` release. This is the cheapest and largest
   half of LEGACY-002, and it is the **only** protection that reaches the un-bridged
   population: `UpdateChecker.CheckGitHubAsync` reads
   `https://api.github.com/repos/DranakCorps-bot/EQBuddy/releases/latest`, and GitHub's
   `releases/latest` excludes prereleases and drafts — so a v2 milestone published as a
   prerelease is invisible to **every** v1 client, bridged or not. That is also exactly
   what charter RELEASE-002 asks for during v2 construction.
   → **Second belt, free:** `ParseRelease` does `Version.TryParse(tag.TrimStart('v','V'))`
   and returns `null` when it fails, so a tag shaped `v2.0.0-beta1` offers nothing even if
   it *were* marked latest.
   → **Hypothesis, not fact:** I read GitHub's documented behaviour for `releases/latest`,
   not an observed response from this repo. Verify with one real prerelease before relying
   on it (P0 gate proof 4).
2. **LEGACY-001 is done when the assets are ON the release, not when CI is green.**
   `gh release view <bridge-tag> --json assets --jq '.assets[].name'` must list all five:
   `EQBuddySetup.exe`, `EQBuddy-portable.zip`, and the three non-Windows artifacts. The
   matrix is `fail-fast: false`, so two of three can fail and the run still looks partly
   fine; and the non-Windows assets land minutes *after* the release exists, which
   `UpdateOffer.AssetUrl`'s doc comment already calls out as a real null window.
3. **LEGACY-004 is a policy, not a commit.** Nothing in the tree can stop a future
   `gh release delete`. Record it in `LEGACY-V1.md` (P0-3) and back it with a GitHub **tag
   protection rule** on the bridge tag plus branch protection on `legacy-v1` (P0-4). A
   platform rule outlives the memory of why it exists.
4. **Version number:** the charter refuses to hard-code one and so does this plan. Current
   `Directory.Build.props` is `1.99.18`. The bridge is whatever `1.99.N` carries P0-2 and
   P0-3, with its own `WhatsNew.json` entry — and that entry is the **only** in-app
   announcement Linux and macOS users will ever get about the transition, so it is
   player-facing text worth writing carefully and crediting the platform contributors
   (Don Thompson, quasarj) by name.

#### P0-2 — LEGACY-002: stop non-Windows v1 chasing v2

**Ground truth, read not assumed:**

- Three call sites per lane decide the same question. WPF: `MainWindow.xaml.cs:2607` (the
  one-second tick, `> TimeSpan.FromHours(6)`), `:3863` `OnCheckUpdates` (Help menu),
  `:3869` `CheckForUpdates(bool manual)`. Avalonia: `MainWindow.cs:2036` (tick), `:1470`
  (menu), `:4904` (body).
- `_lastUpdateCheck = DateTime.MinValue` on both lanes (`MainWindow.xaml.cs:45`,
  `MainWindow.cs:251`). **Trap 47's epoch:** the "every 6 h" branch is true on the first
  one-second tick, so "at startup" and "every 6 h" are one path firing immediately. Benign
  today — it is a network read, not a destructive sweep — but it means there is **no
  separate startup path to fix**, and it is the reason the notice must be idempotent
  rather than "shown once at launch".
- The platform decision already exists as a pure function: `UpdateOffer.Current()` →
  `Desktop { Windows, Linux, MacArm64, MacX64 }` (`src/EQBuddy.Avalonia/UpdateOffer.cs`),
  `internal` to the Avalonia project.

**Design — one policy, in `UI.Shared`, both lanes call it.**

`src/EQBuddy.UI.Shared/LegacyPlatformUpdatePolicy.cs`, framework-free, tested from
`tests/EQBuddy.Tests` — which the `build-avalonia-linux` CI job also runs, so the
non-Windows behaviour is asserted **on a non-Windows machine** while that job still exists.
Move `Desktop` and `Current()` into `UI.Shared` beside it so one enum serves both lanes;
`UpdateOffer` keeps its artifact/wording functions and consults the new enum.

This is trap 47's shape exactly — *never let two code paths decide one destructive
question* — with "destructive" replaced by "may we tell this player to install something
that cannot run on their machine". `UI.Shared/LogJanitorPolicy` is the worked example to
copy, including its source scanner.

Inputs: `UpdateInfo`, `Desktop`, `bool manual`, `bool acknowledged`.
Output: a small **record**, not a bool — a bool is what produced #93.
`(bool ShowUpdateOffer, bool ShowFinalLegacyNotice, bool RecordAcknowledgement, string? BrowserTarget)`.

Rules:

1. **Windows → unchanged, in every case.** The policy returns "behave as today" and the
   Windows diff is a call site, not a behaviour change. Keep it that way: a Phase 0 PR that
   alters the Windows update banner has widened its own blast radius for nothing.
2. **Non-Windows and `info.Latest.Major >= 2`** → never `ShowUpdateOffer`.
   - automatic path (`manual: false`): show the notice **iff** `!acknowledged`, and
     `RecordAcknowledgement`. After that, nothing.
   - manual path (`manual: true`): **always answer.** A player who opens Help → Check for
     updates and gets silence has hit a silent no-op, which this repo treats as broken.
     Show the same notice; do not re-arm the automatic nag.
3. **Non-Windows and `Latest.Major < 2`** → today's behaviour, byte for byte. A later
   `1.99.N+1` legacy patch must still be offerable, and `legacy-v1` existing does not mean
   it will never be touched.
4. **The browser target under rule 2 is the FINAL LEGACY RELEASE, never `latest`.**
   This is the single highest-risk detail in Phase 0. `UpdateOffer.BrowserTarget` currently
   falls back to `UpdateChecker.GitHubLatestPage` —
   `https://github.com/.../releases/latest` — which is *the v2 release page* the moment v2
   ships, whose most prominent asset is `EQBuddySetup.exe`. A correct-looking notice that
   ends on that page is LEGACY-002 point 3 arriving through the back door, and it would
   read as a working feature in every screenshot. Add
   `UpdateChecker.GitHubLegacyReleasePage`, pinned to the bridge tag, and assert the
   negative: `DoesNotContain("releases/latest", target)` for every non-Windows × major-2
   case. **Every equality assertion deserves one negative** (trap 39).
5. **LEGACY-003 needs no code and one test.** `CanAutoInstall` already requires
   `IsWindows(platform)`; add the explicit negative so a future edit cannot quietly remove
   the guarantee that nothing off Windows is ever staged, run or overwritten.

**Setting:** `AppSettings.LegacyFinalNoticeAcknowledged` (`bool`, default `false`). One
reader, one writer per lane. `DeadSettingTests` is satisfied by construction and needs no
`Known` row — check that it stays that way rather than assuming it.

**Affordance — the one thing for Bevel.** The Avalonia banner is a single clickable
`Border` (`_updateBanner`, `PointerPressed → OnUpdateBannerClick`); the WPF one is
`UpdateBanner` with `MouseLeftButtonDown`. So there is exactly one gesture available and it
currently means "do the update". The question: **does clicking the notice mean "open the
legacy release page", "I have read this", or both?** Recommendation — both, because a
notice that needs two gestures on a 320 px widget will grow a control the widget has no
room for. Bevel rules; the policy already returns `RecordAcknowledgement` separately from
`BrowserTarget`, so either answer is a wiring change and not a redesign.

**Guards:**

- `LegacyPlatformUpdatePolicyTests` (in `EQBuddy.Tests`) — the full matrix: 4 platforms ×
  {1.99.x, 2.0.0} × {manual, automatic} × {acknowledged, not}, plus the two negatives
  (no `releases/latest` in any legacy target; no auto-install off Windows).
- A **source scanner** in `LogJanitorPolicyTests`' shape, asserting that all six call sites
  route through the policy, so a seventh cannot drift. Name the participants in the test
  names — *tick / menu / policy*, not `manual` vs `not` (trap 49: a two-actor model is how
  thirteen green tests agreed with a bug).
- `UpdateOfferTests` (Avalonia) keeps its existing rows; add the major-2 cases there too
  while the lane exists, because that is the lane the code actually runs on.

#### P0-3 — Support matrix and transition docs (LEGACY-006, LEGACY-007)

- **New `LEGACY-V1.md` at the repo root** — charter §24 names this file. Contents: the
  LEGACY-006 table verbatim; the final legacy tag; direct links to all three non-Windows
  assets; what continues to work and what stops; the macOS quarantine instruction that
  today lives only in `README.md`; and the LEGACY-005 invitation to fork or continue
  independently, worded as an invitation and **not** as a commitment by David to maintain
  anything.
- **`README.md`** — lines 37–38 currently describe a cross-platform build that "tracks
  closely", maintained by Don Thompson and quasarj. That claim stops being true at the
  bridge. Replace it with the support matrix and a visible **Legacy Linux/macOS** section.
  → **Lines 620–628 are the CREDITS block. Credit stays; status changes.** Someone editing
  status out of a file will find contributor names in the same neighbourhood and the
  charter says nothing about removing them, because removing them was never on the table.
  Say it in the PR description so it cannot be done by momentum.
  → `README.md:556–583` describes building and releasing the Avalonia lane; that is
  developer documentation and it becomes `legacy-v1` documentation.
- **`docs/FeatureGuide.md` §"Updates" (line 657)** describes the update flow to players and
  needs the legacy paragraph. This file ships **inside the Linux tarball and the macOS
  bundle** (`release-assets.yml` copies it into both), so it is the one document a legacy
  user has on disk. That makes it the highest-value edit in this PR, and it is easy to miss.
- **`docs/TestPlan.md`** — new rows for the LEGACY-002 behaviours, marked Auto where the
  policy tests reach them and **Manual where they do not** (the wire fetch does not).
  `DocumentationTests` will check the paths; the numbers in `DocumentationSizeTests` are
  untouched by Phase 0 and will move in Phase 1.
- **LEGACY-007 is a v2-release-time obligation, not a Phase 0 edit.** It lands as a
  checklist row on #275 and a line in `LEGACY-V1.md`.
  → **Optional, cheap, and offered rather than assumed (→ Helm):** a sibling of
  `whatsnew-guard.ps1` that refuses a `v2.*` tag whose release notes carry no "Legacy
  Linux/macOS" section. Same shape as the guard that already exists for exactly this class
  of promise. If Helm would rather not add a release-time gate, the checklist row stands
  alone and the plan is unaffected.
  → **If it is written, mind trap 54.** `whatsnew-guard.ps1`'s first run reported 111 of 129
  entries as edited because PowerShell decoded git's stdout with `[Console]::OutputEncoding`.
  Any new guard that reads `git show`/`gh api` output wraps the call in
  `[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)` — and per the tooling note,
  Windows PowerShell 5.1 defeats even that, so a red first run is a host difference until
  `git diff` says otherwise.

#### P0-4 — Final legacy tag and `legacy-v1` branch (LEGACY-005)

- **The bridge tag IS the final legacy tag.** Do not mint a second one; two tags for one
  state is the shape that produced every stale-hold problem in `HELM.md`.
- `git branch legacy-v1 <bridge-tag>` then `git push origin legacy-v1`. Exact name is an
  implementation decision (charter §26); `legacy-v1` is what the charter suggests and there
  is no reason to differ.
- **Order is load-bearing:** the branch exists before the first Avalonia-removal commit
  reaches `main`. LEGACY-005 says so; and once `main` has lost the Avalonia project,
  recreating the branch means resurrecting from a tag under time pressure.
- Protect the tag and the branch through GitHub settings so LEGACY-004 is enforced by the
  platform. **→ Helm** — repo settings are Helm's sequencing call, not a consequence-list
  door (not values, release go, posture, money, roadmap, wiki policy, third-party policy or
  privacy).
- `legacy-v1` is **preserved, not maintained**. No new CI is wired to it. Say that in
  `LEGACY-V1.md` so nobody later reads a green branch as a support promise.

#### PHASE 0 GATE — and how it is actually proved

> A Linux or Mac user on the final bridge build can continue using v1 after v2 ships
> without being asked to install an incompatible Windows build.

"The suite is green" does not prove this. Four proofs, in ascending strength:

1. **Unit** — `LegacyPlatformUpdatePolicyTests`, the full matrix and both negatives.
   Verify by running the new tests against the **pre-fix tree** and confirming the
   major-2 rows fail there. A guard that has never failed has not been shown to guard
   anything (traps 34, 39).
2. **Scanner** — all six call sites route through the policy; a seventh fails the build.
3. **Rendered** — the Avalonia headless harness (`WidgetRenderTests`' constructor pattern,
   isolated `EQBUDDY_APPDATA`) drives the banner from a hand-built `UpdateInfo(2.0.0)`
   twice and asserts: notice, then nothing; and that the string wraps inside 320 px rather
   than widening the root (trap 12).
   → **Known limit, stated rather than papered over:** `UpdateChecker.CheckGitHubAsync`
   reaches a `private const` URL, so the wire fetch itself is not reachable from a test.
   Options are (a) make the URL an `internal` settable field, or (b) test `ParseRelease`
   plus the policy and leave the fetch uncovered — which is where it already is. **Prefer
   (b)**: (a) adds a mutable global to Core to cover an `HttpClient.GetStringAsync` call
   that has never been the defect, and Phase 0 should not widen Core's surface.
4. **Real, and this is the one that counts** — publish the first v2 milestone as a
   **prerelease** and confirm a bridged Linux or macOS client is offered nothing at all;
   then, before `2.0.0` goes latest, confirm on a bridged client against a real major-2
   release that the notice appears once and the click lands on the legacy release page.
   This is the only proof that exercises GitHub's own `releases/latest` semantics, which
   proof 1 assumes.

**And name what the gate does NOT cover, because it is the real residual risk:** the
notice only reaches installs that took the bridge. Anyone still on an older 1.x sees the
generic release page — which is precisely why LEGACY-007 exists, why the `-Prerelease`
switch matters more than the in-app notice, and why the bridge's own What's-new entry is
worth writing well. Do not let a green gate read as "every legacy user is protected".

---

### PHASE 1 — subtract platform debt (**BLOCKED on the Phase 0 gate — do not start**)

Charter §19 Phase 1 and §16.2. Written now so Phase 0 is executed with the destination in
view; **no Phase 1 PR opens until Helm confirms the LEGACY checklist on #275 is complete.**

#### P1-1 — Disposition of the 23 Avalonia test files, BEFORE any deletion

**This is the finding that reorders Phase 1.** `docs/TestPlan.md` §5 records that the WPF
layer has no unit tests. `tests/EQBuddy.Avalonia.Tests` (23 files) is therefore the repo's
**only** place where a widget is rendered and asserted, and `ci.yml` gates the E2E suite on
`workflow_dispatch` — so it is also the only rendering coverage that runs on a push.
Charter §16.2's "remove Avalonia-only tests from v2" is correct as direction and, executed
literally as a deletion, removes the repo's rendering coverage in one commit. That is trap
34 at project scale: what is left reads as coverage.

Deliverable **before** the delete commit: a table, one row per file, each naming where its
assertion went.

- Likely **port to `EQBuddy.Tests`** (already close to pure): `BreakdownRowsTests`,
  `WindowZoomTests`, `HotkeyManagerTests`, `ChipStackTests`, `IconGeometryTests`,
  `UpdateOfferTests`.
- Likely **port to E2E via `EQBUDDY_EXPAND`**: `WidgetRenderTests`, `OptionsRenderTests`,
  `QuestsRenderTests`, `DropsRenderTests`, `InventoryRenderTests`, `HistoryRenderTests`,
  `WikiPackRenderTests`, `ZoneWindowsRenderTests`, `ThemeBodyCapRenderTests`,
  `FightTimelineRenderTests`, `CompanionWiringTests`.
- Likely **accept the loss, with the reason written down**: `ClickThroughTests`,
  `MacOverlayLevel` coverage, and the capture surfaces `IconSheetTests` / `WidgetSheetTests`
  (both opt-in, both photographing the lane being removed).
- `AppThemeTests.EveryCatalogThemeAppliesCleanly` is a **catalog** guard wearing an Avalonia
  coat — it is the thing that would notice a broken palette, and it has no WPF twin. Decide
  it explicitly.

**Prerequisite this exposes: E2E must run in CI before it is asked to carry that load.**
Today it needs a desktop session and is opt-in. Either that changes, or the ported rows are
guards nobody runs — which is worse than deleting them honestly.

#### P1-2 — The 18 shared-suite tests that scan the Avalonia source

`ArchitectureTests`, `ChipStackPlanTests`, `ClassSourceWritersTests`, `CompanionQuestsTests`,
`CompanionSnapshotArgumentTests`, `DesignRatchetTests`, `DesignSystemTests`,
`DocumentationTests`, `FocusHideTests`, `GameCommandsTests`,
`ImportReportReachesASurfaceTests`, `LogJanitorPolicyTests`, `OverlayActivationTests`,
`QuestLedgerClearCountTests`, `QuestReconcileWiringTests`, `SurfaceOwnershipTests`,
`WatchPinMigrationTests`, `WikiRecheckPathTests`.

Every one exists because **two lanes could drift**. With one lane, each becomes either a
single-lane must-list (still valuable — trap 34 and trap 43 are both single-lane guards) or
vacuous. **A scanner that scans one file it will always find is a guard that cannot fail**,
and it will keep passing forever while reading as coverage. One explicit call per file:
keep as a must-list with its reason rewritten, or delete with a reason. Do not let them
narrow silently — that is the failure mode of every guard in the trap list.

Specifically: `ArchitectureTests` carries `EQBuddy.Avalonia/MainWindow.cs, 5229` in the
hotspot table. Removing the row is right; **do not let the WPF row's headroom grow in the
same commit**, or Phase 2's decomposition budget is spent before it starts.
`SurfaceOwnershipTests`' two curated exemptions (Gear & Loot, Kills & Drops) each name the
PR that removes them — those PRs are Phase 2 work (see the sketch) and the exemption list
must not be quietly re-justified as "one lane, so it does not matter". Trap 45's rule is
about ownership, not about Avalonia; it happens to have been *found* by Avalonia.

#### P1-3 — CI and release pipeline

- **`ci.yml`** — delete the `build-avalonia-linux` job and the "Run Avalonia render tests"
  step. Unambiguous: this workflow triggers on `push`/`pull_request` and runs from the
  branch. Do this **after** P1-1, not with it.
- **`release-assets.yml`** — **verify before deciding, and I did not.** The open question is
  which ref supplies the workflow file for a `release: published` event: the release's
  target commit, or the default branch. It decides the action:
  - if the tag's own copy runs → **delete the file** on v2 mainline; legacy tags keep theirs
    and can be re-published forever, satisfying LEGACY-004;
  - if the default branch's copy runs → **do not delete it.** Guard the job instead
    (`if: ${{ !startsWith(github.event.release.tag_name, 'v2') }}` or a check that the
    Avalonia csproj exists), because deleting it means every future legacy re-publish
    silently loses its three assets and every v2 release shows a red X.
  One check settles it: re-publish the bridge release after the removal lands on `main` and
  see whether the assets reattach. Cheap, and the wrong guess is expensive in exactly the
  direction LEGACY-004 forbids.
- **`EQBuddy.slnx`** — drop both Avalonia rows.
- **`scripts/check.ps1`** — drop the `avalonia` stage (line 64); the `-Quick` switch loses
  its reason for existing and should go with it rather than becoming a no-op flag.
- **`scripts/release.ps1`** — `-Prerelease` lands in Phase 0 (P0-1); noted here so the
  pipeline picture is complete.
- **Delete `src/EQBuddy.Avalonia/` and `tests/EQBuddy.Avalonia.Tests/` LAST**, in their own
  commit, after P1-1 and P1-2 have landed. A deletion mixed with a port is a diff nobody
  can review.
- **`DocumentationSizeTests`** measures `src/<project>` line counts against numbers written
  in `docs/Architecture.md`. Removing a project changes those numbers; the doc edit belongs
  in the deletion commit, not in a follow-up.

#### P1-4 — Wine/CrossOver: name the boundary before deleting (**→ Helm**)

**Fact, and it is the opposite of the intuition:** 40 files mention Wine or CrossOver, and
the load-bearing ones are on the **Windows** lane — `src/EQBuddy/WineText.cs`,
`WineFonts.cs`, `WineOverlay.cs`, `TextProbeWindow.cs`,
`src/EQBuddy.UI.Shared/TextRenderingPolicy.cs`, plus `AppSettings.WineFloatOverFullscreen`,
`WineKeepGameFullscreen`, `WineWholePixelText` and `scripts/crossover/`. They exist for
players running **`EQBuddy.exe` under CrossOver on macOS** — traps 40, 41 and 42, which cost
several rounds each. They are not Avalonia and they do not leave with it.

The charter says two things at different widths: UX-010 forbids "Wine/CrossOver-specific
**settings** in the v2 UI"; §16.2 removes "Wine/CrossOver **UI accommodations** from v2
Windows UI".

**Recommendation:** remove the three Options **knobs** — that is what UX-010 names — and
keep `TextRenderingPolicy` + `WineText`. An auto-detecting rendering policy is not a UI
accommodation, it costs one `OverrideMetadata` call at startup, and deleting it re-breaks
trap 41 for people running the **supported Windows artifact**. `WineOverlay`,
`scripts/crossover/` and `MacOverlayLevel` are macOS-overlay work and go with the platform.

**This belongs to Helm, not David**: the charter settled the direction, and this is where
the line falls inside it — scope, which §21.2 gives Helm. It is deliberately not decided
inside a deletion PR, because that is where a boundary question becomes an executor's
guess. If Helm reads it as "all of it goes", the executor deletes all of it; the point is
that it should be a stated ruling.

#### P1-5 — v1 feature inventory and classification

The deliverable is the table; this is the method.

- **Spine:** `docs/FeatureGuide.md`'s 13 `##` sections, `docs/Themes.md`'s seven themes
  (four BUILT), the ~200 `AppSettings` properties, `BreakoutKind`'s six members, and the 22
  WPF window classes in `src/EQBuddy/*Window*`.
- **One row per feature:** name · today's door(s) · v2 domain (Home / Live / Progress /
  Gear / Quests / World / Search / Settings) · class (Keep / Merge / Replace / Advanced /
  Remove) · why · **what writes it**.
- **Every `Remove` and `Merge` row names what stops writing.** Traps 20, 26 and 43 are one
  event told three times — a surface folds, the data survives, the write path does not —
  and this pass is a fold of the entire product. `DeadSettingTests.Known`,
  `ImportReportReachesASurfaceTests` and `GameCommandsTests.SurfacesNeedingACommand` are
  the three existing lists that will catch it, and each needs its rows revisited per moved
  surface rather than at the end.
- **Trap 30 applies to the tooling:** `scripts/shoot.ps1` enumerates `BreakoutKind` by hand
  and `scripts/` matches windows by **title** (trap 53 — three stale titles dark-lit the
  whole batch for six days). Any row that renames or removes a window is a `scripts/` grep
  before it is a code change, and the batch — not a single `-Shot` — is what proves it.
- **Output** goes to repo markdown (charter §21.1) — propose
  `docs/v2/v1-feature-disposition.md` — with per-feature GitHub issues carrying the work
  state.
- **Gate (charter):** no v2 requirement is blocked on non-Windows desktop parity. Provable
  mechanically: the removed CI jobs, plus a disposition table with no row whose blocker is
  a non-Windows desktop.

---

### PHASE 2 — v2 shell and HUD: architecture sketch (for Bevel's parallel staging)

**This is a seam sketch, not a design.** Bevel owns the IA and the visuals and is staging
that in parallel (Helm, 11:50 AM CT). Nothing here should be read as pre-empting it; it
exists so Bevel's design lands on seams that already exist rather than on ones that must be
invented.

#### The shell already half-exists, and it is called the theme program

`docs/Themes.md` names seven themes; four are BUILT (Quests, Progress, World, Kills &
Drops). **A "theme" is exactly a v2 shell page that currently ships as its own window.**
The mapping is close to one-to-one:

| Today | Charter §4.2 domain |
|---|---|
| Quests theme (`QuestsWindow`) | **Quests** |
| Progress theme (`ProgressWindow`: Experience / Wealth / Faction / Raids) | **Progress** |
| World theme (`WorldWindow`: Map / Camps / Path / Travels) | **World** |
| Gear & Loot theme (`GearLootWindow`) | **Gear** |
| Live Meters theme (planned) + `BreakoutKind` + `FightTimelineWindow` | **Live** |
| Kills & Drops theme (`CreatureWindow`, `DropsCardView`) | folds into Live / World per P1-5 — *not* a top-level domain |
| Alerts theme (planned), `OptionsWindow`, `WikiPackWindow` | **Settings** + HUD edit mode |
| `HistoryWindow`, `SessionPickerWindow` | **Progress** / **Live** history |
| — | **Home** and **Search** are the only genuinely new pages |

That is the useful shape of this phase: v2's shell is **two new pages and a navigation host
over four surfaces that already exist as hosted views**, not a rebuild. It also means the
charter's §16.4 ("MainWindow should lose responsibility, not just lines") has already begun
— `QuestChecklistView`, `WorldWindow` and the Progress fold are that work.

#### Four seams — three of them already exist

1. **A surface factory per host.** `MainWindow.NewProgressSurfaces()` and the trap-45 rule:
   *a method that returns a long-lived UI object is a transfer of ownership wearing a
   getter's clothes.* Every shell page builds its own instances; the HUD builds its own.
   `SurfaceOwnershipTests`' two remaining exemptions are removed **by** this phase — the
   shell is the host that makes keeping them impossible.
2. **Framework-free presentation.** `UI.Shared` (94 files) already holds
   `LootPresentation`, `QuestPresentation`, `QuestChecklistLayout`, `DesignTokens`,
   `ChipStyle`, `WidgetMetrics`, `ChipStackAnchor`, `AlertSoundPlan`, `LogJanitorPolicy`,
   `TextRenderingPolicy`. **Charter §16.3's `EQBuddy.Presentation` IS this project.**
   *Recommend not renaming* — §16.3 says the boundary matters more than the namespace, and
   a rename is 94 files of churn plus every doc path `DocumentationTests` resolves, for no
   behavioural gain. One line in `DECISIONS.md` when the item is taken.
3. **The mobile projection is the parity proof, and it survives Phase 1 untouched.**
   `CompanionProjection*` reads the same shared modules the windows do, and
   `SurfaceParityTests` asserts it. That is charter §16.6 already enforced. **The standing
   rule for every shell page: if a page hand-rolls a rule the projection reads from
   `UI.Shared`, that is #210 happening again** — and #210's direction surprised people,
   because it was the *phone* that was ahead.
4. **Navigation — the one genuinely new seam.** Smallest thing that works, per §16.5 (no
   DI container, no message bus, no plugin host): a `ShellPage` enum, a content host, and a
   page factory registry. **Reuse a string grammar that already exists in the wild:**
   `EQBUDDY_EXPAND` already takes `progress:raids` — a `page:room` deep link, on both lanes
   since 2026-08-26. Making that the shell's navigation address means "Guide Me There"
   (UX-005), Search results (SEARCH-002) and HUD click targets all resolve to one
   destination spelling on day one, which is what WORLD-006 asks for.

#### HUD (Surface A)

- **The HUD is `MainWindow` minus what the shell takes.** Target responsibilities after
  Phase 2: log tail wiring, live snapshot, mini/expanded state, chip windows, tray and
  context menu — and **no card rendering**. `ArchitectureTests`' hotspot entry is a glob
  that **sums** its matches, so splitting the file buys nothing; and per the standing rule,
  **the ratchet baseline comes down in the same commit as each move**, or the freed room
  refills.
- **Chip placement (LIVE-008) has three actors, not two.** `MezChipsWindow`,
  `SpawnChipsWindow` and the HUD itself all observe placement, and `UI.Shared/ChipStackAnchor`
  already owns the anchoring (traps 1 and 2 were both paid for here). One "Edit HUD" unlock
  state belongs in `UI.Shared` beside it. **Put the participants in the test names** —
  trap 49 is thirteen green tests agreeing with a bug because the model had two actors and
  the world had three.
- **Trap 12 is the standing HUD constraint.** The HUD is `SizeToContent`, so any
  timer-driven string is a geometry change on an always-on-top window over a fullscreen
  game. Every new readout reserves a fixed width; `UI.Shared/PerfReadout` is the worked
  example.
- **UX-003 (kill the breakout choreography): change defaults, do not delete.**
  `AppSettings.DisabledBreakouts` already gates all six kinds, the Live page becomes the
  detail home, and **David uses the damage breakout**. Trap 30: touching `BreakoutKind`
  means grepping `scripts/` in the same change.

#### What Phase 2 must not do

Rewrite Core for aesthetics (§16.1). Introduce DI, a message bus or a plugin host (§16.5).
Start before Bevel's IA is signed. And the shell's **first** PR is a host with **one**
existing page moved into it — the World fold is the precedent this repo already has (five
PRs, host first), and it is the only shape that keeps a half-finished shell shippable.

#### Phase 2 gate

> A user can find every retained primary feature without the legacy context-menu maze.

Provable against P1-5's disposition table rather than by judgement: every `Keep` and `Merge`
row names a v2 door, and no row's only door is the context menu (UX-002). Once the table
exists that is a test, not an opinion — and it is worth writing as one, because "every
retained feature has a home" is exactly the claim that rots silently.

---

### Out of scope for this item

Phases 3–6. #250. #240 / the 320-cap. #264. #208 (live hold — do not open). Play Console,
signing and production secrets. Product-direction debate — the charter wins (Helm,
11:50 AM CT). Avalonia removal in the session that takes Phase 0. Tagging. Merging.

### Checked — what I actually read

`docs/v2/EQBuddy-v2-Project-Guide-Requirements.md` rev 1.1 in full; `HELM.md` and
`HELM-FEEDBACK.md` newest entries; issue #275 body; `src/EQBuddy.Core/UpdateChecker.cs`
in full; `src/EQBuddy.Avalonia/UpdateOffer.cs` in full; both lanes' update call sites and
`_lastUpdateCheck` declarations; `scripts/release.ps1` in full; `.github/workflows/ci.yml`
and `release-assets.yml` in full; `scripts/check.ps1`'s stage list; `EQBuddy.slnx`; the
banner XAML and the two `NormalRoot` widths; the file lists for
`tests/EQBuddy.Avalonia.Tests` (23), `tests/EQBuddy.E2E` (5) and the 18 shared-suite files
that name `EQBuddy.Avalonia`; `docs/Themes.md`'s theme headings; `docs/FeatureGuide.md`'s
section headings; `src/EQBuddy/*Window*` (22 classes); the Wine/CrossOver file list.

**Hypotheses, labelled as such — none of these were verified:**

1. GitHub's `releases/latest` excludes prereleases. Read from documentation, not observed
   against this repo. P0-1 item 1 depends on it; gate proof 4 settles it.
2. Which ref supplies the workflow file for a `release: published` event (P1-3). **Do not
   act on either branch of this until it is checked** — the wrong guess costs LEGACY-004.
3. The P1-1 port/loss buckets are a first pass over file names and what each file is for.
   The table is the deliverable; these are where to start, not the answer.

### Decided without asking — for `DECISIONS.md` when this is taken

- **The policy lives in `UI.Shared`, not in `UpdateChecker`.** Core stays a data/parse
  layer; "may we show this offer" is presentation. The default could have been Core, which
  would have put a platform rule where the fetch lives.
- **Windows behaviour is byte-identical through Phase 0.** Could have unified the two
  lanes' banner text while in there; deliberately not, to keep the blast radius at a call
  site.
- **Manual "Check for updates" always answers, even when suppressed.** Could have made
  suppression total, which is simpler and is a silent no-op.
- **The legacy browser target is a new pinned constant, not `GitHubLatestPage`.** The
  default was to reuse the existing fallback, and it is wrong the day v2 ships.
- **`ParseRelease` + policy tests, and the wire fetch stays uncovered.** Could have made
  the API URL injectable; that adds mutable global state to Core for a call that has never
  been the defect.
- **`UI.Shared` is not renamed to `EQBuddy.Presentation`.** §16.3 permits either; churn
  outweighs clarity.
- **`legacy-v1` and the bridge tag are the same commit, and there is only one tag.**
- **Recommend keeping `TextRenderingPolicy` while removing the three Wine Options knobs**
  — raised to Helm as a scope question rather than settled here, because it lands on people
  running the supported Windows artifact.

---

## The WORLD theme — Travels & Deaths + Map, Spawns, Travel, ZoneShare become one theme

- **Priority:** **DONE 2026-08-27 — all five PRs executed, merged, staged in 1.99.13.**
  PR 0/1 on PR #242 (execution report: `FABLE-FEEDBACK.md` 2026-08-27, with the
  four-factories deviation flagged for your last-look); Bevel's six pre-design answers plus
  the amendment signed (Helm 2026-08-26 9:06/9:07 PM); PR 2–4 on PR #244 (execution report:
  `CLAUDE-FEEDBACK.md` 2026-08-27 — WorldWindow both lanes, the three standalone windows
  deleted, theme card, phone travel + marker pins). Flipped DONE in place so a grep for
  `ready` cannot find shipped work. Your last-look rides the 1.99.13 release review
  requested in `FABLE-FEEDBACK.md`.
  (was: `ready` for PR 0 and PR 1; PR 2–PR 4 gated on Bevel's pre-design answers. No
  `needs-david:` — the theme choice was David's, 2026-08-27, question tool, over Alerts.)
- **Class:** `V3` — four surfaces × two lanes fold into one window; the heaviest pair in
  the app (`MapWindow`, 1,079 + 1,061 lines) moves onto a host seam; a card-key decision;
  wire additions on the phone; and the trap-45 ownership rule has to be right before the
  first presentation PR ships. Not V1 because no single answer finishes it, and the wrong
  obvious fold (one phone card with tabs) is wrong for a reason you can only see with the
  whole system in view — see the phone decision below.
- **Source:** David in session 2026-08-27 (roadmap direction, consequence list 5, asked and
  answered with the question tool); `docs/Themes.md` theme 6, which already names the tabs;
  the 2026-08-27 measurement entry in `FABLE-FEEDBACK.md`. David's 2026-08-20 note is
  covered by construction: *"Travels and Death should include travel route and zone maps
  too."*
- **Bevel pre-design: YES, and it gates the presentation PRs.** Filed alongside this plan
  (its standing preference — before the design, not after). The six questions are in
  `BEVEL-FEEDBACK.md`; the two that can reshape the architecture are simultaneity (map +
  timers at once dies on the desktop when both are tabs) and the inline table.
- **Shot offline: mostly yes.** Spawns/Routes/Travels tabs stage from the fixture log and
  seeded stores. **The Map tab is the known-hard one** — it needs zone geometry; the mobile
  harness's fixture already feeds real map files through the real projection, so try that
  seam first, and if the desktop shot still needs a live install it stays in the manual
  bucket **and says so** (trap 22/23 — no silent coverage claim). `map-window`,
  `spawn-circles`, `travel-window`, `zone-share` are committed HAND shots embedded in
  README (trap 21): new shots take new names, never those.
- **Column budgets:** the launcher line replaces the `MiscHeader` "N deaths" StatValue in a
  320-wide card header — headline parts only, tabs carry detail (the Progress clipping
  lesson). The tab strip WRAPS (trap 25). Shoot at 100 % and 125 % and once in Solarized.
- **Guards run eight times:** `drag-verify` on the new WorldWindow, and the Avalonia
  expand → pop out → close → expand sequence test.

### What I re-measured (all of Claude's numbers confirmed, none taken on trust)

| Piece | WPF | Avalonia | Note |
|---|---|---|---|
| `MapWindow` | 1,079 | 1,061 | the heavy pair; the real cost of the theme |
| `SpawnsWindow` | 549 (+125 xaml) | 600 | `SpawnsViewModel` already shared in UI.Shared |
| `TravelWindow` | 91 | 105 | trivial, but reaches `MainWindow` directly on WPF |
| `ZoneShareWindow` | 278 | 274 | Core collaborators only — needs NO lift |
| misc card body | ~6 lines + XAML | ~76 (`BuildMiscSection`) | the smallest fold yet |
| Ratchet | 4,613 / 4,214 / cap 4,635 — **22 lines** | 5,402 / 5,229 / cap 5,751 — ~349 | keep-if-it-fits |
| `LogParser` | 933 / 853 / cap 938 — **5 lines** | — | constraint: this plan must not touch it |

**Findings that shape the plan, verified in source:**

1. **The seam WPF needs already exists on the other lane, and it was written to be copied.**
   Avalonia's `IZoneHost` (`ZoneHost.cs`, 9 members) says in its own doc comment that its
   member names *"mirror the WPF MainWindow surface one-for-one"*. I counted WPF
   `MapWindow`'s reach into `main` and it is exactly that member set (`SpawnPoints` ×9,
   `Settings` ×4, `SpawnTimers` ×3, `CurrentZoneName` ×3, `CurrentSnapshot` ×2, and one
   each of `WikiMobResult`, `SpawnOverridesStore`, `SpawnCatalogData`, `EnsureMobLookup`).
   The heavy lift is therefore mostly mechanical: WPF grows the same interface and
   MainWindow satisfies it implicitly.
2. **`SpawnsWindow` needs only `Settings` + `PlayAlertSound`** beyond its shared VM.
   `TravelWindow` needs `ZoneGraph` + `CurrentZoneName` + `Settings` — inside `IZoneHost`.
3. **`ZoneShareWindow` was built for this day** — *"no MainWindow reach-back, so an
   Avalonia port passes the same three objects"* (its own comment). It stays a dialog
   opened from the Map tab, exactly as it opens from the Map window today. It is absorbed
   by the theme in the sense that its DOOR moves; the window itself is untouched.
4. **`AlertSurface` already exists in Core and its `Spawns` tab is CONFIGURATION** — its
   doc: *"what consolidates here is the CONFIGURATION, not the alerting."* So the boundary
   is: **World owns the timer LIST** (the zone list, editable durations, camps); **Alerts
   will own "alert me, at this volume, with this sound"**. The per-named bell picker inside
   `SpawnsWindow` (`SetSound`/`PlayAlertSound`, ~line 450) rides into the Camps tab
   UNCHANGED and is not redesigned here.
5. **Phone:** `map` and `spawns` are first-class on the wire (`CompanionSurfaces.cs:14-15`,
   `CompanionMapSource` 317 lines, trap 38's sticky `HeldMap`). There is **no travel and no
   zone-share**, and `CompanionMapMarker` plots only the 'you' marker and camp pins —
   **session markers (Drop camp marker's output) never reach the phone map.** The phone
   already has a map WRITE path (`CompanionMapAction`: Confirm/Unconfirm/Remove/ResetZone),
   so a marker-drop action has precedent, not a new trust question.
6. **Everything that must re-route, enumerated** (trap 29's lesson taken in advance): four
   cog entries (`MainWindow.xaml:31-38`); `toggleMap`/`toggleSpawns` hotkeys; the
   `EQBUDDY_*` env openers (`MainWindow.xaml.cs:519-530`); the show/hide-all branches
   (`:4368-4380`); the per-tick `_mapWindow.MaybeRefresh()` (`:2531`); `StarDeaths` — the
   only `MiniStats` writer for `"deaths"` (trap 20/26).

### Architecture

**Core — `WorldSurface`** (recipe step 1), the fifth sibling of `ProgressSurface`:
`WorldTab { Map, Camps, Routes, Travels }` with Themes.md's labels — Map · Camps & timers ·
Routes · Travels. Wire keys reuse names that already exist where they exist: `map`,
`spawns`, `travel`, and `misc` for Travels. `TabForKey` aliases: camps/timers → Camps,
routes → Routes, travels/deaths → Travels.

- **`ThemeCardKey = "misc"` — decided deliberately, the thing the ask flagged.** Every
  theme so far kept an absorbed card's key precisely so nobody's card slot moved, and this
  theme absorbs exactly ONE card, so keeping `misc` means there is **no settings migration
  at all** — the step Themes.md calls "where silent data loss lives" simply does not run.
  Renaming to `world` buys an aesthetic and costs a migration. The doc comment on the
  constant names the oddity out loud so nobody "fixes" it later. The card's TITLE becomes
  "World"; `("misc", "Travels & Deaths")` in `OptionsViewModel` becomes `("misc", "World")`
  with the old titles in `AbsorbedTitles` (#219). Icon stays `Location`.
- **`InlineModeFor`, initial table (Bevel may move any row):** Travels = **Full** (the
  current card body — deaths, zones, markers — small player-driven lists); Map, Camps,
  Routes = **Glance**. `DefaultInlineTab = Travels`. Conservative-glance is the ratified
  posture (the Unlocks precedent: a Glance understates and never lies, and promotion later
  costs no migration). A map canvas inline over the game is a Bevel question, not an
  engineering default.
- **`LauncherSummary(zone, zonesVisited, deaths, runningTimers)`** → e.g.
  `Crushbone · 4 zones · 2 deaths · 3 timers`, parts omitted when empty.
  **Counts, never countdowns — in the launcher AND the tab badges.** A countdown in either
  changes measured size every second (trap 12, the #173 keyboard-killer) and would wake
  every phone every second (trap 8). Deadlines belong to the spawn-due chips, which this
  theme does not touch.

**Core — `TravelPlan`** (recipe step 2 for Routes): a pure module over
`ZoneGraph.Distance` — from, destination, hops, the step list, and the two wordings both
`TravelWindow`s currently hand-roll ("You're already there.", the no-route-with-wiki-hint
text). Desktop Routes tab and the phone travel surface read the SAME module; that is the
parity mechanism, per the standing rule.

**PR 1 — the view lifts, both lanes, zero product change.** `MapView`, `SpawnsView`,
`TravelView`, `TravelsView` (the misc card body) extracted per lane; WPF gains `IZoneHost`
verbatim from Avalonia's; views take `IZoneHost` (+ `SpawnsViewModel`), never `MainWindow`.
The three standalone windows become thin hosts of the views, behaviour identical.
`NewWorldSurfaces()` factories on both lanes (trap 45: no host interface may return a
control it did not just create; `SurfaceOwnershipTests`' positive half grows the rows).
E2E pins the CURRENT behaviour before any of it moves.

**PR 2 — `WorldWindow`, both lanes; the standalone windows retire.** `EqSegmentedStrip`
tabs (wraps), per-host surfaces via the factories. `MapWindow`, `SpawnsWindow`,
`TravelWindow` are **deleted** (the Gear & Loot precedent — a window kept "just in case" is
a second definition). `ZoneShareWindow` survives untouched, opened from the Map tab.
Re-route everything in finding 6: hotkey IDs and `EQBUDDY_*` names stay stable and open the
window on the right tab; the four cog entries go **only after** each capability has its new
home in the same PR — including a **Drop marker button on the Travels tab** (the action
must not lose its home for even one release; "X is now Y" lines for all four moves).
WindowZoom: `WorldWindow` gets its own size key; the orphaned `map`/`spawns`/`travel` size
entries are left inert (the `BreakoutKind.Progress` ruling — a migration would be code to
delete a harmless token). **Trap 46, enumerated at move time:** the Map window repaints
per tick (`MaybeRefresh()` every second, forced on zone change) and follows you zone to
zone; the Spawns list ticks; Travel re-routes on zone. The visible tab keeps per-tick
paint; only chrome may throttle.

**PR 3 — the theme card.** `WorldThemeCard` on both lanes (fifth instance of
`ThemeCardView`/`ThemeCardPanel` — the machinery needs nothing new). The misc XAML section
converts to the theme shell (the `ProgressSection` shape); `StarDeaths` moves into the
WorldWindow's Travels tab header with the surface it stars (trap 26 — list every control
of the old card and say where each went); tutorial page, Options row, `AbsorbedTitles`
("Travels & Deaths · Zone map · Travel route · Spawn timers are in here now" — those are
the words someone scans for); glance wordings in UI.Shared so the Avalonia card says the
same sentences.

**PR 4 — the phone.** The REAL parity gap, confirmed: **Travel and markers, not map or
spawns.**
- **`map` and `spawns` stay separate first-class surfaces — decided, and it is the
  opposite of the desktop fold on purpose.** They are the glance-while-camping surfaces
  (first in display order); the map is the one `fills` surface (trap 9 — its container
  contract is unique); and a tablet propped on a desk showing map AND timers at once is
  the product's uncontested ground. Parity is by shared module (steps 1–2), not by
  matching chrome — folding the phone to match the desktop would DELETE simultaneity from
  the surface that has it.
- **New `travel` surface** reading `TravelPlan`; destination picked on the phone via the
  existing input machinery; fingerprint carries zone + destination + route, nothing that
  ticks (trap 8) — and it must NOT ride the map's sticky-payload machinery (trap 38:
  `HeldMap` stays exactly as it is; a route is small enough to travel every time).
- **Session marker pins on the phone map**, and a **drop-marker action**
  (`CompanionMapAction` precedent; a write the desktop already offers; nothing leaves the
  machine). Mirror whatever the map fingerprint does about ages before adding pins —
  hypothesis (b) below.
- **ZoneShare does NOT go to the phone** (trap 35: a share string on the phone's clipboard
  cannot reach the game or a friend usefully, and the import preview is desk work by the
  surface table). Said here so its absence reads as a decision, not a miss.

### Risks

- **Trap 45 is the crash-class risk**, and it is why PR 1 exists as its own PR: every host
  builds its own instances, the sequence test runs per lane, and no accessor returns a
  live control.
- **Ratchet, keep-if-it-fits:** WPF has **22 lines**. PR 1 and PR 2 must be net-negative
  in MainWindow (they remove the openers, two window fields, the show/hide branches and
  the misc body); PR 3's theme-card wiring costs ~80 (the Progress measurement). If the
  sum will not fit under 4,635, the named relief lift goes first in that PR: the
  spawn-cue block (~`:2524`, banner + sound on timers crossing zero) is a sum, not a
  pixel, and belongs in UI.Shared anyway. **Measure at each PR; do not trust this
  paragraph** — the file's own history says these numbers rot.
- **LogParser at 5 lines is a tripwire, not a task.** Deaths, zones, locs and markers are
  all parsed today; nothing here needs a new line type. A PR that finds itself editing
  `LogParser.cs` has taken a wrong turn — stop and re-read the plan.
- **The Alerts boundary** (finding 4): carry the bell config, do not improve it. Scope
  creep here builds half of the next theme inside this one.
- **Trap 20/26 in both directions:** the fold list (finding 6) is the checklist; PR 2 and
  PR 3 each end with "every control of the absorbed surfaces, and where it went" in the
  PR notes. `DeadSettingTests` cannot see a lost WRITER whose setting has other writers —
  `MiniStats["deaths"]` is exactly that shape.
- **Simultaneity is the real product risk**, and it is Bevel's question 1: today a player
  can float the map and the timer list side by side; one window with tabs ends that on the
  desktop. What survives by construction: spawn-due chips (deadlines, overlay) and the
  phone (both at once). If Bevel rules that is not enough, the answer shapes PR 2 and I
  want the ruling before the window is built, not after.
- **Trap 24/21 on shots**, per the Shot line above.

### Verification

- **E2E first, before PR 1 moves anything:** the current openers as facts in
  `EQBUDDY_EXPAND`; then `worldInline`/`worldTab`/`worldTabs`/`worldWindowOpen` mirroring
  the progress keys, asserted from `tests/EQBuddy.E2E`.
- Unit: `WorldSurface` table tests (keys, aliases, absorbed list, inline table);
  `TravelPlan` (route, already-there, no-route wording — with one negative each, trap 39);
  `ThemeHost` is already generic and tested.
- Avalonia: `WidgetRenderTests` twins; the expand → pop out → close → expand sequence for
  World; the sheet capture with its prediction written first.
- `drag-verify` on WorldWindow, eight consecutive green runs; `mobile-harness` drives the
  shipped page through the travel surface and marker pins, before and after, prediction
  first.
- **The check David can do himself:** expand World on the widget and read deaths and zones
  without opening anything; pop it out; hit the map hotkey and land on the Map tab; drop a
  marker from the Travels tab and see it on the phone map.

### Out of scope

The #241 have-counts stub (independent; sequence after World's plan is in motion); the
Alerts theme and any bell/sound redesign; Session history and Data & imports (the two cog
survivors — their homes are a different decision); the all-time stats view; ZoneShare on
the phone (decided above); any change to spawn-due chips, `SpawnChipsWindow` or the
overlay; map feature work (new layers, new geometry); the README manual-shot backlog
beyond what this theme's staging makes free.

### Already shipped (must not be fought)

`ThemeCardView`/`ThemeCardPanel`/`ThemeHost` and the four theme cards; `IWidgetCard`/
`ICardContext` and the per-host factory rule with `SurfaceOwnershipTests`' empty exemption
list; Avalonia's `IZoneHost`; `SpawnsViewModel`; `ZoneShare` and its deviation gate; the
spawn-cycle ledger and respawn suggestions (1.99.12); `CompanionMapSource` and the sticky
`HeldQuests`/`HeldMap` machinery; the keep-if-it-fits ratchet ruling; the moved-surface
"X is now Y" promise.

### Checked

Read this session: `FABLE-FEEDBACK.md`'s plan ask; `ROADMAP.md` in full; `docs/Themes.md`
in full (theme 6 names the tabs); `ProgressSurface.cs`, `AlertSurface.cs`,
`CreatureSurface.cs`/`LootSurface.cs` key precedents, `InlineMode` sites;
`ProgressThemeCard.cs` in full; `ThemeHost.cs`/`ThemeCardView.cs`/`ThemeCardPanel.cs`
sizes and shapes; `ZoneShareWindow.cs` and `TravelWindow.cs` in full; `MapWindow`/
`SpawnsWindow` constructor reach (grep-counted per member); `ZoneHost.cs` in full;
`CompanionSurfaces.cs` in full; `CompanionActions`/`CompanionMapSource`/
`CompanionSections` marker shapes; the misc card in both MainWindows and
`MainWindow.xaml:667-689`; the cog menu, hotkeys, env openers, show/hide branches;
`OptionsViewModel.cs:150-198`; `ArchitectureTests` baselines and live sums (measured, not
recalled). **Hypotheses, labelled:** (a) Avalonia MainWindow has a `PlayAlertSound`
equivalent for the bell preview — verify before widening `IZoneHost`; (b) the map
section's fingerprint already excludes the 'you' marker's age — mirror its mechanism for
marker pins, verify in `CompanionSnapshot`; (c) `MapWindow`'s layer toggles hold no
settings writers that die with the window — the trap-26 list settles it; (d) the mobile
harness's map fixture can seed a desktop Map-tab shot.

### Decided without asking (→ `DECISIONS.md` when taken)

Card key stays `misc`, card title becomes "World"; phone keeps map + spawns separate and
gains travel + marker pins; ZoneShare stays a desktop dialog and is not ported;
the three standalone windows are deleted, their size keys left inert; badges and launcher
carry counts, never countdowns; initial inline table Travels-Full/rest-Glance with Travels
as default tab; the per-named bell config rides into Camps unchanged.

— Fable 5, 2026-08-27

---

## Quest have-counts: reconcile the ledger with the inventory dump the game already writes (#241)

- **Priority:** **DONE 2026-08-28 — all three PRs executed, merged, staged in 1.99.14.**
  PR 1–2 on PR #248 (Helm-authorized 4b33824; execution report `FABLE-FEEDBACK.md`
  2026-08-27, incl. the `For()` copy bug its own tests caught pre-commit); PR 3 on PR #249
  (Bevel-signed provenance sentence; Helm last-looked and merged). The v1.99.14 release
  review (`FABLE-FEEDBACK.md` 2026-08-28) found and fixed one defect the PRs left: the
  windows' hand-rolled right-click-clear was Verified-blind — the arithmetic is
  `QuestLedgerStore.ClearCount` now, scan-guarded. Flipped DONE in place so a grep for
  `ready` cannot find shipped work (third time this shape has needed it — the flip belongs
  in the executor's merge commit, not the next reader's pass).
  (was: `ready`, Helm-gated on plan last-look; PR 3 gated on Bevel pre-design — both
  gates were satisfied and all three PRs merged before this line was updated.)
- **Class:** `V2`, confirmed — the fix is to change which STORE is authoritative for a number
  on four surfaces (Quest Tracker, Sky, Epic, phone), and the wrong obvious fix (overlay the
  dump at each read site) is wrong for a trap-33 reason you only see with all the readers in
  view. Not V1 because no single answer from David finishes it; the five sub-questions below
  are answered as design decisions instead.
- **Source:** #241 DasGud, 2026-08-26 7:40 PM CT ("Showing I have 4 Sphinx Claws but
  unfortunately I have none… one Mithril Bands when I have zero and 15 Izah runes instead of
  my 17"). Claude's stub (2026-08-27) and the `SCRIBE-FEEDBACK.md` trace, both verified here.
  Nothing posted, no promise made to him.
- **Bevel pre-design: YES, and it gates PR 3 only.** PR 1–2 change numbers, not sentences.
  The questions for Bevel (executor files them verbatim in `BEVEL-FEEDBACK.md` when taking
  the item — this session could not, per the planning-only scope): (1) what the have-count
  MEANS now that it has two possible sources, and whether the detail pane says which one it
  used ("verified from your inventory dump, 2h ago" vs "log tally — EQBuddy can't see
  hand-ins"); (2) whether the no-dump state gets a nudge toward `/outputfile inventory` on
  the Turn-ins section, and where; (3) whether the phone's quest detail needs the same
  provenance sentence, or corrected numbers are enough there.
- **Shot offline: yes, with staging.** Seed a `<Character>_<server>-Inventory.txt` beside the
  fixture's Logs PARENT (where `InventoryFile.FindLatest` looks) and an announcement line in
  the fixture log. Predict the counts BEFORE shooting (trap 23) — the whole point of the shot
  is three numbers whose values the plan dictates. Hypothesis (c) below if the harness can't
  reach the folder.
- **Column budgets:** the detail-pane provenance line is an `IconLine` (a Grid, wraps — trap
  14 already paid for); the import Summary clause rides the existing Gear-card report line on
  a 320-wide card — keep it to the shape "· quest counts trued (3)", no item names.
- **Guards run eight times:** the new store tests are deterministic (tests call `Flush()`,
  never the debounced `Save()`), but the rule is the rule — eight consecutive green runs on
  `QuestLedgerStoreTests` before PR 1 is done.

### What I verified in source (every stub claim re-checked; two new findings)

1. **The stub's mechanism is right.** Have = `Entry.Total = max(0, Looted + Manual −
   Consumed)` (`QuestLedgerStore.cs:33`); `Consumed` is written only for crafts/merges,
   destroys, and named vendor sales (`SessionStats.cs:905–935`); hand-ins are invisible.
   `QuestMatcher` and the ledger contain no reference to inventory.
2. **The window already contains BOTH answers, one tab apart** — the stub missed this, and it
   sharpens the ask. The Quests window's **held** tab computes `QuestItemProgress` from
   `LatestInventory()` = dump + net log since (`InventoryFile.Snapshot.WithChanges`,
   `QuestsWindow.xaml.cs:688–726`, Avalonia twin `:1685`). The **mine/zone/all** views, the
   detail pane's Turn-ins rows, and the phone (`CompanionQuestRequest.Owned` is the ledger
   slice) all read the ledger. DasGud's numbers are the ledger's; the correct ones were on
   the next tab. Trap 4's one-fact-two-sources, live on a shipped surface.
3. **Stub question 5 is ANSWERED, and it is a confirmed defect, not a hypothesis.** A Sky
   Test turn-in — from the Sky tab's button AND from the quest detail's toggle, both lanes —
   routes to `SkyCompleteToggle.MarkTurnedIn` (`QuestsWindow.xaml.cs:1342–1358`, Avalonia
   `:2241–2253`), which writes `SkyQuestCompleted` + item `Acquired` flags and **never
   touches the quest ledger**. `RecordCompletion` — the path that consumes — is reachable
   only for non-Sky quests. So the ✔ click that `Consumed`'s own doc comment promises
   ("that stays the ✔ click") **does not exist for Sky Tests.** DasGud's 4→0 and 1→0 are
   this, mechanically; the 15→17 is off-log acquisition, which only a dump can see.
4. **The reconcile seam already runs end-to-end.** `LogParser.cs:184` parses
   `Outputfile Complete:` → `OutputfileEvent` → `SessionStats.cs:841` forwards →
   both MainWindows already auto-import the dump for gear (`OnOutputfileWritten`, WPF
   `:3016`, Avalonia `:4408`), with a `GearInventoryAppliedStamp` idempotence precedent.
   The launch replay re-offers announcements chronologically every start — the exact
   machinery a replay-safe reconcile needs is already exercised daily.
5. **Absence in a dump means zero.** The dump enumerates every slot — worn, bags, bank —
   with "Empty" rows for gaps (`InventoryFile` doc + `Entry.Location`), sums stacks through
   `BaseItemName`. So "the dump does not list Sphinx Claw" is the game saying he holds none.
6. **The quest is real catalog data.** "Beastlord Sky Test: Windhowl/Spirit Render" is a
   `SkyTestSplit` quest; its four turn-ins including Sphinx Claw, Mithril Bands and Wind
   Rune Izah are rows sky-023…026 in `SkyQuestDefaults.cs:42–45`. The turn-in list is not
   disputed and is not wiki-data — per Helm, this item never touches the wiki.

### Architecture

**Reconcile the STORE, not the readers (trap 33: one value, one builder).** Overlaying the
dump at each read site means changing the mine view, the zone view, the detail pane, the
widget card and the phone projection — five chances to miss one, and the phone would need a
wire change. Writing the truth into `QuestLedgerStore` corrects every reader at once,
including EQBuddy Mobile with zero wire change.

**PR 1 — Core: `ReconcileInventory`.** `Entry` gains `Verified int` + `VerifiedAt DateTime`
(old files parse; no `CountingRulesVersion` bump — the reset machinery is for wrong log
counters, and these start at zero). `Total = max(0, Verified + Looted + Manual − Consumed)`.
New `QuestLedgerStore.ReconcileInventory(characterKey, counts, writtenAt)`:

- **No-op** when `writtenAt` ≤ the per-character `LastInventoryReconcile` watermark (any
  trigger is idempotent — replay, relaunch, manual refresh), and when `counts` is empty
  (the `SetUnlockedClasses` bad-parse precedent: an empty dump must not erase knowledge).
- For the union of (dump items the `TrackFilter` admits) ∪ (the character's existing
  entries): `Verified` = dump count (0 when absent), `Looted = Consumed = Manual = 0`,
  `VerifiedAt = writtenAt`, `LastTime = max(LastTime, writtenAt)` so replayed pre-dump
  events bounce.
- **`Manual` is deliberately superseded** — the dump is the game's own statement of
  possession, strictly better information than "I already had this"; the +1/right-click
  affordances still work afterward, on top of the verified base.
- **Clamps move or the fix breaks the ✔:** `SetManual`'s floor and `RecordCompletion`'s
  decrement floor are `-Looted` today; post-reconcile `Looted` is 0, so a hand-in could
  never lower a verified count. Both become `-(Verified + Looted)`, with the failing case
  as a test.

**Where it runs: inside the ingest, at `case OutputfileEvent`, in log order** — not in the
UI-thread handler. The `BeginInvoke` hop would let a loot line seconds after the dump land
first and be zeroed; in ingest order, pre-dump loot lands and is squared, post-dump loot
lands after and survives, and the launch replay reproduces the identical sequence. Gated on
`!StoresSuppressed` (#74 review replay). SessionStats gets an injected
`Func<string, InventoryFile.Snapshot?>` resolver wired by both lanes beside `QuestStore`,
with a source-scan test in the `CompanionSnapshotArgumentTests` shape so a lane cannot miss
the wiring (trap 20's family). File I/O on the ingest thread is bounded: announcements are
rare, the watermark short-circuits stale ones, and a half-written file must not kill the
tail (the existing handler's catch pattern).

**Report and undo (trap 43): a count that changes off-screen is still a change to the
player's data.** `AutoImportOutcome` gains `QuestCountsTrued`; the existing inventory
`Summary` line gains the short clause; the existing `Undo` also restores the pre-reconcile
entries. `ImportReportReachesASurfaceTests` gets its row — that must-list exists precisely
for this.

**PR 2 — the ✔ that was promised.** `SkyCompleteToggle.MarkTurnedIn` gains the ledger, the
character key and the turn-in needs, and consumes the split quest's items — the same list
`RecordCompletion` would have used. `Reopen` restores nothing (the mirror of "reopen does
not untick items": a mis-click costs one click, and the next dump re-trues regardless).
**The achievements import deliberately does NOT consume** — it is a statement about
history, possibly predating the ledger, and consuming there would over-subtract a player
re-farming the same items. The Epic toggle gets the same treatment if hypothesis (b)
survives contact.

**PR 3 — provenance, Bevel-gated.** The detail pane's status area says which source fed the
counts and how old the anchor is; the no-dump state names the command with a ⧉ copy — which
makes the detail pane a **`GameCommandsTests.SurfacesNeedingACommand` row** (trap 34: the
must-list, not just the no-literal rule). Phone numbers correct themselves for free; if
Bevel wants the sentence there too, it rides the wire (trap 32 — no page-side literal;
`CompanionCommandPrompt` is the precedent, and trap 35 says selectable text + "on your PC",
never a dead copy button).

### The stub's five questions, answered as decisions

1. **Precedence:** the dump overrides, at its write time, for every admitted item —
   present = its count, absent = zero. The log continues from that anchor.
2. **Staleness:** solved structurally, no cutoff rule. The reconcile applies at the dump's
   position in the log TIMELINE, so a three-week-old dump cannot override this week's loot —
   post-dump events land after it by construction. The surface states the anchor's age
   (PR 3); honesty bar met by saying, not by refusing.
3. **Bags vs bank:** total possession counts, as the held tab already counts it. The
   "could I hand it in right now" split is real but separable — `Entry.Location` supports
   it later; a someday line, not this plan.
4. **No dump:** nothing changes. `Verified` is never set, `Total` reduces to today's
   formula exactly; PR 3's nudge is additive. The common case cannot get worse.
5. **Two completion paths:** the explicit turn-in click consumes (PR 2 — the missing
   half); catch-up `SetCompleted` stays non-consuming on purpose (items long gone); the
   achievements import stays non-consuming on purpose (history, not now). Every path is
   squared by the next dump, which is what makes the asymmetry safe to keep.

### Risks

- **The one place a user statement is machine-overwritten:** a Manual count meaning "my
  mule holds two" is zeroed by a dump that truthfully says THIS character holds none. Dump
  wins — the ledger is per-character and a turn-in needs the items on this character — and
  the cost is one +1 click. The alternative (preserve Manual when absent) would have left
  DasGud's numbers wrong forever had he typed them in. Named in `DECISIONS.md`.
- **A partially-written or truncated dump is undetectable** (`ParseEntries` skips bad rows
  silently). Empty is ignored; partial is accepted and the next dump fixes it; the game
  announces only after "Outputfile Complete", which bounds the window.
- **Ratchet, keep-if-it-fits:** the MainWindow cost is a few wiring lines per lane, but
  Helm's 1.99.13 ruling stands — **the next loop that touches `MainWindow.xaml.cs` takes
  the spawn-cue lift FIRST.** That loop is this one, unless the World PRs already spent it.
  Measure at the PR; do not trust this paragraph.
- **Trap 8/12:** `VerifiedAt` ages never enter fingerprints or fixed-width chrome. The
  quests fingerprint already carries `Total`s (`QuestsWindow.xaml.cs:546`), so a reconcile
  wakes devices exactly once, correctly.
- **Double-subtraction is impossible by construction** — a ✔ consume followed by a dump
  reconcile overwrites rather than accumulates — but the TEST for it still gets written.
- **LogParser is not touched.** The announcement regex has existed since 2026-08-20. A PR
  that finds itself editing `LogParser.cs` has taken a wrong turn — stop and re-read.

### Decomposition

- **PR 1 — Core:** `Verified`/`VerifiedAt`, `ReconcileInventory` + watermark, ingest
  wiring + resolver seam + scan test, clamp updates, outcome/undo/summary plumbing,
  `QuestLedgerStoreTests` (below). No sentence changes. Every surface's numbers true up.
- **PR 2 — consumption:** `SkyCompleteToggle` (+ Epic twin per hypothesis (b)), all four
  call sites via the one helper, tests. `docs/TestPlan.md` rows for both PRs.
- **PR 3 — provenance (Bevel-gated):** detail-pane source line + no-dump ⧉ nudge, both
  lanes; the `SurfacesNeedingACommand` row; staged shot with predicted counts; phone
  sentence only if Bevel pulls it in. What's-new entry crediting **DasGud, #241** ships
  with whichever release carries PR 1 — the counts moving IS the player-noticeable change.

### Verification

- **DasGud's triple as a unit test, verbatim:** ledger looted {Sphinx Claw 4, Mithril
  Bands 1, Wind Rune Izah 15}; dump {Izah: 17}; after reconcile → 0 / 0 / 17. If this
  test's name does not mention #241, write it again.
- **Order and replay:** pre-dump loot squared, post-dump loot survives; the identical
  event sequence re-offered (relaunch replay) changes nothing; an older dump than the
  watermark is a no-op; empty dump is a no-op; `StoresSuppressed` suppresses.
- **Clamps:** a ✔ after a reconcile lowers Total (fails on today's clamp — run it against
  the pre-fix tree, trap 34's lesson); `Reopen` restores nothing (the negative, trap 39).
- **Seams:** both-lanes resolver scan test; `ImportReportReachesASurfaceTests` row;
  `GameCommandsTests.SurfacesNeedingACommand` row (PR 3).
- **The check David can do himself, at level 29, no endgame needed:** loot any quest item,
  sell one copy, run `/outputfile inventory`, and watch the tracker's count match his bags
  — then type a manual +1 and watch it survive. This item is UNUSUAL in being fully
  David-verifiable; say so in the release review request.
- Reporter confirmation: after release, ask DasGud to re-dump and read the three numbers.

### Out of scope

#243's leftover-item audit (parked, Helm, different ask — no fold); anything wiki-facing
(this is the "about the player, not the world" side of the line — no pack, no edit link);
the bags-vs-bank "hand it in right now" split; prompting/reminding players to dump on a
schedule; Epic checklist redesign beyond the consumption-gap check; any change to the held
tab or `ItemsGainedSince`; #208; the phone provenance sentence unless Bevel asks.

### Already shipped (must not be fought)

`OutputfileAutoImport` and its announcement seam; `InventoryFile` + `WithChanges` + the
held tab; `SkyTestSplit.WithTurnIns` and its one-store-per-fact rule (this plan keeps
completion state in `SkyQuestCompleted` and possession in the ledger — two facts, not two
sources); `SkyCompleteToggle`/`EpicCompleteToggle` and their restored capability;
`GearInventoryAppliedStamp`; the ledger's debounced save and per-item high-water replay
design; the `ImportReportReachesASurfaceTests` must-list; #74's review-replay suppression.

### Checked

Read this session: `QuestLedgerStore.cs`, `QuestMatcher.cs`, `InventoryFile.cs`,
`OutputfileAutoImport.cs`, `SkyCompleteToggle.cs` in full; `SkyTestSplit.cs`;
`SessionStats.cs` ingest (`:905–935` consumed sites, `:841` OutputfileEvent, `:473`
forward); `LogParser.cs:179–184, :752`; both `QuestsWindow`s' held/mine/detail/
`ToggleCompleted` paths; both MainWindows' `OnOutputfileWritten` and `LatestInventory`;
`CompanionQuestSource.cs` in full; `SkyQuestDefaults.cs:42–45`; the `SCRIBE-FEEDBACK.md`
trace; `HELM.md`'s 2026-08-27 ruling. **Hypotheses, labelled:** (a) the rewardKey →
split-quest turn-in needs (with quantities) are reachable from `SkyQuestDefaults`/
`QuestChecklistLayout` without a catalog instance in hand — verify before widening
`MarkTurnedIn`'s signature; (b) the Epic toggle has the same non-consuming gap AND epic
catalog quests surface ledger have-counts — check both before mirroring PR 2; (c) the
shoot fixture can stage a dump beside its Logs parent — if not, PR 3's shot goes in the
manual bucket and says so; (d) the held tab needs no change and agrees with the ledger
near dump time — confirm once PR 1 is in, as its own sanity check.

### Decided without asking (→ `DECISIONS.md` when taken)

Dump supersedes Manual for admitted items; absence in a dump means zero; reconcile runs in
the ingest in log order, never on the UI hop; achievements import never consumes; catch-up
marking never consumes; total possession counts (bank included); `Verified`/`VerifiedAt`
are new fields, not a counting-rules bump; empty dumps are ignored; the reconcile is
undoable through the existing import outcome.

— Fable 5, 2026-08-27 (from Claude's 2026-08-27 stub, whose mechanism survived
re-verification intact; findings 2 and 3 are new)

---

## README screenshots: 13 of 24 still cannot be regenerated, and `history-progress` needs real staging
To: Fable

- **Priority:** `ready`. No `needs-david:` — he already chose "add shots for the easy ones"
  (2026-08-24, question tool) and the easy ones are done.
- **Class:** V1 for most of it; the one genuinely hard shot is scoped below.
- **Fable's ruling (2026-08-24): ALL of this is V1, including the `Prime` per-run-content
  work — no decision in it is outside the executor's, and if David answered any one question
  it would still be the same harness change. It stays listed here only as the work list;
  take pieces in any V1 loop, no plan needed. The one design note worth carrying:
  `Prime`'s enhancement should take content per invocation (a lines block or a file path),
  not a fraction — the fraction model is WHY appended content was unreachable.**
- **Source:** the 2026-08-24 doc audit. README embedded `gear-locker.png` (a standalone
  window deleted in the 2026-08-21 fold) and `sky-quest.png` (a widget card replaced on
  2026-08-16), plus `widget-compact.png` — a **v1.51-era** capture with an "Update v1.51.0 is
  ready" banner in it, showing Kills, Loot, Sky Quest, Money and Faction as separate cards.

### What is already done

Six README images were repointed at shots `shoot.ps1` already produces, which is cheaper than
adding duplicates: compact widget → `widget-cards`, Sky → `sky-checklist`, mini → `mini-bar`,
breakouts → `damage-breakout`, options → `options-mez`, and the freed row → `raids-card`.
Regenerable coverage went **5/24 → 11/24**. The three stale originals are still on disk,
unreferenced, deliberately not deleted — a live Reddit post may hotlink them.

### `history-progress` — attempted, reverted, and here is why

I built `EQBUDDY_HISTORY=charts` (open History with nothing selected) plus a
`SelectFirstCharacterFilter` helper, shot it twice, and **reverted the lot** rather than ship a
hook with no working shot (trap 43's shape). Both captures were empty-state pictures, which is
exactly the thing trap 22 says reads as reviewed anyway.

**The preconditions, which are in `HistoryWindow.RenderProgress` and were written down
nowhere** — this is the useful part of the attempt:

1. `_vm.FilterIsSingleCharacter` must be true. "All characters" collapses the charts entirely
   ("would braid unrelated ladders"), so the capture must select a character in `CharFilter`.
2. **No session may be selected** — picking one replaces the charts with the detail pane.
3. There must be `dings` or cumulative AA to plot, **across more than one session**.

**DONE 2026-08-24 (Claude) — Route B worked on the first try, and your correction is why.**
My "same log PATH = same archived row" was wrong; `Checkpoint` adopts on
`(Server, Character, StartUtc)`. Verified in `SessionRepository.cs` before building on it —
the three Prime runs collapsed because they sliced one fixture and shared a first timestamp,
not because the filename repeated.

`Prime` gained **`ShiftDays`** (re-stamps the slice into its own session window) alongside
**`Lines`** (per-run content, appended INSIDE that window rather than onto a shared tail —
your design note with the flaw you named removed). `EQBUDDY_HISTORY=charts` +
`SelectFirstCharacterFilter` reach the only state the charts draw in. **Fully real ingest, no
seam: Route A was not needed.**

Prediction written first and met: 846×553, filter `Aludra (test)`, three sessions dated
Aug 21/22/23, and **"Level 22 → 24 (Aug 21–Aug 23, 3 dings)"**. One miss worth recording —
I predicted only the level chart and the panel draws TWO; the AA chart was empty because my
slices carried no ability points. Each run now also stages an AA total, so it reads **"AA
earned, cumulative — 9 total"**, which is what README's caption has always promised.
`history-progress.png` is retired in favour of the regenerable `history-charts`.

**README regenerable coverage: 5/24 → 12/24 → 15/24 (2026-08-26, Claude).** Three more
landed in one pass: `options-behavior` (a tab-key `Set` like its siblings — it was always
free), `fight-timeline` (the `EQBUDDY_TIMELINE` hook existed since drag-verify; the fixture's
own fights render), and `session-picker` (new `ReviewSessions` staging: the pristine fixture
concatenated with day-shifted copies, built OUTSIDE the Logs folder so the tail can never
adopt it, fed through `EQBUDDY_REVIEW`). Predictions written first; one deviation each run
explained (the picker shows recent dates because the fixture is time-shifted to now).
The nine left genuinely need a live zone, a phone viewport or an alert in flight:
`cursor-ring`, `feedback-and-alert`, `map-window`, `mobile-map-phone`, `mobile-map-tablet`,
`spawn-circles`, `travel-window`, `widget-seethrough`, `zone-share`.

**Update 2026-08-27 (Claude): three of those nine got worse, not better.** World PR 2
deleted `MapWindow`, `SpawnsWindow` and `TravelWindow`, so `map-window.png`,
`spawn-circles.png` and `travel-window.png` now depict retired windows — the
`gear-locker.png` class of stale, not slow rot — the moment 1.99.13 ships. The surfaces
live on as WorldWindow tabs, so replacements are WorldWindow shots (still needing a live
zone for the Map tab; Camps/Path may stage from the fixture). `zone-share.png` is still
real — `ZoneShareWindow` survived the fold untouched, its door now on the Map tab.

(3) is what defeated it. `Prime` builds its extra log from a **prefix** of the fixture
(`$lines[0..take]`), and `Append-Log` appends to the END, so an appended "Welcome to level N!"
is unreachable at any fraction below 1.0 — and repeated same-character primes gave one session,
not three. Staging this needs per-prime-run log content, which `Prime` does not currently
support. **That is the actual work item, and it is bigger than a shot.**

**Update 2026-09-02 (Claude): the three that "got worse" are addressed, and the batch that
takes all of them was BROKEN and nobody knew.** `spawns-window`, `spawns-sky` and `zone-map`
still matched on the titles of the windows World PR 2 deleted, so `shoot.ps1` with no `-Shot`
died at shot 37 and the 23 shots after it were unreachable in a batch from 1.99.13 to
2026-09-02 — new trap 53. Titles now match `EQBuddy World`; all three re-shot and committed
(`spawns-window` is the World ▸ Camps tab at Runnyeye, prediction written first and held).
`zone-map` re-shot honestly as the **no-maps-folder empty state**, because the throwaway
profile has no maps folder — a real state, and NOT a replacement for `map-window.png`.
README's two un-regenerable World captures were re-captioned in the repo's own "X is now Y"
form (Zone map → World ▸ Map, Travel route → World ▸ Path, plus the Camps and Map-tab
pointers on the spawn-timer, spawn-circle and zone-share cells), each saying plainly that the
capture predates the fold. Logged in `DECISIONS.md`.

**Still open on this item, and now scoped rather than guessed:**
- `map-window` / `spawn-circles` need a **maps folder** in the throwaway profile.
  `ZoneMap` looks for `<game>/maps` beside `Logs` (`ZoneMap.cs:164-171`), and David's install
  has one (214 files). A `Maps = @('commons')` staging block in `shoot.ps1` that copies from
  the real install when present and skips with a note when absent would make both
  regenerable — on a machine with the game installed, which `shoot.ps1` already requires in
  spirit. The `/loc` marker needs one appended `Your Location` line; the fixture has none.
- `travel-window` needs `EQBUDDY_TRAVEL` to take a DESTINATION zone the way `EQBUDDY_SPAWNS`
  already takes a zone; `TravelView` routes only on a typed destination plus a click and has
  no settings-backed one. **Check `ArchitectureTests.Hotspots` first** — `MainWindow` had 16
  lines of headroom on 2026-09-02.
- Unrelated finding while reading `TravelView.cs:74`: the route list draws a 📍 **emoji**,
  not an `IconPaths` vector (#148/#166). Not touched; not this item.

### The other 12, for whoever picks this up

`cursor-ring`, `feedback-and-alert`, `fight-timeline`, `map-window`, `mobile-map-phone`,
`mobile-map-tablet`, `options-behavior`, `session-picker`, `spawn-circles`, `travel-window`,
`widget-seethrough`, `zone-share`. `options-behavior` and `session-picker` look closest to
free — both are ordinary windows the existing hooks nearly reach. The map/mobile/travel ones
need a live zone or a phone viewport and are their own item.

**None of these is urgent**: unlike `gear-locker` and `sky-quest`, every one of them depicts a
surface that still exists. The risk is slow rot, not a lie on the page today.

— Dranak (Claude Code)

---

## Window height follows content — CLOSED 2026-08-25 (Fable): shipped in 1.99.11's NC-grab design, harness-verified

The V2 item is done, by a third design neither candidate anticipated: 5b0f331 retired the
`ContentRendered` pin and takes ownership at `WM_NCLBUTTONDOWN` on a resize border — the
player's actual grab, so no attribution rule is needed at all — with persistence gated on
`SizeToContent == Manual`. The full acceptance ran green on `scripts/drag-verify.ps1`
(2026-08-25): opens at content (218, not the 203 pin), follows before any drag, undragged
close persists nothing, a REAL border drag takes ownership and sticks, tab switches stop
resizing once owned, the height persists (296), restores, and ownership survives restart;
the new History caller passes the same drag/persist/restore. Both probe questions are moot
— the design never interprets `SizeChanged`. Residual: Item info stays excluded by a
mechanism reason (`ResizableWindowTests.NotResizable`), with a staleness tell that fails
the day it stops fetching async.

---

## A way for players to feed VERIFIED spawn-timer updates to eqlwiki

- **Priority:** **DONE 2026-08-26 (Claude), staged in 1.99.12 — all three PRs, executed to
  the plan with two deviations worth your last-look.** PR 0: `respawn-diff.py` ran live and
  its first report is committed — all 3 trusted Crushbone timers are MISSING from the wiki,
  paste-ready. PR 1: `SpawnCycleLedger` (`spawn-cycles.json`, own file own lock per trap 13),
  written at the three `SpawnTimers` learn points so the honesty gates apply by construction;
  `EqlWikiMobs.Parse` reads `respawn_time` into `MobInfo.RespawnField`, raw. PR 2:
  `RespawnSuggestion` with your exact bar (3 / ±15 % / ≥90 s), the three-way compare, the
  pack row (`RowKind.RespawnObserved`) and export block, both lanes, staged shot predicted
  first and matched. **Deviation 1: `RespawnSuggestion` lives in Core, not UI.Shared** —
  `BuildExport` (Core) must read the verdict and Core cannot reference UI.Shared; same
  framework-free testability either way. **Deviation 2: the ledger records honest gaps the
  never-loosens rule REJECTS for the countdown** — your "the three places a gap is accepted"
  wording, read literally, would mean a perfectly stable timer (12:04 against a learned
  12:03) could never accumulate three cycles; the honesty gates are the write condition, the
  tightening rule is not, and the test names the case. `RespawnSuggestionTests` (16, incl.
  end-to-end through real log lines: triggered never records, cross-stay never records).
- **Class:** was `V2`; the plan carried it. The rest of this item is the plan as executed.
- **Source:** Scribe's spawn-timer mega-thread item (long-standing, community ask: catalogs lag
  and kill-to-kill does not determine a duration), redirected by David.

### What he rejected, and why it matters to the shape

A mega-thread we host would be a **second source of truth competing with eqlwiki** — the
one-fact-two-sources problem, in public, with us maintaining it forever. The standing rule is
that eqlwiki is the tie-breaker and a correction there helps every player and every other tool.
So the answer is not a place to collect timers; it is a **path from a player's own observations
into the wiki**, which is exactly the shape the loot contribution pack already has.

### The hard part, stated plainly

**Scribe's own argument against catalogs is also the argument against us suggesting timers:
kill-to-kill does not determine a respawn duration.** A gap between two kills is an upper
bound, and only sometimes that — the mob may have been up for an hour before anyone looked.
And CLAUDE.md is unambiguous: *"a wrong respawn timer is worse than none"*, and curated timers
are never auto-written.

So the plan's real question is: **what evidence justifies suggesting a respawn_time to the
wiki?** The loot pack already has an answer to the analogous question worth copying — the
10-kill bar, and "no label at all when the sample is thin". Candidate sub-questions:

1. How many corroborating cycles before a duration is suggestible, and must they agree within
   what tolerance? `SpawnOverride.Learned` already exists and already refuses some cases.
2. Does an observation from an INSTANCE ever count? (#109 says no for the timer; the wiki page
   may be about the open-world spawn.)
3. Triggered and raid-instanced entries have no cycle at all — they must never be suggested,
   and today's `IsTriggered` / `RaidInstanced` fences are the existing machinery for that.
4. Is this a new pack, or a section inside the existing contribution pack? `PageSkeleton`
   already emits `| respawn_time  = ` as an empty field, which is a strong hint.

### Already shipped (must not be fought)

The contribution pack and its honesty rules; `SuggestRarity`'s thin-sample refusal; the
triggered/raid-instanced suppression; `SpawnOverrides` and learned durations; the fact that the
pack never publishes anything itself — the player opens the edit link, reviews, and saves.

### Plan — Fable 5, 2026-08-22

**Shape: a RESPAWN section of the existing contribution pack, fed by a new per-named cycle
ledger, gated by an agreement bar that plays the role the 10-kill bar plays for rarity.** Not
a new surface, not a timer-submission tool — the pack pattern applied to a second kind of
world fact, which is what David's rule says to try first. `needs-david:` none: he asked for
the path; the honesty bar is design.

#### What I read, and what it changes

1. **The wiki's idiom is a free-text field per creature, and it is sparse.** `{{Namedmobpage}}`
   carries `| respawn_time = 9.5 min` (A frenzied ghoul), `Triggered` (the bees), and
   NOTHING on Eldrig the Old and Lockjaw — the field is absent, not empty. There is also a
   hand-kept `Respawn Timers` list page ("[[Lord Nagafen]] - 3 days with 12 hour variance",
   "Noble Dojorn - 5 days?") that the catalog's own notes cite. **The paste target is the
   creature's field**, in the wiki's own words ("22 min", "6 hours", "3 days with 12 hour
   variance"); the list page is a second place for one fact and is out of scope.
2. **We do not hold a SAMPLE today, only the tightest value.** `SpawnOverride.RespawnSeconds`
   is the minimum gap ever accepted, with `Learned`/`Sighted`/`Imported` flags — one number, no
   count, no spread. `SuggestRarity` can say "10+ kills" because `MobSummary.Kills` exists; the
   spawn side has no equivalent. The item is therefore mostly about recording evidence, and
   only then about suggesting.
3. **The engine already knows which observations are honest.** `LearnFromRekill` accepts a gap
   only when the named's own death started the clock, the player never left the zone
   (`NeverLeftSince`), the gap is ≥ 90 s, and the entry is not trusted/multi-spawn;
   `LearnFromSighting` only inside the final fifth; both refuse triggered and raid-instanced
   entries before learning; `_currentZoneInstanced && zone.RaidZone` gates instances. Those
   gates ARE the definition of a countable cycle. The ledger records exactly where they pass.
4. **The catalog already holds verified timers the wiki lacks.** Entries with `trusted: true`
   were MEASURED from family logs (Befallen's zone clock, Crushbone's). That is the cheapest
   first contribution and needs no player at all — a diff report, not a feature.
5. **`MobInfo` does not read `respawn_time`.** `EqlWikiMobs.Parse` reads name/zone/level/
   location/loot; the pack cannot currently say what the wiki thinks the timer is. One field.

#### Architecture

**Core — `SpawnCycleLedger`** (`spawn-cycles.json` beside `spawn-overrides.json`): per
`server|zone|name`, a list of `Cycle(DurationSeconds, Kind: Rekill|Sighting, At)`, capped at
the last 20. Written by `SpawnTimers` at the three places a gap is ACCEPTED today —
`LearnFromRekill`, `LearnFromSighting`, `LearnDiscovered` — and nowhere else, so every gate
above applies by construction (no instance, no triggered/raid, named's own kill, same stay,
floor and ceiling). Imports never write it (a stranger's number is not an observation).

**Core — `EqlWikiMobs.Parse` reads `respawn_time`** into `MobInfo.RespawnField` (raw text;
`""` when absent).

**UI.Shared — `RespawnSuggestion`** (pure, unit-tested — the whole honesty bar lives here):
- **Bar:** at least **3** cycles, all within **±15 % of their median**, median ≥ 90 s.
  Agreement is the evidence of attention: a player who left the camp produces scattered gaps,
  not three that agree. Below the bar: no suggestion, numbers travel in the edit summary only
  — the `SuggestRarity` rule verbatim.
- **Never** for `IsTriggered`, `RaidInstanced`, or `MultiSpawn` entries (no cycle / sibling
  noise), whatever the ledger holds.
- **Wording:** the wiki's own — minutes under an hour ("22 min"), hours ("6 hours"), days
  with variance only when the spread supports one. `SpawnDurationText` formats for us; this
  formats for the wiki, and a test pins both idioms apart.
- **Three-way compare:** wiki field (from `RespawnField`), catalog value, observed. Suggest a
  paste only when the wiki field is absent or disagrees with the observed median by more than
  the spread; when it agrees, say "wiki already says 22 min — nothing to add" (the KnownDrops
  line for timers); when it disagrees, phrase it as the stat block does — *compare, don't
  overwrite* — with the cycle list in the edit summary.

**UI.Shared — `WikiContribution.BuildExport` and `WikiPackPresentation`** gain a respawn
section per creature: `RowKind.RespawnObserved` with the paste block
`| respawn_time = 22 min` and the same edit link the loot section uses (served title, trap
3). The pack window needs no new control on either desktop: the row kinds are data and both
windows already draw whatever the presentation returns. **Bevel pre-design: yes** — the row's
note ("observed 22 min over 3 cycles; wiki says 25 min") is a new sentence on a shipped
surface. **Column budgets:** the pack rows wrap; none. **Shot offline:** seed the mob cache AND
a `spawn-cycles.json` in `wiki-pack`; prediction written first.

**Script — `scripts/harvests/eqlwiki/respawn-diff.py`** (flags only, like the rest of the
refresh): for every catalog entry with `trusted: true`, read the creature page's field and
report absent/disagreeing ones as paste-ready lines for a human. Curated catalogs stay
unwritten; this writes a REPORT. It is PR 0 because it needs no player evidence and it is
the "what we already know that the wiki does not" David was pointing at.

#### Risks

- **The bar is the product.** Too low and EQBuddy becomes the source of wrong timers on the
  shared reference — uniquely wrong, the thing the match-the-wiki rule exists to prevent. Too
  high and nothing is ever suggested. 3 / ±15 % is a starting point logged as a decision;
  the pack's edit summary carries the raw cycles so a wiki editor can judge.
- **Trap 4 (one fact, two sources):** the observed median is computed ONCE in
  `RespawnSuggestion` and the paste, the row note and the edit summary all read it.
- **Trap 20:** the ledger is a new file that only the engine writes and only the pack reads —
  add its reader in the same PR as its writer, or it is a written-never-read store.
- **Trap 13 shape:** `spawn-cycles.json` is written from the watcher thread; it is its own
  file with its own lock, as `SpawnOverrides` is. Never merge it into `spawn-overrides.json`.
- **Variance:** EQ timers have real variance ("3 days with 12 hour variance"). Three cycles
  cannot measure it; the plan suggests the median and puts the range in the summary, and
  never writes a variance clause. Say so in the row note when the spread is wide.
- **Long timers:** a 3-day boss needs nine days of one player camping to reach the bar. That
  is correct — under-suggest — and PR 0 is how those reach the wiki instead.

#### Decomposition

- **PR 0 — `respawn-diff.py`** + its report in the weekly refresh; no app change. One-off
  run now, human pastes what it finds.
- **PR 1 — Core:** `SpawnCycleLedger`, the three write points, `RespawnField` parse;
  `SpawnTimerTests` (a cycle is recorded exactly when a gap is learned and never otherwise:
  instance, triggered, placeholder-started, cross-stay, import — one negative each);
  `EqlWikiMobsTests` for the field. No UI.
- **PR 2 — UI.Shared + pack:** `RespawnSuggestion` + tests (bar, agreement, wording both
  idioms, three-way compare); the pack section and row kind; `WikiPackPresentationTests`;
  both pack windows unchanged or near it; staged shot; `docs/TestPlan.md` §3 rows; What's-new
  crediting the mega-thread reporters by name and number (from Scribe's item).
- **Someday:** a "wiki says / you observed" line on the Spawns window row itself; the
  Respawn Timers list page.

#### Verification

Unit as above; the bar's tests include a scattered-gap case that must NOT suggest and a
three-agreeing-cycles case that must. A real-world check David CAN do: camp any low-level
named three cycles (Crushbone's trainers are minutes) and read the pack. Reporter confirmation
via the mega-thread reporters after release.

#### Out of scope

Any store we host; the Respawn Timers list page; suggesting variance; a "since" filter on
cycles; anything on the phone; re-deriving timers from archived logs (the ledger starts from
this release; history is the other item's problem and a spawn cycle, unlike a drop, cannot be
pooled across characters without the stay evidence the archive does not hold).

#### Decided without asking (→ `DECISIONS.md`)

Creature field, not the list page; bar = 3 cycles within ±15 % of median; median suggested,
never variance; ledger capped at 20 cycles; PR 0 is a script not an app feature.

---

## The wiki pack reads one live session; it should read the history already on disk

- **Priority:** **DONE 2026-08-26 (Claude), staged in 1.99.12 — both PRs, executed as
  planned with one addition the plan could not have predicted.** `MobHistory.Pool` +
  `PoolScope` + `SessionRepository.MobRows` (the ProgressSeries probe applied to `Mobs`);
  `UI.Shared/WikiPackPool` (the memo both windows share instead of hand-rolling the cache);
  both pack windows on the pooled source; the new `ScopeLine(PoolScope, kills, creatures)`
  in exactly the plan's wording shape; the Drops footer hint extended; Frankthetankk
  credited in What's-new. All three of the plan's decisions implemented as decided (pool
  across characters and servers, no toggle, no "since" filter, (name, zone) keying).
  **The addition: your double-counting risk was real and one exclusion was not enough.**
  The staged shot's first run caught it — `ActiveRowId` is set by the FIRST checkpoint, so
  a pool computed before it (the re-ingested-log adoption case) counted the archived twin
  AND the live snapshot, doubling every number on screen. The live session is now excluded
  by row id AND by identity `(server, character, session start)` — Checkpoint's own
  adoption rule — with the failing case as a test. `MobHistoryTests` (9 cases incl. a
  temp-DB probe), gates 2,621 / 284 green, shot re-staged with the pooled scope line
  matching its prediction.
- **Class:** was `V2`; the plan carried it.
- **Source:** #217 Frankthetankk, ask 2.

### The concrete miss, which is what makes it worth planning rather than shrugging at

**Three 4-kill sessions never cross the 10-kill rarity bar, despite twelve real kills.** The
pack's honesty rules are the reason: `SuggestRarity` refuses to label anything under 10 kills,
deliberately, so a thin sample cannot become a confident wiki edit. That rule is right and must
not be relaxed — the fix is to stop throwing away the evidence that would satisfy it.

The same thinning hits **money-per-kill ranges**, **faction-hit reporting** ("no hits observed
across N kills" is a claim about N) and **con-derived level ranges**, all of which widen with
observations and all of which currently restart from zero every session.

### What he asked that a plan has to answer

1. **Pool across the account's characters, or stay per-character?** Not obvious: drop rates are
   a property of the MOB and pool cleanly, but con-derived level ranges and faction hits are
   observations made BY a character.
2. **Any "since" filter?** Zones get retuned; a three-month-old drop rate may describe a mob
   that no longer exists in that form. A pack that silently averages across a retune is
   confidently wrong, which is the one thing this surface must not be.
3. **Per-session vs all-time toggle, or neither?** He explicitly does NOT want a toggle. Worth
   holding him to that or overruling it deliberately.

### Where it fits

This is the **all-time stats direction (#168 / #159)** — a query over archives already on disk,
which is exactly how David framed that: not new collection. So it may be the first real
consumer of that work rather than a detour from it, and the plan should say which.

### Already shipped (must not be fought)

Session-scoped export; `#74`'s archived-log review replaying one file at a time; the 10-kill
rarity bar and the "no label when the sample is thin" rule; the wrong-article split and the
motes exclusion that landed today.

### Plan — Fable 5, 2026-08-22

**Shape: a pure `MobHistory` pooler in Core over the snapshots `history.db` already stores,
feeding the SAME `BuildExport` the pack uses today; the pack reads history by default with no
toggle, and its scope line says exactly what it pooled.** The reporter's framing is right on
every point I could check. `needs-david:` none.

#### What I read, and what it changes

1. **The data is on disk, in full.** `SessionRepository` stores every session's complete
   `StatsSnapshot` as `SnapshotJson` (`history.db`, `Sessions` table), and `StatsSnapshot.Mobs`
   is the list of `MobSummary(Name, Kills, Loot[...], CoinMin/Max, Factions, LevelMin/Max,
   Zone)` — the exact record the pack consumes. No log replay, no new collection.
   `ProgressSeries` already probes one field across every row with a `JsonDocument` rather than
   materialising snapshots; that is the access pattern to copy.
2. **`BuildExport` takes `MobObservation(MobSummary, lookup)`.** So pooling produces
   synthetic `MobSummary`s and the export, its honesty rules, and the pack window's rows need
   no change — the 10-kill bar is then met by twelve kills across three sessions because the
   number it reads is twelve.
3. **Review replay writes no sessions** (`SessionStats.cs:197`, #74), so a player re-reading
   an archived log cannot double-count into the pool. Checkpoints of the LIVE session do land
   as a row, so the live session must be taken from the live snapshot and its row excluded.
4. **Session `Zone` on a `MobSummary` is the kill zone** — the #65 fix — so pooling keys on
   (name, zone), not name alone: "an ice giant" in two zones is two mobs.

#### The three questions, answered as decisions

1. **Pool across the account's characters: YES, and across servers too.** Drop tables, level
   ranges and faction hits are facts about the mob in the game, not about who observed them.
   The scope line names every character and server pooled, so nothing is silent. No toggle.
2. **"Since" filter: no filter, and the scope line shows the earliest date pooled.** A retune
   is not a date we hold. What protects against it is what already protects the pack: every
   number is presented for reconciliation, never as a correction, and the edit summary carries
   the per-session breakdown so an editor can see a rate that moved. If a retune ever produces
   a visibly bimodal rate, that is the moment to add a filter — not before.
3. **Per-session vs all-time toggle: none.** The reporter is right that a smaller sample never
   makes a better wiki edit. Drops by Creature keeps the LIVE view — "is this camp worth it" is
   a different job and stays session-scoped; only the pack pools.

#### Architecture

**Core — `MobHistory.Pool(IEnumerable<StatsSnapshot> sessions, StatsSnapshot? live)`** →
`IReadOnlyList<MobSummary>` keyed on (name, zone): kills summed; loot counts summed per
base item (`QuestCatalog.BaseItemName`, the existing fold) with `DropRatePct` recomputed from
the pooled counts and `LastAt` the latest; `CoinMin`/`CoinMax` the extremes across sessions
(−1 stays "never seen"); factions unioned with hit counts summed; `LevelMin`/`LevelMax` the
extremes of conned values (0 = never conned stays 0). Plus a `PoolScope(characters, servers,
sessionCount, earliest, latest)` record for the scope line. Pure; tested with a fixture of
several fake snapshots asserting the pooled counts — the reporter's own test description.

**Core — `SessionRepository.MobRows(server?, character?)`**: the `ProgressSeries` probe
applied to `Mobs`, so the pack opens without deserialising every snapshot's combat ledgers.
Default scope = every character, every server (decision 1).

**UI.Shared — `WikiPackPresentation.ScopeLine`** becomes the pooled form: *"12 kills of 4
creatures across 3 sessions · Dranak and Flossie on freeport · 2026-07-30 → today"*. One
sentence, and it is the sentence that makes decision 1 and 2 honest. **Bevel pre-design:
yes** — this line is the surface's whole claim about itself. **Column budgets:** none (the
line wraps). **Shot offline:** no for the wiki caches (seed them); the `wiki-pack` shot must
also seed `history.db` with two or three sessions so the pooled numbers are visible — the
`history-window` shot already stages sessions; reuse its staging and predict the totals first.

**Both pack windows** swap their data source from `_snapshot.Mobs` to `MobHistory.Pool(rows,
live)`, computed once on open and on the existing 3 s tick only if the live session's mob set
changed (the signature already exists). `EnsureMobLookup` fires for pooled creatures exactly
as it does for live ones — with the 2-in-flight cap, so a long history does not burst eqlwiki.

**The Drops tab is untouched**, and says so in its footer hint (the "moved" text already
points at the pack; add "the pack pools every session you have"). Mobile: no pack surface.

#### Risks

- **Trap 8 / perf:** the pool is recomputed on a signature, never per tick; the DB probe is
  the cheap one. A profile with hundreds of sessions is the case to measure — stage it.
- **Double counting:** the live session's checkpointed row AND the live snapshot — exclude
  the active row by id (`SessionArchiver` knows it). A test with a live session whose row is
  already checkpointed must pool its kills once.
- **Trap 4:** one pooled `MobSummary` feeds the row, the paste and the stat block; nothing
  re-sums.
- **The 10-kill bar was chosen for one session's evidence.** Pooled kills can reach it with
  kills spread over months; the rule stands (the reporter's argument is sound), but the edit
  summary must carry the per-session breakdown so an editor sees the spread.
- **Credit scope:** the log reference's timestamps now span sessions; use the dated form the
  export already has for multi-day sessions.
- **Old snapshots** deserialise `LevelMin`/`CoinMin` as unknown (the record says so); the
  pooler treats unknown as absent, never as zero.

#### Decomposition

- **PR 1 — Core:** `MobHistory.Pool` + `PoolScope` + fixture tests (sum, fold, extremes,
  unknown-stays-unknown, (name, zone) keying, live-row exclusion); `SessionRepository.MobRows`
  with a test over a temp DB. No UI.
- **PR 2 — pack:** both windows on the pooled source; `ScopeLine`; the Drops footer hint;
  `WikiPackPresentationTests`; staged shot with predicted totals; `docs/TestPlan.md`; What's-new
  crediting Frankthetankk (#217, ask 2).
- **Relationship to the all-time stats direction (#168 / #159):** `MobHistory` IS the first
  query of that kind over the archive, and the all-time view should consume it rather than
  write a second pooler. Say so in the class doc; do not build the view here.

#### Verification

Unit as above. The acceptance check a person can run: three short sessions on one camp (the
fixture log can be split), then open the pack and read "12 kills across 3 sessions" with a
rarity label that a single session could not have earned. Reporter confirmation on #217.

#### Out of scope

The all-time stats VIEW; pooling the Drops tab; a since/character toggle (decided above, and
revisited only on evidence); spawn cycles (the other item — a cycle needs stay evidence the
archive does not hold); changing the 10-kill bar.

#### Decided without asking (→ `DECISIONS.md`)

Pool across characters and servers with no toggle; no "since" filter; live view stays
session-scoped; (name, zone) keying.

---

## Avalonia theme bodies need a seam — PR A DONE, PR B DONE 2026-08-26

- **Priority:** **DONE.** PR A executed 2026-08-22; **PR B executed 2026-08-26 (Claude),
  staged in 1.99.12**: `ThemeCardPanel<TTab>` + the Avalonia `ProgressThemeCard`, mirroring
  the WPF pair name for name; `EQBUDDY_EXPAND` reached full WPF parity (named keys and
  `progress:raids`); the expand → pop-out → close → expand sequence is a headless test now
  — INCLUDING the window tab-change via a real click and the hand-back assertion — because
  PR A's per-host surfaces made the plan's "human step" testable
  (`ExpandPopOutCloseExpandDoesNotThrowAndEndsCollapsed`). The sheet capture matched its
  written prediction (strip wraps, Experience body inline, ⧉ on the header). One defect
  found by an existing test and worth carrying: the card's first cut raised
  `ExpandedChanged` on BOTH edges, and the collapse edge queued a live repaint that wiped a
  theme window's freshly painted rows with an empty snapshot — the consumer only ever
  wanted the open edge. The CLAUDE.md `EQBUDDY_EXPAND` parity note is updated.
- **Class:** `V2`, unchanged.

### PR A — DONE, and what it actually cost

Option (a), as planned. `EQBuddy.Avalonia/IWidgetCard.cs` (seam, `ICardContext`,
`ProgressSurfaceSet`), `CardParts` for the shared row builder, and five views —
`ProgressCardView`, `MoneyCardView`, `MotesCardView`, `FactionCardView`, `RaidsCardView`.
`ProgressTabBody` is deleted and replaced by `NewProgressSurfaces()`; `ProgressWindow` builds
its own set in its constructor, eagerly, because two of those views are the only writers of
settings the rest of the app reads. **369 lines out of `MainWindow.cs` (5,598 → 5,229);
baseline lowered to 5,229 in the same commit.** All 271 existing Avalonia tests pass
unchanged — the "tabs draw what the cards drew" claim carried across the seam.

**Two things the plan did not predict, both worth having:**

1. **The two-second throttle nearly ate the live numbers.** `MaybeRefresh()` had only ever
   throttled the window's CHROME; the surfaces were painted by the widget's own per-tick
   `RefreshExpandedSections`, and that distinction existed nowhere but in the arrangement of
   the old code. Rendering in `MaybeRefresh` would have put a 2 s stutter on live values.
   Now trap 46.
2. **`SurfaceOwnershipTests` found the same hand-off on TWO more lanes** on its first run:
   `IGearLootHost.LootTabBody` and `ICreatureHost.CreatureTabBody`, with the same doc comment.
   They are exempt by a curated list naming the PR that removes each. **What holds them today
   is 1.99.4's release-on-close mitigation, not safety** — the day one of them expands in
   place it is the Progress crash again.

Traps 45 and 46 are in `CLAUDE.md`. The `_raidsBody` wrapper from the same morning's
auto-import fix died with the lift, as you asked.

### PR B — Inline themes PR 1, Avalonia half — STILL PLANNED

Unchanged from the plan: `ThemeCardPanel` mirroring `ThemeCardView`, `EQBUDDY_EXPAND=progress:raids`
honoured, the expand → pop-out → close → expand sequence test, a `WidgetSheetTests` shot with
the prediction written first, landing with the What's-new line the WPF half already has.
**Nothing blocks it now** — `EveryHostGetsItsOwnProgressSurfacesAndTwoCanLiveAtOnce` is the
proof that two live hosts no longer collide.

**One human step the plan asked for and no test can do:** expand Progress, pop out, close,
expand, change tab in the window, close, expand. **Runnable HERE — it does not need a Linux
machine** (Fable, 2026-08-23): the Avalonia build runs on Windows, which is how trap 13's
two-builds-one-profile was found. Correcting this because it had been written as "nobody can
do it", which is how a cheap check becomes a permanent open item.

## Inline themes — expand in place, pop out on request — PR 2 DONE 2026-08-26

**PR 2 executed (Claude), staged in 1.99.12, BOTH lanes in one change:** the Kills & Drops
and Gear & Loot cards expand in place on WPF and Avalonia. What it took and found:

- **The Avalonia half was the seam plan's PR 2/PR 3 lifts done together**: `KillsCardView`
  now exists on the Avalonia lane (and closed a quiet drift — the widget's kills panel
  hand-rolled its rows while WPF read `KillsPresentation`; both lanes read the shared
  module now), `NewCreatureSurfaces()`/`NewLootSurfaces()` are the factories, both windows
  build their sets in their constructors, and **the `SurfaceOwnershipTests` exemption list
  is EMPTY** — with a positive-half test asserting the factories exist.
- **Target drops moved from push to pull** on Avalonia, mirroring the WPF twin's
  `TargetDropsContent`: with per-host loot views there is no longer one view to push into.
- The `_gearChecklistDirty`/`_inventoryDirty` flags died with their consumers — per-host
  views render on their own ticks; the Inventory arrival-paint rule lives in the window
  (`InventoryChanged`), as on WPF.
- Glance wordings are Bevel's, in UI.Shared (`CreatureTheme.DropsGlance`,
  `LootTheme.InventoryGlance`). The Items tab is NOT a strip tab (`LootSurface.Hosted` is
  three) — a prediction miss the first test run caught.
- E2E: `killsInline`/`lootInline` + window-owner facts, three new tests; Avalonia: the
  crash-class sequence for both themes and the two Glance rooms. Shots
  `theme-inline-kills`, `theme-inline-kills-glance`, `theme-inline-loot` all matched their
  written predictions; `widget-expanded` deliberately re-shot.

**PR 3 (Quests) DONE the same day — the whole Inline themes item is COMPLETE.** Item 7's
hypothesis verified: the checklist RENDERING lives inside each QuestsWindow (no view class
to instantiate twice), so the inline Full rooms are a new REDUCED body — `Core/QuestInline`
owns the arrangement (one class's rows, capped at 12 with "... and N more", read-only on
purpose: a checkbox inside a capped scroller invites ticks the cap hides context for, and
ticking stays in the window). General is the Glance AND the default, exactly as Bevel
ruled. **One decision Bevel has not ruled: the #238 Unlocks tab (post-table) is a Glance,
conservatively** — flagged in the pending Unlocks review ask. All four theme windows now
raise `TabChanged`; all four hosts hand the room back and forth; the ↗-arrow assertions
became chevron-era assertions per the plan's own "do not keep ↗ on the collapsed row".
Shots `theme-inline-quests` (the default General glance: "Quest Tracker") and
`theme-inline-quests-epic` (Warrior's checklist, four-chip strip wrapping) matched their
predictions. E2E 32/32, Avalonia 287/287, unit 2,646.

## Inline themes — expand in place, pop out on request

- **Priority:** **DONE 2026-08-26 (Claude) — all four themes, both lanes, staged in
  1.99.12; the DONE headers above carry the execution record.** Flipped in place so a
  grep for `ready` cannot find a plan whose work has shipped (eqbuddy-fb's catch — the
  other three items closed on this line and this one had only gained a header).
  (was: `ready` — David answered the one question that was his, 2026-08-22, asked with
  the question tool: build it as Bevel ruled it — expand in place, pop out on request;
  the widget stays the home; the theme windows stay for the second monitor. Plan by
  Fable 5, 2026-08-22. One theme per PR, both UIs in each.)
- **Class:** `V2` — four themes × two UIs, a host-ownership rule a compiler cannot see (a
  body has one visible parent), a retired breakout, and it reverses the *direction* of four
  folds that were themselves signed decisions. Not V1 because the host rule has to be right
  for all four before the first ships, and a wrong one is trap 15 on every card at once.
- **Source:** David, 2026-08-21 (*"expandable sub-categories under them with an option to
  pop out the window"*); #228 (daetien-lab: *"I simply want to track my mote drops in the
  main window"*; joeymavity: *"Motes are buried and seem to move around"*);
  `docs/proposals/InlineThemes.md`; Bevel's ruling 2026-08-21 (tab strip; split rule; host
  rule; pop-out collapses the card; collapsed by default; name pills by the old card titles;
  default tab is the room that moves while you play).
- **Bevel pre-design: YES, before PR 1's screenshots** — specifically the expanded card's
  height per theme at 100 % and 125 % scale, and the one-line bodies of the Glance tabs below.
  The shape is already ruled; this pass is about what it looks like on the thing that sits
  over the game.
- **Shot offline: no** for Kills & Drops (the Drops tab reads the wiki — seed every fixture
  creature's mob cache as `wiki-pack` does); **yes** for the other three.

### What I read, and what it changes

1. **The launchers are four `SectionLink` buttons whose KEYS are already the theme keys**
   (`kills`, `loot`, `quests`, `progress` — `MainWindow.xaml:457/512/534/603`, `SectionMap()`
   at `MainWindow.xaml.cs:649`; Avalonia `_sections[...]` at `MainWindow.cs:999–1060`). The
   cards keep their keys, so **there is no settings migration in this item** — no
   `HiddenSections`/`SectionOrder` fold, nothing for `AbsorbedTitles` to change. That is the
   single biggest reason this is tractable.
2. **The two UIs own theme bodies DIFFERENTLY, and the plan has to say so or the Avalonia
   lane ships a crash.** WPF: each window builds its own instances (`NewProgressSurfaces()`,
   `NewGearCard()`, `new LootCardView`, `new DropsCardView`, `QuestsWindow` its own) — two
   hosts, two instances, no conflict. Avalonia: the widget BUILDS the bodies once
   (`_progressTabBodies`, `_lootTabBodies`, `_creatureTabBodies`) and the window takes them
   through `IProgressHost.ProgressTabBody(tab)` and sets `_body.Content = …`
   (`ProgressWindow.cs:268`). A control has one visual parent; showing a body in the card
   and the window at once throws. The one-owner rule below is therefore not a UX nicety on
   Avalonia, it is the thing that keeps the app up.
3. **`SectionScroll.MaxHeight` already caps the whole card stack** (`WidgetMetrics.
   SectionMaxHeight`, E2E-pinned as `sectionMaxH`), so an expanded theme cannot run the
   widget off the screen — it scrolls inside the cap like any tall card. Per-theme height
   is a Bevel question, not an engineering one.
4. **`BreakoutKind.Progress` exists on WPF only** (`BreakoutWindow.xaml.cs:14`; Avalonia's
   enum has no `Progress`), opened from the `xp` star (`MainWindow.xaml.cs:3616`). Bevel's
   ruling folds it into the pop-out. `scripts/shoot.ps1`'s `mini-bar` shot enumerates
   `BreakoutKind` BY HAND (trap 30) and `OptionsCardsView.BuildBreakouts` enumerates it by
   reflection — both need a look when the member goes.
5. **The phone is the prototype and needs nothing.** `index.html`'s `.qtabs` card-with-tabs
   reads the same Core tab keys (`index.html:1158–1180`). Parity is already by shared module;
   this item brings the desktop TO the phone's shape, not the reverse.
6. **E2E already pins the launcher LINE** (`EndToEndTests.cs:91–97, 196–224, 519–522`:
   "the launcher should summarise the theme"). Keep the summary line verbatim as the
   collapsed header and those assertions keep passing; they become the regression guard
   that the glance survived the expander.
7. **`QuestChecklistView` is one control hosting Epic and Sky** (`MainWindow.xaml.cs` ctor,
   `_quests`); how `QuestsWindow` hosts it versus the General search is not re-read this
   session — **hypothesis:** the General tab is a search box plus a detail pane with its own
   `DetailScroll`, which is exactly Bevel's "do not shrink-wrap a full window" case.

### Architecture

**Core (recipe step 1 — name it where the phone and both desktops can read it).**
Each theme's surface definition (`ProgressSurface`, `LootSurface`, `CreatureSurface`,
`QuestSurface`) gains `InlineMode InlineModeFor(tab)` → `Full | Glance`. Initial table:

| Theme | Full inline | Glance inline (one line + ⧉ into the window) |
|---|---|---|
| Progress | Experience · Wealth · Faction · Raids | — |
| Kills & Drops | Kills · Drops | — |
| Gear & Loot | Loot · Wishlist | Inventory (a long list with its own filter bar) |
| Quests | Epic 1.0 · Plane of Sky | General (search + detail pane; Bevel's host rule) |

Bevel's pre-design pass may move a tab between columns; the table lives in Core so the
move is one line and both desktops follow. Default tab per theme, per Bevel: the room that
moves while you play — Experience, Kills, Loot, and for Quests whichever of Epic/Sky the
player's class has rows in (else Epic).

**UI.Shared — `ThemeHost` (framework-free, unit-tested; the "sum, not pixel" rule).**
A state machine per theme: `Collapsed | Inline | Window`, with inputs `ToggleCard`,
`PopOut`, `WindowClosed`, `OpenWindow(tab?)` (the ⚙/hotkey/`EQBUDDY_*` openers), and a
`SelectedTab` kept for the session only. Invariants it enforces, and the tests assert:
- **One owner.** `PopOut` from `Inline` → `Window` and the card collapses. `ToggleCard` while
  in `Window` does NOT draw a second copy: it brings the window forward (Avalonia cannot
  show the body twice; WPF could, and must not, for the trap-15 reason). `WindowClosed` →
  `Collapsed`, never back to `Inline` — the player closed a thing; do not re-grow the widget.
- **The tab follows the player.** Pop-out opens the window on the card's selected tab;
  closing the window hands the window's tab back to the card for the next expand.
- **A Glance tab never paints a body.** Its inline content is the tab's one-line summary
  and the ⧉; `Render` is not called on the full view.
Both windows already expose `OpenX(tab)`; the state machine tells them when.

**WPF — `ThemeCardView` (one class, four instances; `EQBuddy/ThemeCardView.cs`).**
Replaces each `<Button Style=SectionLink>` with `<Expander x:Name="…Section"
Style="Section">` + `<ContentControl x:Name="…Body"/>`, the `MotesSection` shape, so
`SectionMap()` is unchanged. Header: the existing `EqCardTitle` + the existing summary
`TextBlock` (trap 12: the summary stays the star column and keeps trimming) + a
`DesignSystem.InlineIconButton("External", …)` ⧉ that sets `e.Handled = true` — the
Progress fold's lesson: a button nested in the expander header bubbles to the toggle.
Body: `EqSegmentedStrip` fed by the theme's existing `Tabs(...)` (so badges and labels are
the window's, and the strip WRAPS — trap 25) over a `ContentControl` holding the selected
tab's body. Bodies: **the card builds its own instances** exactly as the windows do
(`NewProgressSurfaces()`, `NewGearCard()`, `new LootCardView`, `new DropsCardView`,
`InventoryView`), built lazily on first expand; the `IWidgetCard` rule (a collapsed card
renders nothing) keeps a never-opened theme at zero cost. `ThemeCardView` takes
`ICardContext` + the factories, never `MainWindow` — the seam exists for this.

**Avalonia — `ThemeCardPanel` (`EQBuddy.Avalonia/ThemeCardPanel.cs`, beside
`SectionLinkPanel`).** A `SectionPanel` expander whose content is the strip + a body host.
Bodies are the widget's single instances; the hand-off is explicit: the host that loses
ownership sets its `Content = null` BEFORE the other sets it, in one method on the main
window (`HandThemeBodyTo(theme, Host.Card|Host.Window)`) that `ThemeHost`'s transitions
call. No other code path may assign a theme body — grep for `TabBody(` assignments and
route every one through it, or the second place is where the crash lives.

**Progress breakout.** `BreakoutKind.Progress` retires into the pop-out: the `xp` star while
minimized opens the Progress WINDOW on Experience (Bevel's "fold it into the theme's
pop-out"). `DisabledBreakouts` entries for `"Progress"` are ignored, not migrated — nothing
is lost, the window has its own position memory. Trap 30: remove `Progress` from the
`mini-bar` list in `shoot.ps1` in the same commit. Trap 20/26: list every control the
breakout carried and say where each went (its xp line → Experience tab, already there).

**Tour and Options (recipe step 7).** The tutorial page "Cards that open windows" is now
wrong; rewrite it to "Cards that expand — and can pop out". `OverlaySections.AbsorbedNote`
stays as is (the absorbed titles are still inside). Options → Cards & windows row tooltips
for the four keys say "expands in place; ⧉ opens its window".

**Mobile:** no change. `CompanionProjection` already emits the Core tab keys; if the
`InlineMode` table is added to Core it is NOT put on the wire (the phone has no windows).

### Risks and the traps they touch

- **Trap 15 (two switches for one state):** `ThemeHost` is the only switch. Neither the
  expander's `IsExpanded` nor the window's `IsVisible` may be consulted as truth; both are
  outputs. Assert it: a test that fires `WindowClosed` twice and `ToggleCard` during
  `Window` and checks no state goes to `Inline`.
- **Avalonia one-parent:** covered above; the `HandThemeBodyTo` funnel is the guard, and a
  `WidgetRenderTests` case must expand a theme, pop it out, close the window and expand it
  again without an exception — the sequence that throws if any assignment bypasses the funnel.
- **Trap 12 (timer-driven size on X11):** a body that grows on a CLOCK inside an expanded
  card changes measured size every tick. The Kills list grows per kill (player-driven, fine);
  Experience's xp line ticks every second — it was a card before the fold and is the same
  control, so this is existing behaviour, not new. Do not add anything clock-driven to the
  header.
- **Trap 14/25:** the tab strip wraps; the header is a two-column grid. Both already true in
  the windows; copy, do not hand-build (`CLAUDE.md`: never hand-build another pill).
- **Trap 16:** the ⧉ is an `InlineIconButton`, hit area `IconInlineHit`.
- **Trap 36/37:** the lifted bodies have no scrollers and some had pinned chrome in their
  windows (Quests' search box, Drops' orientation footer). Full-mode tabs get the body only;
  anything that was pinned window chrome is why a tab is Glance.
- **Trap 21:** `docs/screenshots/` already has `section-progress.png`, `progress-card.png`,
  `widget-expanded.png` embedded in docs. The new shots are `theme-inline-progress`,
  `theme-inline-kills`, `theme-inline-loot`, `theme-inline-quests`; `widget-expanded`
  (`EQBUDDY_EXPAND=1`) will now photograph four expanded themes — **predict it and re-shoot
  it deliberately**, it is in the README.
- **Trap 22/23:** `EQBUDDY_EXPAND=progress` must open the card INLINE (not the window) after
  this change; the E2E opener for the window stays `EQBUDDY_PROGRESS`. Write the prediction
  for each shot before running it.
- **Ratchet:** both MainWindows are near their caps (WPF 4,418 / 4,635; Avalonia 5,591 /
  5,964). The four launcher builds and their openers MOVE into `ThemeCardView` /
  `ThemeCardPanel`; each PR lowers the baseline in `ArchitectureTests` and
  `docs/Architecture.md`'s table in the same commit (`DocumentationTests` checks the table).
- **Trap 1:** the ⧉ and the strip sit under the UI-scale transform; nothing here does
  screen-pixel arithmetic, but shoot at 125 % once.
- **E2E launcher-line assertions** keep passing only if the collapsed header carries the
  exact summary the launcher did. If a PR changes that line, it is changing the glance, and
  #219 says a changed glance is a lost feature until proven otherwise.

### Decomposition (one theme per PR; both UIs in every PR; each leaves `main` shippable)

- **PR 0 — Core + UI.Shared, no UI.** `InlineMode` on the four surfaces with the table above;
  `ThemeHost` with `ThemeHostTests` covering every transition and both invariants; a
  `SurfaceParityTests` case that the phone's tab keys still equal Core's. `docs/TestPlan.md`
  §3 rows.
- **PR 1 — Progress, both UIs.** `ThemeCardView` / `ThemeCardPanel` born here. The breakout
  retirement and the `mini-bar` list. E2E: pin BEFORE the move — `progressInline=0/1`,
  `progressTab`, `progressTabs`, `progressWindowOpen` into `EQBUDDY_EXPAND`; assert expand →
  pop-out → close → card collapsed. `WidgetRenderTests` twin. Tutorial page. Baselines
  lowered. `WhatsNew.json` entry crediting daetien-lab and joeymavity (#228) and David's ask.
  **Bevel's pre-design pass lands between PR 0 and PR 1's screenshots.**
- **PR 2 — Kills & Drops and Gear & Loot.** Same class, two more instances; Inventory is
  the first Glance tab, so this PR proves the Glance body. Drops inline fetches the wiki —
  seed the fixture caches.
- **PR 3 — Quests.** The Glance General tab and the `QuestChecklistView` hosting question
  (item 7 above — verify before building; if the checklist cannot be hosted twice on WPF,
  build a second instance as the windows do). `EQBUDDY_EXPAND=quests` inline shot.
- **Each PR:** `FABLE-FEEDBACK.md` note; `DECISIONS.md` lines for anything the executor
  decided against this plan; Fable last-looks the diff before the release that carries it.

### Amendment — Fable 5, 2026-08-22, after PR 1 (WPF)

**The plan assumed a lift it had already banked.** "Each PR lowers the baseline" was written
for a MainWindow that still held the Progress surfaces; the Progress fold had already moved
them, so PR 1 had nothing to move and the WPF file grew 4,424 → 4,504 (131 of headroom against
~80 a theme). The executor's read is right. Corrected sequencing:

- **WPF:** PR 2's first commit lifts the `EQBUDDY_EXPAND` dump block (~130 lines of pure
  string-building — a sum, not a pixel) into `WidgetDump.cs`, a class taking `MainWindow` and
  reading `internal` accessors. It must NOT be a partial (the ratchet glob sums partials).
  Lower the baseline in the same commit. That buys PR 2 and PR 3.
- **Avalonia:** the seam item above IS the lift — Progress first (PR A), then Loot and Creature
  as the first commit of PR 2 and PR 3 respectively.
- **The Avalonia half of PR 1 is PR B of the seam item**, not a re-run of PR 1.

Also carried from PR 1: the window calls `ThemeHost.SelectTab` on its own tab change
(`ProgressWindow.TabChanged`, both lanes) — done on WPF, to be mirrored in PR B.

### Verification

- Unit: `ThemeHostTests` (every transition; one-owner; Glance never renders), `InlineMode`
  table test, `SurfaceParityTests`; Avalonia `WidgetRenderTests` expand/pop/close/expand.
- E2E (WPF has no unit tests): the facts above, asserted from `tests/EQBuddy.E2E`, written
  against the launcher BEFORE PR 1 changes it.
- Screenshots, predicted first: each `theme-inline-*` at 100 % and 125 % and once in
  Solarized; `widget-expanded` re-shot and the README checked. Prove the binary (trap 18).
- **The one check David can do himself, and it is the #228 job:** expand Progress on the
  widget over the game, read motes and xp without opening anything, pop it out, close the
  window, and see the card stay collapsed. Then the same with Kills & Drops mid-fight.
- Reporter confirmation on #228 after release — tell daetien-lab and joeymavity exactly
  what to click, and that Motes is also still its own card (#227) if they prefer that.

### Out of scope

The phone (it is the prototype); the World, Alerts and Live Meters themes (not built yet —
they will be born as expand-in-place cards when they land, which is the point of `ThemeHost`
being shared); retiring any theme window; shipping any theme expanded by default; the Motes
card (#227, separate); keyboard reach for the card stack (none of the cards have it);
per-theme height caps beyond `SectionScroll` (Bevel may ask for one; it is a follow-up).

### Already shipped (must not be fought)

The four theme windows and their tab strips (`EqSegmentedStrip` everywhere — never
hand-build another); every theme body on the `IWidgetCard` seam; `SectionScroll.MaxHeight`
and its E2E pin; `BreakoutKind` gating via `DisabledBreakouts` for the kinds that stay;
`AbsorbedTitles`/`AbsorbedNote` (#219); the Motes card's return (#227); the launcher summary
lines and the E2E assertions on them; the phone's card-with-tabs.

### Checked

Read this session: `docs/proposals/InlineThemes.md` in full; Bevel's ruling; `docs/Themes.md`
in full; `MainWindow.xaml:450–625` and `SectionMap()`; `MainWindow.xaml.cs` card seam,
`NewProgressSurfaces`, `NewGearCard`, the window openers; Avalonia `MainWindow.cs:990–1075`,
`AppTheme.cs` `SectionCard`/`SectionLinkPanel`, `IProgressHost.ProgressTabBody` and
`ProgressWindow.cs:255–275`; all four theme windows' strip/body wiring in both UIs;
`BreakoutKind` in both; `WidgetMetrics.SectionMaxHeight`; `EndToEndTests` launcher
assertions; `index.html` `.qtabs`; `shoot.ps1` shot table and `docs/screenshots/`;
`ArchitectureTests` baselines and current line counts. **Hypotheses, labelled:** item 7
(how `QuestsWindow` hosts `QuestChecklistView` and the General search) — verify in PR 3.

### Decided without asking (already in `DECISIONS.md`)

Ships collapsed, all four; one owner with "expand while the window is open brings the
window forward"; closing the window never re-expands the card; selected tab is
session-only; Progress breakout retires into the pop-out (Bevel's call, ratified); Glance
for Quests/General and Gear & Loot/Inventory; Progress goes first.

---

*No other items.*
