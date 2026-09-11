# Archived trap novels — moved out of always-loaded CLAUDE.md

This is the incident record for traps 1–69. Live rules stay in
[CLAUDE.md](../../../CLAUDE.md). Link here when the novel still explains *why*.

Progression: incident → verified lesson → executable test/guard → compact live rule.
Once a guard exists, the novel does not need to ride every session.

Source snapshot of the whole pre-split file: [claude-2026-09-08.md](claude-2026-09-08.md).

---

## Traps that have already caused real bugs

Read this list before touching the areas it names. Every entry cost a release.

### Trap 1

1. **Screen pixels vs pre-scale units (WPF).** The widget content sits under a UI-scale
   `LayoutTransform`. Anything you assign to a control *inside* it is in pre-scale units,
   but `SystemParameters.WorkArea` and cursor positions are screen pixels. Mixing them
   silently breaks only at scales â‰  100%. Caused discussion #144.
   â†’ **Now guarded:** every such conversion belongs in `UI.Shared/WidgetMetrics.cs`,
   which is unit-tested. Do not do the arithmetic inline in a window.
### Trap 2

2. **`ActualHeight` is 0 in a `Closed` handler.** The window is already torn down.
   Persisting geometry there records nonsense. Caused #152 â€” chips walked up the screen
   one row per reopen.
   â†’ **TOMBSTONE, 2026-09-05 (Surface A / SA-2). The trap is still true; its named guard is
   gone with the surface it guarded.** UI.Shared/ChipStackAnchor.cs and ChipAnchor.cs
   owned the anchoring â€” they ignored non-positive heights, and ChipStackAnchorTests held
   it. (Un-backticked deliberately: `DocumentationTests` fails a doc that cites a file or a
   suite which no longer exists, and a tombstone must be able to NAME what it is burying.) Both chip stacks folded into one HUD chip row that is recomputed from the widget's own
   position every tick and **persists no geometry at all**, so there is nothing left to save
   in a `Closed` handler and the three files retired together (trap 57's precedent for how to
   record a guard leaving with its subject).
   â†’ **What survives is the RULE, and it binds anything that still saves a size or a
   position at teardown.** Do not read `ActualHeight` (or anything derived from it) in a
   `Closed` handler; capture it while the window is alive, or â€” better, and what SA-2 did â€”
   find out whether the value needs persisting at all. The widget itself and every satellite
   still persist geometry; this trap is about all of them, and it always was.
### Trap 3

3. **`redirects=1` means the page you get is not the page you asked for.** Record the
   *served* title (`WikiPageText.Title`), never the requested one. Caused the same
   article-dropping bug in #65 **twice**.
### Trap 4

4. **One entry, two sources for one fact.** `WikiContribution` computed `killZone`
   twenty lines below the point that needed it, so a page template used the player's
   current zone while its own cross-references used the kill zone.
### Trap 5

5. **CSS: `margin: 0 auto` on a flex item kills cross-axis stretch.** Making `body` a
   flex column collapsed `main` to content width and took the mobile map down to 60px.
   Needs an explicit `width: 100%`.
### Trap 6

6. **CSS class rules beat presentation attributes.** `text.poi { font-size }` silently
   defeated the SVG counter-scaling for months; map labels ballooned on zoom.
### Trap 7

7. **Headless `--window-size` is not the CSS viewport.** Asking for 390 gave a 492px
   page, which looks exactly like a layout bug in a screenshot. Measure `innerWidth`
   before believing a capture.
### Trap 8

8. **Fingerprints must exclude values that drift every tick.** Mobile pushes are gated
   on per-section fingerprints; including a countdown or an age would wake every device
   every second.
### Trap 9

9. **A layout class that also carries behaviour will hand that behaviour to the next
   user of it.** The mobile page's `wide` meant *both* "span the big grid slot" and
   "your body never scrolls, you draw yourself" â€” true only of the map. The quest
   surface asked for the big slot, inherited `overflow:hidden`, and shipped a list
   nobody could scroll. The two meanings are now `wide` and `fills`. Same lesson in
   solo mode, where the page's own scrollbar is gone and only the panel body has one.
   â†’ **When reusing a presentation class, read every rule that selects it**, and split
   it rather than adding an exception.
### Trap 10

10. **A fallback that skips the knobs the main path honours is a second product.** Alert
    playback fell through to `SystemSounds.Asterisk` (WPF) / `Console.Beep` (Avalonia)
    when a file was missing â€” the one route out of the method that the volume slider
    could not reach. Because the seven built-ins ship with the OS and always exist, that
    route was reachable *only* for custom files, so the bug read as "the slider works for
    built-ins and does nothing for my .wav" (#153, adndmike) when the custom sound was
    never playing at all.
    â†’ **Every branch must carry the same settings, or it is a different feature.** The
    decision now lives in `UI.Shared/AlertSoundPlan.cs` and is unit-tested with no audio
    device: a missing file substitutes a built-in *at the chosen volume* and names the
    file so the UI can say so.
### Trap 11

11. **A table of evidence that only one side can produce is a verdict, not a vote.** Class
    inference weighed class-unique signals and took the most-used â€” but every signal in
    the table was a melee skill, so a caster who once produced a melee-ish line wore that
    class for the session: there was nothing in the table he could ever do to argue back
    (#120, Frankthetankk). Frequency-weighting looked like a safeguard and was doing
    nothing, because the other side had no votes to cast.
    â†’ **Before trusting a scoring rule, check that every outcome it can name has a way to
    be named** â€” and that yesterday can be outweighed by today. `Core/ClassInference.cs`
    derives signals for all sixteen classes from the shipped catalogs, decays them, and
    answers "" when the evidence is thin or split.
### Trap 12

12. **Both widgets are `SizeToContent`, so text width IS window geometry.** A label whose
    string changes width makes the app ask the windowing system to resize a transparent,
    always-on-top window. On Windows that is invisible; on X11 it is a geometry change on
    a window stacked over a fullscreen game, and #173 (KoboldCoterie, CachyOS) is that the
    title-bar CPU/RAM readout â€” which redraws every 3 s *whether or not anything else
    changed* â€” cost EverQuest its keyboard. Player-driven changes are fine; **a timer that
    changes measured size is not.**
    â†’ **Now guarded:** `UI.Shared/PerfReadout.cs` formats to a fixed shape and the label
    reserves a fixed width, so a sample repaints and measures identically. If you add
    anything else that updates on a clock, give it a reserved size.
### Trap 13

13. **A settings save writes the WHOLE file from the snapshot loaded at startup.** So a
    second writer's changes are reverted wholesale, with no error and nothing on screen â€”
    which is exactly how "my tick-boxes won't stay ticked" (#169) presents. The Avalonia
    build had no single-instance guard off Windows (the old one was a named mutex), so
    every Linux/macOS launch started another full copy â€” and two undecorated always-on-top
    widgets restore to the same saved position, so you cannot see that there are two.
    â†’ **Now guarded:** `UI.Shared/SingleInstance.cs` (one copy per profile everywhere, and
    a stale lock can never stop a launch), and `AppSettings.Save` logs once when it is
    about to overwrite a file that changed underneath it.
    â†’ **One legitimate exception, and it is narrow: `--textprobe`.** A diagnostic you run
    with the widget already up cannot take the lock, and it holds no file, no port and no
    log tail â€” the three things the guard exists for. But "it only reads" was WRONG when
    first claimed (Fable 5, v1.99.3 release review): `AppSettings.Load` persists migrations
    and generated rule ids at the bottom, so a read IS a write on an un-migrated profile.
    The probe now passes `persistMigrations: false` and takes the app's already-loaded
    instance instead of loading twice. **If you add another lock-skipping path, it must
    write nothing â€” and check what your "read" does at the bottom.**

    â†’ **And the guard itself had the same hole one level up, until 2026-08-19.** Adding
    `SingleInstance` to Avalonia left WPF on its named mutex, so there were TWO guards and
    neither could see the other: on Windows the WPF widget and the Avalonia widget both
    ran on one profile, tailing the same log twice, racing on `settings.json`, and both
    wanting the EQBuddy Mobile port. David's `error.log` carried all three symptoms â€” the
    overwrite warning above fired twice, each time directly after a line only the Avalonia
    build writes, with the companion's "Only one usage of each socket address" at the same
    timestamps. **A guard that is implemented per TOOLKIT does not guard the profile.**
    Both builds now take the same lock, and both claim it before their UI framework
    starts. Verified by launching the two builds against one profile in both orders and
    against a stale lock â€” not by the tests passing, which they did throughout.
### Trap 14

14. **`TextWrapping` does nothing inside a horizontal `StackPanel`.** A stack measures its
    children with *infinite* width in the stacking direction, so the text never reaches a
    boundary to wrap at â€” it is CLIPPED at the panel's edge instead, silently, with no
    ellipsis to say so. The Gate 2 Quests window shipped an icon-plus-note row that read
    "pick classes ab" in both UIs, and no unit test could see it; the first real screenshot
    could, which is the argument for screenshot review being an acceptance criterion.
    â†’ **Use a two-column `Grid` (`Auto,*`)** whenever an icon sits beside wrapping text.
    `QuestsView.IconLine` is the worked example (it was `QuestsWindow.IconLine` until the
    E-3 PR 3 lift took the surface out of the window).
### Trap 15

15. **A control that hides itself, inside a host that also hides itself, has two switches
    for one state â€” and only one of them is ever wired.** The Gate 4 Loot breakout built
    its filter strips, selected the right chips and painted them into a `ContentControl`
    that XAML had declared `Visibility="Collapsed"`; the render only ever set the visibility
    of the panel INSIDE it. The strips were correct and invisible, on every launch. Nothing
    about that shows in a diff, a unit test or a build â€” only in a picture.
    â†’ **Visibility and spacing belong to the thing that decides them.** When you lift a
    surface into a class, the host it hangs in gets no state of its own: give it no
    `Visibility` and no `Margin`, and let the lifted control carry both.

### Trap 16

16. **A vector only hit-tests where it is PAINTED; the emoji it replaced did not.** A WPF
    `TextBlock` (and its Avalonia equivalent) responds across its whole layout rect, so a
    glyph with a click handler is a solid square. Swap in a `Path` of the same size, in the
    same place, with the same handler, and the dead space inside the drawing stops
    responding â€” the loot rows' map-pin quest badge had a gap between its two folds you
    could click straight through (#211, n3cr0nk1tt3n). **Nothing about this shows in a
    diff**: the icon is right, the colour is right, the handler is attached.
    â†’ **A clickable inline icon is a `DesignSystem.InlineIconButton`**, never a bare
    `Icon()` with a `Cursor` and a handler. `DesignTokens.IconInlineHit` (16) is the target;
    the drawn size stays `IconInline` (12), so the hit area grows and the row does not.
    Every iconâ†’vector conversion should ask "was this clickable?" before it lands.

### Trap 17

17. **`IsEnabled = false` is invisible when the style has no disabled visual.** The app's
    `CheckBox` style carries none, so a locked row rendered *exactly* like a live one and
    silently swallowed clicks â€” the "silent no-ops are broken" rule with the switch on the
    other side. Set an explicit `Opacity` (or dim the ink) alongside `IsEnabled`, and say
    why in the tooltip. Found by looking at a screenshot; no test can see it.

### Trap 18

18. **An incremental WPF build can leave a STALE assembly with a FRESH timestamp.** The
    `_wpftmp.csproj` shadow project means `dotnet build` reported success, the `.dll` and
    `.exe` mtimes updated, and the assembly did not contain code that was in the source â€”
    so `shoot.ps1` photographed a window that did not have the feature under review, and
    the honest reading of that picture ("my code did not run") is indistinguishable from a
    logic bug. Half an hour went into the wrong hypothesis.
    â†’ **Before trusting a screenshot that disproves your change, prove the binary has it.**
    .NET stores strings as UTF-16, so grep for the encoded bytes:
    `python -c "d=open('src/EQBuddy/bin/Release/net10.0-windows/EQBuddy.dll','rb').read(); print(d.count('Your new string'.encode('utf-16-le')))"`.
    Zero for a string you can see in the source means `rm -rf src/EQBuddy/obj src/EQBuddy/bin`
    and rebuild â€” not a redesign.

### Trap 19

19. **A resource lookup inside a property setter runs before the control is in a tree.**
    `EqFoldLabel.LabelStyle` did `Application.Current.TryFindResource("SectionLabel")` and
    silently got nothing while XAML was parsing, so two folded-section headings rendered
    as body text â€” bigger and brighter than every other heading, with no error anywhere.
    â†’ Use `SetResourceReference`, which resolves on load and survives a theme swap, or
    express the look in `DesignTokens` and skip the lookup. Only the screenshot said
    anything was wrong, and it took two attempts because the first fix looked right.

### Trap 20

20. **A setting that only READERS touch is the signature of a lost capability.** Three
    player-facing bugs came from one event â€” a surface folded into another, the DATA
    survived the move and the WRITE path did not: `SkyQuestCompleted` (#204/#209),
    `EpicQuestCompleted` (#210, whose helper had passing tests and NO CALLER), and
    `SkyQuestClass` (#212, which filtered EQBuddy Mobile's whole Sky list forever). None
    were visible to a compiler, a test or the ratchet.
    â†’ **Now guarded:** `DeadSettingTests` scans for settings read but never written and
    holds the result to a list with a reason per entry. A sweep on 2026-08-18 found no
    fourth live bug â€” the two remaining writer-less lenses are guarded by their readers,
    and six more are deliberate edit-the-JSON knobs. **When you fold a surface, check what
    still writes each setting it owned.**

### Trap 21

21. **A shot name IS a filename, and `shoot.ps1` overwrites without asking.** Adding a
    `watch-card` shot for the Watch card would have replaced
    `docs/screenshots/watch-card.png` â€” a hand-taken illustration that
    `docs/WatchListGuide.md` embeds â€” with the fixture's three rules. Caught only because
    `git status` said "M" on a file the shot had supposedly created.
    â†’ **Check `docs/screenshots/` and `grep` the docs for the name before adding a shot.**
    The one that landed is `tracked-card`.

### Trap 22

22. **A surface with no fixture state cannot be reviewed, and reads as "reviewed" anyway.**
    The Watch card's sort strip appears only above two or more rules and the Raids card's
    body only once something is defeated â€” so on the default profile both are one-line
    empty states, and a screenshot of them proves nothing about the rows underneath. This
    is the same shape as the Gate 3 note about the spawn progress bar being unit-tested and
    never seen.
    â†’ **Stage the state in `scripts/shoot.ps1` as part of the change**, not later.
    `tracked-card` seeds rules the fixture log actually matches; `raids-card` seeds
    `raid-kills.json` (`Raids = @{â€¦}`, keyed `"{character}_{server}|{boss}"` lowercased).

### Trap 23

23. **Fixture staging in the wrong SHAPE renders a state that is real, so the screenshot
    looks correct and is a picture of something else.** Trap 22 says stage the state;
    this is its second half, and it cost two wrong screenshots in one sitting on the
    `wiki-pack` shot. First the seeded wiki cache was keyed on the names the LOG writes
    ("an asp") when the lookup uses the names EQBuddy STORES ("Asp"), so most entries
    missed and the app quietly fetched the live wiki â€” a plausible picture of whatever
    eqlwiki said that minute. Then the seeded wikitext put drops in free prose when
    `EqlWikiMobs.Parse` only ever reads `{{Namedmobpage}}`'s `known_loot`/`common_loot`,
    so all thirteen creatures rendered "page lists no loot" â€” which is a REAL state the
    surface is supposed to show, and therefore looked like a correct screenshot of a
    broken app rather than a broken fixture.
    â†’ **A shot whose numbers you did not predict in advance has not been reviewed.**
    Write down what the staging should produce BEFORE running it, and treat a mismatch as
    a fixture bug until proven otherwise. Seed through the same key and the same parser
    the app uses â€” the cache filename rule and the template field name are part of the
    staging, not implementation detail.

### Trap 24

24. **A window TITLE is not an identity, and `shot.ps1` matched on one.** The Progress
    theme gave four shots the same title (`EQBuddy Progress`), and a previous shot's app
    that has not finished exiting is a perfect match for the next shot's request â€” so a
    Faction tab was captured and filed as `progress-wealth.png`. It looks exactly like a
    correct screenshot of the wrong feature, which is trap 23's failure mode arriving by a
    different road. Two earlier captures had already been lost this way (`release.ps1`
    relaunches the real app; one shot came back reading David's live character name).
    â†’ **Now guarded, on both sides:** `shot.ps1` takes `-OwnerPid` and `shoot.ps1` always
    passes the process it launched, so a title alone can no longer pick a window. And
    `shoot.ps1` stands the REAL EQBuddy down first (gracefully â€” it finalizes its session
    on exit) and relaunches it in its `finally`, so the app that caused this is not on
    screen at all. If you add a shot that shares a title with another, `-OwnerPid` is the
    thing keeping them apart.
    â†’ **AND `-OwnerPid` CANNOT SEPARATE TWO WINDOWS OF THE SAME PROCESS** â€” the half the
    guard does not cover, and the half E-3 walked into. `MainWindow.xaml`'s title is exactly
    `EQBuddy`, and the Evolved shell is *also* "EQBuddy" to the player, so a shot asking for
    `EQBuddy` would have matched whichever window the enumeration reached first: same owner,
    same title, and a picture of the widget filed as a picture of the shell. Caught by
    reading `MainWindow.xaml` rather than by a wrong screenshot, which is the only cheap way
    this one is ever caught. **The fix belongs in the WINDOW, not the harness** â€” the shell's
    title carries its room (`EQBuddy â€” Progress`), which is what `HistoryWindow` already did
    and what a shell application should do anyway. **Before adding a shot, check its title
    cannot match a SIBLING window of the same app.** A suffix invented for the harness is a
    smell; a name the player should see is not.

### Trap 25

25. **A horizontal `StackPanel` clips a CHIP STRIP exactly as it clips text (trap 14).**
    The Progress window's four tabs were built into a `StackPanel`; a stack measures with
    infinite width in the stacking direction, so the fourth chip was clipped at the panel's
    edge â€” no ellipsis, no overflow, simply not on screen. The strip was CORRECT and one
    quarter of it was invisible, on every launch. Same bug #184 hit when the class strip
    clipped at NEC.
    â†’ **A strip whose contents are not fixed-width belongs in a `WrapPanel`**, and the
    badges make them not fixed-width: "16.0% xp, +1 lvl (2 new), +1 aa" is a tab label.
    Nothing in a diff, a unit test or a build shows this; the first screenshot does.

### Trap 26

26. **Folding cards away is where the last WRITER of a setting goes missing (trap 20's
    other half).** The Progress theme absorbed the three card headers that carried the only
    `MiniStats` writers for `xp`, `money` and `motes` â€” `DeadSettingTests` could not have
    caught it, because `MiniStats` still has writers for the other seven keys. They moved
    into the window with the surfaces they belong to.
    â†’ **When you fold a surface, list every control on it and say where each one went.**
    "The data survived and the write path did not" is the same sentence as #204, #210 and
    #212; a fold is precisely the event that produces it.

### Trap 27

27. **Git Bash rewrites a leading-slash ARGUMENT into a filesystem path, and the tool
    you called blames you for the flag you plainly passed.** MSYS path conversion turned
    `signtool sign /fd SHA256 â€¦` into a signtool that reported *"No file digest algorithm
    specified. Please specify the digest algorithm with the /fd flag"* â€” with `/fd SHA256`
    sitting in the command line being quoted back. Nothing in the error names the shell,
    so the obvious reading is that the argument is wrong rather than eaten.
    â†’ **Invoke Windows tools that take `/flag` arguments from `pwsh`, not Bash.** That is
    why `scripts/signing.ps1` exists as PowerShell and why `release.ps1` calls it directly.
    The same trap is waiting for any `/`-flagged tool: `msiexec`, `robocopy`, `reg`.

### Trap 28

28. **A signing tool's exit code is not evidence that the signature will validate.**
    `signtool` returns 0 for signatures whose chain a player's machine will reject, and
    an Artifact Signing certificate is valid for **three days** â€” so an untimestamped
    signature verifies on the machine that made it and goes invalid by the weekend, on
    everyone who already installed it. Neither failure is visible at release time.
    â†’ **Verify what you just signed, in the same breath as signing it.** `Invoke-EqSign`
    asserts `Get-AuthenticodeSignature` returns `Valid` *and* that a
    `TimeStamperCertificate` is present, and throws otherwise.

### Trap 29

29. **When a feature gate is deleted, the controls it USED to un-hide stay hidden.** The
    title-bar EQBuddy Mobile button shipped `Visibility="Collapsed"` on 2026-08-14 because
    `CompanionPreview.Enabled` made it visible in code. The gate was removed the same week;
    the MENU entry lost its `Visibility` attribute and the BUTTON did not, so the one-click
    way into the feature David had specifically asked for was never once on screen. Six
    days, several releases, and nothing could see it: not a compile (the XAML is valid),
    not a test (the WPF layer has none), not a diff (the attribute was already there), and
    **not a screenshot â€” an absent control photographs as an unremarkable title bar.**
    â†’ **Deleting a gate means finding every control the gate switched**, not just the code
    that read it. Grep the removed flag in HISTORY (`git log -S`), not in the working tree,
    because the thing you are looking for is what is no longer there. The same event leaves
    a second mark: Gate 5c drew the `Phone` vector FOR that button and left the emoji in
    place, because the control being converted was invisible â€” an unused entry in
    `IconPaths` is worth a look for the same reason a written-never-read setting is
    (trap 20).

### Trap 30

30. **A staging list that enumerates an enum BY HAND stops covering it the day the enum
    grows.** `shoot.ps1`'s `mini-bar` shot disables every `BreakoutKind` so that starring
    ten stats while minimized does not open ten windows over the capture. `Progress` joined
    `BreakoutKind` on 2026-08-19 and was not added to that list, so the shot silently began
    photographing the **Progress breakout** â€” a real window, correctly rendered, under the
    filename of a different feature. Re-running it would have overwritten a correct
    committed screenshot with the wrong picture; it is trap 24 arriving through the shot's
    own staging rather than through a title match.
    â†’ **When you add a member to an enum, grep `scripts/` for its siblings.** A staging
    list is code that cannot be type-checked, so the enum has to be checked by hand â€” and
    the failure mode is never an error, it is a plausible picture of something else.

### Trap 31

31. **A capture surface must pin its own theme.** `AppTheme`'s brushes were process-wide
    singletons and the suite applied every theme in the catalog, so a headless capture
    rendered in whichever palette ran last â€” the first EQBuddy Mobile capture came back in
    Turquoise while its seeded `settings.json` said ParchmentBrass. Correctly rendered, real
    palette, wrong state, and only obvious if you happen to know what the theme under review
    looks like.
    â†’ Same family as the profile isolation those captures already needed: **a capture's
    entire output is a picture of whatever global state it found.** The capture that earned
    this went with the Avalonia lane in E-2c, so there is no guard here any more â€” which
    makes it a rule to KEEP rather than one you have inherited. `shoot.ps1` is the surviving
    capture surface and it takes `-Theme`; anything new that shoots must apply its palette
    first rather than trusting the process it happens to run in.

### Trap 32

32. **The EQBuddy Mobile page NEVER re-fetches itself, so a page-side fix does not reach
    an open phone.** The socket reconnects forever with backoff; updating the PC restarts
    the server, the phone reconnects, and the browser goes on executing the JavaScript it
    downloaded when the tab was first opened â€” possibly weeks earlier. `Cache-Control:
    no-store` does nothing, because nothing ever asks for the HTML again. And this is the
    NORMAL way the feature is used: propped on a desk, added to the Home Screen, left alone.
    â†’ **A page-side fix ships, the player updates, the symptom continues, and both sides
    compare version numbers that AGREE while running different code.** That is the leading
    suspect in #202, where the repaint-gate fix is provably in the build bjstrange named
    (verified: the commit is an ancestor of `v1.94.1`, the exclusion list is keyed for the
    camelCase the wire actually uses, and the gate holds still against a real loot payload
    when only the rates move) and his card still churned.
    â†’ **Now guarded:** the envelope's `identity.appVersion` was only ever printed in the
    footer; the page compares it to the version it booted with and reloads once, recording
    what it reloaded FOR so a cache it cannot see becomes a message rather than a loop
    (`CompanionPageUpdateTests`). **Before diagnosing any page-side report, ask what the
    footer on THEIR device says** â€” not what version their PC is on.

### Trap 33

33. **Two callers with DIFFERENT ARGUMENTS do not produce a stale answer and a fresh one â€”
    they produce two different answers, both current, and whichever ran last wins.** This
    is trap 10 with the knobs being arguments rather than settings, and it is #202:
    `SessionStats.Snapshot()` (no rules) returns a snapshot whose `Tracked` list is EMPTY,
    while `Snapshot(window, rules)` fills it. The widget pushed to EQBuddy Mobile from two
    places â€” `RefreshUi` once a second with rules, and the 50 ms low-latency pump without â€”
    so the phone was told the watch list had emptied twenty times a second and refilled
    once a second. The loot card is the only surface carrying the watch rows, so the loot
    card is the one that flickered, for three releases and two wrong diagnoses from here.
    **The page's change detection was correct throughout; the data really was changing.**
    â†’ **When a value has two producers, give them one builder.** `MainWindow.BuildSnapshot()`
    (WPF) and `CurrentSnapshot()` (Avalonia) are it, and `CompanionSnapshotArgumentTests`
    scans both widgets' source so a third push site cannot pick the other overload. It was
    also costing a full snapshot rebuild every 50 ms: the memo is keyed on the arguments,
    so agreeing made the fast path free as well as right.
    â†’ **And the diagnostic is what solved it, not the reasoning.** Two `?debug=1` captures
    from the reporter, nine seconds apart and exact mirror images, said in one line what
    three sessions of hypothesis had not. Ship the instrument before the third theory.

### Trap 34

34. **A guard that forbids the WRONG thing cannot see a MISSING thing, and it reads as
    coverage either way.** `GameCommandsTests` enforced "every surface that names a command
    offers a â§‰ copy" by forbidding any copy source from carrying its own literal. That is a
    real rule, it passed for months, and it was blind to the only failure that mattered: a
    surface with **no copy source at all**. The Gear tab told the player to import something
    and handed over no way to do it â€” on both widgets, for as long as the surface existed â€”
    while the file named after the rule sat green (David, 2026-08-20). Same shape as trap 20:
    the thing you are looking for is what is *not there*, and nothing that scans for a wrong
    token can find it.
    â†’ **Pair every "no X may do Y" with a curated list of "these must do Y", each row
    carrying its reason.** `GameCommandsTests.SurfacesNeedingACommand` is it, written the way
    `DeadSettingTests.Known` is written; adding a surface that asks for an output file means
    adding its row. Verified by checking that the two rows for the broken surfaces fail on
    the pre-fix tree, not merely that they pass on this one.
    â†’ **And the same absence hides from a screenshot** (trap 29): a control that was never
    drawn photographs as an unremarkable panel. So `gearCopyCmd` goes into `EQBUDDY_EXPAND`
    and `EndToEndTests` asserts it against the real exe â€” a picture can confirm the
    affordance reads well, but only an assertion can say it exists. (The Avalonia twin of
    that assertion went with the lane in E-2c; the dump key is what survives, and it is the
    half that was always about the build players run.)

### Trap 35

35. **An affordance the phone cannot honour is not parity, it is a lie with the right
    shape.** The desktop rule is "name a command, offer a â§‰ copy". Copying that literally to
    EQBuddy Mobile puts the command on the phone's clipboard, which cannot reach the game
    running on the PC â€” a button that does exactly nothing useful, which is "silent no-ops
    are broken" with the switch on the other side. David's answer (2026-08-20, asked as its
    own question) was **selectable text plus "on your PC"**: same fact, same
    `GameCommands` source, an affordance the device can actually keep.
    â†’ **When porting a rule to another surface, port the INTENT and re-pick the control.**
    The wire carries the command (`CompanionCommandPrompt`) rather than `index.html`
    spelling it, because trap 32 means a page-side literal can sit on an open phone for
    weeks after the PC has moved on.

### Trap 36

36. **A lifted view that brings its own `ScrollViewer` SWALLOWS the mouse wheel inside a
    host that already scrolls.** A child scroller is measured with INFINITE height by the
    outer one, so it never overflows and never scrolls â€” but it still *handles* the wheel,
    so the outer scroller (the one with the real overflow) never sees the event. The
    Inventory tab could only be moved by dragging the outer slider (David, 2026-08-20).
    Nothing shows it: not a diff, not a test, not a screenshot â€” the scrollbar is right
    there and looks correct. You only find it by putting a mouse on it.
    â†’ **Scrolling belongs to the HOST**, the same way visibility and spacing do in trap 15.
    A view lifted out of a window brings its CONTENT and leaves the window chrome behind.
    `GearCardView` gets away with its own scroller only because a hard `MaxHeight` gives it
    genuine overflow â€” which is a card-sized cap now living in a window, and worth a look.

### Trap 37

37. **Trap 36 has a second half: a lifted view's PINNED chrome stops being pinned.**
    Scrolling belongs to the host, so a view arrives with no scroller of its own â€” but the
    thing it left behind was a `Grid` whose rows put a footer OUTSIDE the scroller, always
    on screen. Concatenate that footer into a `StackPanel` body and it is now the last
    thing after every row: the Drops tab's footer, which carries the only in-app pointer to
    where the wiki contribution pack went (#217), landed under thirteen creatures of rows.
    Nothing sees it â€” not a diff (the control is there), not a test (it renders), not the
    unit suite. The first screenshot did, immediately.
    â†’ **When you lift a view out of a Grid, list what each ROW of that grid was buying.**
    A row that existed to keep something visible is a decision, not layout. Either give the
    fact to the host's own chrome or move it ABOVE the scrolling content, which is what the
    Drops tab did â€” orientation text is read on arrival, so the top is where it belongs.

### Trap 38

38. **"Sent once" is a claim about the DEVICE; what the page actually holds is a claim
    about the LAST PAYLOAD.** `CompanionSnapshot.ForClient` withholds the big static
    payloads â€” the quest catalog, the zone's map geometry â€” from any device whose recorded
    stamp matches, and the page compensates by copying them forward off the PREVIOUS
    payload. Those two rules only agree while the SECTION keeps arriving. Drop the section
    (a `subscribe` that narrows the picks, or the desktop gating the surface off) and the
    page has nothing to copy from, while the server goes on believing the device is holding
    it â€” so the payload never comes again and the surface waits forever. David's phone,
    2026-08-21: Quests stuck on "Waiting for the quest catalog from the PCâ€¦", reached by the
    ordinary act of ticking Quests in âš™ Screens, because the phone's first-run picks are
    spawns+session and the unsubscribed connect push had already spent the catalog on a
    page that was not showing the surface. The map had the identical hole in the same zone.
    â†’ **A sticky payload's memo must record what the last message CARRIED, not what was
    ever sent.** `CompanionClientState.HeldQuests`/`HeldMap` are that, and forgetting costs
    one re-send where not forgetting costs the surface.
    â†’ **And the repaint gate is the second half.** `setCatalog` is a side effect of a
    PAINT, and the gate (#202) excluded `catalog` from its key to avoid stringifying 1,200
    quests â€” so the payload that finally brought a catalog changed nothing the gate could
    see and the panel could never be filled on that page load, even by a correct server.
    **When a render has a side effect, the thing that decides whether to render must be
    able to see what the side effect needs.** Presence, not content: `catalog ? 1 : 0`.
    â†’ Both halves were reproduced in `scripts/mobile-harness.ps1` driving the shipped page
    through the real âš™ picker, before and after. The reasoning had the mechanism right and
    the second half missing; the harness is what found it.

### Trap 39

39. **An assertion that compares `ToString()` of two objects can be comparing two TYPE
    NAMES, and it passes forever.** A render suite proved the #211 fix ("the badge is a
    clickable vector, not a glyph") by parsing the expected icon path and comparing
    `StreamGeometry.ToString()` on both sides. Avalonia's `StreamGeometry.ToString()` returns
    `"Avalonia.Media.StreamGeometry"` â€” so every icon equalled every other icon, and the
    assertions that the Map badge and the Sparkle marker were drawn would have passed with a
    Phone icon in their place. Found 2026-08-22 only because a NEW assertion counted: "two
    re-check buttons" came back as four, which was every icon button in the view. Trap 34's
    shape once more â€” a guard that cannot fail reads as coverage â€” with the twist that this
    one was written *because of* a real bug, so it looked like the most trustworthy test in
    the file.
    â†’ **Identity is a property you PUT on the object, not a string you hope it renders to.**
    `DesignSystem.Icon` stamps the catalog name on `Tag`; tests read that. And
    **every equality assertion deserves one negative** â€” `DoesNotContain("Phone", icons)` is
    what keeps it from going vacuous again.
### Trap 40

40. **A missing FONT WEIGHT does not fail â€” it gets SYNTHESISED, and the result looks like
    a kerning bug in a font whose kerning is perfect.** The bundled Wine font shipped
    Regular/400 alone while the WPF app names SemiBold or Bold in 71 places. WPF matches a
    `FontWeight` to a face by `usWeightClass`; with nothing to match it thickens the Regular
    outlines *where they stand*, so every glyph gets wider and none of its neighbours move â€”
    sidebearings and kern pairs untouched. Reported from CrossOver on macOS, 2026-08-21, as
    "the main font is still having kerning issues", and the natural first move (check the kern table)
    says the font is fine: 5,652 pairs, values identical to upstream Noto Sans. **The defect
    was in a face that did not exist**, which is trap 20's "the thing you are looking for is
    what is not there" wearing a typographic hat. Nothing on Windows can reproduce it, because
    Segoe UI Variable supplies the real weights.
    â†’ **A bundled font is a FAMILY, not a file.** Ship every weight the UI asks for, group
    them with the typographic names (16/17) and not just the legacy family/style pair, and put
    the icon set in *every* face â€” a bold run containing a section icon resolves to the bold
    face, and Wine boxes whatever that face is missing.
    â†’ **The same blindness hid a second bug in the same font**: `smcp`/`c2sc` had been dropped
    from the subset as "unused features" while `Theme.xaml`'s `SectionLabel` asks for
    `Typography.Capitals=AllSmallCaps` on ~40 headings. WPF synthesises no small caps, so those
    headings quietly lost their case *and* the tracking the design was buying from them.
    â†’ **Now guarded:** `BundledFontFaceTests` parses the `.ttf` tables directly (name, OS/2,
    GSUB/GPOS, cmap) and asserts weights, family grouping, features, icon coverage per face and
    the csproj `Resource` rows. `IconFontCoverageTests` could not have caught any of it â€” it
    counts codepoints and never opens the font, so it read as coverage while being blind to
    everything about the file that is not a cmap entry (trap 34's shape exactly). Verified by
    running the new test against the pre-fix tree: 9 of 10 rows fail there.

### Trap 41

41. **Correct font metrics and wrong glyph positions look identical to the person reporting
    it â€” and the word they will use is "kerning".** The same 2026-08-21 CrossOver report
    that produced trap 39 did NOT go away when the missing weights shipped, because the
    weights were never its cause. Measuring the reporter's screenshot settled it in one
    pass: the line was 360px wide against the font's predicted 361.9px, all ELEVEN word
    spaces landed within a pixel of prediction, the letterforms were the bundled font's,
    and the line pitch (16.4px vs a predicted 16.34px) proved it was rendering 1:1 at 96
    DPI with no scaling. Everything the font is responsible for was right. What was wrong
    was five 1-2px gaps *inside* words â€” "an d th is", "bun dles", "Win e".
    â†’ **Wine truncates the fractional glyph advances WPF's default `TextFormattingMode.
    Ideal` depends on**, instead of carrying the remainder, so text creeps left until the
    accumulated error flushes as a visible gap mid-word. `Display` uses whole-pixel
    advances and is the only mode Wine renders correctly. **No .ttf can reach this**, which
    is why a rebuilt font changed nothing.
    â†’ **Now guarded:** `UI.Shared/TextRenderingPolicy` decides per environment (Wine â†’
    Display, Windows â†’ Ideal, `EQBUDDY_TEXTMODE` overrides either way) and is unit-tested;
    `WineText` applies it with one `OverrideMetadata` call on `Window`, before any window
    exists, because the property inherits.
    â†’ **The measurement is the lesson, not the fix.** Two plausible theories died to
    arithmetic that took a minute each â€” synthetic bold (real defect, wrong cause) and DPI
    virtualisation (killed by the line pitch). **A screenshot of text is quantitative
    evidence**: predicted advances, word-space positions and line pitch are all computable
    from the shipped `.ttf`, and they say which layer is lying. Measure before theorising.
    â†’ **And when it is still ambiguous, put the instrument IN the app.** `TextProbeWindow`
    (`--textprobe`) renders one sentence under all eight TextFormattingMode Ã—
    TextRenderingMode combinations and reports which face WPF resolved for each weight.
    One screenshot from the reporter answered what three rounds of hypothesis had not â€”
    including confirming, incidentally, that the trap 39 font DOES group its three weights
    correctly under Wine.

### Trap 42

42. **`OverrideMetadata` on a Window changes the WINDOW. It does not change the text in it â€”
    a metadata default is not a set value, and only set values inherit.** The trap 40 fix
    was applied with one line: override the default of the inherited attached property
    `TextOptions.TextFormattingMode` on `typeof(Window)` and let inheritance carry it down.
    It shipped, and the reporter saw *no change whatsoever* â€” from the far side of the
    machine, indistinguishable from a stale binary, which is where the next round trip
    went. WPF's property-value inheritance propagates a value that has been SET on an
    ancestor; a metadata default is not set, so every descendant went on resolving its own
    default from its OWN type's metadata, which was still `Ideal`. **Nothing is wrong in
    the diff, the build or the tests, and the feature is genuinely in the binary.**
    â†’ **Override the default on `FrameworkElement`** (so every element answers Display on
    its own account, with no inheritance walk involved) **and/or SET it** via
    `EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, â€¦)`.
    `WineText` does both; either alone would probably do, and the failure they prevent
    cannot be seen from a machine that is not running Wine.
    â†’ **The general shape: "present in the build" and "in effect at runtime" are different
    claims, and only the second one is the feature.** This is trap 29 (a deleted gate left
    its controls hidden) and trap 20 (a setting with no writer) in a third costume.
    â†’ **So make the diagnostic report the EFFECT, not the intent.** `TextProbeWindow` now
    prints what the policy decided beside what a plain `TextBlock` with nothing set on it
    actually resolves, tagged `[applied]` / `[NOT APPLIED]`. That one line separates three
    states that had looked identical for two builds: wrong binary, policy chose Ideal, and
    policy chose Display and could not deliver it. Confirmed afterwards without trusting
    the label â€” a chrome line of *identical text* measured 7px narrower between the two
    builds, which is the app-wide mode changing and nothing else.

### Trap 43

43. **Trap 20 has a MIRROR, and it is worse: a value with a producer and no consumer.** A
    setting read but never written loses a capability quietly. A property WRITTEN but never
    read means the app is doing something to the player's data and telling them nothing.
    `MainWindow.LastAchievementsImport` shipped 2026-08-20 documented â€” in both UIs â€” as
    *"read by the Raids surface"*, and no Raids surface ever read it. So when the game
    announced an achievements dump, EQBuddy imported it, marked Sky rewards turned in and
    raid clears complete, **and produced no report, no Undo, and no mention of the rewards
    its own #101 guard had just refused.** The inventory half of the same commit reported
    itself on the Gear tab, so the commit message's "the report is visible on the Gear tab
    with an Undo" was true and the gap was invisible behind it.
    â†’ **Nothing routine can see this.** The compiler is happy (the property is assigned),
    the Core unit tests pass (the outcome was correct all along), the ratchet does not care,
    and **a screenshot shows an unremarkable card** â€” trap 29's point again. `DeadSettingTests`
    scans settings, not properties, so the guard that exists for the other polarity is blind.
    â†’ **Now guarded:** `ImportReportReachesASurfaceTests` â€” a curated must-list (trap 34's
    shape) naming every recorded import outcome and the surface that has to show it, asserted
    against both widgets' source. Verified by running it on the pre-fix tree: 6 of 11 rows
    fail there, and every failure names `LastAchievementsImport`.
    â†’ **The general move: when you write "for X to report" in a doc comment, grep for X.**
    A comment describing an intended reader is the strongest possible signal that the reader
    may not exist â€” nobody writes that sentence about code they have already called.

### Trap 44

44. **A report about something that JUST HAPPENED, appended after the rows, is below the
    fold.** Trap 37 said a lifted view's pinned chrome stops being pinned; this is the same
    lesson arriving from the other end, and the screenshot is what said it. The Raids import
    report was added at the bottom of the card â€” after 21 boss rows, a provenance note and a
    copy button â€” and the widget caps its own height, so the report rendered correctly and
    **behind a scrollbar**, on a surface the player has no reason to scroll. The first take
    happened to fit; the second did not, and the two pictures differ by nothing but timing.
    â†’ **Notifications go where the eye lands.** Above the rows, under the header. "Read on
    arrival" decides position, exactly as it did for the Drops tab's orientation line.
    â†’ And note what caught it: the shot was taken TWICE, and only the second one showed the
    problem. **A single passing screenshot is not proof a surface fits** â€” it is proof it fit
    once.

### Trap 45

45. **A control NEVER moves between two windows on Avalonia â€” and the operation that does
    it looks like a getter.** `IProgressHost.ProgressTabBody(tab)` handed a theme window a
    control the widget had built and was still rendering into. That is a cross-`TopLevel`
    re-parent, which throws `Attempt to call InvalidateArrange on wrong LayoutManager` â€”
    **an open upstream bug, not a mis-sequencing on our side**: avalonia#12753 (2023, still
    open), #17906 (regression in 11.2.0, fine in 11.1.5), #21267 (2026, same message in
    production). We ship 12.1.1. Six attempts to sequence the hand-off safely all failed,
    and the one API that would have forced a layout flush turned out to be `internal` â€”
    because re-parenting across roots is unsupported, not because the version is old.
    â†’ **It hid for months because a CLOSED window's presentation source is cleared**, so
    the reopen move passed by null. It surfaced twice: every theme window crashed on close
    and reopen for Linux and macOS players (two clicks, since each shipped, fixed in
    1.99.4), and the inline theme card â€” the first host alive at the same time as the
    window â€” threw on its first run and blocked Inline themes PR 1 outright.
    â†’ **Now guarded:** every host builds its own instance through a factory
    (`NewProgressSurfaces()`) and no host interface returns a `Control` it did not just
    create. `SurfaceOwnershipTests` scans for the accessor shape and carries a curated list
    of the two surfaces that still have it (Gear & Loot, Kills & Drops) with the reason and
    the PR that removes each â€” an exemption nobody can see is a blind spot, not an
    exemption.
    â†’ **The general shape, and it outlives this toolkit bug: a method that returns a
    long-lived UI object is a transfer of ownership wearing a getter's clothes.** The WPF
    lane never had the crash because its cards were objects from the start, and "each host
    builds its own" cost nothing there either.
    â†’ **AND THE GUARD OUTLIVED THE LANE THAT FOUND IT.** E-2c deleted Avalonia; E-2b had
    already re-pointed this scan at the five WPF hosts, because its first group read
    `EQBuddy.Avalonia` only while its header claimed it covered both â€” one more line from
    going silently vacuous. **Do not let the two exemptions be re-justified as "one lane, so
    ownership does not matter."** A WPF `UIElement` has one parent too; there the symptom is
    a surface silently vanishing rather than an exception, which is harder to notice, not
    easier. E-3's shell becomes a second host for surfaces the widget still renders.

### Trap 46

46. **When a surface moves to a new host, check what the OLD host was doing for it every
    tick.** PR A moved the Progress rooms into views the window owns. The window already had
    a `MaybeRefresh()` with a two-second throttle, so the obvious wiring was to render there
    â€” and that would have put a two-second stutter on live numbers, because the throttle had
    only ever covered the window's CHROME. The surfaces themselves were painted by the
    widget's own per-tick `RefreshExpandedSections`, and that distinction lived nowhere
    except in the arrangement of the old code.
    â†’ The visible surface paints every tick; only the title and tab strip are throttled.
    **A regression like this reads as "feels laggy" and never gets reported as a bug**, so
    the thing to do at move time is enumerate what the old host called and how often â€”
    not just what it called.

### Trap 47

47. **A CONSENT GATE IS ONLY AS GOOD AS ITS SLOWEST PATH â€” and a periodic job whose "last
    run" starts at `DateTime.MinValue` is not periodic, it fires immediately.** Auto-empty
    asks permission on the tour's first page, and the startup sweep waited for the answer
    (`TruncateLogs && !ShowTutorial`). The 10-minute janitor asked `TruncateLogs` alone, and
    `_lastJanitorRun = DateTime.MinValue` means `Now - _lastJanitorRun > 10min` is TRUE on
    the first one-second UI tick. So the guarded path deferred and the unguarded path
    destroyed every log about a second later, with the consent dialog still on page 1. The
    player (StrIIker-TV, Reddit 2026-08-23) ticked the box, lost everything anyway, and could
    only report that it *"didn't take hold properly"* â€” an accurate description of an app
    that asks after it acts. **Four copies of one rule, and the one that disagreed did so in
    the direction that destroys data.**
    â†’ **Never let two code paths decide a destructive question.** `UI.Shared/LogJanitorPolicy`
    is the one answer and `LogJanitorPolicyTests` scans both widgets so a fifth site cannot
    drift. And **when you find an "every N minutes" job, check its epoch** â€” `MinValue` is a
    first-tick job wearing a scheduler's clothes, which is fine for a refresh and not fine
    for anything irreversible.

### Trap 48

48. **A GLOB THAT SELECTS THE APP'S OWN FILES ALSO SELECTS THE USER'S COPIES OF THEM, AND
    IT CANNOT TELL THEM APART.** The same sweep emptied everything matching `eqlog_*.txt` â€”
    the game's naming, and equally the naming of every backup a player keeps beside it.
    Renaming a log as it grows (`eqlog_Name_server_2026-08-01.txt`) is the obvious way to
    keep history and put it directly in the firing line. **Enumeration is not permission**:
    the pattern that FINDS a file is not the test for whether you may destroy it.
    â†’ `Core/GameWrittenLog` gates destruction on the shape the game itself writes, and the
    subtlety is worth carrying: **the discriminator is the CHARACTER SET, not the segment
    count.** "Exactly two parts after `eqlog_`" looks obviously right and is wrong â€”
    `eqlog_Aenari_erollisi_marr.txt` is a real log whose server short name contains an
    underscore, so segment counting would refuse to sweep a legitimate log forever, which is
    the failure the feature exists to prevent. A rename adds digits, a dash, a space, a dot,
    "(1)", " - Copy"; the game writes letters. The residual gap (a letters-only rename like
    `..._old.txt` is unresolvable from the filename) is written into the doc comment rather
    than papered over.
    â†’ **And the reason this was recoverable at all is a default set two weeks earlier for
    the same reason** â€” `ArchiveLogs` on by default since 1.84.0 (#146, wizen: EQBuddy's
    out-of-the-box behaviour was destroying a file nobody asked it to destroy). A net under
    an irreversible operation pays for itself the day the operation turns out to be wrong
    about what it was operating on.

### Trap 49

49. **WHEN ATTRIBUTION IS THE MECHANISM, COUNT THE ACTORS FIRST â€” a rule that names two
    when there are three guards the one that was never the problem.** The window-height
    follower asked "did *I* cause this size change, or the *player*?" and set a `selfSet`
    flag around its own assignment so the answer was a fact rather than a guess. That flag
    was correct and irrelevant: while following, the window is `SizeToContent.Height`, so
    the **TOOLKIT** resizes it on every content change, and every one of those read as the
    player's drag. The window took ownership on its first frame, persisted ~218px on an
    undragged close, and reopened frozen â€” **the pin came back through the settings file**,
    which is worse than the bug it replaced. Reverted (Fable 5, `4548e10`) on evidence from
    `scripts/drag-verify.ps1`.
    â†’ **The unit tests agreed with the bug**, which is the part worth carrying: thirteen
    green tests, all written over the same two-actor world, so the model's missing
    participant was invisible to every one of them. This is trap 34's shape one level up â€”
    not a guard that forbids the wrong thing, but a guard that **cannot conceive of** the
    right thing. A test suite is only as complete as the model it encodes.
    â†’ **So enumerate the participants and put them in the test names.** "selfSet vs not" is
    a binary someone invented; "follower / toolkit / player" is a fact about the system, and
    writing the second one down is what makes the third actor visible before the code ships.
    â†’ And once again the instrument beat the reasoning: a harness driving the real exe
    answered in one run what a review and an executor had both reasoned past. **Ship the
    instrument before the third theory** (trap 33's closing line, earning itself again).

### Trap 50

50. **A LIST THAT IS "TOP N BY COUNT" HIDES EXACTLY THE ROWS A PLAYER CARES MOST ABOUT,
    because the memorable thing is the RARE thing.** #234 (atrzonkowski): named mobs from
    Guk were missing from session history's "Kills by creature" and "Mob farming" while
    still appearing in Encounters. Nothing filtered them. Both rollups were `Take(10)` and
    `Take(8)` over lists Core sorts by kill count descending â€” and a named is the mob you
    killed **once**, so a dozen kinds of trash at ten-plus kills each pushed all four off
    the end. Core was innocent throughout; the data was complete.
    â†’ **The reporter's own discrepancy was the diagnosis.** "Present in one list, absent
    from two others" is a ranking-and-truncation signature before it is a filtering one, and
    the tell is that the missing rows are the rarest. Ask "are the absent entries the ones
    with the lowest count?" before looking for a filter.
    â†’ **And a surviving cap must SAY so** â€” "... and 5 more items". A trimmed list that
    looks complete is "silent no-ops are broken" with the switch on the other side: there is
    no way for a player to tell a short session from a truncated one, which is why this took
    a bug report to find rather than being obvious to anyone who ever farmed a zone.

### Trap 51

51. **ONE SHARED FIXTURE + CUMULATIVE STAGING = ORDER-DEPENDENT SCREENSHOTS, and the
    difference reads as a regression in whatever you happen to be reviewing.** All 50 shots
    in `shoot.ps1` run against ONE profile and ONE fixture log. `Write-Settings` rewrote
    `settings.json` wholesale per shot, so that half was clean â€” but `Append-Log` only ever
    *appended*, and four shots append. So a shot's picture depended on which shots had run
    before it: `progress-card` came back **520Ã—497** in a full run and **520Ã—389** shot on
    its own, twice each, on identical code, because two different shots append *"Welcome to
    level 12!"* and in a batch the Progress ding list had two levels in it.
    â†’ **Both pictures are of a real state. Only one is of the state the shot is about** â€”
    trap 23's failure mode arriving through the HARNESS rather than through the staging, and
    the reason it is worse: it cannot be caught by predicting the numbers, because the number
    is correct for the log that was actually there.
    â†’ **What it silently cost: `shoot.ps1` was not usable as the acceptance criterion this
    file relies on.** A reviewer re-shooting one image to check their change gets a different
    picture than the batch that committed it, and the honest reading of that difference is
    "I broke something". I nearly filed 17 screenshots as "drifted" on exactly that mistake.
    â†’ **Now guarded:** the pristine fixture is copied aside once and restored before EVERY
    shot's appends â€” unconditionally, and before the early return, so a shot with no appends
    of its own still gets a clean log rather than inheriting the last one's. **When staging is
    cumulative and the fixture is shared, reset is not an optimisation, it is the contract.**

### Trap 52

52. **AN EXEMPTION IS ONLY AS GOOD AS THE PREMISE THAT ASKED FOR IT â€” and a false premise
    buys a permanent hole in a real guard.** eqlwiki renamed its class-row template
    (`RadSpellRow2` â†’ `KhazamSpellRow`, 2026-08-31) and dropped its `description` field, so
    the weekly harvest wrote 347 spells where 1,352 had been and broke 17 tests including
    `ClassInferenceTests`. Fixing the parser and adding a spell-page description fallback got
    it to 1,329 of 1,353 â€” and I reported the remaining **24 as "no prose on any eqlwiki
    page", asked for a ruling, and Helm authorised a curated `KnownGaps` exemption list.**
    **All 24 had prose.** The fallback looked them up by the spell page's `spellname` field,
    which is `spellname or title` â€” and `spell-levels-promote.py`'s **own header** already
    calls that a copy-paste artefact, with `Healing Water` (declares `spellname = Greater
    Healing`) as its worked example. The LEVELS half of that same file had stopped trusting
    `spellname` years earlier; the fallback re-introduced it. Keying on the page TITLE
    recovers 24 of 24, so the guard stays strict at 100% and the exemption list has no rows
    to write.
    â†’ **The damage a wrong premise does is not the wrong number, it is the DECISION it
    triggers.** A guard relaxed on a false report stays relaxed long after the report is
    forgotten, and an exemption list with nothing legitimate in it is a hole waiting for the
    next regression to fall through. **Before asking anyone to weaken a guard, re-derive the
    premise from a second source** â€” here, one grep of the page titles.
    â†’ **And read what the file says about the key you just chose.** The answer was in the
    header of the function being edited. Same shape as trap 39: the most trustworthy-looking
    step is the one nobody re-checks, and "the code already told you" is the cheapest
    correction there is.

### Trap 53

53. **A SHOT'S `Title` IS AN IDENTITY THE SURFACE CAN INVALIDATE WITHOUT TOUCHING THE SHOT â€”
    and because `shoot.ps1` runs under `$ErrorActionPreference = 'Stop'`, ONE stale title
    stops the whole batch at that row.** The World fold (1.99.13) deleted `MapWindow`,
    `SpawnsWindow` and `TravelWindow` and re-pointed their env hooks at `WorldWindow` â€” the
    author even wrote "kept apart because they already appear in shot fixtures and docs" at
    the hook. The `Title` fields those fixtures match on stayed `'Spawn'` and `'Zone Map'`,
    and nothing on earth carries a window title but the window. So from 2026-08-27 to
    2026-09-02, across four releases, `scripts/shoot.ps1` with no `-Shot` **could not get
    past shot 37**: `spawns-window`, `spawns-sky`, `zone-map` and the twenty-three shots
    after them were unreachable in a batch. Individual `-Shot` runs kept working, which is
    why it went unnoticed â€” every session that re-shot one image got a picture and moved on.
    â†’ **The acceptance criterion this whole file leans on had been dark for six days, and
    nothing said so.** That is the cost, not the three titles. It is trap 51's cost sentence
    ("`shoot.ps1` was not usable as the acceptance criterion") arriving through a different
    door, and trap 30's lesson (a staging list is code that cannot be type-checked) with the
    stale token being a window title rather than an enum member.
    â†’ **When you delete, rename or fold a WINDOW, grep `scripts/` for its title before its
    class name.** The class name is what a compiler follows; the title is what the harness
    follows, and only one of those two has a compiler. And **run the batch, not one shot** â€”
    a green `-Shot` proves one row, and the batch is the only thing that proves the rest.

### Trap 54

54. **POWERSHELL DECODES A NATIVE COMMAND'S STDOUT WITH `[Console]::OutputEncoding`, WHICH
    IS NOT UTF-8 HERE â€” so a guard that compares a file against `git show` will find every
    non-ASCII line "changed" on a tree `git diff` calls identical.** The first run of
    `scripts/whatsnew-guard.ps1` reported **111 of 129 shipped What's-new entries as edited
    after they shipped**. Every one was a false positive: the entries carry em dashes,
    arrows and âœ¦, and those came back through the OEM code page mangled. The failure is
    maximally convincing â€” a long, specific, per-version list â€” and maximally wrong, and it
    took `git diff v1.99.16 -- <file>` returning empty to disprove it.
    â†’ **A guard's first red run deserves the same scepticism as a green one.** Trap 34 says
    a guard that forbids the wrong thing reads as coverage; this is the mirror â€” a guard that
    cries wolf gets switched off, and it would have been switched off on its first day.
    â†’ **Read git's bytes, not PowerShell's decode of them.** Wrap the call:
    `[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)` around it (restore in a
    `finally`). The same trap waits for any `git show`/`git log`/`gh api` output this repo
    reads back and compares, and all of it runs through `pwsh`.

### Trap 55

55. **UNFOLDING A CARD DOES NOT UNDO ITS FOLD â€” the MIGRATION goes on absorbing it, once per
    launch, forever.** #252 (TiconaX): *"The cards always reset to having 2 cards open even
    though I have hidden all of them. Gear & loot and + Motes."* Both halves were a fold
    running long after its fold was over, because something kept handing it a key it believed
    it still owned. **Motes:** it became a top-level card again on 2026-08-21, and
    `ProgressSurface.AbsorbedCardKeys` was never told â€” so `FoldThemeSections` saw a LIVE
    catalog key in every profile's `SectionOrder`, judged itself stale on every launch, and
    stripped `motes` out of `HiddenSections` each time. **Gear & Loot:**
    `ApplyDefaultGearSection` re-created the `gear` key every launch and `MigrateLootSections`
    absorbed it again every launch â€” and the re-hide rule (`hidden >= present`) refused to
    re-hide `loot` because it was counting a `gear` **no player could ever have hidden**, that
    key having had no row in Options since the fold removed it from the catalog. (`progress`
    came back too, by the same arithmetic, on anyone who had hidden it.)
    â†’ **The fold is written to be idempotent and each half genuinely is.** The bug lived in
    what two migrations did to EACH OTHER across a restart, so nothing testing one migration
    once could see it â€” `ProgressSectionFoldTests` has run it twice and asserted silence since
    the day it was written, and passed throughout. **Now guarded:** the chain is
    `AppSettings.ApplyMigrations`, a method for exactly one reason â€” so a test can run *the
    whole thing* twice. `SectionFoldIdempotenceTests` does, and 14 of its 29 rows fail on the
    pre-fix tree.
    â†’ **And the premise check is the one that would have cost nothing: a fold may only name
    keys that are NO LONGER CARDS.** `OptionsViewModel.AbsorbedTitles` had already dropped
    Motes and says why in as many words â€” two hand-maintained lists describing one fold, and
    only one of them updated. Trap 30's lesson (a staging list is code that cannot be
    type-checked) with the stale token being a card key; trap 20's (the thing you are looking
    for is what is *not there*) as to why nothing flagged it. The guard now checks the
    absorbed lists against `OverlaySections.Catalog` instead of trusting either comment, and
    it matters right now: Bevel has a live ask to give **Faction** its card back (#251), which
    is the same change that broke Motes.
    â†’ **The tell, next time: a migration chain that reports work on every launch.** It meant
    `Load()` was rewriting `settings.json` on every start of the app â€” trap 13's loaded gun â€”
    and #253 was *the same shape* two releases earlier (a one-time upgrade step not marked
    one-time, undoing the player each launch). Two in three weeks: when a settings symptom is
    "my choice does not stick", suspect a migration before you suspect the save.

### Trap 56

56. **ONE DUMP LINE, TWO MOMENTS â€” a satellite window follows the widget's tick on its own
    throttle, so its ROW COUNTS and the widget's TOTALS beside them are read off different
    snapshots.** `RefreshUi` ticks the six theme windows *before* it builds the snapshot it
    dumps, and each one throttles again on top of that (1 s for Kills & Drops and Gear &
    Loot, 2 s for Progress and Quests, 3 s for the wiki pack). So `kills` can be a whole
    creature behind `killsTotal` for seconds, with nothing wrong anywhere.
    â†’ **It cost the E2E suite four rounds and about forty runner-minutes**, because the
    suite's shape is "sample a baseline, append a line, wait for baseline + 1" and
    `WaitForDump` is an EQUALITY: a baseline taken off a window that is still catching up
    makes the expected number one the counter passes *between two polls*, and the wait can
    then never be satisfied. Every failure reads as a broken feature â€”
    `SessionGoesLiveâ€¦`'s "kills to reach 12; last seen 13" is a correct app and a wrong
    question.
    â†’ **Three of the five rounds guessed at "settled" from STILLNESS, and each guess was a
    claim about the machine.** Watching two totals missed the fixture's trailing sale
    lines; watching the whole dump could not tell a mid-ingest lull from an ending; adding
    `ingestDone` (the watcher's own answer) fixed the LOG half and left the RENDER half
    untouched â€” and 2.5 s of quiet cannot cover a 2 s throttle plus a tick, so the fourth
    guess would only have been a bigger number.
    â†’ **The fourth round asked instead of inferring, and STILL waited on a coincidence.**
    Every satellite recorded the version it painted (`RenderedVersion`), the dump counted
    the open ones that were behind (`surfacesBehind`), and `WaitForReplayToSettle` waited
    for that count to reach zero. It reported the disagreement honestly and then timed out
    at 90 s on `ingestDone=1 logPending=0 killKinds=14 kills=13` â€” a complete log, complete
    data, one row short on screen. **Making a two-moment dump legible is not the same as
    making it one moment, and only the second one is a thing a wait can rely on**: the
    throttles were never obliged to line up with the tick that writes the dump.
    â†’ **Now fixed at the source.** `MainWindow.RefreshUi` ticks the satellites AFTER it
    builds the snapshot rather than before (they read `CurrentSnapshot()`, so the old order
    painted every satellite from LAST tick's â€” a second behind the widget beside it, for
    players too), and `WidgetDump.PaintOneMoment` paints any open surface still behind
    before reading a row count off it. One dump is ONE MOMENT; `kills == killKinds` by
    construction; `surfacesBehind` stays as the assertion that this holds, not as something
    to wait for. `FollowingSurfaces` owns the list of open satellites so the tick and the
    count cannot drift. **The general rule: when a dump carries two numbers about one
    thing, say which moment each came from â€” or make them come from the same one. Prefer
    the second: the first still leaves a wait that has to get lucky.** Trap 4 with the two
    sources a tick apart instead of twenty lines apart.
    â†’ **And the diagnostics went in before the last theory, because two failures were
    indistinguishable from outside.** A counter that will not move is a stalled TAIL or a
    line that parsed without counting, and only the app can tell you which:
    `logPending` (bytes the tail has not read), `logSelects` (a re-Select resets and
    replays the session underneath you) and `killKinds`/`lootKinds` (the DATA's count
    beside the window's rendered one). `AppHarness.AppendLogLines` now returns only once
    `logPending` is back to 0, so a stalled tail fails at the append and names itself
    rather than surfacing 90 s later as a wrong row count.
    â†’ **And a THIRD failure hides behind the same symptom: the app is no longer there.** An
    EQBuddy that has exited, or whose tick has stopped, leaves a `debug.txt` that looks
    perfectly healthy and is perfectly frozen â€” every wait then runs its full 90 s and
    blames the assertion. The dump carries `tick` (RefreshUi's own count) and every harness
    wait aborts early, naming the app, when the process has exited or `tick` has stood
    still for 30 s. **A polling wait needs a liveness question as well as a value one**, or
    it will confidently misattribute a dead process to whatever it happened to be reading.

### Trap 57

57. **`[Collection("name")]` ON MOST OF A TEST ASSEMBLY IS NOT SERIALIZATION â€” the classes
    that lack it get a collection each, and xUnit runs collections in PARALLEL.** The
    Avalonia suite has ONE headless session on ONE thread, and
    `HeadlessUnitTestSession.EnsureIsolatedApplication` tears the `Application` down and
    stands a fresh one up around every dispatched test. Two threads doing that to one
    session interleave, and the rebuild lands where the dispatcher belongs to the other
    thread: *"The calling thread cannot access this object because a different thread owns
    it"*, thrown from `DefaultRenderLoop.Add` inside `AvaloniaHeadlessPlatform.Initialize`,
    reported as a **Test Case Cleanup Failure** â€” so the test that FAILS is not the test
    that is wrong, and it is a different one every time (`MezTargetsâ€¦`,
    `ClosingAndReopeningâ€¦`, `MapCircleMenuâ€¦`). Nineteen of twenty-one classes carried the
    attribute; **one did not, and two of its tests were enough.**
    â†’ **It read as three unrelated flakes for as long as CI was asked once per commit.**
    It only became one bug when PR #294 ran the same head nine times: 2 of 9 red on the
    Avalonia lane, on both Windows and Linux, green on every re-run. **A flake you meet
    once a day is a race you have not counted yet.**
    â†’ **The fix was `[assembly: CollectionBehavior(DisableTestParallelization = true)]`,
    not another `[Collection]` attribute** â€” the constraint is a fact about the SESSION, and
    a hand-labelled list stops covering the set the day the set grows (trap 30). It cost
    nothing: 311 tests, 19 s either way.
    â†’ **TOMBSTONE, and the reason this entry is still here.** That suite was deleted in E-2c
    (2026-09-04) with the Avalonia lane, so the guard is gone and nothing in the repo
    re-teaches this. **The shape is not about Avalonia**: any test project sharing ONE
    stateful thing â€” a headless session, a UI thread, a server, a fixed port â€” has this
    exact race, and the tell is a flake that names a different innocent test each time.
    E-3 adds test projects. When you write one that shares something, write the
    assembly-level attribute in the same commit, not after the third flake.

### Trap 58

58. **THE `EQBUDDY_EXPAND` DUMP IS ONE FLAT NAMESPACE, AND A SECOND HOST OF A SURFACE
    WRITES THE SAME KEYS INTO IT â€” the later writer wins, silently, and every existing
    assertion on those keys quietly starts reading the other window.** `MapView.DebugFacts()`
    reports `mapShown`, `mapZones`, `mapNamedRows`, and it reports them identically whether
    it is hanging in `WorldWindow` or in the Evolved shell's World room. Open both â€” which
    is exactly what the two-host E2E assertions do, on purpose â€” and the dump carries one
    `mapZones=` whose value depends on the order the facts were concatenated in. Nothing
    about that shows in a diff, a build or a screenshot: both windows render, both are
    right, and the TEST is what changes meaning.
    â†’ **This is trap 4 (one entry, two sources for one fact) with the two sources being two
    HOSTS rather than two lines twenty apart**, and E-3 makes it structural rather than
    accidental: the shell is a second host for every surface it takes, for as long as the v1
    window survives beside it â€” which is deliberately several PRs.
    â†’ **The fix is NOT to hand-write the second host's facts under new names.** That is a
    second producer of a number the first host already reports (trap 33 one level up), and
    it stops covering the surface the day it gains a seventh fact (trap 30). Ask the SAME
    view for the SAME string and re-key it: `UI.Shared/ShellDumpFacts.Prefixed("shellWorld",
    _map.DebugFacts())`. The two hosts then cannot report different facts, because there is
    only one place the facts are written â€” and the comparison assertion becomes possible to
    write at all.
    â†’ **Recorded before it bit, which is why it is worth writing down.** It was caught by
    asking "what does the dump look like with both hosts open?" while wiring the second
    room, not by a wrong test result â€” and a wrong test result here would not have looked
    like a defect, it would have looked like a passing suite.

### Trap 59

59. **A HOTKEY IS NOT A DOOR â€” nothing is bound by default, so "there is a second way in"
    can be true of the WIRING and false of every player who has not configured anything.**
    HUD subtraction cut 1 removed the Quests card, and the pre-design cleared it on exactly
    that ground: *"Quests has a second, independent way in (`toggleQuests`, wired straight to
    `OnQuestsWindow` â€” a hotkey, not a menu row)"*. The wiring is real. But
    `src/EQBuddy/HotkeyManager.cs` says in its own doc comment that **hotkeys exist ONLY when
    the player binds them â€” nothing is bound by default**, and the widget's context menu
    carries `Worldâ€¦` and had no Quests row, because the 2026-08-16 fold deliberately removed
    the cog's Quest tracker line *when the card became the door*. So the card's â§‰ was the only
    entrance a default profile had, and cutting it as scoped would have made the Quest Tracker
    unreachable â€” CLAUDE.md's "three ways back" with all three gone at once, which is #219's
    mechanism with a subtraction behind it instead of a fold.
    â†’ **Nothing routine sees this.** The app builds, every test passes, and an absent menu row
    photographs as an unremarkable context menu (trap 29). The check that found it was one
    `grep` of `HotkeyManager.cs`, run only because the scope line said "hotkey / **door**" and
    it was worth knowing which.
    â†’ **So when you subtract a surface, enumerate its entrances and ask of each one: does a
    player who has never configured anything have it?** A context-menu row, a bound hotkey and
    an `EQBUDDY_*` variable are three different answers to "is there a second way in", and only
    the first is a door. The row went in with the cut, beside `Worldâ€¦`.
    â†’ **This is trap 52's shape** â€” an exemption is only as good as the premise that asked for
    it â€” with the premise being about a PLAYER rather than about the code, which is the half a
    source grep confirms and a source grep also misses. Eight more cards are queued for the
    same treatment; each one gets this question before its diff, not after.

### Trap 60

60. **A CHANNEL FILE IS SHARED STATE THAT ANOTHER AGENT IS WRITING WHILE YOU WRITE IT â€” and
    the two ways a write destroys bytes you did not author both report success.** Trap 54 is
    the READ side of this (PowerShell's decode of `git`'s stdout); this is the write side, and
    it is the one that leaves damage in the repo rather than in a report. `HELM.md`,
    `HELM-FEEDBACK.md` and the other mailboxes are appended to by Helm, Scribe, Bevel,
    Fable and you, on separate machines, between your pulls â€” so they are the one class of file
    in this repo where "I rewrote it with my entry at the top" is a destructive operation.
    â†’ **(a) A STALE BASE AT SPLICE TIME deletes whatever landed in between, and the diff looks
    deliberate.** PR #325's channel commit (`d36bda25`) deleted 75 lines from
    `HELM-FEEDBACK.md` â€” Helm's own ~1:15 PM CT #323 sign among them â€” and rewrote that
    ruling's 8-line state block out of `HELM.md`. Twenty-four minutes later `dd69478f` â€” the
    commit whose own body says it *"cannot clobber or be
    clobbered the way #325 did to #323"* â€” deleted the twelve lines of Helm's ~1:30 PM CT #324
    sign, which had landed in #327 between its `rev-parse` and its `hash-object`. Restored
    verbatim in `6525549d`. **Appending in explicit UTF-8 is not what makes a channel write
    safe; writing against the head you actually push onto is.** Re-read the ref at splice time,
    never at plan time.
    â†’ **(b) A WHOLE-FILE REWRITE RE-ENCODES THE PRIOR BODY, and the mangle is frozen into the
    file instead of into a diff you can disbelieve.** A read-then-write round trip under
    Windows PowerShell 5.1 reads a UTF-8-no-BOM mailbox as the ANSI code page and writes it
    back as UTF-8, so every `â€”` becomes `Ã¢â‚¬â€` â€” trap 54's exact corruption, on the write side,
    where nothing later disproves it. **446 such lines are in the tree right now** (415 in
    `HELM-FEEDBACK.md`, 29 in `HELM.md`, 2 in `SCRIBE-FEEDBACK.md`), standing since 2026-09-02:
    the first one is at line 2,924 of a 6,272-line newest-first mailbox, which is precisely why
    it is still there. **Nobody scrolls to the bottom of a mailbox, so write-side damage is
    permanent by default.** Appending caps the blast radius at the bytes you actually authored;
    a one-line stub must never rewrite a six-thousand-line file.
    â†’ **Nothing routine sees either half.** The commit is clean, small and plausible; the
    message says what you meant rather than what you did; no test reads these files
    (`DocumentationTests` covers `CLAUDE.md`, `docs/Architecture.md` and `docs/TestPlan.md`, and
    stops there); and the author whose ruling you deleted is an agent that will not re-read its
    own entry to notice. This is trap 20's shape â€” the thing you are looking for is what is *no
    longer there* â€” with the missing thing being someone else's sign-off.
    â†’ **THE CHECK IS ONE COMMAND AND THE RULE IS THAT A CHANNEL DIFF IS ADDITIONS-ONLY.** Run
    `git diff <the-ref-you-based-on>..HEAD -- HELM-FEEDBACK.md` before you push: a `-` line you
    did not write is a clobber, whatever the commit body claims. Restore it verbatim from the
    ref that still has it, in its own commit, and say so in the ask â€” the deletion is the thing
    Helm cannot see from its side.
    â†’ **Helm signed both halves as process** (#322, 2026-09-05 ~1:20 PM CT: *"non-interactive
    `*-FEEDBACK.md` writes must APPEND in explicit UTF-8, not wholesale rewrite"*; the splice-time
    re-read on #326 ~1:40 PM CT), and this entry is the filing it asked for. **There is no guard,
    and that is a named hole rather than an oversight:** `scripts/whatsnew-guard.ps1` and
    `scripts/legacy-notice-guard.ps1` wrap the READ side per trap 54, and nothing in the repo
    looks at the write side. The cheap one â€” a mojibake scan over the channel files â€” would fail
    on the 446 lines already committed, which is the argument for building it and the reason it
    is a follow-up rather than this change.

**2026-09-08 — the third hit, and it is the one the stated check CANNOT see.** Appending a
LIVE ASK to `BEVEL-FEEDBACK.md` through `python -c "…"` from Bash: the note is markdown, so it
is full of backticked identifiers, and Bash command-substitutes every backtick pair inside a
double-quoted string before Python ever runs. Four spans vanished. Bash said so — four
`command not found` lines on stderr — and then **Python wrote the file and reported `appended`**,
because from Python's side the string it received was simply shorter.

→ **The append succeeded. It was additions-only. It was 47 lines. And it was missing every
backticked span in the note** — `SettingsHoverProseTests`, `ToolTip = "…"`, `BodyWordCeiling`,
`SettingsAlertsView` — leaving sentences that read as finished English with the identifier
removed. `git diff --numstat` said `47 0`. The trap-60 check passes on it. **A rule that says
"a channel diff is additions-only" cannot see a truncated addition**, and the failure is
invisible in exactly the place the reader looks: the diff.
→ **The fix is the tooling note that was already in `CLAUDE.md`, and this is the case that
shows why it is not stylistic:** *write file content with the editing tools, not shell
heredocs.* The recovery was `git checkout -- BEVEL-FEEDBACK.md` (the append was uncommitted)
and a re-do with the editing tool, anchored on the file's tail. Where the tail's own bytes are
mojibake from an earlier append and will not round-trip through an anchor, write the note to a
scratch file with the editing tool and let Python concatenate two FILES — the note never passes
through a shell string, so there is nothing for the shell to eat.
→ **What to check, since the diff cannot tell you:** read back a distinctive identifier from
the note after appending. `stderr` from a `python -c` that "worked" is evidence, not noise —
a `command not found` beside a success line means the string you thought you sent is not the
string that arrived.

### Trap 61

61. **THE SCREEN IS A MUTEX NOTHING ENFORCED, AND `shoot.ps1`'S OWN STAND-DOWN IS WHAT TURNS A
    BREACH INTO A RANDOM ROW FAILING MID-BATCH.** `FABLE.md` Â§4 says it plainly â€” *"The one hard
    mutex is the SCREEN â€¦ Dranak enforces this by kick order, not by tooling"* â€” and a convention
    with no interlock fails silently. The mechanism: `Get-Process EQBuddy` matches by PROCESS
    NAME, so a second seat starting `shoot.ps1` stands down the first seat's **in-flight fixture
    app**, mid-settle, and records its exe path for a relaunch it will perform with no
    `EQBUDDY_APPDATA` â€” pointing a stray widget at the real profile. The first seat's shot then
    reports *"no visible window matching 'EQBuddy â€” Gear' in process N"*. **Which row fails is
    just whichever one was on screen when the other seat started**, which is why #306's batch
    failed at `shell-gear-narrow`, `options-window` and `drops-window` across three runs and
    **every one of them passed alone.** Nothing is wrong with those rows.
    â†’ **The diagnosis was already written down and read as a different bug.** `DECISIONS.md`
    (2026-09-05, the W2 round) records *"another seat's EQBuddy was running on the same desktop
    â€¦ multi-shot runs died at a different shell shot each time and every one of them passed
    alone"* â€” a complete root cause, filed as a note beside a screenshot decision, while the ask
    that went to Helm asked whether the harness needed a look. **Two seats reporting one symptom
    in two files is how a solved problem stays open**; grep the channel files for the symptom
    before opening an investigation into it.
    â†’ **Now guarded, in the harness rather than in the kick order:** a lock file held for the
    batch (it cannot go stale â€” the handle dies with the process), plus a refusal when any
    EQBuddy is already running out of a `bin\Release` / `bin\Debug` path, because `EQBuddy.E2E`
    launches the same exe and takes no lock. **A player's EQBuddy never runs from a build
    output**; that path is the discriminator, the same "what does the real thing actually write"
    move `Core/GameWrittenLog` makes for log names (trap 48). The stand-down leaves those
    processes alone even under `-Force`: closing another harness's app *is* the damage.
    â†’ **And the second half, which bites with no second seat at all: the readiness wait was
    satisfied by the WRONG WINDOW.** Both `MainWindowTitle` and `MainWindowHandle` name one
    window â€” "the first visible, unowned top-level window of the process", i.e. the widget â€” so
    for every shot whose target is a satellite or a room the `MainWindowHandle -ne 0` escape
    fired the moment the widget appeared, the 90-second deadline was dead code, and **the target
    window's entire budget was `$Settle`: eight seconds, shared with the startup replay.** Every
    hook in `DebugHooks` opens its window at `DispatcherPriority.ApplicationIdle` â€” starved for
    exactly as long as the app is busy â€” and E-3 put a second full window (the shell, on every
    launch since #316) into those same eight seconds. A budget that was generous is now a race.
    It waits for the window `shot.ps1` will actually look for now, by the same exact-wins rule.
    â†’ **A batch no longer stops at the first bad row.** `$ErrorActionPreference = 'Stop'` made
    one failure end the run *there*, leaving every later row unreachable â€” which is trap 53's
    real cost, six dark days in which each session re-shot one image, got a picture and moved
    on. The run still FAILS (a stale title must); it now names every failing row in one pass.
    â†’ **AND THE GUARD WAS ONE-SIDED FOR A DAY, WHICH IS THE HALF WORTH REMEMBERING.**
    `shoot.ps1` took the lock; `tests/EQBuddy.E2E` launched the same exe, on the same
    desktop, and took nothing â€” so the second guard above (refuse over a `bin\Release` app)
    was standing in for a lock the other party never took, and it can only see this suite
    once its app is already UP. **A mutex only one participant acquires is a convention with
    extra steps.** `AppHarness.Launch` now takes the same file, with the same share mode, for
    the whole test-host run â€” the mirror of a batch holding it â€” and refuses the same way.
    Two things fell out of building it that a regex-only check would have missed: the
    rendezvous is between C# and PowerShell, so `ScreenLockTests` runs a real PowerShell
    holder against it rather than only asserting that both files still *say* the same thing;
    and the suite had never actually been the "one app at a time" its README claimed â€”
    `ShellHostTests` launches a real always-on-top app and carried no `[Collection]`, so
    xUnit gave it one of its own and ran it in parallel with the others. That is **trap 57
    exactly**, found by asking what the lock would be worth if the holder ran two, and fixed
    the way that trap says: `[assembly: CollectionBehavior(DisableTestParallelization = true)]`.

### Trap 62

62. **`AppendLogLines` RETURNS WHEN THE TAIL HAS READ THE BYTES, NOT WHEN THE APP HAS ACTED ON
    THEM â€” so an E2E assertion that something did NOT happen, made straight after an append,
    is asserting against an app that has not decided anything yet.** The harness waits for
    `logPending` to reach 0, which is trap 56's fix and is exactly right for what it covers: a
    stalled tail names itself at the append instead of surfacing as a wrong row count ninety
    seconds later. But `MainWindow.OnTextMatched` alerts through `Dispatcher.BeginInvoke`, so
    the whole watch-rule path runs *after* that wait returns. `WaitForDump(key, 0, â€¦)` is then
    satisfied by the zero that was already there.
    â†’ **It reads as a passing test of the thing you just built.** SA-3's watch-fire chip is
    gated on `TrackedRule.AlertBanner`, and the test written to prove that gate â€” one
    banner-off rule, append, assert zero chips â€” **passed with the gate deleted**. Caught only
    by running the prove-fail, which is the entire argument for doing that step on a NEGATIVE
    assertion even when the feature is new and there is no "pre-fix tree" to run against.
    â†’ **The fix is a synchronisation point on the far side of the decision, not a longer
    wait.** `HudDeadlineChipTests` puts TWO rules on ONE line â€” one banner-on, one banner-off â€”
    and waits for the loud one's chicklet: both are handled inside a single dispatcher
    callback, so a chip on the row proves the other rule has been asked and refused. With the
    gate removed the value is 2 against a demanded 1. A `Thread.Sleep` would have been a guess
    about the machine, which is the mistake trap 56 spent four rounds on.
    â†’ **The general shape: every "and nothing happened" assertion needs to name the moment it
    is true AT.** Trap 34 says a guard that forbids the wrong thing reads as coverage; this is
    the timing version â€” a guard that asks the right question one moment too early. When you
    write `Assertâ€¦(0)` in E2E, ask what positive event you could wait for that can only occur
    after the code under test has run, and wait for that instead.

### Trap 63

63. **A FRAMEWORK DEFAULT OF `int.MaxValue` IS NOT "OFF" â€” IT IS AN OVERFLOWING OPERAND, AND
    IT TOOK THE WHOLE APP'S CLOCK WITH IT.** WPF's `ToolTipService.ShowDuration` defaults to
    `int.MaxValue` ms, which reads as "leave the tooltip up" and is arithmetic everywhere it
    is actually used. `PopupControlService` arms a `DispatcherTimer` with it;
    `DispatcherTimer.Restart` computes `Environment.TickCount + interval` in **int32** and
    overflows negative; `Dispatcher.UpdateWin32Timer` takes the wrap-safe MINIMUM due time,
    so the overflowed value reads as ~24 days in the *past* and wins; `SetWin32Timer`
    overflows the other way and arms the **one Win32 timer WPF shares across every
    `DispatcherTimer` on the thread** 24 days out. `_uiTimer` and `_companionPump` die
    together, permanently, and the thread returns to `GetMessage` â€” so the window still
    paints, still answers clicks, and `Process.Responding` stays **True** while every number
    in EQBuddy has stopped. The player shape is *"EQBuddy stopped updating but I can still
    use it"*, which nobody files as a freeze. **We set `ShowDuration` nowhere, so every
    tooltip in the app was the same loaded gun** â€” and there are dozens.
    â†’ **THE TRIGGER IS A SECOND TIMER BEING LATE, WHICH IS WHY IT IS INTERMITTENT AND WHY
    THE FIRST REPRO "DISPROVED" IT.** The overflowed due time only wins the minimum when
    another registered timer is already â‰¥ 1 ms overdue (`interval + Î´` has to wrap). A
    tooltip opening on an idle thread does nothing at all; one opening while the tick is
    running late is fatal. The diagnosis nearly shipped without that term â€” the standalone
    repro did not fire until the thread was blocked 300 ms first. **When an overflow theory
    fails to reproduce, ask what has to be true for the wrapped value to be SELECTED**, not
    just computed.
    â†’ **Five red CI runs of pure reasoning produced three families; one minidump produced
    the answer** (`freeze-tick3`, run 34075983046: `_dueTimeInTicks = -2147108868` against
    `PopupControlService._currentToolTipTimer`, and `374781 + int.MaxValue` is that number
    exactly). Trap 33/49's "ship the instrument before the third theory", earning itself a
    fourth time.
    â†’ **Now guarded:** `UI.Shared/ToolTipPolicy` holds the bounded default (30 s) *and the
    arithmetic*, so the constant cannot be tidied back up by someone who only sees a
    duration; `ToolTipDefaults.ApplyOnce` is one `OverrideMetadata` on `DependencyObject`
    from `App.OnStartup`; `ToolTipPolicyTests` states the mechanism as arithmetic with WPF's
    own default as its committed negative; and `ToolTipTimerTests` (E2E) holds both halves â€”
    the dispatcher mechanism (prove-failed at exactly **zero** ticks, eight runs) and the
    trap-42 EFFECT, read off a launched app's control rather than off our constant.
    â†’ **The general shape, and it is why this is a trap and not a bug report: a "no limit"
    sentinel is a number that some layer will do sums with.** Before accepting a framework
    default that means "forever", "never" or "unlimited", find the arithmetic it feeds. And
    prefer removing the poisoned operand to defending against its consequence â€” the defence
    here would have been a watchdog, which is a `DispatcherTimer` too and dies with the very
    timer it is guarding.
### Trap 64

64. **`tests/EQBuddy.E2E` DOES NOT REFERENCE THE APP IT LAUNCHES, so `dotnet build
    tests/EQBuddy.E2E` cannot rebuild the thing under test â€” and a PROVE-FAIL run then
    reports the mutation passing.** The project takes `EQBuddy.Core` and `EQBuddy.UI.Shared`
    and finds `EQBuddy.exe` by PATH (`src/EQBuddy/bin/Release/net10.0-windows/`), which is
    deliberate and says so in its own comment: *"The app under test is the separately-built
    `EQBuddy.exe` â€” never an in-process instance"*, so a test run cannot mutate build outputs
    mid-flight. The cost is that the only thing tying a source edit to the binary is a build
    command that names the app.
    â†’ **What it looks like: two guards you just wrote, mutated to prove they can fail,
    coming back GREEN in seven seconds.** That reads as "my assertions are vacuous" â€” the
    exact conclusion the exercise exists to reach â€” and the honest next move (rewrite the
    tests) would have been wrong. It is trap 18's family (a build reporting success while the
    assembly under test is not the source) reached through project references rather than
    through an incremental WPF build, and trap 42's shape one level out: *present in the
    source* and *in the binary being run* are different claims.
    â†’ **So run `dotnet build EQBuddy.slnx -c Release` (or `src/EQBuddy/EQBuddy.csproj`)
    before any E2E run whose result depends on a `src/EQBuddy` edit** â€” mutation runs
    included. The tell is the exe's own timestamp: `ls -l src/EQBuddy/bin/Release/
    net10.0-windows/EQBuddy.exe` against the clock, which is one command and settles it.
    The README's "prerequisite: the exe under test" line is the same fact stated as a
    happy-path instruction; this entry is what it looks like when it bites.

### Trap 64

64. **A GATE WRITTEN AS A PROXY STOPS BEING THAT PROXY THE DAY A SECOND PRODUCER ARRIVES —
    and the gate does not change, so nothing in the diff shows it.** `BuffTracker.OnFade`
    learns a spell's real duration only from a cleanly identified landing, and it asked that
    question as `Candidates.Length == 1`. That was EXACT for as long as a set of one was
    reachable only through a cast line: one candidate *meant* "the log named this spell".
    OE-5 taught the tracker to narrow candidates from a `/outputfile spellbook` dump, which
    can also leave one — so with no edit to that gate, a dump-GUESSED identity would have
    taught a real, persisted, per-character duration. **The seat's whole product lock is
    "the spellbook is never a timer source", and this is the one route by which it becomes
    one**, arriving through a line nobody touched.
    → **Nothing routine sees it.** The gate compiles, reads correctly, and every existing
    buff test passes — they were all written in the one-producer world, which is trap 49's
    lesson (*"a test suite is only as complete as the model it encodes"*) with the missing
    participant being a data SOURCE rather than an actor. Found by asking what the new source
    could reach, not by a failure; the fix is `BuffState.DumpNarrowed`, prove-failed.
    → **The general move, and it is cheap: when you add a second producer of a value, grep
    every condition that reads that value and ask what each one MEANT when it was written.**
    A proxy is a claim about the world that a condition happens to encode. "One candidate",
    "no caster", "count is zero", "the list is empty" are all proxies for something, and the
    something is usually written nowhere. Trap 20's shape once more — the thing you are
    looking for is what is *not there*, here being the sentence that says what the test was
    standing in for. Name the fact instead (`DumpNarrowed`, not a length), which is also
    trap 49's *"put the participants in the test names"* one level down.

### Trap 65

65. **`File.WriteAllText` TRUNCATES THE REAL FILE AND DOES NOT WAIT FOR THE DISK — so a
    process killed mid-save leaves a file of the right LENGTH full of ZEROS, and the app
    that reads it back cannot tell that from a player who reset their own settings.** The
    tell is exact and worth memorising: `'0x00' is an invalid start of a value. Path: $ |
    LineNumber: 0`. NTFS commits the new length in metadata before the data reaches the
    platter, so the file is not truncated or half-written — it is the right size and blank,
    which is why every "corrupt JSON" guess about encoding or partial documents is wrong.
    → **What it cost is the whole of #385.** David's `%AppData%\EQBuddy Evolved\error.log`
    threw it from `AppSettings.Load`, `AaLedgerStore`, `StackingLedgerStore`,
    `QuestLedgerStore` **and** `SpawnCycleLedger` at ONE timestamp (2026-09-07 06:59:54) —
    five files, one abrupt termination. `install-local.ps1 -Evolved` force-kills the
    running portable copy when it has not closed within 15 seconds, so **a republish is
    precisely the event that lands in the window**, and the player-facing shape was "every
    time you publish, EQBuddy forgets everything": theme back to ParchmentBrass, the
    built-in Watch rule re-seeded against a `DefaultRulesVersion` reset to 0, hidden cards
    back because `HiddenSections` was empty. Three settings at once is the signature of a
    profile read as BRAND NEW, not of three bugs.
    → **AND THE CATCH BLOCK FINISHED THE JOB.** `Load` caught the exception, started from
    defaults, reported `hadFile: false`, and the migration chain then reported work — so
    `Load` **saved those defaults over the corrupt file**, destroying the last copy of the
    profile and the evidence in one write. A recovery handler that writes is not a recovery
    handler. The same event is visible from the other side one day earlier, in the trap 13
    clobber warning: *"was 389793 bytes … now 4697 bytes"* — 4,697 bytes IS a defaults file,
    and nobody read it as one.
    → **Now guarded:** `Core/ProfileJson` is the one place any profile JSON is written —
    temp file, `Flush(flushToDisk: true)`, then `File.Replace`, which is atomic on NTFS and
    hands the outgoing copy to `.bak` in the same operation. **The flush is the entire
    point**: a plain `WriteAllText`-then-rename still lets the rename reach the disk ahead
    of the data, so it looks like a fix and is not one. `ProfileJson.Read` is the other half
    — an unreadable file falls back to `.bak`, restores it in the same breath, and sets the
    bad copy aside as `.corrupt` rather than overwriting it. `ProfileJsonTornWriteTests`
    holds it, prove-failed: three of eleven fail with the fallback disabled.
    → **The narrow reading in `Read` is load-bearing and was found by a green suite going
    red.** A MISSING file is `Missing` and the backup is never consulted; only a file that
    is PRESENT and will not parse recovers. Widening it to "present or absent" resurrects a
    profile file somebody deleted on purpose — which is what broke
    `HudStatPromotionLoadTests.AProfileWithNoFileAtAllIsBornPromoted` the moment every save
    started leaving a `.bak` beside it. **A recovery rule has to name the exact failure it
    recovers from**, or it starts answering questions nobody asked it.
    → **The general shape: every `File.WriteAllText` to a file the player cannot recreate is
    this bug waiting for a kill.** Grep for it before adding another. Caches are fine (they
    refetch); a profile is not.

### Trap 66

66. **A FORGIVENESS RULE WRITTEN AGAINST ONE POSITION IN A NAME IS A RULE ABOUT THE FACT, NOT
    ABOUT THE POSITION — and the half it does not cover is a false match nobody can see until
    a player reports it.** `NameMatchesFuzzy` forgives wiki typos, and #181/Sol A taught it
    that two names sharing every word but the LAST are siblings rather than typos ("CWG Model
    XA" beside "CWG Model EXG"). The identical fact at the FRONT of a name went unwritten, and
    Guk's froglok tribes are three-letter first words: "a dar ghoul wizard" is two edits from
    "a kor ghoul wizard", comfortably inside the budget a sixteen-character name earns. The
    arch magi's placeholder was spelled with one of them, so **every tribe within reach lit the
    arch magi respawn chip** — #394 (bjordan2010), *"any wizard kill in Lower Guk hall"*.
    → **The rule is one word apart, at ANY position, unless one word truncates the other**
    (`SpawnTimerTests`). Single-word names stay exempt, because that is what fuzzy matching is
    FOR. When you write a guard that names a position, an index, a suffix or a prefix, ask what
    it would say one position over — trap 64's "a proxy is a claim about the world that a
    condition happens to encode", here with the claim being *where* a distinguishing word sits.
    → **AND THE FALSE POSITIVE HID A FALSE NEGATIVE, which is the half worth remembering.**
    The arch magus's OWN kill lit nothing: `Fold` strips a trailing "s", so the wiki's page
    title "the ghoul arch magi" met the kill line's "arch magus" as "arch magi" against "arch
    magu" — no exact match, and the same-length tail rule then refused the fuzzy one too. The
    wrong mob started the clock and the right mob could not, and only the first half was
    reportable. **When you suppress a wrong trigger, check that the RIGHT one still works** —
    otherwise the fix ships a chip nothing on earth can light and calls it fixed. The alias is
    what closes it; a plural-folding name pair is worth a test whenever a wiki title and a kill
    line disagree about the last letter.
    → **The data half is trap 30's shape: a curated field whose FORMAT nothing checks.**
    `Placeholder` is a '/'-separated list of whole mob names, and "jin/kor ghoul wizard" is
    prefix shorthand — it split into "jin" (matches nothing, ever) and "kor ghoul wizard"
    (matched half the hall). Two more shipped entries carried the same shape, one of them
    holding prose. `EveryShippedPlaceholderSegmentIsAWholeMobName` now fails it.

### Trap 67

67. **A DEFAULT THAT MEANS "EVERYTHING" IS ONLY SAFE IF THE CLIENT ALWAYS NARROWS — and
    the narrowing lived in a branch a FIRST-RUN client could never take.** EQBuddy Mobile's
    server treats a device with no subscription as wanting every surface, which is correct
    and necessary: the page cannot know what the PC offers until the first snapshot arrives.
    The page is then supposed to say what it actually wants. Its only two `sendSubscribe()`
    calls were `if (choice) sendSubscribe()` on open — and `choice` is built FROM the first
    snapshot, so it is null on a device that has never paired — and the ⚙ picker. **A phone
    that has just scanned the QR is neither**, so it stayed on "everything" for the life of
    the connection: 724 KB on connect and 186 KB per push against 2.2 KB for the two panels
    it actually draws, arriving as fast as `PumpCompanion` moves, each one a `JSON.parse` on
    the phone's only thread. The page wedges, and the ⚙ that would have narrowed it is behind
    the page that will not respond (the owner, 2026-09-07: *"scanned QR, got a page that hung
    forever"*).
    → **Every layer was innocent and every layer looked innocent.** The QR decoded, the
    address ranking picked the right NIC, `GET /` answered 200 in 6 ms, the upgrade answered
    101, the protocol numbers matched, and nothing was logged on either side. Six honest
    hypotheses died before the first measurement, and **the measurement — one frame's byte
    count — settled it in a single line**. Trap 33/49's "ship the instrument before the third
    theory" again: a raw RFC6455 client against the owner's own running app, twenty lines.
    → **The assumption was written down and had never been true.** `CompanionQuestsTests`
    says in its own comment *"the connect push (unsubscribed = everything) spends the catalog
    before the page narrows"*. It narrowed for a RETURNING device and for no other kind, and
    that sentence is what made it invisible. Trap 20's shape — the thing to look for is the
    call that is NOT there — with the missing call sitting behind a truthy check that reads
    like a guard and is a branch.
    → **The general move: when a server has a permissive default for "the client has not
    spoken yet", find the code that speaks and ask what state it needs in order to run.** If
    that state is produced by the very message the default was serving, the default is
    permanent for the client that needed it most. Guarded by `CompanionFirstPairingTests`
    (3 of 5 fail pre-fix), and behaviourally by `scripts/mobile-harness.ps1` +
    `window.__SENT` — **on a CLEAN browser profile**, because a second run inherits
    `localStorage`, takes the returning-device path, and makes the broken page look correct.

### Trap 68

68. **A GUARD WRITTEN AS "FILL THE GAP" STEPS ASIDE FOR EXACTLY THE VALUE IT WAS BUILT TO
    OVERRIDE — and a set environment variable is not a decision, it is an inheritance.**
    `TestProfileIsolation` is the module initializer that keeps ~3,770 tests off a player's
    profile, and its whole rule was `if (EQBUDDY_APPDATA is set) return;` with a comment
    saying an already-set value "wins — this only fills the gap". True of a developer typing
    it; false of a shell, and a shell is what runs the suite. `scripts/Launch-Evolved-Shell.
    cmd` and `install-local.ps1 -Evolved` export `EQBUDDY_APPDATA=%AppData%\EQBuddy
    Evolved`, so any seat that had ever launched Evolved carried it into `dotnet test`, the
    guard deferred **because** the value was set, and the suite wrote to the live profile.
    2026-09-07: David's Evolved `settings.json` went from ~390 KB to ~4.8 KB — a defaults
    file, which is trap 65's own signature and the same 4,697-byte shape recorded there.
    → **The guard was strongest when nothing was at stake and absent when something was.**
    Nothing routine sees it: every test passes either way (they were all writing somewhere
    writable), the initializer's own comment reads as deliberate, and the damage lands in a
    file nobody diffs. Trap 20's shape with the missing thing being a REASON — "already set"
    stood in for "somebody chose this", and that sentence was written nowhere because it was
    never true.
    → **Now guarded:** the redirect is unconditional, the single door is
    `EQBUDDY_ALLOW_LIVE_APPDATA=1` (exact string — "0", "true" and a stray space all still
    redirect, because the two mistakes are not symmetric), the displaced value is recorded
    in `EQBUDDY_APPDATA_DISPLACED` so an override that was ignored says so, and
    `TestProfileIsolationTests` asserts the ENVIRONMENT rather than the source (trap 42:
    "the redirect is in the file" and "the redirect is in force" are different claims).
    Prove-failed by running the suite with the opt-out set against a seeded decoy profile:
    the decoy's 27-byte sentinel comes back as a 4,889-byte defaults file, and one isolation
    test goes red naming the variable that let it.
    → **The general move: when a guard's condition is "unless somebody already X", ask who
    else can X, and whether they meant it.** An environment variable, a settings key, a
    marker file and a command-line flag are all inherited by something; only the last is
    typed. If the answer is "a script three layers up", the condition is measuring history,
    not intent — name the intent (an opt-out that exists for no other purpose) and make
    everything else redirect.

### Trap 69

69. **A HARNESS THAT SETS `EQBUDDY_APPDATA` AND THEN APPLIES THE CALLER DICTIONARY HAS A
    HOLE THE SIZE OF ONE KEY.** `TestProfileIsolation` isolates the HOST. E2E and
    `shoot.ps1` write on the CHILD. `AppHarness.Launch` set the isolated profile, then
    `foreach (_environment) psi.Environment[name] = value` — so a scenario (or a helper
    that copied the parent env) could point the real exe at `%AppData%\EQBuddy Evolved`
    or hand it `EQBUDDY_V1_APPDATA` aimed at a real v1 tree. Every host isolation test
    still passed, because they never look at the child. The same inheritance bit
    `shoot.ps1`'s relaunch: `Start-Process $path` cannot strip env, so an Evolved export
    on the seat redirected the restored widget.
    → **Now guarded (EQBuddy lab experiment, E′ 2026-09-08):** `UI.Shared/IsolatedLaunchPolicy.PinChildProfile` is the LAST write
    of the profile key and refuses both live lines as a v1 import source. No opt-in —
    an automated seat that "needs" a player profile is the accident. `shoot.ps1` relaunch
    uses `UseShellExecute=false` and `Clear-EqHarnessProfileOverrides`.
    `IsolatedLaunchPolicyTests` / `IsolatedLaunchScriptTests` prove-fail the rule and the
    script rendezvous. Product launches (`install-local.ps1 -Evolved`,
    `Launch-Evolved-Shell.cmd`) do not call this; they are how the owner runs Evolved.
    This is verification in the EQBuddy lab, not a Corps-wide standard; the formal
    proposal lives on the control-plane.
    → **The general move: when two processes share one job, say which process the guard
    is about.** A host redirect does not cover a child `ProcessStartInfo`. Apply the
    isolated value AFTER the caller dictionary, or the dictionary is the hole.

### Trap 70

70. **SOFT MAX ≤3 IS A COUNT, NOT A MUTEX.** Helm SSC + `Get-Process claude`
    refuses nothing — a fourth `claude.exe` starts, and a second default seat
    on the same issue starts. `scheduled_tasks.lock` and
    `%TEMP%\eqbuddy-screen.lock` are other mutexes; they do not claim a work
    item. (E′ took trap 69 on this same day for the child-profile hole.)
    → **Experiment A′ on EQBuddy (the lab), not a Corps standard.** Claim
    before kick: `scripts/claim-seat.ps1` refuses a second default claim on
    the same work item. `-Mode challenger|disjoint|replacement` is the
    explicit override; `scripts/release-seat.ps1 -ForceStale` recovers a dead
    holder. The store is local (gitignored `.claude/soft-seats/`) so it
    cannot become a mailbox rebase war. Soft/Dranak:
    `run-seat-PROMPT-only.cmd` runs the claim first and never writes
    `HELM-FEEDBACK.md`. How-to: `.claude/soft-seats/README.md`.
    → **Evidence before graduation:** duplicate starts prevented, stale
    claims, false blocks, recovery. Formal proposal lives in the
    control-plane `proposals/` tree, not here.

### Trap 71

71. **A FOLD THAT IS RIGHT FOR IDENTITY IS NOT AUTOMATICALLY RIGHT FOR
    QUANTITY.** `SpellCatalog.BaseName` strips a rank so "Shield of Thorns V"
    and "Shield of Thorns" are the same spell — correct, and load-bearing:
    only one of them can be up on you, one fade line ends either, one active
    entry holds both. `BuffTracker` then reached through that same fold for
    the spell's DURATION, which is the one property the rank decides. eqlwiki
    carries a single duration per spell page (the unranked number), so a level
    50 Druid casting rank V got rank I's 15 minutes for a 23:36 shield.
    → **The cost was not the countdown; it was every alert armed off it.**
    `BuffState.ExpiresAt` is a single producer read by the HUD's expiring
    chicklet, the Buffs card's warn tint and expiring-only mode, so all three
    fired **7:54** early on Shield of Thorns V and **4:12** early on
    Chloroplast V. Nothing looked wrong from the inside: valid JSON, resolved
    landing, SCR applied, a chip drawn with a perfectly ordinary countdown on
    it. The owner found it with a stopwatch.
    → **`ExpiryLinger` hid the other end of it.** Past its expiry a chip holds
    at 0:00 for five minutes rather than vanishing, because an estimate is a
    floor. So a test that asserted only PRESENCE would have passed on the
    broken code at both ends of the window — the chip is there either way, and
    it is the FACE ("0:19 est" versus a bottomed-out "0:00 est") that tells
    the truth apart from the lie. **When a surface degrades gracefully, assert
    what it SAYS, not that it is there.**
    → **The fix keeps the fold and adds a second key.**
    `RankedBuffDurations.json` is keyed on the EXACT ranked name; identity
    still folds. Learned durations moved to the ranked key for the same
    reason, with the folded key as a read fallback so no player loses a
    measurement their own log paid for.
    → **A measured ledger is not an invented one, and the difference has to be
    checkable.** Helm's posture from #414 is *do not invent ranked durations /
    mote multipliers*, and the two measured rows sit at +50% and +25% over
    their wiki bases — no formula fits both, which is the evidence rather than
    an excuse. Every row stores the observation it came from, and
    `RankedBuffDurationTests` re-derives that observation from the stored
    base, so a row somebody typed from a hunch has nothing to land on. Guards:
    `BuffTrackerTests`, `HudChipRowTests.AThornsChickletWaitsForTheDurationTheOwnerMeasured`,
    `RankedBuffDurationTests`. Prove-failed against the folded base: 8 red,
    the HUD one reporting a chicklet already up at "0:48 est".

### Trap 74

**A "byte-identical" gate over a file with a CONTAINER asserts which toolchain
built the container, not that the data is unchanged.**

DRA-45 ships `HarvestedGuides.json.gz` — over a thousand machine-written guides
and eleven thousand objectives that nobody will ever read as a diff. The whole
review of a weekly refresh PR is therefore the re-run: run the transformer again
and the committed file must not move. So `guides-transform.py --check` compared
`OUT.read_bytes()` against what it would write, `build-and-test` ran that step,
and it was green on the box that built it.

CI failed it in 34 seconds, on a file whose contents were **identical** — run
`34615319696`, `STALE: src\EQBuddy.Core\Data\HarvestedGuides.json.gz`, against
`C:\hostedtoolcache\windows\Python\3.12.10`. gzip is not reproducible across
environments: the runner's 3.12 zlib and a 3.14 developer box compress the same
input to different bytes. `mtime=0` and an empty filename field close the two
places a gzip writer leaks the clock and the working directory, and they are not
enough — the deflate stream itself is a property of the zlib build.

→ **Ask what the claim is ABOUT.** "The transformer reproduces the data" is a
claim about the catalog; which zlib shipped it is not part of it. `--check` now
decompresses the committed file and compares the payload.

→ **Gate the WRITE on the same comparison, not just the check.** A writer that
always rewrites puts a fresh deflate stream in every weekly refresh PR — a
350 KB binary diff that says nothing and that a reviewer cannot tell from a real
one. The write now happens only when the decompressed data differs.

→ **Pinning the toolchain is not the fix.** The first response was to pin the
runner to Python 3.12 and pin `*.gz binary`, reasoning that "a byte-for-byte
assertion whose toolchain floats is a gate that can start failing for a reason
nobody changed". Both are good hygiene and neither addresses it: the generating
box still floats (this one is 3.14), and the pin only moves the tripwire onto
whoever next bumps the runner. The version pin survives on its own merits; it is
no longer load-bearing.

→ **The failure mode is the bad one, which is why this is a trap and not a bug.**
A gate that goes red on a toolchain version does not read as "this gate is
wrong"; it reads as "re-run it and commit the result". Do that twice and the gate
is something people route around, which is worse than never having built it — a
guard nobody believes is trap 34's hole wearing a green check.

→ **Generalises past gzip.** Any archive (zip, tar.gz), any format with a
timestamp or a producer string in its header (PNG, PDF), any database file. The
tell is that the artifact has a CONTAINER and the claim is about its CONTENTS.

**The report is part of the artifact, and it went stale unnoticed.** The same
land ships `guides-report.md`, the human-readable half of a diff nobody can read,
guarded by `TheReportIsThereAndItsCountsMatchTheCommittedFile`. That guard checked
the guide total and the objective total. When the Collect rows became `Stub`
rather than `Authored` (Helm ~10:40 AM CT, ask 2), the data was regenerated and
the report was not, and it shipped on `main` claiming `Authored: 5244 / Stub: 27`
beside a catalog holding `1196 / 4075`. **Both totals it checked were unmoved,
because a row changing bucket changes no total — a sum is exactly the wrong
instrument for a redistribution.** The guard now asserts every authoring-state
and objective-type bucket against the committed catalog.

Guards: `guides-transform.py --check` (data, not container), the `build-and-test`
step that runs it, `scripts/check.ps1`, and
`HarvestedGuidesTests.TheReportIsThereAndItsCountsMatchTheCommittedFile`.
Prove-failed in both directions that matter: one character changed inside the
compressed data exits 1 (`4233441 bytes of data on disk, 4233440 generated`),
while the identical data recompressed at a different deflate level — 356,041
bytes against the committed 356,535 — now passes, which is the CI failure above
reproduced on demand. The report assertion was prove-failed against `main`'s
committed report, which fails with *Sub-string not found* for the Stub bucket's
true count of 4075.

### Trap 75

**When a transport reports three different causes with one code, a client that
guesses which one it is has written silence for the other two — and a retry loop
built on that guess can spend the server's own abuse budget on itself.**

DRA-60. A founder smoke test scanned EQBuddy Mobile's pairing QR. The phone
navigated to `http://10.0.0.84:47859/#<code>`, and hung. Everything the report
could reach was fine: the bind was real, the PC answered that GET 200 with the
whole ~155 KB page, and the same URL in the PC's own browser connected and drew.
The hang was the PAGE's, after navigation — which is the half nobody can see.

`index.html` puts a fragment code straight into `#app` and calls `connect()`.
`ws.onclose` then fires with code 1006, and **1006 is what a browser reports for
a refused upgrade, a rate-limited one, and a PC that is switched off alike** —
the WebSocket never existed, so there is no status for the page to read. The page
guessed, and it guessed for exactly one of the three: a code produced from
`localStorage` counted three refusals and then showed the pairing screen. A code
that arrived in the FRAGMENT — which is every QR scan there has ever been —
matched no branch, and was re-dialled forever.

→ **The silence was actively produced, once a second.** `refreshStale()` runs on
a 1 s interval as well as on each heartbeat. Its only never-connected clause was
`everConnected && !lastMsgAt`, false before the first open, so every tick fell
through to the `else` and REMOVED the banner. The page was not stuck; it was
deciding, once a second, to say nothing. Anything the close handler wrote into
the banner would have been erased within the second — **the news had to live in
the one producer**, which is the same shape as trap 4.

→ **And the loop ate the remedy.** `CompanionServer` rate-limits an IP at five
auth failures inside sixty seconds, and the page's 1→2→4→8 s backoff spends all
five in about fifteen. From then on the device is 429'd for most of every minute
— so the phone had locked itself out of the CORRECT code the player was on their
way to rescan, and the rescan looked broken too. **A silent retry against an
endpoint with an abuse guard is a client DoSing its owner on the owner's
behalf.** The fix STOPS on a refusal; stopping costs two failures, not five.

→ **The instrument was already there, unasked.** The same origin answers the
plain GET the socket would have made: 403 (this code is wrong), 429 (this device
burned the budget), 400 (the code is RIGHT and an upgrade was the only thing
missing, so the fault is the network). One request separates the three causes
the transport had flattened into one. **Before writing a sentence that names a
cause, check whether something on the wire can tell you which cause it is** —
otherwise the page has trap 35's shape: an affordance with the right form and
invented content, and "your pairing code is wrong" told to somebody whose PC is
merely asleep is worse than the blank page, because they will go and regenerate
a code that was fine.

→ **The other way to look hung, found by pulling the same thread.** A socket
that OPENS and a snapshot that ARRIVES can still paint nothing: `wanted` is
`picked() ∩ (offered ∪ notOffered)`, and a PC whose gate does not overlap this
device's picks leaves it empty. `#screens` is `display:none` until the ⚙, so what
remains is a header carrying the character's name over a blank page —
indistinguishable, to the player, from the hang above. A surface that can
legitimately have nothing in it needs a sentence for that state, and `render()`
now has one. (Trap 20's family: the state nobody wrote a branch for.)

Guards: `CompanionPairingFailureTests` — the close handler may consult neither
the code's provenance nor a refusal count (the shipped condition committed as the
negative), a 403 must stop the loop, the never-connected branch must live inside
`refreshStale()` and before the clause that hides the banner, and the empty-picks
sentence must exist. The three wire statuses and the lockout arithmetic are
asserted against a real `CompanionServer` on a loopback socket rather than read
out of the source, because the page's message is only honest for as long as they
hold. Prove-failed against the shipped page: five of the six page assertions are
red before the fix, and the two server facts were green throughout — which is
the point, since the server was never the bug.

---

### Trap 76

**A repair gated on "this is the first time" cannot reach a device the BROKEN
build already wrote state onto.** DRA-64, the follow-up to [trap 75](#trap-75),
and the second Founder smoke test on the same phone.

#550 shipped and the Desktop was republished. The Founder rescanned the QR. The
phone was still blank. And the thing that made it look like a network fault for a
second time: **the same URL, pasted into the PC's own browser, worked.** Same
build, same token, same server, same `:47859`. One of them painted and one of
them did not.

→ **It was a CSS breakpoint.** `FIRST_RUN` — the screens a device opens with
before anyone touches the ⚙ — is chosen off `innerWidth >= 900`:
`map, spawns, mez, session, quests` on a tablet-or-wider, `spawns, session` on a
phone. This PC's `CompanionHiddenSurfaces` left `offered` as `quests, gear`. The
wide list overlaps it on `quests`, so a PC browser paints; the narrow list
overlaps nothing, so a phone does not. Two surfaces of one build disagreeing
across a breakpoint is indistinguishable from wrong-Wi-Fi, and both times the
cheap diagnosis was the network.

→ **And the fix for exactly that could not fire.** #550's rescue read
`if (firstPairing && offered.length && !offered.some(…))`, with
`firstPairing = !choice` — "this device has no stored choice". But the phone had
already paired against the BROKEN build, and the first snapshot of that session
persisted the all-off choice:

```
eqbuddy-screens-<token8> = {"order":["quests","gear"],
                            "enabled":{"quests":false,"gear":false}}
```

written by `if (offerChanged) { renderScreens(); saveChoice(); }`, which fires on
every first snapshot because `offered` starts `[]`. So `choice` was non-null,
`firstPairing` was false, and **the one device that needed the rescue was the
only device it could not fire for.** Rescanning did nothing, because the saved
picks are keyed by the token and the token had not changed. Nothing on the PC
could reach it; no amount of republishing could either.

**Before shipping a repair, ask what the BROKEN build persisted, and whether the
new condition is still true on a device that ran it.** A fix verified only
against a clean profile is verified against the one state the bug report is not
in. (`CompanionFirstPairingTests` made exactly the right point about clean
browser profiles for trap 67 — and a clean profile is the wrong fixture for this
one. Both are true: the honest fixture is the state the reporter is actually in.)

→ **`!choice` was a proxy, and the fact was available.** Trap 64b's shape: a
proxy is a claim about the world. The claim wanted was *"nobody has chosen yet"*,
and the page can know it — `commitChoice()` is the ONE door a human's screen pick
comes through (both the checkboxes and the ▲▼ reorder), so it stamps
`choice.playerPicked`. The gate reads that. An all-off choice a player MADE
survives; one that only the defaults produced is repaired once. Absent on every
choice written before DRA-64, which reads as "never touched" — correct, because
the only other writers were the offer-changed save and the fullscreen flag, and
neither is a screen pick (trap 4: one fact, one producer).

→ **And it says so.** Turning a device's screens back on behind its owner is a
settings change, so the page tells them once, naming ⚙ as the way back — but only
when it overrode picks the device ARRIVED holding (`hadStoredChoice`). On a first
pairing the same branch is the default, and announcing a default is noise.

→ **What was NOT added, and why.** The empty-state sentence in `render()` looked
like it needed a third branch for "this device picked screens the PC is not
sharing" — but `CompanionSnapshot.ForSubscription` puts every subscribed-but-gated
name into `NotOffered`, and the page draws those as "Not shared by the PC". So
`wanted` is empty only when `picked()` is empty, and the third sentence would
have guarded a state the server makes impossible. A branch for an unreachable
state is vacuous coverage wearing a fix's clothes.

Guards: `CompanionScreenChoiceRecoveryTests` — the gate may not consult whether a
choice was STORED (both shipped forms committed as negatives), the repair must be
reported to its caller and persisted, `playerPicked` must have exactly one writer
and it must not be either of the two saves that are not a pick, the override must
be announced and a default must not be, and the two `FIRST_RUN` lists are
asserted against a `quests`/`gear` PC so the explanation above stops being true
loudly rather than quietly. Prove-failed: five of the six redden against
`cc020280`'s page; the sixth is the premise anchor and holds on both by design.

**And the harness's own blind spot is the same lesson one layer up.** #552 had
just built the headless door (`-Refuse`, `#harnessState`, `--dump-dom`) and used
it to verify #550's other blank page — "a PC offering quests/gear against a
phone's FIRST_RUN of spawns/session, the Founder's actual configuration, paints
both offered panels". That run was honest and it was green, and it could not have
found this, for two reasons worth writing down: every harness run started with
**empty localStorage**, and `-Snapshot` **rewrote `FIRST_RUN` to the snapshot's
own offer**, which makes the overlap succeed by construction. A fixture that
cannot hold the reporter's state cannot reproduce the reporter's bug, and a
fixture that removes the mechanism verifies the mechanism's absence.

So `-StoredChoice` seeds a device that has already paired, and suppresses that
rewrite. Driven through it, reading `#harnessState`, 2026-09-11:

| run | `panels` | `noScreens` | `stored` after |
|---|---|---|---|
| **BEFORE** (`cc020280` — the page he rescanned) | `[]` | **true**, "No screens picked on this device. Tap ⚙…" | unchanged, all-false |
| **AFTER** | `["Quests","Gear checklist"]` | false | **all-true — the repair persisted** |
| **AFTER**, `playerPicked:true` seeded | `[]` | true | unchanged — a real decision survives |

The BEFORE row also settles what the Founder was actually looking at by the
second attempt: not a hang, but that one sentence and no data, permanently.

Second, cheaper instrument: `node scripts/dra64-choice-probe.mjs [olderPage.html]`
lifts the shipped `ensureChoice()` out of `index.html` and runs it over a fake
`localStorage` across six scenarios — the working PC paste, a clean first
pairing, the Founder's poisoned phone, the same hole reached from a patched
build, a player's deliberate all-off, and a reopen. It takes an older page as an
argument, so the prove-fail is one command. Neither is a CI step: Helm ACKed
leaving node out of CI on #550.

→ **A footnote on the instrument, because it made the mistake it measures.** The
first cut of the harness verdict reported "was the player ever told" by reading
`classList.contains("show")` at readout time — but the notice is a 5 s toast and
the readout is later, so it reported `false` for a sentence the page had
demonstrably shown. The second cut latched it on a `MutationObserver`, registered
on `DOMContentLoaded` — which under `--virtual-time-budget` can fire AFTER the
snapshot push, so it reported `false` again. It now registers immediately and
reports the latch **and** `textContent` (which survives the hide) side by side:
two readings of one fact, because a silent miss in an instrument reads as a
finding about the product.

### Trap 77

**The cheapest reachability test is the one that cannot fail for the reason you
are investigating — and the PC agreed with it out loud.**

*Sibling of [trap 76](#trap-76), same Founder smoke. Trap 76 is the page-side
cause (a poisoned screen choice the rescue could not reach); this is the PC-side
one (nothing can reach the listener at all). They are sequential gates, and the
reason one incident produced two is that nobody could measure which layer was
failing — which is the thing this half fixes.*

DRA-64, Founder smoke 2026-09-11. DRA-60 had just landed two rounds of work on
the phone page (#550's diagnose-connect, #552 running all four pairing-failure
paths), and the phone still would not load. The report carried what everyone
treated as the decisive fact: *"PC browser paste of full companion URL works;
phone still not loading (Kaybek + full 32-char)."* Read that way, the server is
healthy, the token is good, the page is good — so the bug must be on the phone,
and the dig goes to browser cache, cellular-vs-Wi-Fi, `CompanionHiddenSurfaces`,
`firstPairing`, QR-vs-paste.

Every one of those is a fine hypothesis. None of them is what happened, and the
fact that sent the dig there is not evidence of anything.

`CompanionServer` binds LAN addresses only — never loopback, unless the machine
has no LAN at all. So "paste the URL into the PC's browser" means the PC
connecting to the PC's **own LAN address**. Windows routes traffic from a
machine to an address that machine holds internally; it does not go out the NIC
and it is not evaluated against the inbound firewall filter. That connection
succeeds whether the firewall permits EQBuddy or forbids it, whether the router
isolates clients or not, whether the phone is on the same SSID or in another
country. **The test passes identically in the world where the bug exists and the
world where it does not.**

Then the PC confirmed the misreading. `ClientCount` counted that browser and
`CompanionPairingText.Status` rendered "1 device connected" — a sentence that is
true of a browser and that a person reads as "a device paired". The smoke ended
with the PC reporting success while nothing had ever reached the machine.

**What was actually wrong**, measured on the Founder's box:

- Running exe: `C:\Users\david\AppData\Local\EQBuddy Evolved\publish\EQBuddy.exe`.
- Inbound allow rules naming an EQBuddy: `...\Local\Programs\EQBuddy\eqbuddy.exe`
  (v1) and `...\source\eqbuddy\dist\publish\eqbuddy.exe` (a dev publish). **None
  names the running file.**
- Firewall enabled on all three profiles, `DefaultInboundAction=NotConfigured`
  (= Block). The Wi-Fi NIC is on the **Private** profile.
- The only rule that could otherwise have covered it is `Tailscale-In`
  (Program=Any) — scoped `LocalAddress=100.118.30.124`, the tailnet address alone.
- The listener holds `10.0.0.84:47859` (Wi-Fi) and `100.118.30.124:47859`
  (Tailscale). `LanAddressRank` scores Wi-Fi −5 and Tailscale +85, so the QR
  hands the phone **the one address with no allow rule**, and the only reachable
  one is ranked last and never offered.

A phone on the house Wi-Fi, with the right address and the right token, SYNs into
a drop. v1 Mobile worked for years because v1's exe path had a rule; v2 moved
install directories and inherited none. Nothing on either side can see it: the PC
never gets an accept, the phone gets a TCP timeout.

**The second half is the one that would have cost another evening.** The pairing
window's advice told the player to check *"Windows Security → Firewall → Allow an
app"*. That list is keyed on the executable's **path** and displays only its
**name**. A player who follows that instruction finds `eqbuddy.exe` sitting there
already ticked — the v1 rule — concludes the firewall is fine, and goes looking
somewhere else. The advice does not merely fail to find the bug; it actively
produces a false negative for it. It also told the player the "best check" was to
open the address on the PC, which is the test above.

**The fix is not a firewall rule.** EQBuddy still makes no netsh or elevation
calls — the spike's rule stands. It is that the PC stopped guessing and started
measuring, and stopped hiding the identity the player needs:

- `CompanionServer.IsSameMachine(remote, local, machineAddresses)` — pure, three
  tests. Loopback; source equalling destination; and membership of the machine's
  full address set, which is not belt-and-braces: a PC on both Wi-Fi and Tailscale
  reaches `10.0.0.84` from `100.118.30.124`, and the first two tests call that a
  phone.
- `OffBoxConnects` / `SameMachineConnects`, counted at **accept** — before the
  connection gate, before the parse, before auth. A phone refused with a stale
  token is a phone that got here, and that is the fact separating "blocked" from
  "refused" (the latter being exactly what DRA-60 taught the page to say).
- `CompanionReachability` turns the two counters and a clock into a verdict. The
  clock starts when the WINDOW opens, not when the app did, so a PC that has been
  up all evening is not reported as having failed to pair all evening.
  `OnlyThisPc` is deliberately decided BEFORE the patience gate: a player holding
  a result they are about to misread deserves the correction immediately.
- The connected line names this PC's browser as a browser, at every count.
- The escalation prints `Environment.ProcessPath` and ships the whole
  `New-NetFirewallRule` command (CLAUDE.md's "a surface that needs a command must
  SHIP the command"), scoped to the bound port and the Private profile — a
  diagnostic aid has no business opening the Public one, and a player whose home
  network is miscategorised is told to fix the category instead.

**Prove-failed both ways.** Making `IsSameMachine` return false — the pre-DRA-64
behaviour, every connection is a device — turns 7 red, including all three
real-socket tests. Restoring the old one-argument `Status` line turns 2 red. The
real-socket half matters because one box cannot produce a connection from a second
machine: what IS provable here is the half that actually went wrong, a browser on
this PC doing exactly what the Founder did.

**The generalisation, which is the reason this is a trap and not a bug report:**
before spending a measurement, ask whether it can distinguish the hypothesis from
its negation. "It works from here" almost never can. And when a surface counts
participants, make ORIGIN part of the count — a local caller is not evidence about
a remote one, and a count that conflates them will confirm whatever the reader
already believes.
