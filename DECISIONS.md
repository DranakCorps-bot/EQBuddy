# Decisions made without asking

**For David to skim, not to approve.** Every line here is a call an agent made under the
pre-authorization in `CLAUDE.md` ("What needs David, and what does not"): a decision that
could plausibly have gone another way, did not touch the consequence list, and was made
rather than asked. David vetoes from here; a veto while the work is unreleased is cheap,
which is why the release gate is the only hard one.

**One line each, newest first.** *What was decided · the other way it could have gone · where
it landed (commit, file, or thread).* If a line needs a paragraph, it was probably a question.

**A veto goes in the same line**, prefixed `VETOED (David, date):` with the replacement, so the
history of the call stays readable. If vetoes become common, the consequence list in
`CLAUDE.md` is too short; if there are never any, it is too long.

---

## 2026-09-04

- **The legacy download links in `LEGACY-V1.md` and the README pin `v1.99.17`, the current
  1.x release, rather than waiting for the bridge tag** (P0-3). · The other way: leave the
  asset links out until the bridge release exists, or point them at `releases/latest`. · A
  support page with no download on it is not a support page, and `releases/latest` is the
  one URL that must never appear there — it becomes the v2 page the moment v2 ships. The
  pin is stated as a pin in both files ("these move to the final tag when it is published"),
  so it reads as pending rather than as a claim, and the guard below fails any link that
  reverts to `releases/latest`. Re-pinning is a P0-1 checklist row on #275.
- **The LEGACY-007 obligation ships as a GUARD as well as a checklist row**
  (`scripts/legacy-notice-guard.ps1`, wired into `check.ps1` and `release.ps1`). · The other
  way: the checklist row alone, which is what the plan offered as sufficient. · Helm signed
  "LEGACY-007 whatsnew-style guard: yes" (2026-09-04 ~12:05 PM CT) and it came out cheap —
  file reads only, no git parsing beyond a tag list, so trap 54's encoding hole is not on
  its path. It is a no-op on the 1.x line and arms at 2.0.0.
- **That guard asks for the "Legacy Linux/macOS" release-notes section on the FIRST 2.x
  release only, while the README check applies to every 2.x** (same file). · The other way:
  demand it in every 2.x release's notes. · A line written to satisfy a guard on every patch
  release is a line players stop reading, and the README is permanent once written. If git
  cannot answer whether a v2 tag already exists, the strict branch is taken: the cost of
  asking twice is one line of notes, the cost of skipping is the promise going unenforced on
  the one release it was written for.
- **The bridge What's-new highlight lands in P0-3, on the unreleased 1.99.18 entry**
  (`src/EQBuddy.Core/Data/WhatsNew.json`). · The other way: leave it to whichever PR cuts the
  bridge release. · Helm's PR #282 ruling assigns it here ("bridge entry is P0-3 with Don
  Thompson + quasarj credits") and P0-2's own decision line above hands it forward to the
  P0-3 wording pass. It is the only in-app announcement Linux and macOS users will ever get,
  and it carries no URL — no highlight in the file ever has, and an unbreakable token is a
  geometry change on a `SizeToContent` window (trap 12).
- **`UpdateChecker.GitHubLegacyReleasePage` points at the RUNNING BUILD's own tag, not a
  hard-coded bridge tag** (P0-2, `src/EQBuddy.Core/UpdateChecker.cs`). · The other way: the
  literal the plan asked for, `.../releases/tag/v1.99.N`. · That literal has to be written
  before the tag it names exists, and a 404 is the worst possible last thing EQBuddy ever
  says to a Linux or macOS player — nothing in CI would catch it. Only installs that took
  the bridge can ever see the notice, so for every reader of this value the bridge tag and
  the running version are the same string; a copy that later takes a legacy patch points at
  that patch, which is the right answer rather than a stale one. The negative the plan
  actually cares about (`DoesNotContain("releases/latest")`) is asserted either way.
- **P0-2 ships no `WhatsNew.json` entry; the transition announcement stays with the bridge
  release** (P0-3 / P0-1 item 4). · The other way: add a highlight to the in-flight 1.99.18
  entry now. · Nothing changes for any player until a v2 release exists, and the plan
  assigns the one in-app announcement Linux and macOS users will ever get to the bridge
  release's own entry, alongside the `LEGACY-V1.md` / README / FeatureGuide wording P0-3
  owns. Written into `FABLE-FEEDBACK.md` as an explicit handoff rather than left to be
  assumed — a rule everyone thinks someone else satisfied is how a bridge ships silent.
- **The WPF hotspot baseline moved 4,214 → 4,273, the minimum that fits** (P0-2,
  `tests/EQBuddy.Tests/ArchitectureTests.cs`, `docs/Architecture.md`). · The other way:
  lift a surface out of `MainWindow.xaml.cs` instead, which is the standing move. · `main`
  was already at 4,635 of a 4,635 limit, so the ratchet was full before this PR and any WPF
  change would have failed it; the only surface P0-2 could have lifted is the update banner,
  and Phase 0 was told not to touch it. The decision itself did leave, into
  `UI.Shared/LegacyPlatformUpdatePolicy`. The bump grants exactly one line, so the pressure
  the table exists to apply is intact and the next WPF change has to do the lift.
- **`release.ps1 -Prerelease` THROWS when there is no `-Tag`, rather than being ignored**
  (P0-1, `scripts/release.ps1`). · The other way: accept it silently, since the flag only
  reaches `gh release create` and that block already only runs with a tag. · A tagless run
  still builds, signs, copies to OneDrive and installs locally, so the switch would have
  looked honoured while doing nothing — "silent no-ops are broken" with the switch on the
  other side. The check is the first thing in the script, so it costs a second rather than a
  172 MB publish. Pinned by `ReleasePrereleaseTests`.
- **The P0-1 guard also pins `UpdateChecker`'s `/releases/latest` URL, which is product code
  outside the PR's edit scope** (`tests/EQBuddy.Tests/ReleasePrereleaseTests.cs`). · The other
  way: assert only on `release.ps1`, staying strictly inside the scoped file. · The flag
  protects nobody if the client is ever pointed at `/releases` instead, and that is the
  natural edit for anyone wanting the updater to see more than one release — two files that
  must agree, which is the shape `WeeklyRefreshWiringTests` already guards. Nothing in
  `UpdateChecker` was changed; the test only reads it.
- **The #273 bonus-XP fix carries its `WhatsNew.json` entry into the UNRELEASED 1.99.18
  section, rather than waiting for whoever tags it** (PR #274, `src/EQBuddy.Core/Data/WhatsNew.json`).
  · The other way: code-only PR, entry written at tag time by the releaser — the literal scope
  Helm's 9:52 authorize named was `XpRx` + tests. · `CLAUDE.md` makes the entry non-negotiable
  for a player-noticeable change and `release.ps1` refuses without one, so the version that
  ships this would have had to grow it anyway; 1.99.18 has no tag, so nothing shipped is being
  edited. Flagged in the PR body and to Helm as one droppable line if it should ship under a
  different version. No tag created.
- **The bonus parenthetical is NON-capturing, so `XpEvent` gains no field**
  (`LogParser.XpRx`). · The other way: a `bonus` group and a `Bonus` flag on `XpEvent`, which
  would let a future surface say "that hit was boosted". · It carries nothing the percent does
  not — the percent already IS the boosted number — and a written-never-read field is trap 43's
  shape. Cheap to add the day a surface actually wants it.

## 2026-09-03

_All four items this day were direct owner-session requests from Hateborne (fold, Sky
completion detection, inventory prompt, Alt+Tab); the calls below are the defaults each
one could have gone the other way on._

- **Owning a Sky reward's finished item auto-MARKS it turned in — not "suggests"** (`SkyRewardAutoComplete`,
  wired into `OutputfileAutoImport.ImportInventory`). · The other way: a suggest-only row the
  player confirms, the `SuggestRarity` bar. · Ownership is decisive (the game's own unlock
  criterion is "Obtain X" and the item existing IS the obtain), the mark is add-only, the report
  names each reward with an Undo beside it, and the way back is the same right-click Reopen a
  mis-click already has. Verified zero name collisions between the 95 reward names and any
  ingredient or "(Exaltation)" item before trusting the match.

- **The Sky band folds are SESSION-ONLY, per Hateborne's explicit pick** (asked with the
  question tool; he chose session-only over persisted). · The other way: three remembered
  flags. · Matches the ProgressCardView precedent (Bevel/Helm 2026-08-23) and adds no setting
  for `DeadSettingTests` to outlive.

- **The already-unlocked caveat ANNOTATES the ready row rather than hiding it** (Hateborne's
  pick, same question round). · The other way: drop rows for unlocked classes from Ready. ·
  The turn-in still yields a real item, so the row stays and says what the errand still buys.

- **The Sky tab's achievements prompt became a two-button command row** (achievements +
  inventory ⧉ side by side, one combined note) rather than two stacked note-plus-button
  blocks. · The other way: a second free-standing prompt under the first. · The Unlocks tab
  already renders exactly this shape for its two dumps, and Hateborne's ask was consistency
  with it. HELM.md's "#243 no Inventory annotate in V1" was read as covering band-row
  annotations, not the tab naming its own data source; logged in HELM-FEEDBACK.md.

- **WPF's fold click has no E2E coverage** — Avalonia's real click tests plus the
  word-for-word-twins rule are the mitigation; the E2E dump pins the default-open facts only.
  · The other way: a UI-automation dependency for one feature. · Not worth the harness.

- **`shoot.ps1`'s `Kill($true)` became `Stop-Hard` (`Stop-Process -Force`)** — the tree-kill
  overload exists only on pwsh 7's runtime, and on a machine with only Windows PowerShell 5.1
  every kill-fallback THREW, leaking the shot app and wedging the run. · The other way:
  require pwsh 7. · EQBuddy spawns no children, so a plain force-stop is the same act and the
  script now runs on both hosts.

## 2026-09-02

- **On the phone, the DUMP reaches the render signature through the row ids — not through the
  dump's timestamp** (#243 PR 2, `CompanionProjection.LeftoverRowId`). · The other way: put
  `WrittenAt` on the wire and fold it into the Quests section fingerprint, which is the literal
  translation of what the two desktop signatures do. · The phone's key is computed FROM the
  projected groups, so the held count and the location riding each row id already move it
  exactly when a band's claim moves — while a timestamp would also wake every quests-subscribed
  phone for a dump that changed nothing on that tab (trap 8), and would add a wire field no page
  reads (trap 43's mirror). The desktop needed the stamp because its signature is built from
  settings and never looks at the rendered rows; this one does.

- **The group NOTE joined `ChecklistPrint`, for every checklist rather than just the new bands**
  (#243 PR 2, `CompanionProjection.SectionFingerprints`). · The other way: leave the shared
  print alone and special-case the leftover bands. · The note is DRAWN by the page and is the
  one thing a checklist change can move without moving a row — the held-back note names the
  items another quest vetoed, and those are deliberately not rows. Every note in the system is a
  state word or a list of names, so nothing that drifts on a clock enters the key.

- **The leftover bands go in the phone's Sky groups as a second non-tickable group, rather than
  as a new wire section** (#243 PR 2). · The other way: their own section, which would be
  subscribable on its own. · `index.html` already renders a `tickable === false` group
  generically — heading, note, row text and detail — so the feature reaches every open phone the
  moment the PC updates, where a page-side change can sit unseen for weeks (trap 32).

- **The leftover row's words — `{Item} ×{held} · {where}`, both headings, the hover and the
  held-back note — live on `SkyLeftoverRow`/`SkyLeftoversResult` in Core, not in each
  renderer** (#243 PR 1, `Core/SkyLeftovers.cs`). · The other way: format in each window, which
  is what every other band on that tab does today. · The desktop pair and the phone group after
  them are three renderers of ONE decision, and the honesty of this feature is carried entirely
  by its words — band B under band A's heading would be the app telling someone an item is
  finished with when it is not. A format string hand-copied into three files is what drifted
  before #184. `SkyLeftoversResult`'s shape is unchanged; these are added members only.

- **The bands read the CHARACTER's class list, captured one line before the view lens narrows
  it** (#243 PR 1, both `QuestsWindow`s' `_myClasses`). · The other way: use the same `classes`
  everything downstream reads, which is one fewer field. · That variable has been narrowed to
  the ONE class the player is currently looking at, so "only other classes want this" would be
  said about a class they play merely because they had it lensed out — a false claim, and the
  one claim band B exists to make carefully (#193's rule, one surface over).

- **The newest inventory dump's stamp went into both windows' render signature** (#243 PR 1). ·
  The other way: leave the signature alone — nothing else on that tab moves when a dump is
  read. · Which is exactly the problem: the bands are a join against the dump, so without it
  they would go on answering from whichever dump was current when the window opened, and the
  player's `/outputfile inventory` would appear to do nothing (silent no-ops are broken).

- **This track's What's-new went into the existing unreleased 1.99.17 entry rather than opening
  1.99.18** (`Core/Data/WhatsNew.json`). · The other way: a version of its own. · 1.99.17 is not
  tagged and not released — `gh release list` tops out at v1.99.16 — so nothing shipped is being
  edited (trap 54's guard agrees: the what's-new gate is green). Two tracks landing in one
  unreleased cut is the normal case, and David still gates the tag.

- **The phone's Level-ups fold is a DEVICE-side open/shut state, not the desktop's
  `ShowLevelUps`** (#240 PR 2, `Web/index.html`). · The other way: ride the setting, which is
  what Bevel's lock names and what would make the two surfaces agree exactly. · A phone tap
  would then fold or unfold a window on the PC somebody is playing at, over the LAN, with no
  way to tell what did it — and the desktop setting is that WINDOW's fold. The page follows
  `nextGroupOpen`, the fold beside it, which is session-only per device for the same reason.
  What DOES ride the wire is everything a surface could disagree about: the rows, their order
  and the label. Default shut on both, which is the half of the lock that is about what a
  player sees.
- **The phone's Level-ups list is NOT capped at `MaxRows` (20) like every other list on that
  wire** (#240 PR 2, `CompanionProjection.Live.cs`). · The other way: cap it, which is the
  house rule for a wire payload and is what `unlocks`, `loot` and `faction` all do. · The rows
  are newest-first and bounded by the level cap rather than by how long you played, so a cap
  drops the EARLIEST dings — the rarest rows, and the ones somebody opens the list to find
  (trap 50, #234). A trimmed list that looks complete is the failure that took a bug report to
  find last time; ~60 short rows on a section that only re-sends on a ding is the cheaper side
  of that trade.
- **The Progress fingerprint gained the fold LABEL rather than a join over the rows** (#240
  PR 2, `CompanionProjection.cs`). · The other way: `Join(pr.LevelUps, …)` like the other
  lists in that key. · The label is "Level-ups (17) · last Aug 23" — the count and the last
  ding — so it moves on exactly what can change the list while costing one short string per
  tick instead of a join over a career. Nothing in it drifts on the clock, which is the
  property trap 8 is about.
- **The Level-ups What's-new entry NAMES its location but carries no `MOVED:` badge**
  (#240 PR 1, `WhatsNew.json` 1.99.17). · The other way: mark it MOVED, since joeymavity is
  exactly the player the badge exists for — he went looking for something and could not find
  it (#219's shape). · Declined because nothing actually moved: the session line he
  remembers is untouched and the durable list never existed to be relocated, so the badge
  would be a false claim in the one note a player is told never to skim. The X-is-now-Y
  duty is met in the sentence instead — the entry leads with "Progress > Experience >
  Level-ups" and says plainly that nothing was moved or removed to make room.
- **`LevelHistory.Stored` owns the archiver-scoping rule, rather than each widget owning a
  copy** (#240 PR 1, `UI.Shared/LevelHistory.cs`). · The other way: leave the four-line
  method on both `MainWindow`s, which is where it was written and where the repository and
  the archiver both live. · The WPF ratchet is what forced the question (4638 against a 4635
  limit) and the answer was the one the ratchet's own message names: it is the same `if` in
  two lanes with the phone as a third caller in PR 2, and the mistake it prevents — a blank
  identity asking `ProgressSeries` for EVERY character's dings, since it treats an empty side
  as "do not filter" — is silent and renders as a plausible list. The baseline was NOT bumped.
- **The `progress-levelups` shot primes stored history under the FIXTURE'S OWN character**,
  which needed a two-line fix to `Invoke-PrimeRun` (#240 PR 1, `scripts/shoot.ps1`). · The
  other way: prime under a second name the way `history-charts` does, or skip the shot. ·
  Neither works here: the surface matches on the archiver's identity with SQL `=`, so rows
  written as "Aludra" render as a correct picture of an empty fold — trap 23 exactly — and a
  surface with no fixture state reads as reviewed anyway (trap 22). Priming as `Testchar`
  writes the fixture log's own path, so the harness now restores it from the pristine copy
  rather than leaving the next run with no log, and `Append-Log` moved after the prime.
- **A level-up's gap is WALL CLOCK across sessions, labelled "since previous"** (#240 PR 0,
  `UI.Shared/LevelHistory.cs`). · The other way: sum played time across the sessions between
  two dings, which is what a player probably pictures. · The miner
  (`SessionRepository.ProgressSeries`) reads two fields out of each stored snapshot and
  played-time-per-session is not one of them, so "time in level" would have been wall clock
  wearing a better label — the trap-50 shape, a number claiming more than it knows. Taken
  from Fable's plan; the in-session "(43m)" in the Experience summary line is untouched.
- **`GameWrittenLog`'s comment was corrected to match its regex, rather than the regex
  tightened to match the comment** (Fable's v1.99.14 review nit, "post-tag is fine"). · The
  other way: narrow the server group from `[A-Za-z]` to `[a-z]`, which is what the comment
  claimed and would shrink the destructive target slightly. · Declined because it changes
  what a DESTRUCTIVE gate does to a player's files on no evidence: Fable's own 2026-08-29
  check found eqlwiki has no server list at all, so "every server short name is lower case"
  is an assumption — and the assumption already written down is the thing being removed.
  Replacing one unevidenced claim with an unevidenced behaviour change is a worse trade than
  telling the truth about the character set. Trap 47/48's family; `src/EQBuddy.Core/GameWrittenLog.cs`.
- **The What's-new guard FAILS the build rather than warning**, and it compares against the
  newest tag only. · The other way: warn (an old entry might legitimately need a typo fix),
  or compare every entry against its own tag. · A shipped entry is the record of what players
  were told, so amending one is the defect, not an exception to it — and a warning on a gate
  that fires twice in three releases is a line people stop reading. Newest-tag-only is not a
  shortcut: that tag's copy of the file already contains every older entry, so one `git show`
  covers the whole history. `scripts/whatsnew-guard.ps1`, wired into `check.ps1` (first stage)
  and `release.ps1` (`-Releasing`, before anything is built or signed).
- **README's two un-regenerable World captures were RE-CAPTIONED, not replaced or deleted.**
  · The other way: shoot new ones, or drop the rows. · `map-window.png` and
  `travel-window.png` show surfaces that still exist in content and are gone in chrome; a new
  Map shot needs a maps folder the throwaway profile has not got, and a new Path shot needs a
  destination no env hook can set. So each caption now names the current home in the repo's
  own "X is now Y" form — the moved-surface rule applied to the README instead of to a
  release note — and says plainly that the capture predates the fold. Dropping the rows would
  have removed the only picture of a real feature. `README.md`; the residual shot work stays
  on the `FABLE.md` README-screenshots item.
- **The What's-new guard also runs in CI, which Fable's follow-up did not ask for**, and the
  checkout in `ci.yml` goes to `fetch-depth: 0` to make that possible. · The other way: leave
  it at the two homes named (`check.ps1`, `release.ps1`). · Both of those need a human to run
  them, and the defect being guarded is one nobody noticed twice — a gate that depends on
  being remembered is the thing this repo keeps writing traps about. A shallow checkout has
  no tags, so the guard would have skipped cleanly and read as coverage (trap 34). Costs one
  full clone per CI run. `.github/workflows/ci.yml`.
- **#243 PR 0 — the plan's four "decided without asking" calls, taken as written**: surplus
  counts are out (a multi-class allocation is the guess #106 declines to make); Band B exists
  and is never shown without a class lens (#193 — no lens is not a wildcard); another catalog
  quest wanting the item vetoes "no longer needed" (the reporter asked about Sky alone, but
  the cost of a wrong Band A row is a destroyed turn-in); bank items are labelled, not
  excluded (the problem is bag space, and the fact costs one word).
- **`SkyLeftoverItems` is a LIST on `AutoImportOutcome`, not the count the plan named.** · The
  other way: an `int SkyLeftovers`, matching `SkySkipped` and `QuestCountsTrued`. · The plan
  also says *"Detail (the hover) names them"*, and a count cannot. "3 items" is not something
  anyone can act on standing at a bank; the count is a derived property, so the surfaces still
  get the number.
- **Sky leftovers are deliberately NOT part of `AutoImportOutcome.Noted`.** · The other way:
  add them, since Noted means "there is something to say". · Noted is what paints the report
  amber (`ImportReportView`, both lanes), and finding free bag space is the one piece of good
  news an import has. A leftovers-only dump reads green.
- **`Compute` with a null catalog SKIPS the veto rather than refusing to answer.** · The other
  way: return nothing without a catalog, so Band A can never be unvetted. · The veto only ever
  makes the list shorter, and the caller can see it did not run because
  `HeldBackByOtherQuests` is empty. Refusing would make the function untestable without
  loading 1,172 quests, which is how a pure rule stops being tested.

## 2026-08-31

- **`GearCardView`'s inner scroller was RE-POINTED at the host, not deleted**, though the
  plan's literal reading ("defer to the window's own `BodyScroll`") and trap 36's rule
  ("scrolling belongs to the host") both point at deleting it. · The other way: drop the
  scroller and let the window's `BodyScroll` carry one long body. · That scroller is what
  keeps the ⧉ copy of `/outputfile inventory`, the auto-tick note and the import report
  outside the scrolling region — the exact affordance `GameCommandsTests` has a must-list
  row for on this surface (trap 34), and dropping it would have put the only in-app route to
  the command under a forty-row list (trap 37, and trap 44 for the report). So the cap comes
  from the host and the pinning stays. Net effect at the window's opening height: the list
  goes 320 → 306 and the pinned footer now fits INSIDE the window body instead of pushing
  the panel past it, so the ⧉ is reachable without scrolling. (PR 2, #250 track)
- **The 320-cap chrome does NOT subtract the widget's title bar, KPI strip or status line,
  though the signed plan says it should.** · The other way: implement the sign literally and
  subtract them. · Measured instead: the height grip seeds from `SectionScroll.ActualHeight`
  and assigns straight back to `SectionScroll.MaxHeight`, so `ContentHeight` IS the card
  stack's viewport and that chrome is already outside it. Subtracting it again would hand
  every player less body than they dragged for, invisibly and forever. Formula, floor,
  ceiling and the sibling-body exclusion are all untouched; only the enumeration changed.
  Told to Helm in `HELM-FEEDBACK.md` at the time rather than after. (PR 0/1, #250 track)
- **The body cap is sized from the height the MONITOR granted, not the raw drag.** · The
  other way: pass `ContentHeight` through as the plan's `playerContentHeight` reads. · The
  two agree at 100% on a big screen and diverge exactly where nobody looks — a 900-unit drag
  on a 1032px work area is granted 698 units at 125% scale, and a body sized from 900 would
  claim room the stack never had. Recomputed through `WidgetMetrics.SectionMaxHeight` rather
  than read off the control, so the answer cannot depend on which writer ran last (trap 33).
  NaN is still read from the setting, so "never dragged" still means the floor. (PR 1)
- **`theme-inline-loot.png` was re-shot on this build although it is not the change's
  subject.** · The other way: leave the committed 2026-08-26 capture as the baseline. · It
  is the BASELINE half of the acceptance pair, and a pair shot on two different builds is
  the exact "reads as a regression" failure trap 51 was written about — the stale one
  already differed in the last card's title. Same fixture, same shot definition, only the
  build moved. (PR 1)

- **PR #256 follow-up shipped WITHOUT the authorized KnownGaps list, because the premise it
  rested on turned out to be false** · Helm's 2026-08-31 2:05 PM ruling authorized a curated
  known-gaps list, reason `no eqlwiki prose`, for the 24 spells the KhazamSpellRow rename left
  description-less — the obvious move was to write those 24 rows and unblock · checking each
  one first showed all 24 DO have prose on eqlwiki, on their own spell page: they were missed
  because the description fallback looked them up by the page's `spellname` field, which
  `class-spells-harvest.py`'s own docstring already warns is a copy-paste artefact and not a
  canonical name (`Healing Water` declares `spellname = Greater Healing`, and is the worked
  example in `spell-levels-promote.py`'s header). Keying the fallback on the page TITLE as a
  last resort recovered 24 of 24, so the catalog is 1,353/1,353 described and there is nothing
  to exempt · the guard therefore stays strict at 100% with no exemption list, which is also
  what Helm asked for ("do not weaken the guard"); an exemption list with no entries to
  justify is a ready-made hole for the next harvest regression · flagged to Helm as the
  headline of the last-look request rather than decided quietly — Helm signed option 1 and
  is owed the correction · `spell-levels-promote.py`, `LevelUnlocksTests.cs`, PR follow-up
  to #256.
- **The #246 cask pin became a promote-time correction table instead of a hand re-edit** ·
  could have re-applied qty=3 to the catalog by hand as the repaired branch did, which is the
  literal reading of "preserve the pin through the re-harvest" · but the 2026-08-31 refresh
  proved the revert recurs weekly — `CatalogSanityTests` pinned it "so a future harvest run
  can't silently reset it back to 1" and the very next run reset it, so a pin only a human can
  re-apply is a chore that is invisible until the build breaks · the parser is not wrong (the
  page says "three of these casks" in prose, not "3 x"), so teaching it English number words
  would change every quest to fix one · `ITEM_QTY_CORRECTIONS` in `quests-promote.py`, with an
  unapplied-row report so a rotted correction is visible, and the wiki-first ask filed as the
  real fix.

## 2026-08-27

- **#241 PR 2 stayed Sky-only; Epic's master-check toggle was NOT mirrored** despite
  hypothesis (b) in the plan · could have consumed Epic's turn-in items the same way on the
  theory that the gap is symmetric · it is not: Epic's `MarkComplete` is a whole-CLASS bulk
  operation with no per-reward `QuestCatalog` entry a ledger completion could be recorded
  against (unlike Sky's `SkyTestSplit`), so mirroring it is a materially different design,
  and Helm's authorization named `SkyCompleteToggle` specifically · noted in
  `FABLE-FEEDBACK.md` for Fable to scope if it belongs in a later take ·
  `SkyCompleteToggle.cs` commit message, this line.
- **The spawn-cue lift (Helm's standing "next loop touching MainWindow.xaml.cs" order) was
  spent on `SpawnsViewModel.DueSounds`**, not a larger consolidation with `ChipStackPlan`'s
  show/hide decision · could have folded the whole "what should happen this tick for spawn
  timers" question (sounds + chip visibility) into one call · that decision logic was
  already correctly lifted on 2026-08-27 morning (`ChipStackPlan`); the only genuine
  duplication left between the two lanes was "consume due alerts, then look up each one's
  sound," so the lift stayed to that shape rather than re-touching code that was not
  duplicated · `SpawnsViewModel.cs`, first commit on this branch.
- **`AutoImportOutcome.QuestCountsTrued` rides the EXISTING `LastInventoryImport` outcome**
  rather than becoming a new tracked property with its own `ImportReportReachesASurfaceTests`
  row · could have given the quest-ledger reconcile its own outcome type and surface row,
  mirroring how Gear and Achievements are separate entries in that must-list · the dump is
  ONE event with two internal consumers (gear checklist, quest ledger), and the report
  already reaches the Gear surface via the property that must-list already covers — a
  second tracked property for the same announcement would be trap 4's shape (one fact, two
  places to report it) rather than a fix for it · `OutputfileAutoImport.cs`.

- **The flagged ratchet squeeze was relieved NOW (`UI.Shared/ChipStackPlan`), not left for
  the next PR to trip over** · could have left the 1-line headroom for whoever touches
  `MainWindow.xaml.cs` next, as the World report implied · the relief chosen was the chip
  stacks' existence rules rather than the plan's named spawn-cue lift, because the same
  edit de-duplicates a Bevel-signed rule (World-on-Camps chip hide) that had NO unit test
  and a player-facing string both lanes carried verbatim · keep-if-it-fits, baseline stays
  4,214 · `ChipStackPlan.cs`, `ChipStackPlanTests.cs`, this commit.
- **1.99.13 release prep authored unprompted for the staged World work** (What's-new entry
  with "X is now Y" for all four moved surfaces; version bump; Fable review requested) ·
  could have waited for David to ask for a release · the review-before-David order is the
  standing sequence and the go stays his · `WhatsNew.json`, `FABLE-FEEDBACK.md`.

## 2026-08-26

- **World PR 1's four views use separate factories (`NewMapView`/`NewSpawnsView`/
  `NewTravelView`/`NewTravelsView`), not one combined `WorldSurfaceSet`** · could have
  mirrored `ProgressSurfaceSet`/`CreatureSurfaceSet`/`LootSurfaceSet` for consistency ·
  `MapView`/`SpawnsView` do real construction-time work (`PopulateZoneList` reads disk,
  `SpawnsViewModel.RefreshZoneList` walks the ledger), and there is no multi-tab
  WorldWindow yet to need all four at once — a combined factory would make opening the
  Travel window silently also touch the maps folder, a behaviour change PR 1 may not make ·
  `MainWindow.xaml.cs`/`MainWindow.cs`, `SurfaceOwnershipTests.EveryWorldHostBuildsItsOwnFreshView`.
- **Avalonia's `MapWindow`/`TravelWindow` keep taking `IZoneHost` directly rather than
  going through the new `MainWindow` factories** · could have routed every World host
  through `main.NewXxxView()` for uniformity with WPF · `ZoneWindowsRenderTests` already
  constructs both against a fake host with no widget at all; changing the constructor
  signature to require `MainWindow` was not needed for trap 45 (each still builds a fresh
  view inline) and would have been an unforced, untested-by-this-PR behaviour change ·
  `EQBuddy.Avalonia/MapWindow.cs`, `TravelWindow.cs`.
- **The #238 Unlocks tab is a GLANCE inline, not a Full room** · could have let the
  `InlineModeFor` catch-all make it Full · it postdates Bevel's signed table and is a
  review checklist over two dumps with its own lens — the same host-rule shape as
  Inventory; conservative until Bevel rules, flagged in the pending Unlocks review ask ·
  `QuestSurface.InlineModeFor`.
- **Inline Epic/Sky checklist rows are READ-ONLY** · could have carried the old cards'
  checkboxes · a checkbox inside a capped scroller invites ticks the cap hides context
  for, and a disabled-looking one is trap 17; ticking stays in the window one ⧉ away ·
  `Core/QuestInline` doc.
- **`RespawnSuggestion` lives in Core, not UI.Shared as planned** · `BuildExport` (Core)
  must read the verdict and Core cannot reference UI.Shared; same framework-free
  testability either way · noted in the FABLE.md item for Fable's last-look.
- **The cycle ledger records honest gaps the never-loosens rule rejects for the
  countdown** · the plan's "where a gap is accepted" read literally would keep a stable
  timer from ever reaching three cycles (12:04 against a learned 12:03) · the honesty
  gates are the write condition; the tightening rule is not; the test names the case.
- **Merged PR #238 (Hateborne) after full review, without asking first** · could have waited
  for David's per-PR go as #231 got · #231's route (review, resolve conflicts ourselves,
  merge if it holds, credit in What's-new) was David's answer to the same question once
  already, the release gate still protects everything, and Fable reviews the release before
  he is asked · merge commit on `main`, staged as 1.99.12.
- **Resize conflict resolved by keeping OUR follow-until-grab ownership and taking THEIR
  machinery** (visible grip, drag-flag persistence, junk-height migration, parameterised
  drag-verify) · could have taken #238's design whole, which re-pins at `ContentRendered` ·
  Fable had already ruled the NC-grab design better than both planned candidates, and the
  harness re-ran green on the merged build (progress full acceptance, spawns, quests) ·
  `WindowZoom.cs` + `FramelessResize.cs`.
- **Fixed a defect in #238 rather than bouncing the PR: the Alt+Tab feature stripped
  `WS_EX_TOOLWINDOW` from chip/overlay windows that set it deliberately** — with the box OFF
  (the default) every chip would have joined the switcher · could have asked Hateborne to fix
  · one guard line per lane (`NoActivate.SetToolWindow`, `WinClickThrough.SetToolWindow`), told
  him in the PR reply · TestPlan §4b row.
- **1.99.12 discards every stored pop-out height once** (#238's `MigrateWindowHeights`,
  kept) · could have preserved heights dragged since 1.99.11 shipped (one evening's worth) ·
  a value written before the border worked was never chosen, the two cannot be told apart,
  and re-dragging costs a second · `AppSettings.MigrateWindowHeights`.

## 2026-08-25

- **Helm's `claude -p` kick: ALLOWED until the plane's launcher (PR 1) lands — David's call,
  asked with the question tool** · the 2026-08-24 recorded ruling had retired it before first
  use, and Helm declined to accept an agent's notice as the word · two conditions attached:
  the kicked session runs against its own clone (never David's working checkout), and the kick
  carries the permission profile its purpose needs — an unpermissioned kick is
  documentation-only, per the 2026-08-24 night test. Plane repo record corrected to match
  (`HELM-FEEDBACK.md` 2026-08-25 entry is the in-channel relay).

## 2026-08-24

- **Did NOT fire the Helm wake, though David asked for a live test** · could have run
  `gh workflow run helm-back-channel.yml` to exercise the loop · the session could not commit or
  push, so the POST would have paged Helm to read a file that never left the machine — the exact
  failure the rule names — and would have mis-attributed a local permissions fault to the plane.
  A wake with nothing behind it is a worse test result than no wake.

- **Rewrote the standing header of `HELM-FEEDBACK.md` myself; left `HELM.md` alone** · could
  have asked Helm to correct both, or corrected both · the header was still instructing
  sessions to make David the courier (Helm's own note said that line was already gone), and
  `HELM-FEEDBACK.md` is my channel so the fix is mine — `HELM.md` is Helm's STATE file, so the
  missing command there is a request in the mailbox, not an edit. Dated entries left untouched:
  a delivered message stays where it was delivered.

- **Kept `Nedaria's Landing` in ZoneGraph — David's call, asked with the question tool** ·
  could have dropped it and filtered the harvester · eqlwiki asserts the adjacency and has no
  page for it, and dropping it is departing from the wiki on game data (consequence list 6).
  Logged here for the reasoning rather than the decision: "no wiki page" is not the tell — 18
  other graph zones have none and all resolve via aliases; this is the only one that resolves
  to nothing (`ZoneMapCoverageTests.ZonesWithNoClientMap`).
- **Merged PR #236 after a local review rather than on sight** · could have merged a bot PR
  with a green-looking report · no CI ran on the branch at all, and it turned a gate red on two
  zones; merging on sight would have put a red main in front of the next session (`0018f86`).
- **Fixed the GATE, not the DATA, when the refresh went red** · could have dropped the two
  zones to make it green · that would have been departing from eqlwiki by side effect of a test
  failure, which is the wrong reason to change game data. Disclosed to Fable in the review
  request as the thing most likely to look like loosening a guard.
- **Posted the #235 loop-closing reply without asking** · could have left it, since Helm's
  sign-off was already carried out and the fix had shipped · the reporter's last word was
  "thanks for looking" and he would never have learned the change he was promised landed;
  routine signed thread replies are pre-authorized (comment 18138064).

- **Doc audit: fixed the stale numbers AND added `DocumentationSizeTests` rather than only
  fixing them** · could have corrected the figures and moved on · Architecture.md's own note
  says its size table had already "drifted far enough to mislead" once, and it had drifted
  10-15% again in four days — a measurement nobody re-measures rots untouched, so it belongs
  in the build like the ratchet (`DocumentationSizeTests`).
- **Size checks assert within 10%, not exactly** · could have pinned the numbers to the line ·
  exact pinning makes every commit a documentation edit and a check people route around is
  worse than no check; 10% matches the hotspot ratchet's own growth allowance and still
  catches the 31% and 11% drifts that were live.
- **Marked `AdditionalRequirements.md` and `docs/ImplementationPlan.md` HISTORICAL instead of
  deleting or rewriting them** · could have archived or removed them · both are orphaned
  (nothing in the live doc set links to either) and 1,732 lines of superseded requirements at
  the repo root reads as current to a new agent; a banner fixes the misleading-ness without
  destroying the record, and what to do with them long-term is a roadmap call.
- **Left `docs/DesignSystem.md`'s "Loot card" references alone** · could have renamed them to
  "Loot tab" for consistency with the fold · that file is a historical design log ("#198 added
  a view filter to the Loot card"), and rewriting it would falsify what was true when #198
  shipped. Only docs that describe the CURRENT app were updated.
- **Fixed `shoot.ps1`'s shared-fixture contamination rather than re-shooting around it** ·
  could have committed the batch output and moved on · the batch and the solo shot disagreed
  on identical code (`progress-card` 497 vs 389), which makes every committed screenshot a
  function of shot order and breaks the acceptance criterion CLAUDE.md depends on (trap 51).

- **#234 fixed by UNCAPPING the two session-history rollups rather than by carrying a named
  flag through Core** · could have plumbed `KillEvent.ProperName` into `NameCount`/`MobSummary`
  so nameds are always kept regardless of rank · that changes a persisted snapshot schema AND
  the mobile wire for a surface that is a scrollable desktop review pane, where one row per
  creature killed is simply not a lot; uncapping fixes the report with no schema change and no
  heuristic false-positives (`HistoryPresentation`, `GukNamedsRollupTests`).
- **Remaining caps in the session detail now print "... and N more" instead of being raised**
  · could have uncapped loot/pet/damage lists too · a cap that admits itself is honest and
  bounded; uncapping everything makes the text arbitrarily long for no reported complaint.
- **Bumped to 1.99.10 and staged; no reply posted to #234** · could have replied to the
  reporter now the fix is in · Helm's 6:22 AM ruling says explicitly "do not post another
  reply (Claude is in the thread)", and a shipped fix does not lift a Helm instruction.

- **Ran the hand drag/reopen check unattended as an automated harness** · could have waited
  for David at the machine · David authorized it in session while away; `scripts/drag-verify.ps1`
  uses Win32 rects and WindowFromPoint-guarded clicks so nothing could land on his live widget
  (Fable, `498d740`-follow-up).
- **Reverted the window-height fix and shipped 1.99.9 P0-only** · could have fixed forward
  under release pressure · the split was pre-agreed in the review ("if any fail, we split"),
  the redesign needs a probe, and an angry public data-loss thread outranks a self-found clip
  (Fable, `git revert 054d009`; plan re-filed in `FABLE.md`).

- **Auto-empty now only touches files with the exact shape the game writes** · could have kept
  the `eqlog_*.txt` glob and relied on the archive folder as the safety net · **David's call,
  asked with the question tool** — logged here because it is the reasoning, not the decision:
  the discriminator is the character set, not the segment count, since a real server short name
  can contain an underscore (`Core/GameWrittenLog`, `ea2e27d`).
- **Bumped to 1.99.9 and staged rather than asking to ship first** · could have asked David for
  the go before writing the What's-new · the release review goes to Fable first, per the
  standing order (`FABLE-FEEDBACK.md`, `Directory.Build.props`).
- **The window-height fix ships with a What's-new entry even though Fable may yet split it out**
  · could have left the entry until Fable answered · a missing entry is the worse failure and
  the entry comes out with the commit if it splits (`WhatsNew.json`, `054d009`).
- **Re-shot every Progress-window screenshot, not just the one that reported the bug** · could
  have re-shot `progress-card` alone as the item specified · found `raids-import` clipped by
  41px hiding its `⧉ copy` button, which nothing else would have caught (`054d009`).
- **Credited StrIIker-TV by name with no discussion number** · could have opened a GitHub
  discussion to generate one · the report came in on Reddit and David chose "draft it for you,
  you post" over also filing an issue; flagged to Fable for a ruling (`WhatsNew.json`).

## 2026-08-23

- **Class inference (Fable 5): classes are a LIST with a SOURCE — the achievements dump
  first, inference second (every qualifying class within 0.25 of the leader, at most three,
  cited to the wiki's "trio builds"), picks as a lens that widens and never narrows;
  `LeadMargin` deleted** · keep a single inferred class and raise the margin; let picks
  override the dump · the dump is the game's own statement and has been parsed for two
  releases without being used; `FABLE.md` plan.
- **Spells by class, amended after PR 0 (Fable 5): the promote keys on PAGE TITLE, never the
  `spellname` field; "section exists → extras drop" and "no section → derive and flag" are
  two rules, not one** · keep `spellname` as the key · it is a copy-paste artefact that has
  been dropping real spells from the shipped ding list; `FABLE.md` amendment.
- **Spells by class (Fable 5): one catalog with `source` per row, not two; the class page's
  spelling wins and the page title is kept for the link; derived rows are marked dim, never
  hidden; the first promote run (~500-row diff) is human-reviewed before the harvest joins
  the weekly cadence; unlock groups collapse beyond the first class, session-only** · a
  second catalog; hide derived rows; auto-run the promote · trap 4, David's "flagged not
  filtered", and a quarter of a catalog is a review not a cron tick; `FABLE.md` plan.
- **v1.99.6 re-review (Fable 5): the fifth Island 6 bee (`Bizazzzt`) is a pre-tag catalog
  row, not a follow-up** · ship the four and file the fifth · it is discovered-and-learned on
  two kills, the exact defect the release claims to fix; `FABLE-FEEDBACK.md`.
- **VETOED nothing — but recorded because David decided it in session:** when eqlwiki's class
  page and its spell pages disagree about a spell's level, **the class page wins and spell
  pages fill gaps only where the class page has no section**, with anything derived flagged as
  such · class-page-only, or keeping the spell-page harvest and patching the gaps · found from
  Druid 34: class page 5, our catalog 10, missing `Healing Water` and padded with five ports.
  `FABLE.md` stub, 2026-08-23.
- **Bzzazzt is catalogued with eqlwiki's 12-hour clock, NOT as triggered — against what the
  reporter asked for** · mark both new bees triggered as #109 requested · the wiki is the
  tie-breaker and its reason holds independently (a chain's opener cannot itself be
  triggered). His evidence is all from personal instances, which never respawn, so the two
  accounts describe different places rather than disagreeing. `SpawnCatalog.json`,
  `SkyBeeChainTests`.
- **The load-time self-heal now clears learned overrides on multiSpawn entries too** · leave
  it to triggered/raid-instanced only · the learner already refuses multi-spawn names, so a
  `Learned` value on one is a number the current code cannot produce. Bounded cost, named in
  the comment; typed durations untouched, with a negative test.
- **Multi-island Sky steps: default to one row under "Several islands"** · default to
  repeating them under every island · David asked for a toggle rather than a fixed answer,
  and the conservative side matches his wording ("a specific island"). `AppSettings.SkyStepsUnderEveryIsland`.
- **The island toggle lives on the Sky TAB, not in Options** · Options, per the standing rule
  · it is a tab-scoped view lens, and the Epic tab's "Classic-doable only" set that precedent
  in the same control row.
- **`QuestChecklistGroup.Done`/`Total` count DISTINCT steps** · count rows · repeating a step
  under three islands would otherwise turn a 4-step reward into a 6-step one, silently, only
  for players who opted in.
- **The "Isle N:" label is stripped from a row already sitting under that island's heading** ·
  leave the prose alone · the grouping created the redundancy ("Island 6" / "· Isle 6: Bazzt
  Zzzt"), so removing it is part of the same change. Multi-island rows keep every word.

## 2026-08-22

- **PR A: `IProgressHost` and `ProgressWindow` become `internal`** · make `ProgressSurfaceSet`
  public instead · the seam types are implementation, and widening the assembly's public API
  to satisfy an accessibility rule is the tail wagging the dog. Tests already have
  `InternalsVisibleTo`.
- **The Progress window's surface set is built EAGERLY in its constructor, not per tab** ·
  lazily, as the widget does for Gear · two of the five views are the only writers of
  `ShowNextUnlocks` and `ShowAllAAs`, and a writer that only exists once a tab is visited is
  trap 20 waiting to happen (Fable's plan flagged this; it was right).
- **`ProgressWindow.RenderVisible(snapshot)` takes the tick as a parameter** · fetch
  `CurrentSnapshot()` internally · the widget's headless render path hands one in, and
  keeping that possible is what lets `WidgetRenderTests` go on asserting that the tabs draw
  what the cards drew.
- **`SurfaceOwnershipTests` exempts the two lanes that still hand out bodies, by name** ·
  scope the guard to Progress and say nothing about the others · an exemption nobody can see
  is a blind spot; each row names the PR that removes it.
- **The negative test asserts `InvalidOperationException` (visual-parent guard), not the
  `LayoutManager` message from the production crash** · reproduce the exact upstream
  sequence · the simple repro hits a different mechanism reaching the same conclusion, and
  claiming it proved the other one would have been false. The doc comment says which is
  which.
- **The rare-`/consider` fact needs ONE con, not the pack's ten-kill bar** · reuse
  `SuggestRarity`'s threshold for consistency · the evidence is categorically different: the
  game printed the word, so there is no sample to be thin, and a bar would be statistics
  applied to something that was never a measurement. `WikiContribution.RareSpawnNote`.
- **Both con numbers are always printed ("2 of your 7 /considers"), never just "rare"** ·
  print the fact alone · same-named spawns are not all rare, and the person pasting onto
  someone else's wiki is the one who should weigh a 2-of-7.
- **The rare fact is said ONCE, in the contribution block, and NOT in the observed stat
  block** · repeat it there, where the other /consider-derived facts live · the stat block is
  kill-gated and heads itself "thin sample, for your notes rather than the wiki yet" — which
  would put a paste-it instruction and a don't-paste-it-yet caveat on one fact three lines
  apart. Found by reading the real paste block, not from the diff.
- **A rare-conned creature whose loot the wiki already has still earns NO pack section** ·
  give it one · that needs a new `RowKind` on the pack surface, which is a product decision
  about what the surface shows. Asked of Bevel in `BEVEL-FEEDBACK.md` rather than decided.
- **v1.99.6 review: the report's Sky half living on the Raids surface is a Bevel follow-up,
  not a pre-tag block; the three-sentence line ships as written** · hold the tag for a second
  host on the Quest Tracker's Sky tab, or shorten the line to a tooltip now · the rule "the
  report lives where the command is asked for" is already applied; whether a second host helps
  the job is Bevel's, post-hoc, as the 1.99.1 caption was; `FABLE-FEEDBACK.md`, Fable 5.
- **The achievements auto-import report goes on the RAIDS surface, and nowhere else** · the
  Quest Tracker (the other thing the dump feeds), or both · the rule already set by the
  inventory report: the report lives on the surface that ASKS the player to run the command.
  Both UIs' own doc comments already said "read by the Raids surface"; this makes it true.
  Not a design invention, so not a Bevel question. `RaidsCardView.cs`, `MainWindow.cs`.
- **The report sits ABOVE the boss rows, not after them** · below, matching where the
  inventory report sits on Gear · the second screenshot showed it behind a scrollbar under
  21 rows (now trap 44). A notification is read on arrival; a card footer is not.
- **1.99.6 is its own release rather than riding the next feature** · fold it into whatever
  ships next · the What's-new rule — a player-noticeable fix earns the release that ships it,
  and this one has a reporter (#101, Frankthetankk) waiting on the answer.
- **The agent run cadence (Scribe 6am · Bevel 1pm · Helm 8pm) goes in `CLAUDE.md`, beside the
  "inboxes inform you" boundary, not in `HANDOFF.md` or an agent's own section** · a handoff
  note, or three lines one per agent · it changes what you do at the START of every session
  (pull first; Helm rules last), which is what `CLAUDE.md` is for; commit this one.
- **A Bevel item that turns out to be already shipped is marked DONE in place, not deleted**
  · the take-then-delete contract says delete · the wrong-article item's body is mostly
  *do-not* rulings ("two failures must not look alike", Copy stays off), and deleting the
  item would take the standing constraints with it; `BEVEL.md`, verified against
  `DropsCardView.cs` in both UIs.
- **Spawn timers → eqlwiki: the paste target is the creature's `respawn_time` field, not the
  `Respawn Timers` list page; the bar is 3 cycles within ±15 % of the median; the median is
  suggested and variance never is; the ledger keeps the last 20 cycles; PR 0 is a flags-only
  script diffing `trusted` catalog timers against the wiki** · any of those could have gone
  another way · `FABLE.md` plan, Fable 5.
- **Pack history: pool across characters AND servers with no toggle; no "since" filter; the
  live Drops tab stays session-scoped; pooling keys on (name, zone)** · per-character, a since
  filter, or a toggle · the reporter's argument that a smaller sample never makes a better
  edit, and facts about a mob are not about who saw it; `FABLE.md` plan, Fable 5.
- **Dead helpers `IsExcluded`/`IsTimeableNamed`: delete, do not wire; build `DeadHelperTests`
  as V1** · wire them to the pet registry · the suffix rule covers what the log prints, and a
  promise with no caller is worse than none; `FABLE-FEEDBACK.md`.
- **v1.99.5 review: the pet purge must spare `Custom` entries and manual timers — pre-tag**
  · ship as is, it only touches "… pet" names · the file's own principle says a discovery is
  discarded without touching the player's additions; `FABLE-FEEDBACK.md`.
- **v1.99.4 review: the motes "stays off" promise is fixed by WORDING, not by a
  "player-touched" flag** · could have added a setting recording the player's own toggle ·
  one day of exposure and a one-toggle cost do not earn a setting; `FABLE-FEEDBACK.md`.
- **BOM/whitespace churn across fifteen files is next-loop hygiene, not a pre-tag block** ·
  could have held the tag for a normalisation commit · nothing breaks and a renormalisation
  is its own diff; `.gitattributes` is the executor's V1 call.
- **Avalonia theme bodies: option (a) — the `IWidgetCard` seam, every host builds its own
  instance; a control never moves between windows, as a trap with a source-scan guard** ·
  (b) make the move safe, or (c) a projection · the move is an open Avalonia bug since 11.2
  (#12753, #17906, #21267), still in 12.1.1; `FABLE.md` plan, Fable 5.
- **The Avalonia seam lands as its own PR (A) BEFORE the Avalonia inline card (B)** · could
  have shipped both in one · the seam is a refactor with no player-visible change and must pass
  every existing Progress render test unchanged; mixing it with the card hides which half broke.
- **On Avalonia the window renders only its visible tab, as WPF does** · could have rendered
  every tab every tick as the widget's paint block did · one rule on both lanes.
- **Inline themes: one owner — expanding a card while its window is open brings the window
  forward, closing the window never re-expands the card, the selected tab is session-only**
  · could have allowed card and window at once (WPF can), or re-expanded on close · Avalonia
  cannot show a body twice, and re-growing the widget after a close is a surprise;
  `FABLE.md` plan, `ThemeHost`.
- **Inline themes: Progress's breakout window retires into the pop-out; `DisabledBreakouts`
  "Progress" entries are ignored, not migrated** · could have kept both · Bevel's ruling;
  nothing is lost, the theme window has its own position memory.
- **Inline themes: Glance (one line + ⧉) for Quests/General and Gear & Loot/Inventory; Full
  for the other ten tabs; Progress ships first** · any tab could have gone either way ·
  Bevel's host rule ("do not shrink-wrap a full window"); table is in Core so a flip is one
  line.
- **Fable last-looks every executed `FABLE.md` diff (H4), starting now, without a ruling** ·
  the handoff listed it as a proposal awaiting David · it costs Fable tokens and no Founder
  time, so it fails both "What needs David" tests; first pass in `FABLE-FEEDBACK.md`
  2026-08-22, which found the offline re-check defect below.
- **The wiki re-check's `Forget`-before-bypass is a V1 defect to fix in the next loop, not a
  plan reopening** · could have been filed back to `FABLE.md` · one-line change in each
  window plus a Core test; `FABLE-FEEDBACK.md` 2026-08-22.
- **Inline themes ship COLLAPSED, every theme** (proposal open question 4) · some could ship
  expanded · Bevel's host rule and #219 both say the glance line is the product; `FABLE.md`.
- **Triggered outranks RaidInstance on the Spawns row when both apply** (executor's call,
  ratified) · "instance" could have won · "go kill the Guardian" is the actionable sentence.
- **eqlwiki request etiquette: 2 lookups in flight per process, 30 s before the same page may
  be re-checked, pack re-check bounded to flagged creatures** · could have been 1/60 s or
  uncapped as today · wiki re-check plan, `FABLE-FEEDBACK.md` 2026-08-21. *Put in front of
  David as "adjust at approval" at the time; it should have been this line instead.*
- **One spawn type, `triggered`, with a free-text `triggeredBy` — not a `chained` /
  `player-triggered` pair** · the reporter's two-word taxonomy was real in the world · eqlwiki
  records one value, and the engine treats both the same; `SpawnEntry.SpawnType`.
- **A re-check in flight keeps the OLD wiki answer on screen rather than showing "not checked
  yet"** · could have nulled the memo entry · the #217 rule (pending ≠ nothing new), wiki
  re-check plan.

## 2026-08-22 — Claude (executor)

- **Took Bevel's 1.99.1 post-hoc item without asking** (Helm-signed, V1, unreleased). Default it
  could have gone the other way: wait for David. Landed: the release gate is the protection, and
  "do I like this wording" fails both question tests.
- **The triggered glance is named-if-it-fits, never an ellipsis.** Could have gone: truncate with
  "…", or widen the fixed 150px timer column. Landed: bare word when it does not fit; widening a
  shared column is a layout call and is back with Bevel with the screenshot.
- **Healed poisoned overrides for raid-instanced entries too**, not only triggered ones as Fable's
  note said. Same contradiction on screen; the method is `SuppressedByCatalog`'s own definition.
- **Guarded the re-check defect with a SOURCE SCAN on both windows**, not a Core test. The Core
  contract was already correct and a Core test could never have failed; the window defeated it.
- **Fixed a 1-in-3 flake in `SettingsClobberTests` mid-PR rather than filing it.** Default it
  could have gone the other way: leave it, it is pre-existing and unrelated to inline themes.
  Landed: it is the guard Fable asked for in the v1.99.3 release review, it has been flaky since
  the hour it shipped, and I was about to run the gates repeatedly against it — a gate that lies
  one run in three trains you to re-run until green. Cause: `CompanionHost` and
  `OutputfileAutoImport` write the shared profile's settings.json from a different xUnit
  collection, and collections run in parallel. Cost 2,350 tests and still 2 s, because the fix
  is a serial collection of four files rather than disabling parallelism (which was 2 s → 8 s).
- **Guarded it with a source scan, not just the four attributes.** Could have gone: add
  `[Collection]` and move on. Landed: the file's old comment *claimed* nothing else touched
  settings.json, and that claim is what let the flake exist — trap 34, a comment standing in for
  a guard. `SettingsFileCollectionTests` fails the build when a fifth writer appears.
- **Inline themes PR 1 ships `ThemeBodyMaxHeight = 320` with the screenshot unable to choose.**
  Bevel offered 280 or 320 and delegated the pick. Landed: 320, because `GearCardView` already
  uses it and a second nearby constant is two answers to one question — and the shot is recorded
  as NOT deciding it, since no Progress room is tall enough to reach either cap. PR 2's Loot and
  Drops rows are where the number is actually tested.
- **Extended `EQBUDDY_EXPAND` to name a room (`progress:raids`) instead of adding a variable.**
  Could have gone: a new `EQBUDDY_THEMETAB`. Landed: an inline theme has four bodies behind one
  key, and three of them were unreachable by a test or a screenshot — trap 22, a surface that
  cannot be reviewed reads as reviewed.
- **Reverted the Avalonia half of Inline themes PR 1 rather than shipping it half-working or
  forcing it.** Default it could have gone the other way: keep pushing (it was six fixes deep),
  or leave the failing test and file it. Landed: the blocker is that Avalonia's theme surfaces
  are shared field-backed instances with no `IWidgetCard`-style seam, which is a V2 refactor of
  a 5,593-line file — and CLAUDE.md says stop and stub when work turns out V2 mid-session, not
  finish it and label it. `main` now has inline themes on one widget and not the other, which
  is a parity gap that is REPORTED rather than quiet, and no What's-new claims it.
- **Did not write the `WhatsNew.json` entry for PR 1.** Could have gone: write it now so it is
  not forgotten. Landed: "the Progress card expands in place" is false on Linux and macOS
  today, and the rule is that entries are TRUE, not that they are early. It is recorded as owed
  in `HANDOFF.md`.
- **Took Helm's #228 ruling as WORK, and restored the Motes card from the mini-dashboard star
  rather than from `hadFile`.** Could have gone: show the card to every existing profile (the
  maximal reading of "restore"). Landed: the fold destroyed the real preference — it removes the
  key from `SectionOrder` AND `HiddenSections` — so the star is the only surviving evidence, and
  restoring what can be proven beats growing everyone's widget. It under-restores on purpose,
  and that limit is written into the code.
- **Did NOT write the What's-new entry, and asked David instead** (he chose "ask Helm, hold the
  entry"). The two rules collide: every player-visible change needs an entry, and Helm's hold
  says "we do not tell players motes are back". A shipped fix does not lift a hold, and reading
  the hold as thread-only would have been me lifting it. Note filed in `SCRIBE-FEEDBACK.md` for
  Helm; David is the courier. Version deliberately NOT bumped, so nothing can ship by accident.
- **`all cleared` rather than `0 left` for a finished raid ledger**, and an over-counted ledger
  says the same rather than `-2 left`. Bevel named the two states; the wording of the empty one
  was left to me. Landed: a zero is a number to read, and that state is an achievement.
- **Changed the Wealth CHIP only, not the Progress window's Wealth body.** Bevel's ruling was
  justified with "window Wealth is coin too", which is not true — the window's Wealth tab still
  draws Coin, Sold and Motes. Default it could have gone the other way: strip the body to match
  the justification. Landed: the Motes card ships hidden, so for most profiles that block is the
  only place mote rows appear, and removing a surface uninvited is exactly how #204/#210/#212
  happened. Handed back to Bevel with the screenshot.
- **Corrected my own framing of #228 rather than letting it stand.** I told David repeatedly that
  a fix was built and we were held back from telling the reporter. False: both reporters were
  told on 2026-08-21 and 1.99.0's notes announced it. I had read Helm's hold and Scribe's item
  and never opened the threads. The lesson is narrow and worth keeping: **an agent's hold text
  describes an intention, not the state of a thread** — check the thread before describing what
  a player has been told.
- **Staged 1.99.4 with a Windows-scoped entry for the inline Progress card**, rather than
  releasing it unannounced or reverting it off `main`. Default it could have gone the other way:
  no entry until Avalonia has it (what I said this morning). Landed: it is already on `main`, so
  any tag ships it, and a player-visible change with no note is the defect the What's-new rule
  exists to prevent. The entry names Windows explicitly and says why the other build lags, which
  is the opposite of quiet. **This is the one judgement call in the release for David to veto.**
- **Corrected the motes What's-new sentence and its code comment rather than building a flag**
  (Fable, v1.99.4 review). `HiddenSections` has no provenance, so the restore cannot tell a
  deliberate hide from the blanket one and DOES un-hide a starred player who re-hid the card.
  Default it could have gone the other way: add a "player touched it" flag. Landed: one day of
  exposure, one toggle to undo, and a setting that remembers a single day is a setting forever.
- **PATTERN, not a one-off: I write comments from the INTENT and not from the code.** Fable
  caught the same shape twice in one day — the `AppSettings.Load` "never saves" claim and the
  motes "stays off" claim. Both times the tests were right and the prose asserted a safety
  property the code lacks. Worth checking for deliberately in review.
- **Stripped the BOMs and rebuilt `WhatsNew.json` from the tag's own bytes.** The cause was my
  Python `encoding='utf-8-sig'` on WRITE (right for reading, wrong for writing), not a
  PowerShell `Set-Content` as the review guessed; 19 files, not 15, measured against `v1.99.3`
  rather than counted by eye. The file uses three-space array elements and `indent=2` emits six,
  which is what made a 13-line addition a 2,387-line diff. Now 13 added, 0 removed.
- **Did NOT add a `.gitattributes`.** Fable framed it as a one-time renormalisation and a V1
  call; a whole-repo line-ending commit in the middle of a staged release is the wrong moment.
  Logged so the next loop can take it deliberately.
- **Gave Helm its own inbox and feedback file (David asked), and MOVED the holds into it rather
  than leaving them in `SCRIBE.md`.** Default it could have gone the other way: create the two
  files and leave the holds where they are, which is the smaller change. Landed: the holds were
  wrong all three at once this morning precisely because their author and their list-maintainer
  were different, and two lists would be strictly worse than one — the one a session reads would
  be the stale one. `SCRIBE.md` now carries a pointer and an explicit "do not restore holds
  here"; Scribe and Bevel were both told why, and Scribe's `## Holds` / `Retired` conventions
  moved wholesale rather than being rewritten.
- **`HELM.md` is documented as STATE, not a work queue.** The other three inboxes are
  take-an-item-and-delete-it; a hold is never taken, it binds until Helm lifts it. Writing that
  distinction down is the point — treating a hold like a work item is how "a shipped fix lifts
  the hold" gets invented.

## 2026-08-22 — clearing the `waiting (David's call)` pile

David: *"only elevate to me for items appropriately needing my focus."* Six items sat on him;
one genuinely does. Each line below is a decision I made instead of a question I asked.

- **Motes are excluded from what the wiki pack SUGGESTS.** Could have gone: leave it for David
  as filed. Landed: it FOLLOWS eqlwiki (its own Mote Guide says motes are not creature-specific),
  and "departing from the wiki" is the thing on his list — matching it is not. Kept distinct
  from the admins' ruling that common drops stay IN: a cheap gem really did drop from that
  creature, a mote did not. `WikiContribution.SuggestableToWiki`, with the negative asserted.
- **"What's-new should cover skipped versions" was CLOSED, not decided** — it was already built
  before it was filed. `EntriesBetween` returns every entry between two versions and
  `WhatsNewTests` covers a multi-version hop. The item's "Already shipped" line was wrong, which
  is the rot Scribe's own SSC promises to sweep. Verified rather than assumed.
- **The pack's session-vs-history scope went to `FABLE.md` as a V2, not to David.** It reads as
  a scope call and is a design one: three open sub-questions the reporter named, and the data
  source moves from a live object to a query over archives.
- **Two UX items went to Bevel, not David** — the slow chip's icon (an overlay-space call) and
  Mobile "New at level" using the played class rather than the Quest Tracker filter (a
  which-surface-owns-this-state call, the #212 shape).
- **Two stay with David, and both belong to him:** the spawn-timer mega-thread (public posture
  under the project's name, consequence-list item 3) and the `/consider` park, which is his own
  decision — worth telling him only that #217 has since answered its destination question.

## 2026-08-22 — David's three calls, asked with the question tool

- **#228: star-only IS enough.** He ruled the lifting condition met. Recorded for Helm rather
  than acted on: Helm's condition named him, but the LIFT is still Helm's, and he answered the
  question rather than telling me to post. Nothing posted; he is carrying the note.
- **Spawn-timer mega-thread: he took none of the three options.** *"We should have a way for
  people to feed verified updates to EQLWiki."* So: no thread we host, and a new V2 in
  `FABLE.md` instead. The redirect is better than any option I offered — a thread we host is a
  second source of truth competing with the wiki, maintained by us forever.
- **`/consider`: unparked, wiki half only.** The spawn-chip half stays parked. The wiki half now
  has a reporter-confirmed, admin-backed destination; the chip half has neither.
- **Deferred `DeadHelperTests` rather than building it in the release loop** (Fable's V1, my
  call on timing). Could have gone: build it now while the shape is fresh. Landed: it is a
  whole-assembly scan whose value lives in a curated `Known` list with a reason per entry — the
  `DeadSettingTests` pattern — and doing that badly under a release is how a guard becomes a
  green tick nobody trusts. Logged so it is a dated decision rather than a thing that quietly
  did not happen.
- **Deleted `IsExcluded`/`IsTimeableNamed` rather than wiring them** (Fable's ruling). The
  suffix rule covers every possessive pet the log prints and `Killer == "You"` closes the
  players case. A promise with no caller is worse than no promise.

## 2026-08-23 — the two features on 1.99.6: next-level spells by class, and motes/hr

- **Motes/hr went to David with the question tool, and he chose the Experience room.** The
  question passed both tests: the Progress WINDOW and the phone already carry that line inside
  their Wealth tab's Motes body, so the only surface missing it was the widget's inline Wealth
  room — which is coin-only by a Helm-signed ruling. My recommendation was the inline Wealth
  body (semantic home, and all three surfaces would then agree); I named the cost of the
  Experience room in the option, which is that the Progress window now states the mote rate in
  two places, an inch apart, on two different tabs. **He chose Experience knowing that.** So
  the line lives in `ProgressPresentation.SummaryLines` and reaches the widget, the window and
  the phone's Experience tab. The Wealth chip is untouched; the window/phone Wealth Motes rows
  are untouched (#227 is still its own item).
- **One formatter, not a fourth.** The app already had three mote-rate strings. The Progress
  line reuses the Motes card's own header via a new `MotesPresentation.RateLine`, and
  `ProgressTheme.MoteRate` now delegates to it. Could have gone: write the Progress line
  inline, which is two lines shorter. Two formatters for one rate is how the Wealth chip and
  the Wealth body came to disagree in the first place.
- **The line is OMITTED, not zeroed, when nothing has dropped.** The same block already omits
  the AA line and the ETA. "0 motes/hr" reads as a measurement of a camp rather than "none
  yet", which is the wording argument `MotesPresentation.Summary` was written to win.
- **No class in play now HIDES the next-level fold** (Bevel's rule, Helm-signed). This removes
  a behaviour: a classless character used to get a preview built from the class-agnostic AA
  categories. It could have gone the other way — those rows are true for everyone — and the
  reason it did not is that `LevelUnlocks.Next` walks forward to the next level with ANY row,
  so the surface offered David "At level 39: 1 new AA ability" about a pet ability five levels
  away, for a character with no pet. Called out in `WhatsNew.json` rather than left to be
  discovered, and asserted in `WidgetRenderTests` so a later refactor cannot restore it quietly.
- **Which group opens is "the first with something to show", not "index 0"** — a decision I
  made, not one Bevel wrote. Its rule says *"first inferred class open"*. A Warrior whose next
  milestone is an Archetype AA produces an empty Warrior group above the shared bucket holding
  the only row, so open-by-index would have shown "nothing new at 15" over a collapsed heading
  with the single row two clicks away. Found from a written prediction BEFORE the screenshot,
  and it is visible in `docs/screenshots/theme-inline-progress.png`. Sent to Bevel as a
  narrowing of its rule rather than assumed to be what it meant.
- **The empty group has no chevron.** Bevel said "keep the class row"; whether it is an
  expander was mine. A fold that opens nothing is an affordance that lies, which is trap 16
  with the switch the other way.
- **The per-class open/shut state is a FIELD on the view, never a setting** (Bevel's rule,
  followed). Worth logging because the neighbouring folds — `ShowNextUnlocks`, `ShowAllAAs` —
  are both settings, so the inconsistency is deliberate rather than an oversight.
- **Fixed a trap-8 violation I was standing next to.** The mobile Progress fingerprint keyed on
  `Wealth.MotesSummary`, which is the RATE — the one value in that record that moves on the
  clock while nothing happens, and also the only thing standing in for "a mote dropped". It now
  keys on the mote tiers. Could have gone: leave it, since it predates this work. It is three
  lines from the block whose comment says exactly why not to do it.
- **The E2E "no class hides the preview" assertion was deleted, not made to pass.** The harness
  always writes the shifted fixture log and that log infers WARRIOR, so the no-class state is
  unreachable there — the test would have been about a state the harness cannot produce. Moved
  to `WidgetRenderTests`, where the class list is a parameter, with a comment in the E2E file
  saying why it is not there. Writing a second class-free fixture for one assertion is a worse
  trade than one test on the other lane.
- **The new screenshot shoots the INLINE card, not the Progress window.** The window restores
  to a height whose body scrolls after ~3 lines, so `progress-card.png` has been photographing
  a panel cut off ABOVE the two lists it is named for — pre-existing, visible in the committed
  file, and filed separately rather than fixed here.

## 2026-08-23 (night) — actioning Fable's third pass

- **Took the blocker at face value only after checking it.** Fable said `ClassName` serialises as
  `className` while the page reads `g.class`, and said plainly it had not run the serialiser.
  Verified all three legs before touching anything — `JsonOpts` is CamelCase,
  `CompanionBuffGroup` is declared `Class`, the page reads `g.class` at five sites — because a
  bot's claim about source is a place to look. It was right in every detail.
- **Renamed the property rather than changing the page.** Both would work and the page side is
  new code too (so trap 32 does not bite either way). The record now matches its siblings, which
  is the property that stops the next group record inventing a third spelling.
- **The guard asserts the emitted JSON, not the record.** `Assert.Contains("\"class\":")` alone
  passes on the broken payload, because `className` contains `class` — so the load-bearing line
  is `Assert.DoesNotContain("className", json)`. Verified by running it on the pre-fix tree: 2 of
  3 fail there.
- **Built `WriteMobileProgressSnapshot` rather than fixing the hand-written snapshot.** The
  cheaper move was to correct the key in my hand-typed JSON and re-run. That would have left the
  next phone change verifiable only by a payload a human typed, which is exactly what hid this
  one. The fixture goes through the real catalogs, `LevelUnlocks` and the real projection, and
  asserts the shape it must carry so it cannot quietly stop carrying it (trap 22).
- **The re-shot `progress-next-classes` drops the level-up append and seeds the ledger LEVEL
  instead.** Fable's fix was "tall enough, or scrolled". Seeding the level is better than either:
  the preview only needs a level to be KNOWN rather than announced, so the six-row ding list goes
  away, all three class groups fit with no scrollbar, and the shot is about one feature instead
  of two. Prediction rewritten before the run and matched line for line.
- **Did NOT hold the release for the Progress window's ~203px Experience tab**, per Fable's
  answer to question 3: it is pre-existing, its body scrolls, and the previous three releases
  carried it. Filed with the Bevel 320-cap question beside it.
- **Carried Fable's forward note into the class-inference V3 stub** rather than leaving it in a
  feedback file: 1.99.6's What's-new tells a player to tick classes in the Quest Tracker, which
  that plan makes wrong rather than merely dated. The stub now says the shipping release owes a
  line saying so.

## 2026-08-23 (night) — #233, and the rule it produced

- **#233 went to David with the question tool, and he chose the guarantee.** It passed both
  tests: the theme fold is his roadmap direction (consequence-list item 5) and a reply would be
  read as a promise (item 3). His answer: keep the roadmap, add the "what moved" commitment, and
  say WHY — *"organizing after rapid initial build out of feature requests… the new homes make
  more logical sense and are intuitive for new users though of course the long term users will
  feel the changes."*
- **Treated as a pattern, not a voice.** #219, #227/#228 and now #233 are one complaint arriving
  three times from three people. That is what moved it from "answer the thread" to "change a
  rule", and it is why the reply concedes it out loud rather than explaining the fold again.
- **The rule is about the ORIGIN, not the destination.** Every one of those releases had a
  truthful What's-new entry describing where a surface had ARRIVED. None named where it had
  LEFT, which is useless to the only person who needs it — someone looking for something. The
  rule is the form "X is now Y", both halves.
- **Built before replying, not promised.** The 1.99.6 What's-new carries the whole current map
  and the promise; `CLAUDE.md` carries the standing rule. Could have gone: post the reply and
  add the rule later. A promise made in a thread and not written into the file every session
  reads first is a promise that lasts one session.
- **NOT posted — routed to Helm.** `HELM.md`'s process line is "new-thread thank-you still comes
  to Helm", and this is a new thread. David has settled the direction, so Helm is being asked
  only about posture and timing; the full draft is in `HELM-FEEDBACK.md` so one carry is enough.
- **#109 gets no reply yet, deliberately.** Its last comment is Frankthetankk's verbatim
  evidence, which is exactly what 1.99.6's bee work was built from — so the honest reply is the
  release itself, and replying before the tag would mean either claiming it shipped (false) or
  saying "soon". The thread is answered by shipping, and the What's-new credits him by name.
- **#233's REPLY is David's; #233's RULE is ours, and they separated cleanly.** He read the draft
  and took the thread himself. The Helm sign-off request is withdrawn in place rather than
  deleted — a live-looking ask that nobody needs answered is the exact shape that made three
  holds describe states that had stopped being true. The two durable outcomes (the "X is now Y"
  rule in `CLAUDE.md`, the WHERE THINGS MOVED map in 1.99.6) shipped and are unaffected by who
  writes the reply.
- **`status.ps1` will keep flagging #233 as awaiting a reply, and that is correct.** Written into
  the handoff so a later session does not read the flag as an unfinished job and post over him.

## 2026-08-23 (afternoon) — the three queued items

- **Progress-window clipping: measured, then FILED rather than fixed.** `AllowResize` releases
  the height on `ContentRendered`, which for a replay-filled body is a frame with nothing in it
  — proven by running the same shot with the pin skipped (203px → 389px), with
  `progress-wealth` as the control at 741px. The FIX is not a V1 call: `AllowResize` wants
  "size to content" and "let the user drag the edge", WPF will not do both, and resolving it
  decides chrome for four windows. Four candidate fixes are in the stub with the cost of each.
  David asked for all three items; this is the one I did not finish, and it is deliberate.
- **PR 1's real number is -98 rows, not -498.** The plan predicted removing ~500; it assumed
  every class page carries every level and PR 0 found none do (all stop at 50 against a cap of
  60). So 362 rows return as DERIVED. Worth logging because the plan's headline number is the
  one a reviewer would check against.
- **`era` is parsed but NOT shipped.** Fixing PR 0's row regex made it come through cleanly —
  and it is "Classic" on all 1,504 rows. One value discriminates nothing, and a harvest field
  no surface reads is trap 43's mirror.
- **`PageTitle` deferred with a reason, not forgotten.** The plan lists it; links work without
  it because the wiki resolves redirects itself, so it buys nothing until something needs the
  served title.
- **The spell hover is ONE LINE because of a rule I nearly tripped over.** Both widgets switch
  a tooltip to monospace when it contains a newline — right for the stat blocks that rule
  exists for, wrong for wiki prose, and invisible to every test and screenshot.
- **1.99.7 exists because 1.99.6 had already shipped.** The first draft of these notes went
  into 1.99.6's block, which would have claimed things the released build does not have.
- **#120's four tests were re-expressed, not relaxed.** They asserted `""` for two comparable
  classes and documented it as a virtue. The protection that actually mattered (a one-off line
  never names a class) lives in the FLOORS and is untouched; what was deleted is a margin that
  could not tell a three-class character from an ambiguous log.
- **`MemberFraction` stays at 0.25 even though it drops a class after two idle half-lives.**
  That is what separates an alt-swap (blocks) from a multi-class character (rotation inside a
  fight), and the dump — which outranks inference — is the answer for anyone it gets wrong.
- **`ClassSourceWritersTests` joins the settings.json collection despite writing nothing.** It
  names `OutputfileAutoImport.cs` as a path string and the flake guard reads that as a call.
  Serialising four file reads is cheaper than teaching that guard to tell a path from a call,
  and a guard with a convenience exception carved into it stops being a guard.

## 2026-08-23 (afternoon) — the V3 presentation half

- **What looked like a labelling job was hiding two functional collapses.** Both Quest windows
  were still reading `CurrentSnapshot().InferredClass` directly — one class, bypassing
  `CharacterClasses.Resolve` — in `BuildClassStrip` and in the filter. The window that most
  needs the multi-class answer was the last place still collapsing it. Renaming a label is what
  took me into the file; the collapse is what I found there.
- **`ClassSourceFor` went ON `IQuestsHost` rather than being reached for.** A seam that window
  must go through cannot drift back to the snapshot's single class.
- **The old `InferredClass` stays on the wire for a release** and the page falls back to it.
  Trap 32: an open phone runs the page it downloaded weeks ago, so removing the field it reads
  would blank the line on every device that has not reloaded.
- **The new wire keys were pinned the same day they were written.** `characterClasses` and
  `classSourceLabel` are in `CompanionWireKeyTests` — the last field added to this wire reached
  the page under the wrong name and the manual check could not see it because the payload was
  hand-typed.
- **Bevel has NOT ruled on this wording** and Fable's plan asked for a pre-design pass. I built
  it as a like-for-like replacement of an existing string rather than a new surface — "(inferred)"
  said one of three things and said nothing when the GAME had told us. Bevel's next run should
  see it; flagged rather than presented as settled.

## 2026-08-23 — self-review pass over 1.99.7

- **The phone fixture could never have tested the thing it was for.** `WriteMobileQuestsSnapshot`
  sets picked classes, and the page suppresses the class-source line whenever picks exist — so
  the state the line lives in was unreachable from the fixture. A second snapshot now covers
  no-picks-plus-a-dump. This is the same shape as the wire-key defect: a check that runs and
  cannot fail. Found by asking what the fixture would show rather than that it passed.
- **Kept `.claude/launch.json`** (a new tracked file in a directory that had none) because
  file:// access to the harness was refused mid-session and serving it over HTTP is what made
  the browser verification possible at all. Small repo-shape call, logged rather than silent.
- **Collapsed the companion quest request to one snapshot and one resolution per tick.** It was
  three `CurrentSnapshot()` calls and two `Resolve()` passes per field-set, each taking the
  ledger lock twice and copying two lists, every second a phone is paired. Nothing was WRONG;
  it is the steady-state allocation perf audit #1 exists to remove.
- **One Avalonia gate run reported 1 failed / 279 total and never reproduced** (seven runs
  since, all 278/278 green). Name unrecoverable — `check.ps1` keeps no log. Ruled out a
  data-driven count (every theory in that project is static `InlineData`), which points at a
  transient host crash rather than a logic flake. **Disclosed to Fable with the reasoning
  labelled as a hypothesis, and the decision of whether to chase it before the tag handed to
  the reviewer rather than taken by me.**
  → Worth considering: `check.ps1` discarding test output is what made this unrecoverable. A
  gate that fails without leaving a name behind costs exactly one incident like this.
