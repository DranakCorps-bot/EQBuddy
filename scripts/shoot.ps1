# Screenshot fixture: a real EQBuddy.exe, a seeded session, an OPAQUE render.
#
# Two things made a capture unusable before this existed (2026-08-17):
#
#   1. An isolated EQBUDDY_APPDATA profile has no session, so every card renders
#      "0 dps / 0 kills / 0 items". Fixed by seeding the profile's log folder with the
#      time-shifted fixture (scripts/make-test-session.ps1) — the same recipe
#      tests/EQBuddy.E2E/FixtureLog.cs uses — so the app replays a rich session at
#      startup and the shot shows real numbers that are not a real person's.
#   2. Every window is translucent by design (it sits over a running game), so whatever
#      was behind it bled into the PNG. Fixed on two fronts: EQBUDDY_OPAQUE=1 makes the
#      window GROUND opaque (UI.Shared/CaptureTheme.cs), and a plain full-screen backdrop
#      sits behind everything so the rounded corners land on one flat colour instead of
#      the desktop.
#
# Nothing here touches the real profile: EQBUDDY_APPDATA points at a temp tree that is
# deleted afterwards unless -KeepProfile.
#
#   pwsh -NoProfile -File scripts/shoot.ps1                        # every shot
#   pwsh -NoProfile -File scripts/shoot.ps1 -Shot quest-tracker    # just one
#   pwsh -NoProfile -File scripts/shoot.ps1 -List                  # what it can shoot
#
# PREREQUISITE: dotnet build EQBuddy.slnx -c Release. This launches the BUILD output,
# not dist/publish, exactly like the E2E suite.
[CmdletBinding()]
param(
    # Which shots to take; omit for all of them. Names are the keys in $Shots below.
    [string[]]$Shot = @(),
    [string]$Out = '',
    # Behind every window, so a transparent corner lands on one flat colour. Neutral and
    # deliberately not a palette colour, so "outside the window" reads as outside.
    [string]$Backdrop = '#202225',
    [string]$Theme = 'ParchmentBrass',
    # Seconds to let the startup replay land after the window appears. There is no
    # readiness signal without EQBUDDY_EXPAND (which changes what the widget looks like,
    # so it cannot be forced on every shot) — this is a settle, not a handshake.
    [int]$Settle = 8,
    [switch]$KeepProfile,
    [switch]$List
)
$ErrorActionPreference = 'Stop'

$repo = Split-Path $PSScriptRoot -Parent
if ($Out -eq '') { $Out = Join-Path $repo 'docs/screenshots' }

# --- what we can shoot -------------------------------------------------------------
# Title  = the window to capture, matched as a substring (scripts/shot.ps1).
# Env    = the EQBUDDY_* hook that opens it (the same family MainWindow already reads).
# Set    = extra settings.json overrides for this shot.
$Shots = [ordered]@{
    'widget-cards'    = @{ Title = 'EQBuddy'; Env = @{}; Set = @{} }
    'widget-expanded' = @{ Title = 'EQBuddy'; Env = @{ EQBUDDY_EXPAND = '1' }; Set = @{} }
    # The title area with a LONG zone name. The zone and the session line shared one grid
    # cell with no columns, so a long name overprinted the session text rather than
    # trimming — "The Plane of Fear 4 (Refine[sed]sion 0:11 · active 11m". Found in a
    # screenshot attached to #219, which was about something else; nobody reported it.
    # The fixture's own zones ("West Commonlands") are far too short to show it, which is
    # exactly why it survived every capture this repo has taken.
    'long-zone'       = @{ Title = 'EQBuddy'
                           Env = @{}
                           Append = @('You have entered The Plane of Fear 4 (Refined).')
                           Set = @{} }
    # One card, opened by name: a card's expanded state is not persisted, so a body can
    # only be photographed through this hook. EQBUDDY_EXPAND takes a comma-separated list
    # of the same keys SectionMap uses.
    'loot-card'       = @{ Title = 'EQBuddy'; Env = @{ EQBUDDY_EXPAND = 'loot' }; Set = @{} }
    'kills-card'      = @{ Title = 'EQBuddy'; Env = @{ EQBUDDY_EXPAND = 'kills' }; Set = @{} }
    # The two remaining heavy card bodies (Gate 5b). Shot before and after their lift so
    # a refactor that changes what a player SEES shows up as a diff in the picture —
    # behaviour-preserving is a claim, and these are how it gets checked.
    'combat-card'     = @{ Title = 'EQBuddy'; Env = @{ EQBUDDY_EXPAND = 'combat' }; Set = @{} }
    'healing-card'    = @{ Title = 'EQBuddy'; Env = @{ EQBUDDY_EXPAND = 'healing' }; Set = @{} }
    # The PROGRESS THEME's four tabs (docs/Themes.md). These replaced 'value-cards'
    # (motes,money,faction) and the widget half of 'raids-card': those five cards are one
    # launcher now, and their bodies live in the Progress window, which only EQBUDDY_PROGRESS
    # can open. A surface with no way to be photographed reads as reviewed anyway (trap 22).
    'progress-wealth' = @{ Title = 'EQBuddy Progress'; Env = @{ EQBUDDY_PROGRESS = 'wealth' }; Set = @{} }
    'progress-faction' = @{ Title = 'EQBuddy Progress'; Env = @{ EQBUDDY_PROGRESS = 'faction' }; Set = @{} }
    # The breakout needs no hook of its own: it shows whenever the widget is minimized and
    # its stat is starred, and both are plain settings. Session scope is the one with the
    # filter strips on it (Target is a different axis and hides them).
    # #182 (Ladylag): the damage-by-ability rows, in the narrow window she had. This is
    # the shot whose rows read ".", ".." and nothing at all.
    'damage-breakout' = @{ Title = 'Damage breakout'
                           Env = @{}
                           Set = @{ Minimized = $true; MiniStats = @('dps'); BreakoutDamageScope = 'session' } }
    # The Progress breakout (#214, Liminal Warmth). Same staging as the others: starred
    # while minimized is what opens it. Its folds default open so one shot shows the
    # ding list, the skill-ups and the session AAs rather than three closed headings.
    'progress-breakout' = @{ Title = 'Progress breakout'
                           Env = @{}
                           Set = @{ Minimized = $true; MiniStats = @('xp')
                                    ShowAllAAs = $true; ShowNextUnlocks = $true } }
    'loot-breakout'   = @{ Title = 'Loot breakout'
                           Env = @{}
                           Set = @{ Minimized = $true; MiniStats = @('loot'); BreakoutLootScope = 'session' } }
    'quest-tracker'   = @{ Title = 'Quest Tracker'; Env = @{ EQBUDDY_QUESTS = '1' }; Set = @{} }
    'quest-tracker-all' = @{ Title = 'Quest Tracker'; Env = @{ EQBUDDY_QUESTS = 'all' }; Set = @{} }
    # The Plane of Sky checklist, staged so all three reward states are on one screen:
    # one turned in (offers Reopen), one with every piece held (offers Mark turned in),
    # one part-collected (offers neither). Ticks survive the catalog merge because
    # ApplyDefaultSkyQuestChecklist matches on Id and never touches Acquired.
    'sky-checklist'   = @{ Title = 'Quest Tracker'
                           Env = @{ EQBUDDY_QUESTS = 'sky' }
                           # TWO classes, because the cross-class surfaces are the whole
                           # point of #205/#209/#210 and neither of them draws itself for
                           # one class: the Ready band would list a single row and the
                           # D/R/P summary suppresses itself below two. Shot with one
                           # class, both are invisible and the screenshot proves nothing.
                           Ledger = @{ Classes = @('Warrior', 'Cleric') }
                           Set = @{
                               # Warrior is what the fixture log infers; Cleric rides in
                               # on the ledger above.
                               SkyQuestCompleted = @('Warrior|Azure Ruby Ring')
                               SkyQuestChecklist = @(
                                   @{ Id = 'sky-194'; Acquired = $true }   # turned in
                                   @{ Id = 'sky-195'; Acquired = $true }
                                   @{ Id = 'sky-200'; Acquired = $true }   # every piece held
                                   @{ Id = 'sky-201'; Acquired = $true }
                                   @{ Id = 'sky-202'; Acquired = $true }
                                   @{ Id = 'sky-203'; Acquired = $true }   # part collected
                                   @{ Id = 'sky-050'; Acquired = $true }   # Cleric, ready
                                   @{ Id = 'sky-051'; Acquired = $true }
                                   @{ Id = 'sky-041'; Acquired = $true }   # Cleric, part
                               )
                           } }
    # The same staging, with the state lens ON — the control restored for #205/#209 acts
    # only on OTHER controls, so a shot of it switched off proves nothing.
    'sky-ready'       = @{ Title = 'Quest Tracker'
                           Env = @{ EQBUDDY_QUESTS = 'sky:ready' }
                           Ledger = @{ Classes = @('Warrior', 'Cleric') }
                           Set = @{
                               SkyQuestCompleted = @('Warrior|Azure Ruby Ring')
                               SkyQuestChecklist = @(
                                   @{ Id = 'sky-194'; Acquired = $true }
                                   @{ Id = 'sky-195'; Acquired = $true }
                                   @{ Id = 'sky-200'; Acquired = $true }
                                   @{ Id = 'sky-201'; Acquired = $true }
                                   @{ Id = 'sky-202'; Acquired = $true }
                                   @{ Id = 'sky-203'; Acquired = $true }
                                   @{ Id = 'sky-050'; Acquired = $true }
                                   @{ Id = 'sky-051'; Acquired = $true }
                                   @{ Id = 'sky-041'; Acquired = $true }
                               )
                           } }
    # The Epic tab's per-class master check (#138, restored for #210). Two classes, so
    # the band is visibly PER CLASS rather than a single header that could be anything.
    'epic-checklist'  = @{ Title = 'Quest Tracker'
                           Env = @{ EQBUDDY_QUESTS = 'epic' }
                           Ledger = @{ Classes = @('Warrior', 'Cleric') }
                           # The flag is written DIRECTLY, so Cleric reads "complete" at
                           # 0/20 — a state the app itself never produces, because the
                           # real MarkComplete ticks every row on its way in. It is here
                           # to photograph the band's two states and the locked rows; do
                           # not read the count as evidence of anything.
                           Set = @{ EpicQuestCompleted = @('Cleric') } }
    # The minimized bar with EVERY cell up — the only way to see all ten icons at once,
    # and the surface that is on screen for the whole session. Its icons were glyphs
    # until Gate 5c; a glyph that fails to render is a blank here and nowhere else.
    'mini-bar'        = @{ Title = 'EQBuddy'
                           Env = @{}
                           # Every breakout OFF: starring dps/hps/pet/loot while minimized
                           # is exactly what opens those windows, and the capture matches
                           # on title — so without this it photographs a breakout instead.
                           Set = @{ Minimized = $true
                                    DisabledBreakouts = @('Damage','Healing','Pet','Watch','Loot','Buffs')
                                    MiniStats = @('kills','dps','hps','pet','procs','loot','motes','money','xp','deaths') } }
    # The Watch card with rules that the fixture session actually matches — without them
    # the card is a one-line empty state and its sort strip does not exist at all (it
    # appears only above two or more rules). "Spider parts" is deliberately a rule with
    # three kinds under it, so the "all N kinds" fold shows too.
    # The Raids card with clears in it: one boss with a witnessed tiered kill (badge and
    # count), one marked from an achievements import (no badge — honesty over flattery),
    # and the rest still open, so the tick, the bullet and the "0/n" heading all appear on
    # one screen.
    'raids-card'      = @{ Title = 'EQBuddy Progress'
                           Env = @{ EQBUDDY_PROGRESS = 'raids' }
                           Set = @{}
                           Raids = @{
                               'testchar_test|phinigel autropos' = @{
                                   Kills = 3
                                   FirstKill = '2026-07-02T21:15:00'
                                   LastKill = '2026-08-09T22:40:00'
                                   AchievementComplete = $false
                                   TierKills = @{ d2 = 2; open = 1 }
                               }
                               'testchar_test|lord nagafen' = @{
                                   Kills = 0
                                   AchievementComplete = $true
                                   TierKills = @{}
                               }
                           } }
    # NOT called watch-card: docs/screenshots/watch-card.png is a hand-taken shot that
    # docs/WatchListGuide.md embeds, and a shot name IS its filename — this would have
    # quietly overwritten a guide's illustration with the fixture's three rules.
    'tracked-card'    = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'tracked' }
                           Set = @{ TrackedRules = @(
                                   @{ Id = 'shot-spider'; Name = 'Spider parts'
                                      Pattern = 'Spider'; Kind = 0 }
                                   @{ Id = 'shot-bone'; Name = 'Bone chips'
                                      Pattern = 'Bone Chips'; Kind = 0 }
                                   @{ Id = 'shot-kills'; Name = 'Giant spiders'
                                      Pattern = 'giant spider'; Kind = 1 }
                               ) } }
    # "Who wants this drop?" (#108, liminalwarmth) — the item-grouped search. Trap 22: this
    # layout EXISTS ONLY WHILE A QUERY IS LIVE, so with no staged search the tab shows the
    # ordinary per-class list and a shot of it proves nothing about the feature. The query
    # rides in on EQBUDDY_QUESTS after the colon.
    #
    # "Wind Rune Azia" is the case the ask was about: SEVEN classes want that one drop, so
    # before this it was seven sections to scroll between. Two are pre-ticked so the
    # "N of 7 in hand" count has something to say, and one of the seven belongs to a class
    # the fixture does not play — which is the point, since search crosses the class picker.
    'sky-item-search' = @{ Title = 'Quest Tracker'
                           Env = @{ EQBUDDY_QUESTS = 'sky:Wind Rune Azia' }
                           Ledger = @{ Classes = @('Warrior', 'Bard') }
                           Set = @{ SkyQuestChecklist = @(
                                   @{ Id = 'sky-002'; Acquired = $true }   # Bard
                                   @{ Id = 'sky-199'; Acquired = $true }   # Warrior
                               ) } }
    # The Progress card, lifted into ProgressCardView.cs (Gate 5b). Trap 22: its two most
    # interesting lists exist ONLY in response to a level-up, and the shared fixture has
    # none — so without the append below this shoots a card with an empty ding list and
    # proves nothing about the rows the lift actually moved. ShowNextUnlocks unfolds the
    # preview, which is collapsed by default and would otherwise be a label alone.
    'progress-card'   = @{ Title = 'EQBuddy Progress'
                           Env = @{ EQBUDDY_PROGRESS = '1' }
                           Append = @('You have gained a level! Welcome to level 12!')
                           Set = @{ ShowNextUnlocks = $true; ShowAllAAs = $true } }
    'spawns-window'   = @{ Title = 'Spawn'; Env = @{ EQBUDDY_SPAWNS = 'Runnyeye Citadel' }; Set = @{ TrackSpawns = $true } }
    'options-window'  = @{ Title = 'Options'; Env = @{ EQBUDDY_OPTIONS = '1' }; Set = @{} }
    # Options → Cards & windows, which is the screen a player opens when a card has gone
    # missing — #219 (typical-usual-chaos) went looking for Motes here and found nothing
    # saying where it went. The "… are tabs in here now" lines under the folded cards only
    # exist on this tab, and the tab is a SETTING rather than a hook, so it has to be
    # staged or the shot photographs "Look" and proves nothing.
    'options-cards'   = @{ Title = 'Options'
                           Env = @{ EQBUDDY_OPTIONS = '1' }
                           Set = @{ OptionsTab = 'cards' } }
    'zone-map'        = @{ Title = 'Zone Map'; Env = @{ EQBUDDY_MAP = '1' }; Set = @{} }
    'drops-window'    = @{ Title = 'Drops'; Env = @{ EQBUDDY_DROPS = '1' }; Set = @{} }
    # The wiki contribution pack (#217 Ask 1). Trap 22: with an empty profile every row
    # is "not checked yet", because the pack's state comes from the WIKI LOOKUP and not
    # from the log — a shot of that proves nothing about the rows underneath and reads as
    # reviewed anyway. So the wiki page cache is seeded below, which also keeps the shot
    # offline and deterministic: without it the app would fetch eqlwiki for real and the
    # picture would change with the wiki.
    #
    # The spread is deliberate — one page with no loot at all, two pages missing drops,
    # and the rest complete so the "already on eqlwiki" count has something to say.
    # PageMissing and Pending are NOT staged: both need a lookup that fails, which cannot
    # be forced from a cache file. WikiPackRenderTests covers those two.
    'wiki-pack'       = @{ Title = 'Wiki contribution pack'
                           Env = @{ EQBUDDY_WIKIPACK = '1' }
                           Set = @{}
                           # KEYS ARE THE NAMES EQBUDDY STORES, not the names the log
                           # writes: the parser strips the article and capitalises, so the
                           # lookup (and therefore the cache filename) is "Asp", never
                           # "an asp". Seeding the log spelling silently misses, the app
                           # falls through to a real fetch, and the shot quietly becomes a
                           # picture of whatever eqlwiki says today — which is exactly what
                           # the first run of this shot did.
                           Wiki = @{
                               # Page exists, records no loot: everything looted is news.
                               'Orc pawn' = @()
                               # Pages that know some of it but not all.
                               'Puma' = @('Chunk of Meat')
                               'Giant spider' = @('Spider Silk', 'Spider Legs')
                               # Complete pages — no contribution, but they are what makes
                               # a small pack read as "the wiki is in good shape here".
                               'Skeleton' = @('Bone Chips', 'Rusty Scimitar')
                               'Asp' = @('Giant Snake Fang', 'Giant Snake Rattle', 'Snake Meat')
                               'Large rattlesnake' = @('Snake Egg', 'Snake Fang')
                               'Rattlesnake' = @('Snake Fang')
                               'Willowisp' = @('Burned Out Lightstone')
                               'Young kodiak' = @('Bear Meat', 'Chunk of Meat', 'Thick Grizzly Bear Skin')
                               'Zombie' = @('Cloth Cape', 'Zombie Skin')
                               'Ghoul' = @('Mote of Infinitesimal Potential')
                               'Lesser mummy' = @('Rusty Morning Star', 'Splintering Club')
                               'Plains cat' = @('Ruined Cat Pelt')
                           } }
}

if ($List) {
    $Shots.Keys | ForEach-Object { "{0,-20} {1}" -f $_, $Shots[$_].Title }
    return
}

$wanted = if ($Shot.Count -gt 0) { $Shot } else { @($Shots.Keys) }
foreach ($name in $wanted) {
    if (-not $Shots.Contains($name)) { throw "Unknown shot '$name'. Try -List." }
}

$exe = Join-Path $repo 'src/EQBuddy/bin/Release/net10.0-windows/EQBuddy.exe'
if (-not (Test-Path $exe)) {
    throw "EQBuddy.exe not built at $exe. Run: dotnet build EQBuddy.slnx -c Release"
}

# The What's-new popup fires whenever LastSeenVersion trails the build, and it would sit
# over every shot. Read the shipping version rather than hardcoding one.
$version = ([xml](Get-Content (Join-Path $repo 'Directory.Build.props'))).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1

# --- the isolated profile ----------------------------------------------------------
$root = Join-Path ([IO.Path]::GetTempPath()) "eqbuddy-shoot-$([Guid]::NewGuid().ToString('N').Substring(0,8))"
$profileDir = New-Item -ItemType Directory -Force (Join-Path $root 'profile')
$logsDir = New-Item -ItemType Directory -Force (Join-Path $root 'game/Logs')
# Existing but empty: UpdateChecker reads "configured folder, no EQBuddySetup.exe" as
# "no update", so no OneDrive scan and no GitHub call during a shoot.
$updateDir = New-Item -ItemType Directory -Force (Join-Path $root 'updates')

Write-Host "Profile: $profileDir"
& (Join-Path $PSScriptRoot 'make-test-session.ps1') -Out $logsDir.FullName | Write-Host

# Extra log lines for one shot, stamped NOW so the replay treats them as the newest
# events. Some surfaces exist only in response to a line the shared fixture does not
# carry — the Progress card's ding list needs "Welcome to level N" — and the fixture
# CANNOT simply gain one: tests/EQBuddy.E2E replays the same file, and one E2E case
# asserts that the ding list is absent BEFORE it appends its own level-up. Per-shot
# appends give a shot the state it needs without making the fixture lie to a test.
function Append-Log([string[]]$lines) {
    if (-not $lines -or $lines.Count -eq 0) { return }
    $log = Get-ChildItem -Path $logsDir.FullName -Filter 'eqlog_*.txt' | Select-Object -First 1
    if (-not $log) { throw "No fixture log to append to in $($logsDir.FullName)" }
    # The game's own stamp shape, e.g. [Mon Jul 20 19:03:34 2026].
    $stamp = (Get-Date).ToString("[ddd MMM d HH:mm:ss yyyy]", [Globalization.CultureInfo]::InvariantCulture)
    foreach ($line in $lines) { Add-Content -Path $log.FullName -Value "$stamp $line" -Encoding utf8 }
}

function Write-Settings([hashtable]$extra) {
    $s = @{
        LogFolder    = $logsDir.FullName
        UpdateFolder = $updateDir.FullName
        Theme        = $Theme
        WindowLeft   = 120
        WindowTop    = 120
        QuestsLeft   = 120
        QuestsTop    = 120
        Minimized    = $false
        # Every popup that would cover a shot, pre-answered.
        ShowTutorial = $false
        LastSeenVersion = $version
        WatchPinsMigrated = $true
        # No chip windows floating over the capture, and no log rewriting under it.
        TrackSpawns  = $false
        TruncateLogs = $false
        ArchiveLogs  = $false
        # Already current, so Load() doesn't add the built-in CC-broke rule and the
        # Tracked card shows only what the fixture actually earned.
        DefaultRulesVersion = 1
    }
    foreach ($k in $extra.Keys) { $s[$k] = $extra[$k] }
    $s | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $profileDir 'settings.json') -Encoding UTF8
}

# The class picker lives in quest-ledger.json, NOT settings.json, so a shot that needs
# more than the one class the log infers has to seed it here. Key is the ledger's own
# "{character}_{server}" lowercased, which for the fixture session is fixed.
function Write-Ledger([hashtable]$ledger) {
    $path = Join-Path $profileDir 'quest-ledger.json'
    if ($null -eq $ledger) { Remove-Item $path -ErrorAction SilentlyContinue; return }
    @{ 'testchar_test' = $ledger } | ConvertTo-Json -Depth 6 |
        Set-Content $path -Encoding UTF8
}

# Raid clears live in raid-kills.json, not settings.json, and the card's body only exists
# once something is defeated — with an empty ledger it is a one-line empty state, so the
# boss rows (the tick, the difficulty badge, the trimming) could not be photographed at
# all. Keys are "{character}_{server}|{boss}" lowercased; for the fixture the character
# half is fixed. Kills are high-water gated on replay, so seeded records survive it.
function Write-Raids([hashtable]$records) {
    $path = Join-Path $profileDir 'raid-kills.json'
    if ($null -eq $records) { Remove-Item $path -ErrorAction SilentlyContinue; return }
    @{ Records = $records; HighWater = '2026-08-01T00:00:00' } | ConvertTo-Json -Depth 6 |
        Set-Content $path -Encoding UTF8
}

# The wiki page cache, which is where the contribution pack's state actually comes from
# (EqlWikiMobService's 7-day disk cache, under <profile>/wiki-cache/mobs). A seeded entry
# is served without a fetch, so the shot is offline and deterministic; an unseeded
# creature would go to the live wiki and photograph whatever it says today.
#
# Format is the service's own CacheEntry: Title, Wikitext, FetchedAt. Drops are the
# wiki's {{:Item}} transclusions, which is what its parser reads.
function Write-WikiCache([hashtable]$pages) {
    $dir = Join-Path $profileDir 'wiki-cache/mobs'
    if ($null -eq $pages) { Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue; return }
    New-Item -ItemType Directory -Force $dir | Out-Null
    foreach ($title in $pages.Keys) {
        # The service's own filename rule: non-alphanumerics become underscores, lowercased.
        $file = ((($title.ToCharArray() | ForEach-Object {
            if ([char]::IsLetterOrDigit($_)) { [char]::ToLowerInvariant($_) } else { '_' }
        }) -join '') + '.json')
        # Drops are read from the {{Namedmobpage}} template's known_loot field, NOT from
        # free wikitext — EqlWikiMobs.Parse only ever looks inside known_loot/common_loot.
        # Free "== Loot ==" prose parses to a page with no drops at all, which is a real
        # state (PageHasNoLoot) and therefore renders perfectly plausibly: the first run
        # of this staging showed all thirteen creatures as "page lists no loot" and looked
        # like a correct screenshot of a wrong app.
        $loot = (($pages[$title] | ForEach-Object { "{{:$_}}" }) -join ' ')
        $wikitext = "{{Namedmobpage`n|name=$title`n|zone=Test Zone`n|known_loot=$loot`n}}"
        @{
            Title = $title
            Wikitext = $wikitext
            FetchedAt = (Get-Date).ToUniversalTime().ToString('o')
        } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $dir $file) -Encoding UTF8
    }
}

# --- stand the real EQBuddy down, and put it back afterwards ------------------------
# The running app is a worse problem than a mismatched capture. It is always-on-top, it
# holds the very window titles these shots ask for, and a capture of it would commit a
# real character name into docs/screenshots/. That has bitten three times: twice after
# release.ps1 reinstalled and relaunched it, and once as a Faction tab filed under
# another shot's name. -OwnerPid (shot.ps1) stops the wrong window being photographed;
# this stops the wrong window being on screen at all.
#
# CLOSED GRACEFULLY, not killed. EQBuddy finalizes the session into history.db on
# ApplicationExit, so a hard kill would throw away whatever the player was in the middle
# of — the cost of a screenshot must never be someone's session record. Force is the
# fallback for a window that will not go, not the opening move.
$relaunch = @()
foreach ($proc in @(Get-Process EQBuddy -ErrorAction SilentlyContinue)) {
    $path = try { $proc.Path } catch { $null }   # Access denied on a process we can't read
    if ($path) { $relaunch += $path }
    Write-Host "Standing down the running EQBuddy (pid $($proc.Id)) — it will be relaunched."
    try {
        if (-not $proc.CloseMainWindow()) { $proc.Kill($true) }
        if (-not $proc.WaitForExit(15000)) { $proc.Kill($true); $proc.WaitForExit(5000) | Out-Null }
    }
    catch { }   # already gone between the enumerate and the close
}
$relaunch = @($relaunch | Select-Object -Unique)

# --- the backdrop ------------------------------------------------------------------
# A plain maximized form, NOT topmost, so the app's own always-on-top windows stay above
# it. This is what stops a rounded corner photographing the desktop.
# One assembly per call: the comma-list form silently loads neither here (pwsh 7).
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$backdropForm = New-Object System.Windows.Forms.Form
$backdropForm.FormBorderStyle = 'None'
$backdropForm.WindowState = 'Maximized'
$backdropForm.BackColor = [System.Drawing.ColorTranslator]::FromHtml($Backdrop)
$backdropForm.ShowInTaskbar = $false
$backdropForm.Show()
$backdropForm.Refresh()

New-Item -ItemType Directory -Force $Out | Out-Null
$taken = @()
try {
    foreach ($name in $wanted) {
        $spec = $Shots[$name]
        Write-Host "`n=== $name → $($spec.Title) ==="
        Write-Settings $spec.Set
        Write-Ledger $spec.Ledger
        Write-Raids $spec.Raids
        Write-WikiCache $spec.Wiki
        Append-Log $spec.Append

        $psi = New-Object Diagnostics.ProcessStartInfo $exe
        $psi.UseShellExecute = $false
        $psi.EnvironmentVariables['EQBUDDY_APPDATA'] = $profileDir.FullName
        $psi.EnvironmentVariables['EQBUDDY_OPAQUE'] = '1'
        foreach ($k in $spec.Env.Keys) { $psi.EnvironmentVariables[$k] = $spec.Env[$k] }
        $proc = [Diagnostics.Process]::Start($psi)
        try {
            # Wait for the window this shot is about, then let the replay settle.
            $deadline = (Get-Date).AddSeconds(90)
            $seen = $false
            while ((Get-Date) -lt $deadline) {
                Start-Sleep -Milliseconds 500
                if ($proc.HasExited) { throw "$exe exited early (code $($proc.ExitCode))." }
                $proc.Refresh()
                if (Get-Process -Id $proc.Id | Where-Object { $_.MainWindowTitle -like "*$($spec.Title)*" }) {
                    $seen = $true; break
                }
                # Satellite windows are not MainWindowTitle; shot.ps1 enumerates properly,
                # so once the app has ANY window, hand off to it after the settle.
                if ($proc.MainWindowHandle -ne 0) { $seen = $true; break }
            }
            if (-not $seen) { throw "No window appeared for '$name' within 90s." }
            $backdropForm.Refresh()
            Start-Sleep -Seconds $Settle

            $png = Join-Path $Out "$name.png"
            # -OwnerPid, so a previous shot's app that is still exiting cannot be
            # photographed under this shot's name: four Progress-theme shots share the
            # title 'EQBuddy Progress', and a title is not an identity.
            & (Join-Path $PSScriptRoot 'shot.ps1') -TitleLike $spec.Title -Out $png -OwnerPid $proc.Id | Write-Host
            $taken += $png
        }
        finally {
            if (-not $proc.HasExited) { $proc.Kill($true) }
            $proc.WaitForExit(10000) | Out-Null
        }
    }
}
finally {
    $backdropForm.Close()
    $backdropForm.Dispose()
    if ($KeepProfile) { Write-Host "`nProfile kept at $root" }
    else { Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue }
    # In the finally, so a thrown shot or a Ctrl+C still gives the app back. Start-Process
    # inherits THIS process's environment, and nothing here sets EQBUDDY_APPDATA globally
    # — the throwaway profile rides on each child's ProcessStartInfo — so the relaunched
    # app finds the real profile. If that ever changes, this line starts pointing the live
    # app at a directory that is deleted three lines above.
    foreach ($path in $relaunch) {
        if (Test-Path $path) {
            Write-Host "Relaunching $path"
            Start-Process $path
        }
    }
}

Write-Host "`n$($taken.Count) shot(s):"
$taken | ForEach-Object { Write-Host "  $_" }
