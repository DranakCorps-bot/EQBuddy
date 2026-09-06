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
    # Run even though another screen job appears to hold the desktop. See the screen-lock
    # block below for what it overrides and what it deliberately does not.
    [switch]$Force,
    [switch]$List
)
$ErrorActionPreference = 'Stop'

$repo = Split-Path $PSScriptRoot -Parent
if ($Out -eq '') { $Out = Join-Path $repo 'docs/screenshots' }

# --- what we can shoot -------------------------------------------------------------
# Title  = the window to capture, matched as a substring (scripts/shot.ps1).
# Env    = the EQBUDDY_* hook that opens it (the same family MainWindow already reads).
# Set    = extra settings.json overrides for this shot.

# EVERY creature the fixture drops from, seeded once and shared by every shot that
# photographs a Drops surface. It is a variable rather than three copies because a staging
# list is code a compiler cannot check (trap 30) — and this one has a specific way of going
# wrong: a PARTIAL seed does not fail, it makes the app correctly fetch the rest from the
# live wiki, so the capture becomes a picture of whatever eqlwiki said that minute. That is
# trap 23, and it cost two wrong 'wiki-pack' shots before anyone noticed.
# Skeleton is deliberately five days old: inside the 7-day lifetime (so it is not re-fetched)
# and outside the 30-second "just now" rule, which is the only way a shot can show the
# freshness caption doing anything at all.
$DropsFixtureWiki = @{
    'Orc pawn' = @()
    'Puma' = @('Chunk of Meat')
    'Giant spider' = @('Spider Silk', 'Spider Legs')
    'Skeleton' = @{ Loot = @('Bone Chips', 'Rusty Scimitar'); AgeDays = 5 }
    'Asp' = @('Giant Snake Fang', 'Giant Snake Rattle', 'Snake Meat')
    'Large rattlesnake' = @('Snake Egg', 'Snake Fang')
    'Rattlesnake' = @('Snake Fang')
    'Willowisp' = @('Burned Out Lightstone')
    'Young kodiak' = @('Bear Meat', 'Chunk of Meat', 'Thick Grizzly Bear Skin')
    'Zombie' = @('Cloth Cape', 'Zombie Skin')
    'Ghoul' = @('Mote of Infinitesimal Potential')
    'Lesser mummy' = @('Rusty Morning Star', 'Splintering Club')
    'Plains cat' = @('Ruined Cat Pelt')
}

$Shots = [ordered]@{
    # EIGHT CARDS since 2026-09-05 - nine after HUD subtraction cut 1 took Quests, eight
    # after cut 2 took World the same day. Seven of them visible on the default profile:
    # Motes ships hidden. PREDICTION for both of these shots, in order down the stack:
    # Combat, Healing, Kills & Drops, Gear & Loot, Watch, Buffs, Progress - with NO
    # "Quests" header between Kills & Drops and Gear & Loot, and NO "World" header at the
    # BOTTOM of the stack, which is where it was. Writing the order down is the point: an
    # absent control photographs as an unremarkable panel (trap 29), so the only way to
    # review a subtraction in a picture is to have said first what should NOT be in it.
    #
    # 'widget-expanded' loses a body as well as a header: EQBUDDY_EXPAND=1 opened Combat,
    # Healing, Watch AND World, and World was the only theme card in that set - so the
    # deaths/zones/markers lists at the bottom of that picture are gone with it. What is
    # expanded now is Combat, Healing and Watch, and nothing else.
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
    # 'loot-card' is gone: the Loot card is a LAUNCHER now (the Gear & Loot theme), so
    # EQBUDDY_EXPAND = 'loot' would photograph a one-line button. The rows it used to
    # show are 'gearloot-loot' below, and the launcher line itself is in 'widget-cards'.
    # MOTES, which ships HIDDEN (AppSettings.MigrateMotesCard) — so the default profile
    # photographs a widget with no Motes card at all, which says nothing about the surface
    # (trap 22). MotesCardOffered is set here too, or the migration hides it again before
    # the window is drawn.
    'motes-card'      = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'motes' }
                           Set = @{ MotesCardOffered = $true; HiddenSections = @() } }
    # The widget's Kills & Drops LAUNCHER since the 2026-08-21 fold, not a card body -
    # the name is kept because docs/TestPlan.md cites it and the surface is still "what
    # the kills slot on the widget looks like". EQBUDDY_EXPAND stays for the debug dump;
    # there is no longer a card to expand.
    'kills-card'      = @{ Title = 'EQBuddy'; Env = @{ EQBUDDY_EXPAND = 'kills' }; Set = @{} }
    # The two remaining heavy card bodies (Gate 5b). Shot before and after their lift so
    # a refactor that changes what a player SEES shows up as a diff in the picture —
    # behaviour-preserving is a claim, and these are how it gets checked.
    'combat-card'     = @{ Title = 'EQBuddy'; Env = @{ EQBUDDY_EXPAND = 'combat' }; Set = @{} }
    # The same card, ALONE. The quick tour's Combat page wants the board and nothing else,
    # and the tour frame scales an image to fit 528x320 — a full widget with ten cards and
    # one of them open is 994px tall, which arrives 109px wide and unreadable.
    #
    # Hidden cards, not a pixel crop. A crop is a number that rots the moment a card gains
    # a row: it keeps producing a picture, of the wrong part, with nothing on screen to
    # say so (trap 23). HiddenSections is the app's own setting, so this shot is a real
    # state a player can also have.
    'combat-solo'     = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'combat' }
                           Set = @{ HiddenSections = @(
                               'healing','kills','loot','tracked',
                               'buffs','progress') } }
    # The Watch card alone, same reason, with the same three seeded rules as tracked-card.
    'watch-solo'      = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'tracked' }
                           Set = @{ HiddenSections = @(
                                       'combat','healing','kills','loot',
                                       'buffs','progress')
                                    TrackedRules = @(
                                   @{ Id = 'shot-spider'; Name = 'Spider parts'
                                      Pattern = 'Spider'; Kind = 0 }
                                   @{ Id = 'shot-bone'; Name = 'Bone chips'
                                      Pattern = 'Bone Chips'; Kind = 0 }
                                   @{ Id = 'shot-kills'; Name = 'Giant spiders'
                                      Pattern = 'giant spider'; Kind = 1 }
                               ) } }
    'healing-card'    = @{ Title = 'EQBuddy'; Env = @{ EQBUDDY_EXPAND = 'healing' }; Set = @{} }
    # The PROGRESS THEME's four tabs (docs/Themes.md). These replaced 'value-cards'
    # (motes,money,faction) and the widget half of 'raids-card': those five cards are one
    # launcher now, and their bodies live in the Progress window, which only EQBUDDY_PROGRESS
    # can open. A surface with no way to be photographed reads as reviewed anyway (trap 22).
    'progress-wealth' = @{ Title = 'EQBuddy Progress'; Env = @{ EQBUDDY_PROGRESS = 'wealth' }; Set = @{} }
    'progress-faction' = @{ Title = 'EQBuddy Progress'; Env = @{ EQBUDDY_PROGRESS = 'faction' }; Set = @{} }
    # ---- The EVOLVED SHELL (E-3 PR 1) ------------------------------------------------
    #
    # The illustration lock (Helm-signed 2026-09-04) says an illustration of our own UI is
    # a capture WITH A RECIPE or it does not ship, so these land in the same change as the
    # window. The shell has no player-facing door yet — EQBUDDY_SHELL is the only way in,
    # which is exactly trap 22's condition and the reason the hook exists at all.
    #
    # Title is 'EQBuddy — Progress'. NOT the theme windows' 'EQBuddy Progress': this is
    # a normal Windows window with native chrome, which is the whole product point of the
    # host, and its title bar names the room the way a shell application's does.
    #
    # **That suffix is load-bearing for the harness, not decoration.** MainWindow.xaml's
    # title is exactly 'EQBuddy', so a bare 'EQBuddy' here would match the widget too —
    # trap 24 arriving INSIDE one process, where -OwnerPid cannot separate them because
    # both windows have the same owner. HistoryWindow already had this shape.
    #
    # Trap 53 applies from here on: this Title is an identity the WINDOW can invalidate
    # without touching this file, and one stale title stops the whole batch at its row.
    # It is derived from ShellPages.Label(page), so it moves only if a room is renamed —
    # at which point these three rows should indeed fail rather than photograph something
    # else.
    #
    # PREDICTION, written before the shot (trap 23):
    #   'shell-progress'  — the strip is THREE chips since E-3 PR 5 (Experience · Wealth ·
    #     Faction), not four: Raids moved to the Live room. The v1 Progress WINDOW still
    #     shows four, so 'progress-window' and this shot legitimately differ, and a fourth
    #     chip appearing here again would mean the room stopped reading `MovedToLive`.
    #   'shell-progress'  — a native title bar reading "EQBuddy — Progress" with real
    #     minimise / maximise / close and a taskbar entry, NOT the hand-drawn chrome
    #     every theme window has. Under it a title row: app icon, "EQBuddy", and a search
    #     field on the right with a magnifier and the hint "Search  Ctrl+K". Down the left,
    #     a rail on a panel ground. **This prediction was written at PR 1 and said ONE row
    #     — a chart icon and "Progress" — and it is FIVE now** (Home · Progress · Gear ·
    #     Quests · World), because a room's row lands in the PR that lands the room. What
    #     has not changed and is still the assertion: Progress is lit as selected, and there
    #     is no Settings row, because that room does not exist and a disabled row is an
    #     affordance that opens nothing. (There IS a Live row now — E-3 PR 5 — and this
    #     prediction has been amended rather than left, since a stale prediction is worse
    #     than none: it makes a correct picture look like a regression.) To its right, the
    #     Progress room: a THREE-chip wrapped strip (Experience · Wealth · Faction) with the
    #     same badges the Progress WINDOW's strip carries, Experience lit, and the Experience
    #     body under it. The window's own fourth chip, Raids, is on `shell-live-raids` now.
    #   'shell-narrow' — the SAME window at the floor width. The rail must be icons only
    #     (chart glyph, no "Progress" word) with the room name still on hover, and the
    #     room content must not clip. That is degrade axis 1, and it is the half of the
    #     resize story no unit test can photograph.
    #
    # **'shell-progress-raids' IS GONE, and its disappearance is the point** (E-3 PR 5). The
    # Raids tab moved from the Progress room to the Live room, so `progress:raids` no longer
    # resolves — and trap 53 is exactly what happens to a shot whose address a surface
    # invalidated without touching this file: `$ErrorActionPreference = 'Stop'` makes one
    # stale row stop the whole batch at that line, which is how `shoot.ps1` was dark for six
    # days across four releases. The replacement is 'shell-live-raids' below; the committed
    # `docs/screenshots/shell-progress-raids.png` is DELETED rather than left, because an
    # illustration of a state the code no longer produces is precisely the drift the
    # illustration lock exists to stop.
    'shell-progress'  = @{ Title = 'EQBuddy — Progress'; Env = @{ EQBUDDY_SHELL = 'progress' }; Set = @{} }
    'shell-narrow'    = @{ Title = 'EQBuddy — Progress'
                           Env = @{ EQBUDDY_SHELL = 'progress'; EQBUDDY_SHELL_SIZE = '580x480' }
                           Set = @{} }
    # ---- E-3 PR 2: the World and Gear rooms ------------------------------------------
    #
    # Same lock, same reason: an illustration of our own UI is a capture with a recipe or
    # it does not ship, so a room's shot lands in the PR that lands the room — exactly the
    # way its rail row does.
    #
    # Titles are 'EQBuddy — World' and 'EQBuddy — Gear', derived from ShellPages.Label.
    # Trap 53 applies: these are identities the WINDOW can invalidate without touching this
    # file, and one stale title stops the whole batch at its row. They should indeed fail if
    # a room is renamed.
    #
    # PREDICTIONS, written before the shots (trap 23):
    #
    #   'shell-world' — the same native chrome and title row as 'shell-progress', with a
    #     rail of THREE rows now, in RailOrder: Progress (chart), Gear (bag), World (pin).
    #     World lit, the other two dim. Still no Home / Live / Quests / Settings row — those
    #     rooms do not exist and a disabled row is an affordance that opens nothing.
    #     The room: a four-chip wrapped strip Map · Camps · Path · Travels with Map lit and
    #     badged with the fixture's zone, and under it the zone map canvas 'zone-map' shows
    #     — the same MapView, in a different host. Pinned BELOW the body, on every tab: a
    #     location icon and the words "Drop camp marker".
    #     **And one thing that must NOT be there**: the star and "Show in mini dashboard"
    #     that sit beside that button in WorldWindow. It is the only writer MiniStats has
    #     for "deaths" and it stays with the window this PR does not retire — copying it
    #     here would make two writers of one settings key (trap 13). A picture is the only
    #     thing that can confirm an absence like that was deliberate rather than lost.
    #
    #   'shell-gear' — rail of five (three when this was written) with Gear lit; a
    #     three-chip strip Loot · Wishlist ·
    #     Inventory carrying the same badges 'gearloot-loot' shows (a loot count on Loot;
    #     no wishlist badge and no inventory badge, since this profile seeds neither); and
    #     the loot list itself in the body. Again one deliberate absence: the
    #     "Show in mini dashboard: ★ Loot" row stays with GearLootWindow, for the same
    #     reason and with the same retirement blocker recorded against it.
    #
    #   'shell-gear-narrow' — THE ONE THAT CAN DISPROVE SOMETHING, and it is here for that
    #     rather than for illustration. ShellLayoutPolicy.MinRoomWidth is 520 —
    #     ProgressWindow's shipped width — and PR 2 added a room whose own window opens at
    #     880. This is that room, on its widest tab, at the floor (520 + the 60-unit
    #     collapsed rail). Predicted: the rail is icons only, three glyphs and no words,
    #     room names on hover; the five seeded wishlist rows read without horizontal
    #     clipping; and the ⧉ copy of /outputfile inventory is still visible without
    #     scrolling, which is the affordance trap 34 keeps a must-list row for on this
    #     surface. **If a row clips, the constant moves — not this shot, and not a
    #     horizontal scrollbar, which would hide a layout failure behind an affordance.**
    'shell-world'     = @{ Title = 'EQBuddy — World'
                           Env = @{ EQBUDDY_SHELL = 'world:map' }; Set = @{} }
    'shell-gear'      = @{ Title = 'EQBuddy — Gear'
                           Env = @{ EQBUDDY_SHELL = 'gear' }; Set = @{} }
    # ---- E-3 PR 3: the Quests room, and the split threshold ---------------------------
    #
    # Same illustration lock: a room's shot lands in the PR that lands the room. Title is
    # 'EQBuddy — Quests', derived from ShellPages.Label — trap 53 applies, and it should
    # indeed fail rather than photograph something else if the room is renamed.
    #
    # PREDICTIONS, written before the shots (trap 23):
    #
    #   'shell-quests' — native chrome and the title row as every shell shot has, with the
    #     rail now FOUR rows in RailOrder: Progress (chart), Gear (bag), QUESTS (quest
    #     icon), World (pin). **Quests must sit BETWEEN Gear and World** — the rail walks
    #     RailOrder filtering by Landed, so this is correct by construction and would look
    #     identical to a healthy build if it silently were not, which is the whole reason
    #     this line is written down. Quests lit, the other three dim.
    #     The room: the character caption ("Quest Tracker — <name>", dim, one line), then
    #     a four-chip wrapped strip Quests · Epic 1.0 · Plane of Sky · Unlocks with their
    #     real badges, the search box spanning the header with its placeholder, the
    #     era/state/class filter row, the mode strip on the right, and under it the LIST at
    #     400 wide beside the DETAIL pane — the fixture's ledger picks a first row and the
    #     pane shows its rewards and turn-ins.
    #     **And one thing that must NOT be there**: the view's own title row with the app
    #     icon and the close button. QuestsRoom calls HideOwnTitleBar(), and a second title
    #     bar under the shell's native one is exactly what a picture is for.
    #
    #   'shell-quests-sky' — the same frame addressed to a room inside the room. Plane of
    #     Sky lit; the two ⧉ command buttons (/outputfile achievements and /outputfile
    #     inventory) side by side above the rows; the #243 leftover boxes if the fixture's
    #     dump produces any; and NO detail pane — a checklist has nothing to select, so its
    #     width goes back to the rows. This is the shot that says the five presentation
    #     rules came across the lift.
    #
    #   THE SPLIT THRESHOLD, both sides, which is what the signed ruling asked for. The
    #   room's share is the window minus the 200-wide rail, so at 900 the room is EXACTLY
    #   SplitRoomWidth and at 899 it is one unit short. The rail is expanded at both
    #   (RailLabelWidth is 520 + 200 = 720), so the pair isolates axis 2 from axis 1 —
    #   which is the whole reason the two thresholds are separate numbers.
    #
    #   **THE FIRST RUN OF THIS PAIR DISPROVED THE CONSTANT, which is what it was for.**
    #   The ruling said to shoot SplitRoomWidth = 640 both sides, so the pair was 840/839
    #   and the prediction was "two panes, then one". The 840 picture came back with two
    #   panes and a detail column of about 190 units: the quest title broke MID-WORD
    #   ("Bone / Chips / (Kaladi / m)") and the 220-capped reward tiles clipped. 640 was
    #   HistoryWindow's measured pair (a 330-wide list), and this room's list is 400 —
    #   Gate 2's shipped number, which a lift may not re-decide. So the CONSTANT moved to
    #   700 and this pair with it, per MinRoomWidth's own rule: the number is a claim, the
    #   screenshot is what tests it, and a room that clips at the threshold moves the
    #   number rather than the shot. The 840 picture is not committed — an illustration of
    #   a state the code no longer produces is exactly the drift the lock exists to stop.
    #
    #   PREDICTION for the second run, written before it:
    #   'shell-quests-split' (900) — two panes: the 400-wide list and a ~300 detail pane
    #     beside it. The title reads on one or two lines with NO mid-word break, and a
    #     reward tile shows its whole name or ellipsizes cleanly. No back button.
    #   'shell-quests-narrow' (899) — ONE unit narrower and a different layout: the list
    #     takes the full width, the detail pane is gone, and no back button is on screen
    #     yet (there is nothing to come back from until a row is clicked). The rail must
    #     still show its labels in BOTH — if it collapses in one of them, the two axes are
    #     not independent and it is the arithmetic that moves, not this shot.
    'shell-quests'    = @{ Title = 'EQBuddy — Quests'
                           Env = @{ EQBUDDY_SHELL = 'quests:general' }; Set = @{} }
    'shell-quests-sky' = @{ Title = 'EQBuddy — Quests'
                           Env = @{ EQBUDDY_SHELL = 'quests:sky' }; Set = @{} }
    'shell-quests-split' = @{ Title = 'EQBuddy — Quests'
                           Env = @{ EQBUDDY_SHELL = 'quests:general'
                                    EQBUDDY_SHELL_SIZE = '900x640' }; Set = @{} }
    'shell-quests-narrow' = @{ Title = 'EQBuddy — Quests'
                           Env = @{ EQBUDDY_SHELL = 'quests:general'
                                    EQBUDDY_SHELL_SIZE = '899x640' }; Set = @{} }
    # ---- E-3 PR 4: the HOME room, and the default landing ----------------------------
    #
    # **`EQBUDDY_SHELL = '1'` is deliberate and is half of what these shots prove.** Every
    # other shell row above names an explicit address; the bare hook asks for no room at
    # all, so the picture is evidence about the WINDOW's own default rather than about
    # whatever the harness typed. That is the same reason the E2E for it exists — until
    # PR 4 nothing walked this path, and the default was written in three places that were
    # never forced to agree.
    #
    # PREDICTION, written before the shot (trap 23):
    #   'shell-home' — a native title bar reading "EQBuddy — Home". The rail now has FIVE
    #     rows and Home is the TOP one, above Progress, lit as selected — it did not have to
    #     be arranged there, `RailOrder` has had Home first since PR 1 and the room joining
    #     `Landed` put it in place. A rail that appended Home at the BOTTOM, or a shell that
    #     still opened on Progress, is a build that looks healthy in every way except this
    #     picture. Under it, four blocks with small-caps headings, in this order:
    #       Character  — "Testchar" in accent ink, "test · <zone>" under it.
    #       Readiness — heading "Readiness — 3 not run yet"; three rows (Bags, Achievements,
    #         Factions), each with "Not run yet" in accent ink on the right, a dim line
    #         saying what it feeds, and a ⧉ copy button under it. Three buttons, because the
    #         shoot profile has no dumps.
    #       Recent session — "Session in progress" and one sentence. **No numbers**: the
    #         fixture IS a live session with 82 kills in it and the Home/Live boundary says
    #         Home does not draw them.
    #       Go to — four rows (Progress, Gear, Quests, World) with their one-line pitches.
    #         NOT five: Home does not link to itself. NOT six: Live has not landed, and a
    #         link that opens nothing is the rail's forbidden shape one level in.
    #     The block column is capped at `MinRoomWidth` and pinned LEFT — the first take of
    #     this shot is what asked for that, with "Not run yet" stranded about 600 units from
    #     the row it belonged to. If the answers drift back toward the right edge as the
    #     window widens, it is the cap that has come off.
    #   'shell-home-narrow' — the SAME room at the floor. The rail is icons only and the
    #     block column must look UNCHANGED, because its cap IS the floor's room width: this
    #     is the shot that can disprove that, the way 'shell-gear-narrow' can disprove
    #     MinRoomWidth itself. Anything clipping horizontally here means the cap is wrong,
    #     not the shot — and never a horizontal scrollbar, which hides a layout failure
    #     behind an affordance.
    #   'shell-home-ready' — the SAME room with an inventory dump staged, which is the only
    #     way to photograph the difference the Readiness block exists to draw. Heading reads
    #     "Readiness — 2 not run yet"; the Bags row now carries a DATE in dim ink and an
    #     "Open" link instead of a ⧉ button, and the other two are unchanged. If the two
    #     pictures are indistinguishable, never-scanned and healthy have collapsed into one
    #     state, which is exactly what the pre-design forbade.
    #
    # There is NO shot of the room-level empty (no character at all), and that is a gap
    # named rather than hidden: this harness seeds a fixture log by construction, so
    # "EQBuddy has never seen a character" cannot be staged without a second profile shape
    # that nothing else here needs. `RoomEmptyState` and its words are unit-tested
    # (`HomeRoomTests`) and `shellHomeEmpty` is in the dump; the POSITION — centred in the
    # room's cell — is the part still unphotographed. Whoever adds that profile shape gets
    # the shot with it.
    # ---- E-3 PR 5: the LIVE room, and the Raids move ---------------------------------
    #
    # Same illustration lock: a room's shot lands in the PR that lands the room, exactly the
    # way its rail row does. Title is 'EQBuddy — Live', derived from ShellPages.Label — trap
    # 53 applies, and it should indeed fail rather than photograph something else if the room
    # is renamed.
    #
    # PREDICTIONS, written before the shots (trap 23):
    #
    #   'shell-live' — native chrome and the title row as every shell shot has, with the rail
    #     now SIX rows in RailOrder: Home (tray), LIVE (bolt), Progress (chart), Gear (bag),
    #     Quests (quest), World (pin). **Live must sit BETWEEN Home and Progress** — the rail
    #     walks RailOrder filtering by Landed, so this is correct by construction and would
    #     look identical to a healthy build if it silently were not, which is the whole reason
    #     this line is written down. Live lit, the other five dim.
    #     The room: a session report at the top — "This sitting — <fixture zone>" in accent
    #     ink with a facts line under it (elapsed · N kills · dps), then a SIX-chip wrapped
    #     strip Damage · Healing · Pet · Timeline · Kills · Raids carrying their real badges,
    #     Damage lit. Under it the Damage body: the title "Your damage", a subtext line, the
    #     compact Fight/Session toggle with **Session** selected (the room is about the
    #     sitting; the floating breakout defaults the other way, deliberately), the four-chip
    #     sort strip, the Combat card's own summary lines, and the ability bar rows.
    #     **And one thing that must NOT be there**: a "0 deaths" anywhere in the report. The
    #     one number whose absence is the good news is omitted rather than printed as a zero,
    #     and a picture is the only thing that can confirm an absence like that.
    #   'shell-live-raids' — the same frame addressed straight to a room inside the room, and
    #     the DESTINATION half of the Raids move (the departure half is `shell-progress`
    #     showing three chips). Raids chip lit, badged "0 / 21".
    #     THE BODY IS THE EMPTY STATE, and that is the prediction rather than a miss: this
    #     shot seeds no raid-kills.json (that is 'raids-card' / 'raids-import'), so what
    #     shows is "Nothing defeated yet …" plus the ⧉ copy of /outputfile achievements.
    #     That button is the thing worth photographing here — the room reuses the real
    #     RaidsCardView, so "a surface that needs an in-game command must SHIP the command"
    #     survived a SECOND host change for free, and trap 34's whole lesson is that a
    #     missing affordance is invisible to everything except a picture or a must-list.
    #     (The predecessor shot, 'shell-progress-raids', predicted a ledger and was wrong for
    #     the same reason; the note is carried rather than re-learned.)
    #   'shell-live-timeline' — the tab that is a CANVAS rather than a list, which is the one
    #     layout claim in this room no unit test can make. Predicted: the fight name and its
    #     "m:ss · N events · peak N dps @ m:ss" line, a DPS graph 96 units tall, and under it
    #     the lanes filling the rest of the cell — a lane per skill with the 176-unit name
    #     gutter on the left. **No vertical scrollbar**: the room disables that scroller for
    #     this tab so the canvas gets the viewport instead of an infinite measure. If a
    #     scrollbar is there, the canvas is being measured with infinite height and the lanes
    #     are the wrong size (trap 36's arithmetic, on the axis that hides).
    'shell-live'      = @{ Title = 'EQBuddy — Live'
                           Env = @{ EQBUDDY_SHELL = 'live' }; Set = @{} }
    'shell-live-raids' = @{ Title = 'EQBuddy — Live'
                           Env = @{ EQBUDDY_SHELL = 'live:raids' }; Set = @{} }
    'shell-live-timeline' = @{ Title = 'EQBuddy — Live'
                           Env = @{ EQBUDDY_SHELL = 'live:timeline' }; Set = @{} }
    # E-3 S3 — HistoryWindow's this-session half, the two rooms it brings.
    #
    # PREDICTIONS, written before the shots (trap 23):
    #
    #   'shell-live-pace' — the same 'EQBuddy — Live' chrome and six-row rail as
    #     'shell-live', and an EIGHT-chip wrapped strip: Damage · Healing · Pet · Timeline ·
    #     PACE · ENCOUNTERS · Kills · Raids. **Pace must sit between Timeline and
    #     Encounters** — narrowest scope first — and it must NOT read "Timeline", which is
    #     the whole signed §3 refusal and the one thing this picture can disprove that a
    #     unit test cannot make obvious: two chips, two different words, on one strip, an
    #     inch apart.
    #     Pace lit, badged "peak N dps" from the fixture's own timeline.
    #     The body: a dim caption "DPS over time — peak N/s (h:mm PM–h:mm PM, per minute)"
    #     over a 120-unit panel-backed frame carrying ONE accent polyline. Nothing else —
    #     no lanes, no gutter, no names. That is the difference from 'shell-live-timeline'
    #     photographed rather than described, and the two pictures side by side are the
    #     argument for the rename.
    #     **What must NOT be there**: the "Not enough of this sitting has happened…" empty
    #     line. The caption, the frame and that line are one switch (trap 17), so a picture
    #     with both is a pair that has drifted apart.
    #   'shell-live-encounters' — the same frame with Encounters lit, badged "N pulls".
    #     The body is a list of COLLAPSED rows, oldest first, each "▸ <creature> — h:mm tt ·
    #     N dmg · N dps · Ns · took N" with a dim ⧉ beside it. No row is open on first
    #     paint, and the ⧉ is the thing worth photographing: it is the fourth caller of
    #     `FightExport.ToText`, and a missing affordance is invisible to a diff, a build and
    #     a test alike (trap 34).
    #     A vertical scrollbar is EXPECTED here and not on Pace — the room's scroller is
    #     Auto for both, and this is the tab with real overflow.
    #   'shell-progress-history' — 'EQBuddy — Progress' chrome, rail of six with Progress
    #     lit, and a FOUR-chip strip: Experience · Wealth · Faction · HISTORY. That fourth
    #     chip is the picture's point: it is the first tab in `ProgressSurface` that only
    #     one host draws, so a phone screenshot or a v1 `ProgressWindow` shot taken the same
    #     day must show THREE and four respectively — the two shots together are what says
    #     `DesktopShellOnly` is wired and not merely written.
    #     History lit, badged "3 sittings" from the three primed sessions.
    #     The body is a LIST BESIDE A DETAIL PANE — the first time Progress has needed the
    #     second axis (`RoomSinglePane`, Bevel §4's predict-before-shoot). Left: "3
    #     sessions" over three two-line rows, newest first, each "<Day> <Mon d>, <h:mm tt> —
    #     West Commonlands" over "0h NNm · N kills · N% xp · <coin>".
    #     **The zone and the duration are DERIVED, not invented** — this shot replays the
    #     one shared fixture log, so its zone is the fixture's (West Commonlands, never a
    #     zone of one's choosing) and its span is the fixture's own compressed hour. The
    #     first draft of this block guessed "Lower Guk" and "2h 14m" and the shot came back
    #     disagreeing with its own prediction on two literals that were never predictions at
    #     all. That is trap 23's tripwire firing on noise: a prediction you did not derive
    #     costs the next reader a real investigation, because the honest response to a
    #     mismatch is to suspect the fixture. Predict the SHAPE and only those literals the
    #     staging actually pins. The DATES are `ShiftDays` behind the run day and so are
    #     unpinnable by construction. Right, with nothing picked: the
    #     ladders block — "Character progress — every stored session" in small caps, a level
    #     caption "Level 22 → 24 (…, 3 dings)" over an accent staircase, an AA caption over
    #     a green one — and under it the dim studio-pointer paragraph naming
    #     "Session history…".
    #     **The ladders are the half that can be wrong and look right**: they need dings
    #     across MORE THAN ONE stored session, which is exactly what the three primes are
    #     for, and an unprimed profile would render a correct picture of an empty career.
    #     Primed under the FIXTURE'S OWN character for the reason 'progress-levelups'
    #     already records: `SessionSummary.Stored` compares (server, character) with SQL
    #     `=`, so rows under any other name are rows this surface can never match.
    'shell-live-pace' = @{ Title = 'EQBuddy — Live'
                           Env = @{ EQBUDDY_SHELL = 'live:pace' }; Set = @{} }
    'shell-live-encounters' = @{ Title = 'EQBuddy — Live'
                           Env = @{ EQBUDDY_SHELL = 'live:encounters' }; Set = @{} }
    #   'shell-progress-history-narrow' — THE ONE THAT CAN DISPROVE SOMETHING, and it is
    #     here for the reason 'shell-gear-narrow' is. `RoomSinglePane` is arithmetic
    #     `ShellLayoutPolicyTests` already covers; what no unit test can say is whether the
    #     ROOM applied it — "present in the build" and "in effect at runtime" are different
    #     claims and only the second is the feature (trap 42). At 580 wide the room is below
    #     `SplitRoomWidth`, so: rail collapsed to icons, the LIST filling the whole room, no
    #     detail pane, and NO "‹ All sittings" button — that appears only after a row is
    #     picked, and an affordance that opens nothing is a trap. If the detail pane is still
    #     beside the list here, the forward from `ProgressRoom.ApplyLayout` is not wired.
    'shell-progress-history' = @{ Title = 'EQBuddy — Progress'
                           Env = @{ EQBUDDY_SHELL = 'progress:history' }
                           Set = @{}
                           Prime = @(
                               @{ Character = 'Testchar'; Fraction = 0.35; ShiftDays = 3
                                  Lines = @('You have gained a level! Welcome to level 22!',
                                            'You have gained an ability point!  You now have 3 ability points.') }
                               @{ Character = 'Testchar'; Fraction = 0.65; ShiftDays = 2
                                  Lines = @('You have gained a level! Welcome to level 23!',
                                            'You have gained 3 ability point(s)!  You now have 6 ability point(s).') }
                               @{ Character = 'Testchar'; Fraction = 0.9;  ShiftDays = 1
                                  Lines = @('You have gained a level! Welcome to level 24!',
                                            'You have gained 3 ability point(s)!  You now have 9 ability point(s).') }
                           ) }
    'shell-progress-history-narrow' = @{ Title = 'EQBuddy — Progress'
                           Env = @{ EQBUDDY_SHELL = 'progress:history'
                                    EQBUDDY_SHELL_SIZE = '580x480' }
                           Set = @{}
                           Prime = @(
                               @{ Character = 'Testchar'; Fraction = 0.35; ShiftDays = 3
                                  Lines = @('You have gained a level! Welcome to level 22!') }
                               @{ Character = 'Testchar'; Fraction = 0.9;  ShiftDays = 1
                                  Lines = @('You have gained a level! Welcome to level 24!') }
                           ) }
    'shell-home'      = @{ Title = 'EQBuddy — Home'; Env = @{ EQBUDDY_SHELL = '1' }; Set = @{} }
    'shell-home-narrow' = @{ Title = 'EQBuddy — Home'
                           Env = @{ EQBUDDY_SHELL = '1'; EQBUDDY_SHELL_SIZE = '580x480' }
                           Set = @{} }
    'shell-home-ready' = @{ Title = 'EQBuddy — Home'; Env = @{ EQBUDDY_SHELL = '1' }; Set = @{}
                           Dump = @{ 'Testchar_test-Inventory.txt' = @(
                               "Location`tName`tID`tCount`tSlots"
                               "General1`tBone Chips`t0`t12`t0"
                               "General2`tFlawless Diamond`t0`t1`t0") } }
    'shell-gear-narrow' = @{ Title = 'EQBuddy — Gear'
                           Env = @{ EQBUDDY_SHELL = 'gear:gear'; EQBUDDY_SHELL_SIZE = '580x480' }
                           Set = @{
                               GearChecklistName = 'Kael push'
                               GearChecklist = @(
                                   @{ Slot = 'HEAD'; Item = 'Crown of Narandi'; Source = 'Kael Drakkel' }
                                   @{ Slot = 'HANDS'; Item = 'Gloves of Dark Embers'; Source = 'Sebilis'; Acquired = $true }
                                   @{ Slot = 'PRIMARY'; Item = 'Blade of Carnage'; Source = 'Kael Drakkel' }
                                   @{ Slot = 'NECK'; Item = 'Silver Chain of Dread'; Source = 'Plane of Fear' }
                                   @{ Slot = 'HEAD'; Item = 'Exquisite Velium Shard'; IsExaltation = $true
                                      ExaltationEffect = '+15 hp'; Source = 'Kael Drakkel' }
                               ) } }
    # ---- E-3 lane S, S2: World's fifth tab -------------------------------------------
    #
    # Same illustration lock: a room's shot lands in the PR that lands the room, and this is
    # a tab ARRIVING in a room that already had four. Both use $DropsFixtureWiki, seeded at
    # the top of this file for the reason recorded there — a partial seed does not fail, it
    # sends the app to the live wiki and turns the capture into a picture of whatever
    # eqlwiki said that minute.
    #
    # PREDICTIONS, written before the shots (trap 23/51):
    #
    #   'shell-world-drops' — native chrome reading "EQBuddy — World", the rail of SIX in
    #     RailOrder with World lit. The room's wrapped strip is FIVE chips now —
    #     Map · Camps · Path · Travels · Drops — with DROPS lit and badged "13 creatures"
    #     (the fixture's creatures-with-loot: the same thirteen seeded above and the same
    #     number 'drops-window' shows). Map is badged with the fixture's zone; Camps and
    #     Path carry no badge, because a running timer count on a tab strip is a countdown
    #     by another name. Under it the Drops body exactly as 'drops-window' draws it: the
    #     filter box with Copy text / Copy CSV / Save CSV… beside it, the dim orientation
    #     footer ABOVE the rows (trap 37 — it carries the only in-app pointer to where the
    #     wiki pack went), then a heading per creature with its drop rows, each reading
    #     "wiki read just now" with a dim ↻ except Skeleton at "wiki read 5d ago" with a
    #     live one. Pinned BELOW the body, as on every other World tab: "Drop camp marker".
    #     **Two things that must NOT be there**: the deaths star and its "Show in mini
    #     dashboard" label, which stay with WorldWindow because that star is the only writer
    #     MiniStats has for "deaths" (trap 13/20); and any second title bar, since the room
    #     is a view in the shell's chrome rather than a window shrink-wrapped into one.
    #
    #   'shell-world-drops-narrow' — THE ONE THAT CAN DISPROVE SOMETHING, and the reason it
    #     is here rather than for illustration. Bevel's pre-design §5 named one real width
    #     risk before any of this was built: the filter/export bar is a four-column Grid —
    #     the filter as the STAR column, three auto-sized TEXT buttons — and
    #     DropsCardView's own remarks say it was "sized for a 560px window". MinRoomWidth is
    #     520. This is that row at the floor (520 + the 60-unit collapsed rail).
    #     Predicted: the rail is icons only, six glyphs and no words; the three buttons keep
    #     their full labels, because auto columns take what they ask for; the filter box
    #     absorbs the whole squeeze as the star column and stays wide enough to type in; and
    #     the creature headings and drop rows read without horizontal clipping.
    #     **The failure this can show is the star column collapsing to nothing** — three
    #     buttons crushed against a filter box with no width left. If it does, the fix is
    #     the BAR (wrap it, the way the Inventory bar already had to be) and NOT
    #     MinRoomWidth, which is ProgressWindow's shipped width and a measured floor rather
    #     than a fresh guess.
    #
    # OUTCOME, both taken 2026-09-05 and both matching, which is worth writing down
    # because the narrow one was taken to DISPROVE something and did not:
    #  * 946x633 / 566x473. Five chips on one wrapped row at both widths, Drops lit and
    #    badged "13 creatures"; Map badged "West Commonlands"; Camps, Path and Travels
    #    unbadged (the fixture has no deaths, so Travels has nothing to say either).
    #  * The freshness captions read "wiki just now" and Skeleton "wiki 5d ago" — the
    #    prediction said "wiki read just now", which is the surface's older wording. The
    #    behaviour predicted is what shipped; only my quotation of it was stale.
    #  * **THE WIDTH RISK IS CLOSED AND `MinRoomWidth` DOES NOT MOVE.** At the floor the
    #    three buttons keep their full labels, the filter box absorbs the whole squeeze
    #    as the star column and is still comfortably typable, and no creature heading or
    #    drop row clips horizontally. The bar "sized for a 560px window" survives 520
    #    because the only thing that had to give was the star column, which is what a
    #    star column is for.
    #  * Neither picture shows the deaths star or a second title bar — the two absences
    #    only a picture can confirm were deliberate.
    'shell-world-drops' = @{ Title = 'EQBuddy — World'
                           Env = @{ EQBUDDY_SHELL = 'world:drops' }; Set = @{}
                           Wiki = $DropsFixtureWiki }
    'shell-world-drops-narrow' = @{ Title = 'EQBuddy — World'
                           Env = @{ EQBUDDY_SHELL = 'world:drops'; EQBUDDY_SHELL_SIZE = '580x480' }
                           Set = @{}
                           Wiki = $DropsFixtureWiki }
    # The PROGRESS theme EXPANDED IN PLACE (Inline themes PR 1). Title is 'EQBuddy' — this
    # is the widget, not the window, which is the whole point of the change. A NEW name,
    # per trap 21: 'progress-card' and 'section-progress' are both embedded in the docs and
    # both still mean the old thing.
    #
    # It stages the same level-up 'progress-card' does, and for a second reason on top of
    # that one: the inline body is CAPPED (WidgetMetrics.ThemeBodyMaxHeight), and a room
    # shorter than its cap photographs as a card with no cap at all. A shot that cannot
    # reach the state under review reads as reviewed anyway (trap 22).
    #
    # PREDICTION, written before the shot (trap 23): the Progress card is the ONLY expanded
    # one (the named EQBUDDY_EXPAND form opens just its key). Under its header, a four-chip
    # wrapped strip — Experience, Wealth, Faction, Raids — each carrying the badge the
    # window's strip carries. Experience is lit, being the room that moves while you play.
    # Below it the Experience body, now TALLER than the cap: the same lines 'progress-card'
    # shows (16 xp gains, Last 15m, 1 AA point, Next level, Level 12 at ...), then "New at
    # level 12" with Heroic Leap and Unbound Wrath, and it should CUT with a scrollbar
    # rather than run the widget off the screen. On the header, right of the summary and
    # left of the chevron, a ↗ that opens the window; the chevron itself reads DOWN.
    #
    # THE CUT DID NOT HAPPEN, and the prediction was wrong about the interesting half.
    # Everything above is what the picture shows EXCEPT the cap: the full body — through
    # "Double Riposte / Archetype · 3 ranks" — fits in about 175 units against a cap of
    # 320, with no scrollbar. Staging a level-up does not make this room tall; nothing in
    # the Progress theme is tall. That is the finding, not a fixture bug: the cap is a
    # guard for the themes with LISTS in them (PR 2's Loot rows and Drops), and the reason
    # to keep the append anyway is the "New at level 12" block, which is real content this
    # card has to draw and the shared fixture never produces.
    # SKILL-UPS staged since 1.99.15 (the fold, David's ask): the shared fixture never
    # produces a skill-up line, so before this append NO shot could show the heading at
    # all — the fold shipped into a surface no picture covered (trap 22), which the
    # release review closed. PREDICTION (trap 23), added before the re-shoot: between the
    # ding block and the AA lines, a "Skill-ups" heading with an open chevron and two
    # rows — "1H Slashing  112 (+1)" and "Dodge  55 (+1)" — because ShowSkillUps defaults
    # true; everything else identical to the previous capture.
    'theme-inline-progress' = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'progress' }
                           Append = @('You have gained a level! Welcome to level 12!'
                                      'You have become better at 1H Slashing! (112)'
                                      'You have become better at Dodge! (55)')
                           Set = @{ ShowNextUnlocks = $true; ShowAllAAs = $true } }
    # PR 2's first theme. PREDICTION: the Kills & Drops card expanded with a two-chip
    # strip (Kills carrying the fixture's kill count, Drops carrying "N creatures"), and
    # under it the Kills room - the kills/hr summary line, the per-creature kill rows,
    # and the Farming block with loot sub-rows. NOT the drops list: that room is a
    # Glance, and this shot proves the FULL room.
    'theme-inline-kills' = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'kills' }
                           Set = @{} }
    # The Drops GLANCE (Bevel's move: it reads the wiki, which an expanded card over a
    # running game must not). PREDICTION: the Drops chip lit, and under the strip ONE
    # dim line reading "Drops by Creature - N types" - no creature headings, no rows,
    # no filter, no export buttons. The window is one ⧉ away and that is the point.
    'theme-inline-kills-glance' = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'kills:drops' }
                           Set = @{} }
    # 'theme-inline-quests' AND 'theme-inline-quests-epic' WERE HERE, and both went with
    # the Quests card on 2026-09-05 (HUD subtraction cut 1). There is no card to expand and
    # EQBUDDY_EXPAND no longer answers 'quests', so leaving the rows would have stopped the
    # whole batch dead at this line - $ErrorActionPreference is 'Stop' and a shot whose
    # window never appears takes every shot after it with it (trap 53, which cost six days
    # of a dark acceptance criterion). Their committed PNGs were deleted in the same commit:
    # an illustration is a capture WITH A RECIPE or it does not ship, and these two no
    # longer have one. What they showed is still shot, by 'shell-quests' and
    # 'shell-quests-sky' - the same four rooms, in the room that owns them now.
    # PR 2's second theme. PREDICTION: the Gear & Loot card expanded with a three-chip
    # strip (Loot with the item count badge, Wishlist, Inventory), and under it the Loot
    # room's slice strip and rows, capped by the shared body height with its own
    # scrollbar if the fixture overflows it.
    'theme-inline-loot' = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'loot' }
                           Set = @{} }
    # ---- #250: the theme body's cap follows the height grip ----
    #
    # These two are the ACCEPTANCE for the 320-cap change, and 'theme-inline-loot' above is
    # their baseline: same card, same room, same fixture, undragged. The Loot room is the
    # one that overflows 320 on this fixture (the committed baseline shows 17 rows and a
    # scrollbar), which is what makes "more rows" a thing a picture can settle.
    #
    # NOT the Paineless Motes/SectionScroll image: that is #250's OWN track (Helm-signed
    # 2026-08-29) and using it here would be accepting one change with another's evidence.
    #
    # PREDICTIONS, written before the first run (trap 23 — a shot whose numbers you did not
    # predict in advance has not been reviewed):
    #
    #  theme-body-dragged (100%, ContentHeight 900)
    #    * The widget window is TALLER than the baseline's 851px — the drag is what does
    #      that, and it is the half Paineless could already see working.
    #    * The Loot body is capped near the CEILING rather than the floor: 900 units of
    #      granted stack minus the other cards' headers (eleven of them, plus this card's
    #      own header and its two chip strips) leaves comfortably more than 640, so the cap
    #      clamps to 640 — exactly 2x the baseline.
    #    * So the room shows MORE loot rows than the baseline's 17, and its scrollbar is
    #      shorter or gone. It must NOT show a different SET of rows: same order, same
    #      counts, same "×11 Bone Chips" at the top. A changed order would mean the shot is
    #      of a different state (trap 23), not of a taller body.
    #    * Every sibling card is still visible and still collapsed. The point of the cap is
    #      that one open card does not push the glance off the widget, and 640 with the
    #      stack at 900 leaves room for all of them.
    #
    #  theme-body-dragged-125 (125%, same drag)
    #    * FEWER body units than the 100% shot, not more — and this is the prediction most
    #      likely to be got wrong by intuition. ContentHeight is pre-scale, so a 900-unit
    #      drag on a work area of H screen pixels is granted min(900, (H-160)/1.25): on a
    #      1080p screen that is 736 units, and the cap lands under the ceiling rather than
    #      on it. Everything is DRAWN 1.25x larger, so the row count drops again.
    #    * It must still be well above the 320 floor. If this shot shows the same rows as
    #      the baseline, the monitor clamp is eating the whole drag and the pairing above
    #      is the thing to re-read — not the formula.
    #
    # WHAT ACTUALLY HAPPENED, measured off the app's own dump on a 1032px work area, kept
    # beside the prediction rather than replacing it — a corrected prediction that erases
    # the miss teaches nobody anything:
    #
    #    100%   sectionCapScreen 872, granted 872, chrome 379, cap 493   (predicted 640)
    #    125%   sectionCapScreen 872, granted 698, chrome 379, cap 320   (the floor)
    #    auto   cap 320                                                  (predicted, exact)
    #
    #  * Every VISIBLE claim held: 925px vs 851px, 21 rows vs 17, the body scrollbar gone,
    #    same order and counts, every sibling card still on screen. The 640 was wrong for
    #    two reasons worth writing down. The drag never gets what it asked for — 900 is
    #    clamped to the 872-unit work area before the cap sees it — and the CHROME is far
    #    bigger than a card-count guess suggests: ten sibling headers plus this card's own
    #    header, two chip strips and its padding come to 379 units, nearly half the stack.
    #    **The ceiling is not the operative bound on a 1080p screen; the chrome is.**
    #  * The 125% shot landed on the branch the prediction named as its own falsifier: 698
    #    granted minus 379 chrome is 319, under the floor, so the floor holds and the
    #    picture is the baseline's 17 rows. That is CORRECT and not a defect — at 125% with
    #    ten cards showing, a 1032px screen has no room left to give, and the widget is
    #    already at its full height. It is also the honest limit of this change: #250's fix
    #    buys real room at 100% and nothing at 125% on a small screen. Raising the floor
    #    globally is the alternative, and the three-class lock forbids it.
    'theme-body-dragged' = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'loot' }
                           Set = @{ ContentHeight = 900 } }
    'theme-body-dragged-125' = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'loot' }
                           Set = @{ ContentHeight = 900; UiScale = 1.25 } }
    # The GLANCE room. Raids is the Progress theme's only one, and its contract is that it
    # draws a LINE instead of a body — so a picture of it is the only way to see that the
    # 29-row ledger did not come along for the ride.
    # PREDICTION (re-shot after Bevel's ruling, Helm-signed 2026-08-22): the Raids chip lit
    # and still reading the scoreboard "2 / 21"; under the strip ONE line reading exactly
    # "19 left" in the dim summary ink -- the remainder, which is the thing the chip cannot
    # say. No second fraction and no second "Raids": the first shot printed "Raids — 2 / 21"
    # an inch under a chip saying the same, which is one fact twice. No rows, no zone
    # headings, no re-check button. The ↗ on the header is the way to the ledger and is the
    # reason the line is allowed to be a line.
    'theme-inline-raids' = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'progress:raids' }
                           Set = @{}
                           # The same two clears 'raids-card' seeds — one witnessed, one
                           # imported — so the line's numerator is a number both shots
                           # agree on and the E2E suite already asserts as 2.
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
    # Wealth inline is COIN ONLY (Bevel's table, Helm's correction): the four summary
    # lines, no sold ledger, no mote rate. The window's Wealth tab shows all three, so this
    # shot and 'progress-wealth' are deliberately DIFFERENT pictures of one room, and the
    # difference is the ruling.
    # PREDICTION (re-shot after Bevel's ruling): the Wealth chip lit and now reading COIN
    # ONLY -- "Wealth 5p 1g 4s 8c", with the "· 1 mote · 0.9/hr" it used to carry GONE, so
    # the chip matches the body under it. Four lines — Corpses, Merchant sales, per hour /
    # per active hour, and "Last 15m" — and NOTHING else. No "Sold" heading, no sold rows
    # (the window's shot has 24), no motes line. The Progress LAUNCHER line above still
    # carries motes/hr, which is a different surface and stays.
    'theme-inline-wealth' = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'progress:wealth' }; Set = @{} }
    # The breakout needs no hook of its own: it shows whenever the widget is minimized and
    # its stat is starred, and both are plain settings. Session scope is the one with the
    # filter strips on it (Target is a different axis and hides them).
    # #182 (Ladylag): the damage-by-ability rows, in the narrow window she had. This is
    # the shot whose rows read ".", ".." and nothing at all.
    'damage-breakout' = @{ Title = 'Damage breakout'
                           Env = @{}
                           Set = @{ Minimized = $true; MiniStats = @('dps'); BreakoutDamageScope = 'session' } }
    # 'progress-breakout' RETIRED 2026-08-25. The tab-less 272x125 Progress float is gone
    # (Bevel's fold, Helm-signed): the mini bar's xp chip opens the Progress WINDOW, which
    # has the tabs. Shoot 'progress-card' / 'progress-wealth' / 'progress-faction' /
    # 'raids-card' for that surface instead. Not re-pointed at the window under the old
    # name, because a shot name IS a filename (trap 21) and the old PNG is a picture of a
    # surface that no longer exists.
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
    # The #243 leftover bands plus the inventory import report (Hateborne, 2026-09-03),
    # staged through the real seam: a dump beside the log and the game's own announcement.
    # PREDICTED before shooting (trap 23): Ready band "— 2" (WAR Belt of the Four Winds,
    # CLR Necklace of Resolution); report "1 Sky reward marked turned in" naming
    # Enchanter · Ivory Mask (the dump holds the finished reward, its class unplayed);
    # band A "No longer needed — 1" (Azure Ring — its one Sky wanter is the completed
    # Azure Ruby Ring); band B "Other classes still want — 1" (Silken Strands — Monk
    # only, and Monk is not played); and BOTH ⧉ copy buttons above the bands.
    'sky-leftovers'   = @{ Title = 'Quest Tracker'
                           Env = @{ EQBUDDY_QUESTS = 'sky' }
                           Ledger = @{ Classes = @('Warrior', 'Cleric') }
                           Dump = @{ 'Testchar_test-Inventory.txt' = @(
                               "Location`tName`tID`tCount`tSlots"
                               "General 1-Slot1`tAzure Ring`t0`t1`t10"
                               "General 1-Slot2`tSilken Strands`t0`t1`t10"
                               "Bank1-Slot1`tIvory Mask`t0`t1`t10"
                           ) }
                           Append = @('Outputfile Complete: Testchar_test-Inventory.txt')
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
    # The same staging with all three bands FOLDED (sky:folded — a screenshot-only hook,
    # because the fold is session-only by design and has no settings backing; trap 22).
    # PREDICTED: three one-line RaisedBrush boxes reading "Ready to turn in — 2",
    # "No longer needed — 1", "Other classes still want — 1", chevrons pointing right.
    'sky-folded'      = @{ Title = 'Quest Tracker'
                           Env = @{ EQBUDDY_QUESTS = 'sky:folded' }
                           Ledger = @{ Classes = @('Warrior', 'Cleric') }
                           Dump = @{ 'Testchar_test-Inventory.txt' = @(
                               "Location`tName`tID`tCount`tSlots"
                               "General 1-Slot1`tAzure Ring`t0`t1`t10"
                               "General 1-Slot2`tSilken Strands`t0`t1`t10"
                           ) }
                           Append = @('Outputfile Complete: Testchar_test-Inventory.txt')
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
    # sky-ready's staging with Cleric's unlock already complete (Hateborne, 2026-09-03).
    # PREDICTED: the Ready view's CLR — Necklace of Resolution row carries "Cleric
    # already unlocked — turn in for the item only" and the WAR row does not.
    'sky-ready-unlocked' = @{ Title = 'Quest Tracker'
                           Env = @{ EQBUDDY_QUESTS = 'sky:ready' }
                           Ledger = @{ Classes = @('Warrior', 'Cleric')
                                       UnlockedClasses = @('Cleric') }
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
    # The collapsed HUD with EVERY cell up — the only way to see all the icons at once,
    # and the surface that is on screen for the whole session. Its icons were glyphs
    # until Gate 5c; a glyph that fails to render is a blank here and nowhere else.
    #
    # PREDICTION, rewritten BEFORE the re-shoot (trap 23), for Surface A / SA-1. The seed
    # below still names all ten keys on purpose — it is the pre-promotion profile, which
    # is what most players are updating FROM — and AppSettings.MigratePromotedHudStats
    # strips three of them on load. So expect, left to right:
    #   * the ALWAYS-ON TRIO first: the character name slot ("Testchar"), a Swords + dps
    #     reading, and a Chart + %/hr reading. The fixture session is melee, so the third
    #     slot is the XP rate and NOT hps.
    #   * then SEVEN starred cells in MiniBarPresentation.Order: kills, pet, procs, loot,
    #     motes, money, deaths. dps, hps and xp are NOT among them — they are the trio now,
    #     and a duplicate of any of the three is the bug this prediction exists to catch.
    #   * hairline dividers between all ten, none after the last.
    # The three metric slots are FIXED WIDTH (HudGlance), so the bar's width must not
    # change between takes of the same seed — a wobble there is trap 12 arriving.
    'mini-bar'        = @{ Title = 'EQBuddy'
                           Env = @{}
                           # Every breakout OFF: starring dps/hps/pet/loot while minimized
                           # is exactly what opens those windows, and the capture matches
                           # on title — so without this it photographs a breakout instead.
                           # EVERY kind in BreakoutKind, and the list has to grow with the
                           # enum: Progress joined it on 2026-08-19 and was not added here,
                           # so this shot silently stopped photographing the mini bar and
                           # started photographing the Progress breakout — same title, real
                           # window, wrong feature (trap 24). Re-running it would have
                           # overwritten a correct committed screenshot with that.
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
    # The AUTO-IMPORT REPORT on the Raids surface. It exists only in response to a dump the
    # game announced, so trap 22 applies at its purest: with no staged dump this surface has
    # NO state at all, and until 2026-08-22 it had no renderer either — LastAchievementsImport
    # was written and never read, in both UIs, so an achievements dump marked Sky rewards and
    # raid clears silently with no report and no Undo.
    #
    # Staged through the REAL seam, not a back door: a dump file where the game writes them
    # (game/, the Logs folder's parent) plus the announcement line the log carries, so the
    # widget's own tail-parse-import path is what produces the picture.
    #
    # PREDICTION, written before the shot (trap 23). The dump names three things and each
    # exercises a different arm of the report:
    #   * Cleric — Primary Class Unlock, COMPLETE, with the "will autocomplete" criterion
    #     also complete. Granted, not earned, so its two Obtains prove nothing: SKIPPED = 2.
    #   * Warrior — Class Unlock, INCOMPLETE, so its per-criterion flags are trustworthy.
    #     "Azure Ruby Ring" is a real Warrior reward: MARKED = 1 (Apply counts REWARDS).
    #   * "Windblade of the Sky" is a reward no class has. It cannot fuzzy-match "Pauldrons
    #     of the Blue Sky" (neither "windblade" nor "pauldrons" finds a partner):
    #     UNRECOGNIZED = 1.
    # No Conqueror section, so RaidsMarked = 0 and the two seeded clears are untouched.
    # Expect, under the boss rows and the ⧉ copy button, ONE wrapped line in the warning ink:
    #   "Read your achievements dump (HH:mm) — 1 Sky reward marked. 2 rewards were skipped —
    #    the class unlock that flagged them was granted, not earned. 1 obtained reward
    #    matched nothing on the checklist — Import achievements names it."
    # and an Undo button beneath it, because one reward really was written.
    #
    # SHOT 2026-08-22: all three counts as predicted. The FIRST take also proved its own
    # worth on the copy rather than the code — it read "1 obtained reward … names them",
    # because the plural was baked into the string. Nothing but a picture was ever going to
    # catch that; the unit test asserted the same wrong sentence quite happily.
    #
    # RE-SHOT 2026-08-23 after Bevel's ruling (Helm-signed): the three sentences became ONE
    # counted line — "1 Sky reward marked · 2 skipped · 1 unmatched" — with the reasons on
    # hover. Predicted and confirmed. Expect a SCROLLBAR now and the footer (provenance note
    # + ⧉ copy) below the fold: 21 boss rows plus a report exceed the card's height cap, so
    # the scroller is correct rather than a regression. What matters is what holds the TOP,
    # and that is the report and its Undo (trap 44). A tooltip cannot be photographed, so
    # the hover half is asserted in OutputfileAutoImportTests, not here.
    'raids-import'    = @{ Title = 'EQBuddy Progress'
                           Env = @{ EQBUDDY_PROGRESS = 'raids' }
                           Set = @{}
                           Dump = @{ 'Testchar_test-Achievements.txt' = @(
                               'Untapped Potential: Classes'
                               "C`tPrimary Class Unlock - Cleric"
                               "C`t`tObtain Aegis of the Wind."
                               "C`t`tObtain Baton of the Sky."
                               "C`t`tThis achievement will autocomplete if you chose to confirm your Primary Class as a Cleric."
                               "I`tClass Unlock - Warrior"
                               "C`t`tObtain Azure Ruby Ring."
                               "C`t`tObtain Windblade of the Sky."
                           ) }
                           Append = @('Outputfile Complete: Testchar_test-Achievements.txt')
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
    # The collapsed HUD as the quick tour's page describes it — the few stats you picked,
    # plus watch-rule chips — rather than as mini-bar shoots it, which is every cell up so
    # all the icons can be reviewed at once. Two stats and two PINNED rules: pinning is
    # what puts a rule on the bar, so without it the chips the sentence promises are absent
    # and the picture quietly contradicts the words beside it.
    #
    # PREDICTION, rewritten before the re-shoot (SA-1): the always-on trio (name, dps,
    # %/hr), then TWO starred cells — kills and loot — because the seed's third key, dps,
    # is now the trio's own second slot and the migration strips it. Then the two watch
    # chips, Motes and Ghouls. Seven cells; a bar with a dps reading TWICE on it is the
    # failure this names in advance.
    #
    # THE PIN IS SEEDED EXPLICITLY, and it had to be. This shot's chips were being
    # produced by a BUG, not by its staging: `Write-Settings` sets `WatchPinsMigrated`,
    # so `WatchPinMigration` skips and `PinWatchChips` stays at its default false — but
    # until 2026-08-31 the "any per-rule pin turns on the group pin" line sat ABOVE that
    # gate and ran every launch, which is #253 (HiramDucky) itself. `9b7f4daf` moved it
    # inside the gate, and from that day this shot's two chips were gone and nobody
    # noticed, because the committed PNG was last taken on 2026-08-24. Trap 22 exactly:
    # a surface with no fixture state photographs as an unremarkable bar, and the
    # sentence beside it in the tour goes on promising chips.
    'mini-tour'       = @{ Title = 'EQBuddy'
                           Env = @{}
                           Set = @{ Minimized = $true
                                    DisabledBreakouts = @('Damage','Healing','Pet','Watch','Loot','Buffs')
                                    MiniStats = @('kills','dps','loot')
                                    PinWatchChips = $true
                                    TrackedRules = @(
                                        @{ Id = 'shot-mote'; Name = 'Motes'
                                           Pattern = 'mote'; Kind = 0; Pinned = $true }
                                        @{ Id = 'shot-ghoul'; Name = 'Ghouls'
                                           Pattern = 'ghoul'; Kind = 1; Pinned = $true }
                                    ) } }
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
    #
    # RE-SHOT 2026-08-23 for two changes: the summary block grew a motes line, and the
    # next-level preview grew per-class groups. PREDICTION, written before the run — the
    # fixture picks NO classes, so the class source is the combat-inferred one, and the
    # fixture infers WARRIOR (its ding at 12 is Heroic Leap + Unbound Wrath, both Warrior
    # Class AAs, which is what 'dingRows=2' has always been). From there:
    #   - the summary block gains ONE line, "1 mote * <rate>/hr" -- the fixture loots
    #     exactly one Mote of Infinitesimal Potential (line 1388), so the count is 1 and
    #     the rate is 1 over the shifted session length rather than a number to predict.
    #   - "New at level 12": Heroic Leap and Unbound Wrath, unchanged.
    #   - the preview reads "At level 15: 1 new AA ability" and now splits: a chevron-less
    #     "Warrior" heading over a dim "Nothing new at 15", then an OPEN "Any class" fold
    #     holding "Double Riposte / Archetype * 3 ranks". Warrior has no spell table at any
    #     level and the AA catalog's only level-15 row for it is class-agnostic, so this is
    #     the exact case DefaultOpenIndex exists for: opening group 0 would have put an
    #     empty heading above the collapsed group holding the single row.
    'progress-card'   = @{ Title = 'EQBuddy Progress'
                           Env = @{ EQBUDDY_PROGRESS = '1' }
                           Append = @('You have gained a level! Welcome to level 12!')
                           Set = @{ ShowNextUnlocks = $true; ShowAllAAs = $true } }
    # THE LEVEL-UPS FOLD (#240, joeymavity), UNFOLDED. A new name per trap 21 —
    # 'progress-card' is embedded in the docs and keeps meaning the old thing, and it will
    # now also carry the fold's FOLDED label, which is the default state and needs no shot
    # of its own.
    #
    # Trap 22 applies as hard as it does to 'history-window': the whole point of this
    # surface is that it survives a session roll, and a single live fixture session can
    # only ever show what the summary line above it already showed. So the store is primed
    # with THREE finished sessions, each shifted to its own day and carrying its own ding.
    #
    # AND THEY ARE PRIMED UNDER THE FIXTURE'S OWN CHARACTER, which no shot had needed
    # before. 'history-charts' primes as 'Aludra' because its charts take a character
    # FILTER; this surface takes the archiver's identity and compares it with SQL `=`, so
    # rows written under any other name are rows it can never match — the picture would be
    # a correct render of an empty fold, which is trap 23's failure mode exactly.
    #
    # PREDICTION, written before the run. The fixture session announces no level of its own
    # (tests/EQBuddy.E2E asserts precisely that before it appends one), so every row comes
    # from the store:
    #   - the heading reads "Level-ups" — unfolded, so the count moves onto the rows.
    #   - THREE rows, newest first: Level 24 (yesterday), Level 23 (two days ago),
    #     Level 22 (three days ago), each with a wall-clock stamp like "Aug 30, 7:14 PM".
    #   - NO third token on any row. The gap since the previous ding is hover text only
    #     (Bevel, Helm-signed 2026-09-02), so nothing on screen says "3h 20m" or "x ago".
    #   - the Skill-ups heading below stays DOWN — the fixture has no skill-ups — so the
    #     fold's own rows are the last thing in the body.
    'progress-levelups' = @{ Title = 'EQBuddy Progress'
                           Env = @{ EQBUDDY_PROGRESS = '1' }
                           Set = @{ ShowLevelUps = $true }
                           Prime = @(
                               @{ Character = 'Testchar'; Fraction = 0.35; ShiftDays = 3
                                  Lines = @('You have gained a level! Welcome to level 22!') }
                               @{ Character = 'Testchar'; Fraction = 0.65; ShiftDays = 2
                                  Lines = @('You have gained a level! Welcome to level 23!') }
                               @{ Character = 'Testchar'; Fraction = 0.9;  ShiftDays = 1
                                  Lines = @('You have gained a level! Welcome to level 24!') }
                           ) }
    # THREE classes at once, which is what a Legends character actually is (David,
    # 2026-08-23) and what 'progress-card' cannot show: the fixture infers one, and one
    # class draws no expanders at all. A NEW name per trap 21 -- 'progress-card' and
    # 'section-progress' are both committed and both still mean the old thing.
    #
    # It shoots the INLINE card, not the window, and that is a finding rather than a
    # preference: the Progress WINDOW restores to a height whose body scrolls, so
    # 'progress-card' has been photographing a panel cut off mid-summary -- above the ding
    # list and the preview it is named for -- since it was taken. The inline body fits in
    # about 175 units (see 'theme-inline-progress'), so it is the only host that can show
    # this feature at all. Fixing the window shot is its own job; a shot that cannot reach
    # the state reads as reviewed anyway (trap 22).
    #
    # NO LEVEL-UP APPEND, and that is the second version of this shot. The first announced
    # level 12 the way 'progress-card' does, which added a six-row ding list above the
    # preview and pushed the THIRD group under the inline body's 320-unit cap -- so the
    # committed picture ended at Expulse Summoned with no Monk in it, while the prediction
    # below said the two empty groups were the point (found by Fable 5, v1.99.6 review;
    # trap 44 -- the shot fitted once). The level is seeded on the LEDGER instead: the
    # preview only needs a level to be KNOWN, not announced, so this isolates the feature
    # and the whole split fits.
    #
    # PREDICTION, written before the run. Ledger classes Warrior/Druid/Monk (David's own
    # combination) and ledger level 12, with nothing appended to the log:
    #   - NO "New at level 12" block at all -- nothing dinged this session, and the ding
    #     list is what just happened rather than what is remembered.
    #   - the summary block is five lines and includes "1 mote * <rate>/hr" (the fixture
    #     loots exactly one Mote of Infinitesimal Potential, line 1388).
    #   - the preview reads "At level 13: 3 new spells" -- 13 rather than 15, because with
    #     Druid in the list the next level with anything is the Druid spell tier.
    #   - under it, THREE groups in the ledger's own order: a chevron-less "Warrior" over
    #     "Nothing new at 13", an OPEN "Druid" holding Befriend Animal, Expulse Summoned
    #     and See Invisible (each "Druid spell"), and a chevron-less "Monk" over "Nothing
    #     new at 13". Druid opens because it is the first group with anything in it.
    #   - and no scrollbar on the Progress body.
    # The two empty groups are the point of the shot: they are what a tidy-minded refactor
    # deletes, and on screen their absence is indistinguishable from those classes not
    # being yours.
    'progress-next-classes' = @{ Title = 'EQBuddy'
                           Env = @{ EQBUDDY_EXPAND = 'progress' }
                           Ledger = @{ Classes = @('Warrior', 'Druid', 'Monk'); Level = 12 }
                           Set = @{ ShowNextUnlocks = $true; ShowAllAAs = $true } }
    # THE ONE CHIP ROW (Surface A / SA-2) — the companion window that replaced
    # SpawnChipsWindow and MezChipsWindow. A NEW name, checked against docs/screenshots/ and
    # the docs first (trap 21): nothing embeds 'hud-chips', and the two existing chip images
    # (mini-pet-chip, widget-mini-chips) are about the collapsed HUD bar's cells, not these.
    #
    # It is the only surface in this file that cannot be staged from settings alone: with no
    # running countdown and no mez, the row does not exist at all and a capture would be of an
    # empty desktop (trap 22 at its purest). So both families are seeded — spawn through
    # `Timers` (spawn-timers.json, the app's own file and shape), mez through two appended log
    # lines the game itself writes.
    #
    # PREDICTION, written BEFORE the shot (trap 23). Expect ONE horizontal row of four
    # chicklets, left to right, in HudChipRow.DefaultOrder — the MEZ family first:
    #   1. 💤-moon "Skeleton" with a counting mm:ss and a DRAINING gauge along its bottom.
    #      One skeleton only, so no "(1)" suffix.
    #   2. ⏳-timer "Bones Brackins" reading the word DUE in the warn ink, warn border, and
    #      its gauge SOLID in the bad ink — the flip the spawn family has and mez does not.
    #   3. ⏳-timer "Fright" — 20 minutes into a 30-minute cycle, so about 10:00 left and a
    #      gauge two-thirds filled.
    #   4. ⏳-timer "Kizdean Gix" — 60 s into a 30-minute cycle, so a countdown near 29:00
    #      and a gauge FILLING from the left, barely started.
    # Chicklets are separated by 3 units horizontally, each with the 7-radius border and the
    # BgBrush ground. The row sits under the widget, left edges aligned; this capture is of
    # the ROW WINDOW alone, so the widget is not in frame.
    #
    # A gauge direction that is the same on all four, or a fourth chicklet reading a
    # countdown instead of DUE, is the fold flattening a difference the two windows had —
    # which is precisely what this picture exists to catch and what no diff would show.
    # SHOT 2026-09-05: four chicklets, mez first, exactly as above — EXCEPT that the
    # PREDICTION had the three spawn chips in SEED order and they come out SOONEST-FIRST
    # (Bones DUE, Fright 9:50, Kizdean 28:50). That is not a render bug and not a staging
    # bug: SpawnTimers.Snapshot has always ordered by DueAt, and HudChipRow.Merge preserves
    # each family's own order rather than imposing one. The prediction was written from the
    # seed list instead of from the family's order; corrected above rather than quietly
    # accepted (trap 23 — a mismatch is a fixture bug until proven otherwise, and this one
    # was proven a prediction bug in one read of Snapshot).
    'hud-chips'       = @{ Title = 'EQBuddy HUD Chips'
                           Env = @{}
                           Set = @{ TrackSpawns = $true; MezChipsEnabled = $true }
                           Timers = @(
                               @{ Zone = 'Runnyeye Citadel'; Name = 'Kizdean Gix'
                                  KilledSecondsAgo = 60; DurationSeconds = 1800 }
                               @{ Zone = 'Befallen'; Name = 'Bones Brackins'
                                  KilledSecondsAgo = 30; DurationSeconds = 10 }
                               @{ Zone = 'Lower Guk'; Name = 'Fright'
                                  KilledSecondsAgo = 1200; DurationSeconds = 1800 }
                           )
                           Append = @('You begin casting Mesmerization.'
                                      'a skeleton has been mesmerized.') }
    # SA-3's TWO NET-NEW FAMILIES on that same row — a watch rule that has just fired, and a
    # buff inside its expiry warning window. A NEW name, checked first (trap 21): nothing
    # embeds 'hud-chips-deadlines', and 'hud-chips' stays exactly as SA-2 shot it — that
    # picture is a reviewed record of the FOLD, and superseding it with a busier one would
    # spend a signed illustration to save a PNG.
    #
    # This is the only shot in the file that needs lines read while the app is RUNNING
    # (AppendLive, added with it). A watch rule staged the ordinary way is a rule that
    # correctly does nothing: the startup replay fires no banners, on purpose.
    #
    # PREDICTION, written BEFORE the shot (trap 23). ONE horizontal row of FIVE chicklets,
    # left to right, in HudChipRow.DefaultOrder — which is the first picture there has ever
    # been of all four families at once, and is the order SA-4's setting will default to:
    #   1. MEZ, moon: "Skeleton", counting mm:ss, gauge DRAINING.
    #   2. SPAWN, timer: "Bones Brackins" reading the word DUE in warn ink, warn border,
    #      gauge SOLID in the bad ink. Spawn chips are soonest-first, so the overdue one
    #      leads (the correction SA-2's own prediction earned).
    #   3. SPAWN, timer: "Kizdean Gix" near 28:5x, gauge FILLING and barely started.
    #   4. WATCH-FIRE, bell: "Assist call" — the RULE'S name, not the line it matched —
    #      counting its 30 s linger down, so about 0:21-0:23 at an 8 s settle, gauge
    #      DRAINING. The matched line is in the tooltip, which a capture cannot show.
    #   5. BUFF, hourglass: "Stalwart Regeneration" reading about "0:52 est" — the shortest
    #      buff in the shipped catalog is 60 s, so it lands INSIDE the DEFAULT 60 s warning
    #      window and this needs no cranked setting to exist. Gauge DRAINING, nearly full:
    #      it is measured against the WARNING WINDOW, not the spell. Cast by Sanctari rather
    #      than by You, so Spell Casting Reinforcement cannot lengthen the estimate and make
    #      the number here depend on the fixture's AAs.
    # Two chicklets reading DUE, or a bell and a timer drawn as the same shape, is the
    # net-new half failing exactly where #148/#166 says it fails — and no diff shows it.
    # SHOT 2026-09-05, 694x32: five chicklets, five DISTINCT vectors, in the predicted order
    # and with every number inside its predicted range — "Skeleton 0:13", "Bones Brackins
    # DUE" (warn ink, warn border), "Kizdean Gix 28:50", "Assist call 0:22", "Stalwart
    # Regeneration 0:51 est". The prediction is recorded unamended because nothing needed
    # amending, which has not been true of the last three shots added to this file.
    'hud-chips-deadlines' = @{ Title = 'EQBuddy HUD Chips'
                           Env = @{}
                           Set = @{ TrackSpawns = $true; MezChipsEnabled = $true
                                    TrackedRules = @(
                                        @{ Id = 'shot-assist'; Name = 'Assist call'
                                           Pattern = 'assist on'; Kind = 6; AlertBanner = $true }
                                    ) }
                           Timers = @(
                               @{ Zone = 'Runnyeye Citadel'; Name = 'Kizdean Gix'
                                  KilledSecondsAgo = 60; DurationSeconds = 1800 }
                               @{ Zone = 'Befallen'; Name = 'Bones Brackins'
                                  KilledSecondsAgo = 30; DurationSeconds = 10 }
                           )
                           Append = @('You begin casting Mesmerization.'
                                      'a skeleton has been mesmerized.')
                           AppendLive = @(
                               'Sanctari begins casting Stalwart Regeneration.'
                               'Your feet anchor to the ground as you begin to regenerate.'
                               "Sanctari tells the group, 'assist on a froglok tad shaman'") }
    'spawns-window'   = @{ Title = 'EQBuddy World'; Env = @{ EQBUDDY_SPAWNS = 'Runnyeye Citadel' }; Set = @{ TrackSpawns = $true } }
    # Plane of Sky's triggered spawns (#109 follow-up; FABLE.md). A NEW name — trap 21:
    # 'spawns-window' is embedded by the docs and stays Runnyeye. PREDICTION, written
    # before the shot: Bzzzt, Bazzt Zzzt, The Spiroc Guardian and The Spiroc Lord read
    # "triggered" in dim ink with NO track and an empty duration box; their tooltips name
    # the trigger. Every boss in the raid-target list (Noble Dojorn, Thunder Spirit
    # Princess, Eye of Veeshan, ...) reads "instance" with an EMPTY box -- the first
    # prediction here said "7d"/"6h" and was wrong, because RaidInstanced blanks the
    # default (trap 23: the render was right, the prediction was not). Only the four
    # catalog names outside that list (a presence, Gwan, Key Master, Sirran) read "8h".
    # 2026-09-02 re-shoot (World title fix): held, with the enumeration above corrected -
    # Bzzazzt is a fourth non-triggered, non-instanced row and reads 12h, not 8h. The
    # catalog did not change; the prediction was written short.
    # Nothing is seeded: the rows ARE the shipped catalog, which is the point.
    'spawns-sky'      = @{ Title = 'EQBuddy World'; Env = @{ EQBUDDY_SPAWNS = 'Plane of Sky' }; Set = @{ TrackSpawns = $true } }
    'options-window'  = @{ Title = 'Options'; Env = @{ EQBUDDY_OPTIONS = '1' }; Set = @{} }
    # Options → Cards & windows, which is the screen a player opens when a card has gone
    # missing — #219 (typical-usual-chaos) went looking for Motes here and found nothing
    # saying where it went. The "… are tabs in here now" lines under the folded cards only
    # exist on this tab, and the tab is a SETTING rather than a hook, so it has to be
    # staged or the shot photographs "Look" and proves nothing.
    # Options -> Alerts & chips, scrolled to the mez-duration editor. A row that is a
    # label, a box and a source line is exactly the shape traps 14 and 19 bite: a name
    # clipped against its box, or a heading that resolves to nothing and renders as body
    # text. Neither shows in a diff or a render test.
    # Zoomed out, because the rows sit below the fold of a tab this long and the window
    # scrolls: at 100% the shot is a picture of the buff-set editor above them.
    'options-mez'     = @{ Title = 'Options'
                           Env = @{ EQBUDDY_OPTIONS = '1' }
                           Set = @{ OptionsTab = 'alerts'
                                    WindowZooms = @{ options = 0.55 } } }
    # PREDICTION since 2026-09-05 (HUD subtraction cuts 1 and 2): EIGHT rows in the card
    # list - Combat, Healing, Kills & Drops, Gear & Loot, Watch, Buffs, Progress, Motes -
    # with no "Quests" and no "World" among them, and none of the four notes those two
    # carried: no "Sky Quest / Epics are tabs in here now", and no "Travels & Deaths / Zone
    # map / Travel route / Spawn timers are tabs in here now". A note hangs under the
    # SURVIVING card and there is none in either case.
    #
    # This screen is the one #219 was filed from, so it is the picture that says out loud
    # what the two cuts cost: someone hunting for any of those six names finds no row here
    # at all. Recorded in HELM-FEEDBACK.md rather than papered over - and cut 2's half is
    # the bigger one, four names against two.
    #
    # AMENDED SA-3 (2026-09-05), and the amendment IS the re-shoot that was owed. The two
    # paragraphs above describe a gap that has been CLOSED: #335/#336 landed Bevel's
    # Options-gap ruling (I-11 section 4), so the six names are on this screen again, under a
    # "No longer on the widget" heading below the card rows - "Sky Quests / Epics ... are
    # tabs in the Quest tracker now", "Travels & Deaths / Zone map / Travel route / Spawn
    # timers ... are tabs in the World window". The committed PNG predated that and could not
    # show it, which is precisely why the re-shoot was owed rather than optional.
    # SHOT 2026-09-05, 420x490: eight card rows exactly as predicted (Combat, Healing, Kills &
    # Drops, Gear & Loot, Watch, Buffs, Progress, Motes), no Quests row, no World row, and the
    # retired list present with both sentences. The rest of the tab is unchanged.
    'options-cards'   = @{ Title = 'Options'
                           Env = @{ EQBUDDY_OPTIONS = '1' }
                           Set = @{ OptionsTab = 'cards'
                                    WindowZooms = @{ options = 0.55 } } }
    # The quick tour itself, page by page. Its five illustrations went a month out of date
    # showing an app that no longer existed — emoji card icons, a card called "Tracked",
    # no KPI strip — and nothing caught it, because seeing page 4 meant installing the app
    # and clicking Next three times. These are what make the tour reviewable at all; shoot
    # them whenever an image under Assets/tutorial changes.
    'tour-widget'     = @{ Title = 'Welcome to EQBuddy'; Env = @{ EQBUDDY_TOUR = '2' }; Set = @{} }
    'tour-combat'     = @{ Title = 'Welcome to EQBuddy'; Env = @{ EQBUDDY_TOUR = '4' }; Set = @{} }
    'tour-watch'      = @{ Title = 'Welcome to EQBuddy'; Env = @{ EQBUDDY_TOUR = '5' }; Set = @{} }
    'tour-mini'       = @{ Title = 'Welcome to EQBuddy'; Env = @{ EQBUDDY_TOUR = '7' }; Set = @{} }
    'tour-history'    = @{ Title = 'Welcome to EQBuddy'; Env = @{ EQBUDDY_TOUR = '8' }; Set = @{} }
    # The GEAR & LOOT theme's window, one shot per tab. Trap 22 on the gear one: the
    # shared fixture imports no gear list, so without seeding it that tab is a one-line
    # empty state and the shot proves nothing about the rows.
    'gearloot-loot'   = @{ Title = 'Gear & Loot'
                           Env = @{ EQBUDDY_GEARLOOT = 'loot' }
                           Set = @{} }
    # #250 PR 2 re-shot this. PREDICTION, written before the run: the gear list stops
    # carrying its own hard 320 and takes the WINDOW's cap instead, so at the design opening
    # height (400) the list gets 306 and the 94 units of pinned chrome below it — the
    # auto-tick note, the ⧉ copy of /outputfile inventory, and the import report — fit
    # INSIDE the window body rather than pushing the panel past it. So: no outer scrollbar
    # on the window, and the ⧉ copy visible without scrolling, which is the affordance
    # trap 34 has a must-list row for on this very surface. Five seeded rows nowhere near
    # either cap, so the ROWS themselves must be identical to the committed shot — a
    # changed row set would mean this is a picture of something else (trap 23).
    'gearloot-gear'   = @{ Title = 'Gear & Loot'
                           Env = @{ EQBUDDY_GEARLOOT = 'gear' }
                           Set = @{
                               GearChecklistName = 'Kael push'
                               GearChecklist = @(
                                   @{ Slot = 'HEAD'; Item = 'Crown of Narandi'; Source = 'Kael Drakkel' }
                                   @{ Slot = 'HANDS'; Item = 'Gloves of Dark Embers'; Source = 'Sebilis'; Acquired = $true }
                                   @{ Slot = 'PRIMARY'; Item = 'Blade of Carnage'; Source = 'Kael Drakkel' }
                                   @{ Slot = 'NECK'; Item = 'Silver Chain of Dread'; Source = 'Plane of Fear' }
                                   @{ Slot = 'HEAD'; Item = 'Exquisite Velium Shard'; IsExaltation = $true
                                      ExaltationEffect = '+15 hp'; Source = 'Kael Drakkel' }
                               ) } }
    # The EMPTY gear tab, which is the state David was actually looking at on 2026-08-20
    # when he said the surface "is telling me to import it but not telling me how or giving
    # me the tool with which to do it". Trap 22 says a surface with no fixture state cannot
    # be reviewed - but here the empty state IS the state under review, and it is the only
    # one a new player ever sees. So it gets its own shot rather than being the accident of
    # an unseeded profile: same tab, deliberately nothing seeded, and the review question is
    # whether both routes out of it are legible (the shopping-list import, and the in-game
    # command that makes the ticks happen by themselves).
    # The INVENTORY tab - the Gear Locker and Inventory windows merged into it (David,
    # 2026-08-20: "we should at least put our gear locker into
    # this window so Gear and Loot can complete a theme"). It reads the real inventory
    # dump from the game folder, which the throwaway profile does not have - so this
    # shot photographs the no-dump state, which is a REAL state and the one a new player
    # meets. Predicted before running: the recipe line, the copy button, no slot groups.
    'gearloot-inventory' = @{ Title = 'Gear & Loot'
                           Env = @{ EQBUDDY_GEARLOOT = 'inventory' }
                           Set = @{} }
    'gearloot-gear-empty' = @{ Title = 'Gear & Loot'
                           Env = @{ EQBUDDY_GEARLOOT = 'gear' }
                           Set = @{} }
    # The post-update popup, which no shot covered until 2026-08-20 - and it is the ONE
    # surface every player sees on every release, on every platform. It opens by itself
    # when LastSeenVersion differs from the running build, so the fixture just has to lie
    # about which version was last seen; there is no env hook and it does not need one.
    #
    # 1.96.1 rather than "the previous release", deliberately: it makes the popup render
    # BOTH shipped versions, so a shot shows the MOVED badge next to ordinary bullets
    # rather than in isolation. A badge photographed alone proves it draws; a badge
    # photographed beside a bullet proves it reads as different (David, 2026-08-20).
    'whats-new'       = @{ Title = "What's new in EQBuddy"
                           Env = @{}
                           Set = @{ LastSeenVersion = '1.96.1' } }
    'zone-map'        = @{ Title = 'EQBuddy World'; Env = @{ EQBUDDY_MAP = '1' }; Set = @{} }
    # THE TRAVELS TAB, which had no recipe until 2026-09-05 and did not need one: it was
    # the one World room the WIDGET drew, on the misc card, so EQBUDDY_EXPAND=1 put it in
    # 'widget-expanded' for free. HUD subtraction cut 2 removed that card, which would have
    # left the surface unphotographable and therefore unreviewable-but-looking-reviewed
    # (trap 22) - so EQBUDDY_WORLD landed with the cut and this row landed with the hook.
    # It is also the illustration lock working the way it is supposed to: a capture arrives
    # WITH its recipe, in the same change.
    #
    # PREDICTION: the World window, native chrome reading "World", a four-chip strip (Map
    # badged with the fixture's last zone, Camps, Path, Travels - Travels lit), and under it
    # the Travels body: a "Deaths" heading with no rows (the fixture has no death line), a
    # "Zones visited" heading over SIX rows with times - the replay zones Befallen / West
    # Commonlands / Befallen / West Commonlands / East Commonlands / West Commonlands - and
    # NO markers heading at all (MarkersLabel collapses when the list is empty). Pinned
    # BELOW the body, exactly once: the "Drop camp marker" action row with the "Show in mini
    # dashboard" star and its label, which appear on the Travels tab alone - that star is
    # the only writer MiniStats has for 'deaths', and this shot is the only picture of it.
    #
    # TWO PREDICTION MISSES ON THE FIRST RUN, and the second is why this row was worth
    # adding at all. (a) "Befallen and West Commonlands" undercounted: the fixture zones
    # SIX times, not twice, and the number came from a doc line rather than from the log -
    # trap 23's rule is to derive the prediction, and a phrase copied from another comment
    # is not a derivation. (b) "Drop camp marker" appeared TWICE, once inside the scroller
    # and once pinned. That was not new: `TravelsView` inserted its own copy at the top of
    # the body FOR THE INLINE CARD - its own doc comment said so - while both surviving
    # hosts pin one as chrome. It had rendered twice since the World fold in a window no
    # committed illustration had ever photographed. The in-body copy went with the card.
    'world-travels'   = @{ Title = 'EQBuddy World'; Env = @{ EQBUDDY_WORLD = '1' }; Set = @{} }
    # The KILLS & DROPS theme (2026-08-21). Both were reachable before the fold — one as
    # a widget card, one as a cog-menu window — and both are tabs now, so both get a shot:
    # a tab nobody photographs is a tab nobody reviews (trap 22).
    #
    # THE FILENAME STAYS 'drops-window' even though the window did not. README.md embeds
    # docs/screenshots/drops-window.png, and a shot name IS a filename (trap 21) — renaming
    # it here would leave a broken image in the README and a stale PNG in the repo. The
    # TITLE had to change, because that is what the capture matches on.
    # The wiki re-check ↻ and its freshness caption on every creature heading (#226).
    # EVERY creature the fixture drops from is seeded, the same set as 'wiki-pack' below,
    # because the shot is NOT offline: the first run seeded two pages and predicted
    # "wiki not read yet" for the rest — and the app, correctly, fetched the rest live and
    # captioned them "just now". A real state, a correct render, and a picture of the
    # wrong fixture (trap 23). Seeding all of them is what makes the shot deterministic.
    #
    # PREDICTION, written before the shot: every heading reads "wiki read just now" with a
    # DIM ↻ (inside the 30 s rule) EXCEPT Skeleton, seeded five days old, which reads
    # "wiki read 5d ago" with a live ↻. Still inside the 7-day lifetime on purpose: older
    # than that is expired, re-fetched live, and "just now" again.
    'drops-window'    = @{ Title = 'Kills & Drops'
                           Env = @{ EQBUDDY_CREATURE = 'drops' }; Set = @{}
                           Wiki = $DropsFixtureWiki }
    'creature-kills'  = @{ Title = 'Kills & Drops'
                           Env = @{ EQBUDDY_CREATURE = 'kills' }; Set = @{} }
    # The quick tour's last page illustrates this window. Trap 22 applies hard: history
    # rows come from FINISHED sessions, and make-test-session.ps1 deliberately compresses
    # every idle gap so the fixture is ONE live session — so an unseeded profile shows an
    # empty list and a shot of it says nothing about the surface. Pre-runs below.
    'history-window'  = @{ Title = 'Session History'
                           Env = @{ EQBUDDY_HISTORY = '1' }
                           Set = @{}
                           Prime = @(
                               @{ Character = 'Aludra'; Fraction = 0.45 }
                               @{}
                           ) }
    # The cross-session level/AA charts. They render ONLY with a single-character filter
    # and NO session selected, so EQBUDDY_HISTORY=charts exists to reach that state.
    #
    # THREE primed sessions for ONE character, each shifted to its own day and carrying its
    # own ding. The shift is what makes them three: SessionRepository adopts on
    # (Server, Character, StartUtc), so same-fixture slices share a start and collapse to
    # one row no matter how their content differs. Fully real ingest — parse, SessionStats,
    # exit-checkpoint — the same path every other shot drives. Each run also carries an AA
    # total, because the surface draws TWO charts and a shot of one of them would quietly
    # under-report what the panel does (README's caption promises "level and AA charts").
    'history-charts'  = @{ Title = 'Session History'
                           Env = @{ EQBUDDY_HISTORY = 'charts' }
                           Set = @{}
                           Prime = @(
                               @{ Character = 'Aludra'; Fraction = 0.35; ShiftDays = 3
                                  Lines = @('You have gained a level! Welcome to level 22!',
                                            'You have gained an ability point!  You now have 3 ability points.') }
                               @{ Character = 'Aludra'; Fraction = 0.65; ShiftDays = 2
                                  Lines = @('You have gained a level! Welcome to level 23!',
                                            'You have gained 3 ability point(s)!  You now have 6 ability point(s).') }
                               @{ Character = 'Aludra'; Fraction = 0.9;  ShiftDays = 1
                                  Lines = @('You have gained a level! Welcome to level 24!',
                                            'You have gained 3 ability point(s)!  You now have 9 ability point(s).') }
                               @{}
                           ) }
    # The fight timeline, from the fixture log's own fights — the EQBUDDY_TIMELINE hook
    # existed (drag-verify uses it) and no shot ever did, so README's fight-timeline.png
    # was a hand-taken one-off nobody could regenerate.
    'fight-timeline'  = @{ Title = 'EQBuddy fight timeline'
                           Env = @{ EQBUDDY_TIMELINE = '1' }
                           Set = @{} }
    # Options → Behavior: the tab that answers "why is EQBuddy doing/not doing X", and as
    # of #238 the home of the Alt+Tab opt-out with its taskbar-cost warning. Zoomed out
    # like its siblings — the tab is one of the two longest and at 100% the shot is a
    # picture of its top third.
    'options-behavior' = @{ Title = 'Options'
                            Env = @{ EQBUDDY_OPTIONS = '1' }
                            Set = @{ OptionsTab = 'behavior'
                                     WindowZooms = @{ options = 0.55 } } }
    # The "Review which session?" picker (#74): shows only for an archive holding MORE
    # than one session, which the fixture log never does — so the shot stages a
    # three-session archive (the fixture concatenated with day-shifted copies of itself;
    # sessions split on a 60-minute gap, so day shifts are unambiguous). The file lives
    # OUTSIDE the Logs folder on purpose: an extra eqlog in there with patched stamps
    # could become the newest log and hijack what the app tails (trap 24's shape).
    'session-picker'  = @{ Title = 'Review which session?'
                           Set = @{}
                           ReviewSessions = 3 }
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
                           # The pack POOLS history (#217 ask 2), so the shot stages two
                           # stored sessions (fixture slices, day-shifted) under a second
                           # character - the scope line must read "across 3 sessions -
                           # Aludra and Testchar on test" and the per-creature kill counts
                           # must exceed the live session's own, or the picture is of the
                           # old single-session pack wearing the new chrome.
                           Prime = @(
                               @{ Character = 'Aludra'; Fraction = 0.4; ShiftDays = 2 }
                               @{ Character = 'Aludra'; Fraction = 0.7; ShiftDays = 1 }
                               @{}
                           )
                           # Three agreeing camped cycles for the Asp (its page below is
                           # complete, so the timer IS the contribution): the respawn row
                           # must read "observed 12.3 min over 3 agreeing cycles" beside
                           # the rare row, and the headline must count both facts.
                           Cycles = @{
                               'test|West Commonlands|Asp' = @(
                                   @{ DurationSeconds = 738; Kind = 'Rekill'; At = '2026-08-24T19:00:00' }
                                   @{ DurationSeconds = 744; Kind = 'Rekill'; At = '2026-08-24T19:15:00' }
                                   @{ DurationSeconds = 731; Kind = 'Sighting'; At = '2026-08-25T19:00:00' }
                               )
                           }
                           # The rare-only row (Bevel's kind): the Asp's page below is
                           # COMPLETE, so without these two cons it contributes nothing —
                           # which used to be the bug. One plain con and one rare con, so
                           # the row reads "rare on 1 of 2 /considers" rather than the
                           # degenerate one-con wording. The line shape is bjstrange's
                           # verbatim #185 evidence with the fixture's own creature.
                           Append = @(
                               'an asp scowls at you, ready to attack -- looks like quite a gamble. (Lvl: 19)',
                               'an asp - a rare creature - scowls at you, ready to attack -- looks like quite a gamble. (Lvl: 19)')
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

# The fixture log exactly as make-test-session wrote it. Every shot is restored to this
# BEFORE its own appends, because the log is shared by all 50 shots and Append-Log is
# cumulative — which made shots ORDER-DEPENDENT and the committed PNGs a function of
# which shots had run before them.
#
# Found 2026-08-24: `progress-card` came back 520x497 in a full run and 520x389 shot on
# its own, twice each, on identical code. Two different shots append "Welcome to level
# 12!", so in a batch the Progress ding list had TWO levels in it and the card grew. Both
# pictures are of a real state; only one is of the state the shot is about. That is trap
# 23's failure mode reached through the harness rather than through the staging, and it
# quietly made `shoot.ps1` unusable as the acceptance criterion CLAUDE.md relies on: a
# reviewer re-shooting one image to check a change would get a different picture than the
# batch that committed it, and read the difference as their own regression.
$pristineLog = Get-ChildItem -Path $logsDir.FullName -Filter 'eqlog_*.txt' | Select-Object -First 1
if (-not $pristineLog) { throw "make-test-session wrote no fixture log to $($logsDir.FullName)" }
$pristineCopy = Join-Path $root 'fixture-pristine.txt'
Copy-Item $pristineLog.FullName $pristineCopy -Force

# Extra log lines for one shot, stamped NOW so the replay treats them as the newest
# events. Some surfaces exist only in response to a line the shared fixture does not
# carry — the Progress card's ding list needs "Welcome to level N" — and the fixture
# CANNOT simply gain one: tests/EQBuddy.E2E replays the same file, and one E2E case
# asserts that the ding list is absent BEFORE it appends its own level-up. Per-shot
# appends give a shot the state it needs without making the fixture lie to a test.
function Add-LogLines([string[]]$lines) {
    if (-not $lines -or $lines.Count -eq 0) { return }
    $log = Get-ChildItem -Path $logsDir.FullName -Filter 'eqlog_*.txt' | Select-Object -First 1
    if (-not $log) { throw "No fixture log to append to in $($logsDir.FullName)" }
    # The game's own stamp shape, e.g. [Mon Jul 20 19:03:34 2026].
    $stamp = (Get-Date).ToString("[ddd MMM d HH:mm:ss yyyy]", [Globalization.CultureInfo]::InvariantCulture)
    foreach ($line in $lines) { Add-Content -Path $log.FullName -Value "$stamp $line" -Encoding utf8 }
}

function Append-Log([string[]]$lines) {
    $log = Get-ChildItem -Path $logsDir.FullName -Filter 'eqlog_*.txt' | Select-Object -First 1
    if (-not $log) { throw "No fixture log to append to in $($logsDir.FullName)" }
    # Unconditional, and BEFORE the early return: a shot with no appends of its own must
    # still be given a clean log, or it inherits the previous shot's level-ups.
    Copy-Item $pristineCopy $log.FullName -Force
    Add-LogLines $lines
}


# Prefer the secondary display for fixture windows so overnight shoot/E2E does not cover
# EQ on the primary. Falls back to 120,120 when only one screen is attached (CI).
# Same AllScreens pick as the backdrop below — one function, so the grey and the
# windows cannot land on different monitors.
function Get-EqShotSecondaryScreen {
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
    return [System.Windows.Forms.Screen]::AllScreens | Where-Object { -not $_.Primary } | Select-Object -First 1
}
function Get-EqShotOrigin {
    $sec = Get-EqShotSecondaryScreen
    if ($sec) {
        return @{ Left = [int]($sec.WorkingArea.X + 120); Top = [int]($sec.WorkingArea.Y + 120) }
    }
    return @{ Left = 120; Top = 120 }
}
function Write-Settings([hashtable]$extra) {
    $s = @{
        LogFolder    = $logsDir.FullName
        UpdateFolder = $updateDir.FullName
        Theme        = $Theme
        WindowLeft   = (Get-EqShotOrigin).Left
        WindowTop    = (Get-EqShotOrigin).Top
        QuestsLeft   = (Get-EqShotOrigin).Left
        QuestsTop    = (Get-EqShotOrigin).Top
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

# An /outputfile dump sitting where the game writes them: the Logs folder's PARENT, which
# is what OutputfileAutoImport.ResolvePath looks at. Paired with an Append line announcing
# it, this is the only way to photograph the auto-import REPORT — the surface that exists
# solely in response to a dump, and the one that shipped unreachable on 2026-08-20 because
# nothing rendered it (see ImportReportReachesASurfaceTests).
#
# **IT CLEARS FIRST, UNCONDITIONALLY AND BEFORE THE EARLY RETURN.** Staging here used to be
# additive only, so a dump written for one shot sat in the game folder for every shot after
# it — trap 51's cumulative-staging failure with an /outputfile dump in place of a log
# append, and the damage is the same shape: a picture that depends on which shots ran
# before it, correct for the state that was actually there and not for the state the shot
# is about. It went unnoticed because the only three dump-staging shots were near the END
# of the table and each wrote the one it needed. E-3 PR 4's Home shots are the first pair
# near the TOP, and an inventory dump leaking forward auto-ticks the wishlist that
# `shell-gear-narrow` photographs — a committed screenshot changing because a shot forty
# rows earlier gained a fixture. Reset is the contract, not an optimisation.
function Write-Dump([hashtable]$dump) {
    # Only the game folder itself, never its Logs child: the fixture log lives down there
    # and is restored by its own path.
    Get-ChildItem (Join-Path $root 'game') -Filter 'Testchar_*.txt' -File `
        -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    if ($null -eq $dump) { return }
    foreach ($file in $dump.Keys) {
        Set-Content -Path (Join-Path $root "game/$file") -Value $dump[$file] -Encoding UTF8
    }
}

# The wiki page cache, which is where the contribution pack's state actually comes from
# (EqlWikiMobService's 7-day disk cache, under <profile>/wiki-cache/mobs). A seeded entry
# is served without a fetch, so the shot is offline and deterministic; an unseeded
# creature would go to the live wiki and photograph whatever it says today.
#
# Format is the service's own CacheEntry: Title, Wikitext, FetchedAt. Drops are the
# wiki's {{:Item}} transclusions, which is what its parser reads.
function Write-Cycles([hashtable]$cycles) {
    $path = Join-Path $profileDir 'spawn-cycles.json'
    if ($null -eq $cycles) { Remove-Item $path -Force -ErrorAction SilentlyContinue; return }
    # The ledger's own shape: "server|zone|name" -> [{DurationSeconds, Kind, At}].
    $cycles | ConvertTo-Json -Depth 6 | Set-Content $path -Encoding utf8
}

function Write-Timers([array]$timers) {
    $path = Join-Path $profileDir 'spawn-timers.json'
    if ($null -eq $timers) { Remove-Item $path -Force -ErrorAction SilentlyContinue; return }
    # SpawnTimers.LoadPersisted's own shape: a list of SpawnTimerState.
    #
    # Server is 'test' — the FIXTURE CHARACTER'S server, and the whole staging turns on it.
    # LogWatcher assigns Spawns.Server from the character log it selects, and
    # SpawnTimers.Snapshot FILTERS on that value: seeded with anything else the timers load,
    # survive every purge, and are filtered out of every snapshot, so the row simply does not
    # appear. That is trap 23 exactly — a real state, and a picture of a different one.
    # Ages are relative to now so a countdown is always mid-cycle at capture time.
    $now = Get-Date
    @($timers | ForEach-Object {
        [pscustomobject]@{
            Server = 'test'
            Zone = $_.Zone
            Name = $_.Name
            KilledAt = $now.AddSeconds(-$_.KilledSecondsAgo).ToString('o')
            DurationSeconds = $_.DurationSeconds
        }
    }) | ConvertTo-Json -Depth 4 -AsArray | Set-Content $path -Encoding utf8
}

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
        # A value is either the loot list, or @{ Loot = @(...); AgeDays = N } to stage a
        # page read N days ago — how the Drops tab's freshness caption (#226) gets a
        # state worth photographing. Keep it INSIDE the 7-day lifetime: a page older than
        # that is expired, the app re-fetches it live, and the caption reads "just now" --
        # a real state, and a picture of the wrong one (trap 23).
        $entry = $pages[$title]
        $items = if ($entry -is [hashtable]) { $entry.Loot } else { $entry }
        $age = if ($entry -is [hashtable] -and $entry.AgeDays) { [int]$entry.AgeDays } else { 0 }
        $loot = (($items | ForEach-Object { "{{:$_}}" }) -join ' ')
        $wikitext = "{{Namedmobpage`n|name=$title`n|zone=Test Zone`n|known_loot=$loot`n}}"
        @{
            Title = $title
            Wikitext = $wikitext
            FetchedAt = (Get-Date).ToUniversalTime().AddDays(-$age).ToString('o')
        } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $dir $file) -Encoding UTF8
    }
}

# The force-stop, spelled once. `$proc.Kill($true)` (kill the tree) exists only on the
# .NET Core runtime pwsh 7 rides on — Windows PowerShell 5.1's .NET Framework has no
# such overload, so on a machine with only 5.1 every "fallback" below THREW instead of
# killing, the shot app outlived its shot, and the run wedged (Hateborne's machine,
# 2026-09-03). EQBuddy spawns no children, so a plain force-stop is the same act.
function Stop-Hard([Diagnostics.Process]$proc) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
}

# The graceful close, aimed at the WIDGET by name rather than at whatever
# `CloseMainWindow()` picks.
#
# `Process.MainWindowHandle` is "the first visible, unowned top-level window of the
# process" — a description that fitted exactly one window until E-3, and now fits two:
# ShellWindow sets no Owner, and both the prime runs below and David's own
# `install-local.ps1 -Evolved` copy have it open. Only the widget's OnClosed finalizes the
# session into history.db and calls Application.Current.Shutdown(); closing the shell
# instead leaves the app running, which costs a prime run its stored session (staged
# history that silently is not there — trap 23's shape) and costs the stand-down a hard
# kill of a real player's session.
#
# The widget's title is exactly "EQBuddy"; the shell's carries its room. Returns $false
# when no such window is up, so the caller can fall back rather than assume.
Add-Type -Namespace EqShot -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
public delegate bool EnumProc(IntPtr h, IntPtr l);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, ref int pid);
[DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
'@
function Close-EqWidget([Diagnostics.Process]$proc) {
    $hit = [IntPtr]::Zero
    $cb = [EqShot.Win+EnumProc]{ param($h, $l)
        if ([EqShot.Win]::IsWindowVisible($h)) {
            $owner = 0
            [EqShot.Win]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
            if ($owner -eq $proc.Id) {
                $sb = New-Object System.Text.StringBuilder 256
                [EqShot.Win]::GetWindowText($h, $sb, 256) | Out-Null
                if ($sb.ToString() -eq 'EQBuddy') { $script:hit = $h; return $false }
            }
        }
        return $true
    }
    [EqShot.Win]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    if ($hit -eq [IntPtr]::Zero) { return $false }
    [EqShot.Win]::PostMessage($hit, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null   # WM_CLOSE
    return $true
}

# THE WINDOW THIS SHOT IS ABOUT, found the way shot.ps1 finds it — the same enumeration,
# the same owner check, and the same exact-wins-over-substring rule, so the readiness wait
# below cannot answer "yes" about a window the capture will then refuse.
#
# It exists because that wait was satisfied by the WRONG window (see the loop), and because
# a failed capture carried no evidence: "no visible window matching 'Options'" says nothing
# about what the process DID have on screen, which is the one fact that separates "the hook
# never fired" from "the window was still coming" from "something closed my app underneath
# me". Ship the instrument before the third theory (traps 33, 49, 56).
#
# Returns the matched TITLE (a non-empty string is the truthy answer) or $null. Note the
# $script: prefixes: the callback runs at script scope, so a plain $exact here would be a
# local the delegate never writes — a helper that always answers $null, which is the exact
# shape of guard that reads as coverage while being blind (trap 34).
function Find-EqShotWindow([string]$titleLike, [int]$ownerPid) {
    $script:eqShotExact = $null
    $script:eqShotLoose = $null
    $cb = [EqShot.Win+EnumProc]{ param($h, $l)
        if ([EqShot.Win]::IsWindowVisible($h)) {
            $owner = 0
            [EqShot.Win]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
            if ($owner -eq $ownerPid) {
                $sb = New-Object System.Text.StringBuilder 256
                [EqShot.Win]::GetWindowText($h, $sb, 256) | Out-Null
                $t = $sb.ToString()
                if ($t -like "*$titleLike*") {
                    if ($t -eq $titleLike) { $script:eqShotExact = $t; return $false }
                    if ($null -eq $script:eqShotLoose) { $script:eqShotLoose = $t }
                }
            }
        }
        return $true
    }
    [EqShot.Win]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    if ($script:eqShotExact) { return $script:eqShotExact }
    return $script:eqShotLoose
}

# Every visible window one process owns, for the failure message. An empty list and a list
# of four windows that are all the wrong one are two different diagnoses and they used to
# print identically — as nothing at all.
function Get-EqShotWindowTitles([int]$ownerPid) {
    $script:eqShotTitles = @()
    $cb = [EqShot.Win+EnumProc]{ param($h, $l)
        if ([EqShot.Win]::IsWindowVisible($h)) {
            $owner = 0
            [EqShot.Win]::GetWindowThreadProcessId($h, [ref]$owner) | Out-Null
            if ($owner -eq $ownerPid) {
                $sb = New-Object System.Text.StringBuilder 256
                [EqShot.Win]::GetWindowText($h, $sb, 256) | Out-Null
                if ($sb.Length -gt 0) { $script:eqShotTitles += $sb.ToString() }
            }
        }
        return $true
    }
    [EqShot.Win]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    return $script:eqShotTitles
}

# --- THE SCREEN IS A MUTEX, AND UNTIL NOW NOTHING ENFORCED IT ----------------------
#
# `FABLE.md` §4 says it in as many words: *"The one hard mutex is the SCREEN … Dranak
# enforces this by kick order, not by tooling."* A convention with no interlock fails
# silently, and this is what the failure looks like from inside a batch:
#
#   `Get-Process EQBuddy` matches every EQBuddy on the machine by PROCESS NAME. Another
#   seat's `shoot.ps1` starting up therefore stands down THIS batch's in-flight fixture
#   app — and records its exe path for a relaunch it will do, with no EQBUDDY_APPDATA,
#   into the real profile. The shot that was mid-settle then finds its window gone:
#   *"no visible window matching 'EQBuddy — Gear' in process N"*. Three different rows
#   failed across three runs of #306's batch, each passing alone, and `DECISIONS.md`
#   (2026-09-05) already records the cause in one line: *"another seat's EQBuddy was
#   running on the same desktop … multi-shot runs died at a different shell shot each
#   time and every one of them passed alone."* The row that fails is whichever one was on
#   screen when the other seat started; nothing about it is a defect in that row.
#
# Two guards, because they catch different collisions:
#
#   1. A LOCK FILE held for the whole batch. It cannot go stale — the handle dies with the
#      process — and it is opened FileShare.Read so a refused seat can say WHO holds it.
#      This is the opposite call from UI.Shared/SingleInstance, deliberately: there, a
#      widget that will not launch is worse than two of them; here, a batch that runs
#      anyway corrupts someone else's acceptance criterion at a random row.
#   2. A RUNNING EQBUDDY OUT OF A BUILD OUTPUT. `tests/EQBuddy.E2E` launches the same exe
#      and takes no lock, so the lock alone cannot see it. A player's EQBuddy never runs
#      from `bin\Release`; a harness's always does, which makes the path the discriminator
#      — the same "what does the real thing actually write" move Core/GameWrittenLog makes
#      for log names (trap 48).
#
# -Force overrides both, for the case where the holder is known dead. It does NOT make the
# stand-down touch a build-output app: closing another harness's fixture app is the damage.
$screenLockPath = Join-Path ([IO.Path]::GetTempPath()) 'eqbuddy-screen.lock'
$screenLock = $null
try {
    $screenLock = [IO.File]::Open($screenLockPath, [IO.FileMode]::OpenOrCreate,
        [IO.FileAccess]::Write, [IO.FileShare]::Read)
}
catch [IO.IOException] {
    $holder = try { (Get-Content $screenLockPath -Raw -ErrorAction Stop).Trim() } catch { '(unreadable)' }
    $msg = "Another screen job holds $screenLockPath — $holder. " +
           "shoot.ps1 and the E2E suite own the desktop exclusively (FABLE.md §4); " +
           "running anyway kills that job's fixture app and fails a random row of BOTH batches. " +
           "Wait for it, or pass -Force if you know the holder is gone."
    if (-not $Force) { throw $msg }
    Write-Warning "$msg`n-Force given; continuing."
}
if ($screenLock) {
    $screenLock.SetLength(0)
    # ASCII only, deliberately: this line is read back by another process with Get-Content,
    # and under Windows PowerShell 5.1 that decodes as the ANSI code page. A holder line
    # nobody can read is trap 54 in a file whose whole job is to be read by a stranger.
    $stamp = [Text.Encoding]::UTF8.GetBytes(
        "pid $PID | $(Get-Date -Format o) | $repo")
    $screenLock.Write($stamp, 0, $stamp.Length)
    $screenLock.Flush()
}

$fixtureApps = @(Get-Process EQBuddy -ErrorAction SilentlyContinue | Where-Object {
    $p = try { $_.Path } catch { $null }
    $p -and $p -match '[\\/]bin[\\/](Release|Debug)[\\/]'
})
if ($fixtureApps.Count -gt 0) {
    $where = ($fixtureApps | ForEach-Object { "pid $($_.Id) $(try { $_.Path } catch { '?' })" }) -join "`n  "
    $msg = "An EQBuddy is already running from a BUILD OUTPUT, which means another harness " +
           "(shoot.ps1 or tests/EQBuddy.E2E) has the screen:`n  $where`n" +
           "It is not the player's app and this script will not close it. " +
           "Wait for that run, or pass -Force."
    if (-not $Force) { throw $msg }
    Write-Warning "$msg`n-Force given; continuing."
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
#
# AND IT STANDS DOWN THE PLAYER'S APP ONLY. `Get-Process EQBuddy` matches by process NAME,
# so it used to include another harness's in-flight fixture app — closing it mid-capture,
# failing a random row of that batch with "no visible window", and then relaunching its exe
# from the `finally` with no EQBUDDY_APPDATA, pointing a stray widget at the real profile.
# A build-output path is never a player's installed copy; the screen-lock block above
# refuses the run over it, and this loop leaves it alone even under -Force.
$relaunch = @()
foreach ($proc in @(Get-Process EQBuddy -ErrorAction SilentlyContinue)) {
    $path = try { $proc.Path } catch { $null }   # Access denied on a process we can't read
    if ($path -and $path -match '[\\/]bin[\\/](Release|Debug)[\\/]') {
        Write-Warning ("Leaving pid $($proc.Id) alone — it runs from a build output " +
            "($path), so it is another harness's fixture app, not the player's EQBuddy.")
        continue
    }
    if ($path) { $relaunch += $path }
    Write-Host "Standing down the running EQBuddy (pid $($proc.Id)) — it will be relaunched."
    try {
        if (-not (Close-EqWidget $proc)) { if (-not $proc.CloseMainWindow()) { Stop-Hard $proc } }
        if (-not $proc.WaitForExit(15000)) { Stop-Hard $proc; $proc.WaitForExit(5000) | Out-Null }
    }
    catch { }   # already gone between the enumerate and the close
}
$relaunch = @($relaunch | Select-Object -Unique)

# --- the backdrop ------------------------------------------------------------------
# A plain full-screen form, NOT topmost, so the app's own always-on-top windows stay above
# it. This is what stops a rounded corner photographing the desktop.
# One assembly per call: the comma-list form silently loads neither here (pwsh 7).
#
# WinForms Maximized lands on the PRIMARY display, with no screen assignment. Fixture
# windows already go to the secondary via Get-EqShotOrigin / EQBuddy SecondaryOrigin
# (#316); a Maximized backdrop would cover EverQuest on the primary while shots run.
# When a non-primary screen exists, pin Bounds to that screen (same pick as
# Get-EqShotOrigin). Single-screen (CI) keeps the Maximized fallback.
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$backdropForm = New-Object System.Windows.Forms.Form
$backdropForm.FormBorderStyle = 'None'
$backdropForm.ShowInTaskbar = $false
$backdropForm.BackColor = [System.Drawing.ColorTranslator]::FromHtml($Backdrop)
$backdropScreen = Get-EqShotSecondaryScreen
if ($backdropScreen) {
    $backdropForm.StartPosition = 'Manual'
    $backdropForm.Bounds = $backdropScreen.Bounds
} else {
    $backdropForm.WindowState = 'Maximized'
}
$backdropForm.Show()
$backdropForm.Refresh()

# Sessions only reach history.db when a session ENDS, and the fixture never ends one —
# every idle gap is compressed so the whole log reads as one live session. A shot that
# needs history rows therefore has to make some: run the app, let it replay, and close it
# GRACEFULLY, because EQBuddy finalizes the active session into history.db on
# ApplicationExit and the capture loop below kills its app instead (deliberately — that
# one is a throwaway). Each prime run is one real archived session, with the fixture's own
# numbers rather than invented ones.
function Invoke-PrimeRun([object[]]$runs) {
    $i = 0
    foreach ($run in $runs) {
        $i++
        Write-Host "  priming history ($i/$($runs.Count))…"
        # A second run over the SAME log does not mint a second session — the archiver
        # recognises the replay and updates the row it already has (#74). So a prime run
        # that wants a DISTINCT session writes a distinct log: another character, and a
        # prefix of the fixture rather than all of it, which gives that session its own
        # duration and its own numbers instead of a suspiciously identical twin.
        # A run gets its own SESSION WINDOW, not just its own length. SessionRepository
        # adopts an existing row on (Server, Character, StartUtc) — Fable checked the query
        # after my first diagnosis blamed the log path — so two runs that slice the same
        # fixture carry the SAME first timestamp and collapse into one row however much
        # their content differs. ShiftDays re-stamps the slice so each run is a distinct
        # session to the adopter, through the fully real ingest path.
        $extraLog = $null
        if ($run.Character) {
            $source = Get-ChildItem -Path $logsDir.FullName -Filter 'eqlog_*.txt' | Select-Object -First 1
            $lines = Get-Content $source.FullName
            $fraction = if ($run.Fraction) { $run.Fraction } else { 1.0 }
            $take = [Math]::Max(1, [int]($lines.Count * $fraction))
            $body = @($lines[0..($take - 1)])

            if ($run.ShiftDays) {
                $fmt = 'ddd MMM dd HH:mm:ss yyyy'
                $ci = [Globalization.CultureInfo]::InvariantCulture
                $span = [TimeSpan]::FromDays([double]$run.ShiftDays)
                $body = @($body | ForEach-Object {
                    if ($_ -match '^\[(?<t>[^\]]+)\] (?<m>.*)$') {
                        $t = [datetime]::ParseExact($Matches.t, $fmt, $ci)
                        "[$(($t - $span).ToString($fmt, $ci))] $($Matches.m)"
                    } else { $_ }
                })
            }

            # Per-run content, appended INSIDE this run's own window so it belongs to this
            # session rather than to a shared tail (Fable's design note, corrected: the flaw
            # was appending to a shared prefix, not the per-invocation idea).
            if ($run.Lines) {
                $fmt = 'ddd MMM dd HH:mm:ss yyyy'
                $ci = [Globalization.CultureInfo]::InvariantCulture
                $last = [datetime]::Now
                if ($body[-1] -match '^\[(?<t>[^\]]+)\]') {
                    $last = [datetime]::ParseExact($Matches.t, $fmt, $ci)
                }
                $body += @($run.Lines | ForEach-Object {
                    $last = $last.AddSeconds(1)
                    "[$($last.ToString($fmt, $ci))] $_"
                })
            }

            $extraLog = Join-Path $logsDir.FullName "eqlog_$($run.Character)_test.txt"
            $body | Set-Content $extraLog -Encoding utf8
        }
        $psi = New-Object Diagnostics.ProcessStartInfo $exe
        $psi.UseShellExecute = $false
        $psi.EnvironmentVariables['EQBUDDY_APPDATA'] = $profileDir.FullName
        $psi.EnvironmentVariables['EQBUDDY_OPAQUE'] = '1'
        # A prime run is a launch like any other, so it opens the shell like any other —
        # the order is about what appears on the monitor, and this is the one launch in
        # the script that used to put a bare v1 widget there for eight seconds.
        $psi.EnvironmentVariables['EQBUDDY_SHELL'] = '1'
        $proc = [Diagnostics.Process]::Start($psi)
        $deadline = (Get-Date).AddSeconds(60)
        while ((Get-Date) -lt $deadline -and $proc.MainWindowHandle -eq 0) {
            Start-Sleep -Milliseconds 400
            if ($proc.HasExited) { break }
            $proc.Refresh()
        }
        # The replay has to finish before the close, or the session archived is a partial
        # one — the numbers in the picture would then be smaller than the fixture's and
        # nothing on screen would say why.
        Start-Sleep -Seconds $Settle
        # The WIDGET, by name. This close is the whole point of a prime run — only the
        # widget's OnClosed finalizes the session into history.db — and with the shell up
        # `CloseMainWindow()` is no longer guaranteed to be aiming at it (see
        # Close-EqWidget). A prime that closed the wrong window would leave the app
        # running, be killed twenty seconds later, and stage history that is simply not
        # there, with the shot rendering a real empty state over it (trap 23).
        if (-not $proc.HasExited) {
            if (-not (Close-EqWidget $proc)) { $proc.CloseMainWindow() | Out-Null }
        }
        if (-not $proc.WaitForExit(20000)) { Stop-Hard $proc; $proc.WaitForExit(5000) | Out-Null }
        # Removed before the capture run: two logs in the folder means the widget follows
        # whichever grew last, and the shot's own character would flip under it.
        if ($extraLog) {
            Remove-Item $extraLog -Force -ErrorAction SilentlyContinue
            # A prime run for the FIXTURE'S OWN character writes eqlog_<char>_test.txt,
            # which IS the fixture log — so the line above has just deleted the file the
            # next prime run reads as its $source and the capture run tails. Put it back.
            #
            # That case is the only way to stage stored history for the character the shot
            # actually follows: `ProgressSeries` compares (server, character) with SQL `=`,
            # so priming under a different name (which is all `history-charts` ever needed)
            # archives rows the shot's own surface can never match. Harmless for a
            # different-character prime, where the fixture log was never touched.
            if (-not (Test-Path $extraLog) -and $extraLog -eq $pristineLog.FullName) {
                Copy-Item $pristineCopy $extraLog -Force
            }
        }
    }
}

New-Item -ItemType Directory -Force $Out | Out-Null
$taken = @()
$failed = @()
try {
    foreach ($name in $wanted) {
      # ONE BAD ROW MUST NOT DARKEN THE REST OF THE BATCH.
      # `$ErrorActionPreference = 'Stop'` made a single failure end the run AT that row, so
      # the ~25 shots after it were simply unreachable — which is trap 53's actual cost:
      # three stale titles took the batch dark for six days across four releases, and every
      # session that re-shot ONE image got a picture and moved on. The run still FAILS (a
      # stale title must), it just says so about every row rather than the first one.
      try {
        $spec = $Shots[$name]
        Write-Host "`n=== $name → $($spec.Title) ==="
        # THE ARCHIVE IS STAGING TOO, AND IT WAS THE ONE CUMULATIVE THING LEFT.
        # Trap 51 made the fixture LOG pristine before every shot; history.db sat in the
        # shared profile and accumulated every prime run in the batch, so a Prime shot
        # photographed its own sittings plus whatever earlier shots had archived.
        # Measured, not assumed: 'shell-progress-history-narrow' primes two sittings and
        # says "2 sessions" on its own, and said "3 sessions" in a batch behind
        # 'shell-progress-history' — same code, same spec, two pictures, and the batch one
        # is the picture that gets committed. It hid because the extra rows are PLAUSIBLE:
        # a career browse with one more sitting in it looks exactly like a career browse.
        # Unconditional and before the early return, for trap 51's own reason — a shot with
        # no Prime of its own must not inherit the last shot's archive either.
        Remove-Item (Join-Path $profileDir 'history.db*') -Force -ErrorAction SilentlyContinue
        Write-Settings $spec.Set
        Write-Ledger $spec.Ledger
        Write-Raids $spec.Raids
        Write-Dump $spec.Dump
        Write-WikiCache $spec.Wiki
        Write-Cycles $spec.Cycles
        Write-Timers $spec.Timers
        # AFTER the prime runs, not before. A prime for the fixture's own character
        # overwrites the very log an append had just been written into, so staging the
        # live session first and the stored history second silently discarded the first
        # half. Append-Log restores the pristine fixture unconditionally before it appends,
        # which makes this ordering the one that leaves the capture run tailing exactly
        # what the shot asked for.
        if ($spec.Prime) { Invoke-PrimeRun $spec.Prime }
        Append-Log $spec.Append
        # A multi-session archive for the review picker: the pristine fixture plus
        # day-shifted copies, oldest first so the file reads chronologically. Built
        # outside the Logs folder so the tail can never adopt it.
        $reviewLog = $null
        if ($spec.ReviewSessions) {
            $fmt = 'ddd MMM dd HH:mm:ss yyyy'
            $ci = [Globalization.CultureInfo]::InvariantCulture
            $src = Get-Content $pristineCopy
            $all = @()
            for ($d = $spec.ReviewSessions - 1; $d -ge 0; $d--) {
                $span = [TimeSpan]::FromDays($d)
                $all += @($src | ForEach-Object {
                    if ($_ -match '^\[(?<t>[^\]]+)\] (?<m>.*)$') {
                        $t = [datetime]::ParseExact($Matches.t, $fmt, $ci)
                        "[$(($t - $span).ToString($fmt, $ci))] $($Matches.m)"
                    } else { $_ }
                })
            }
            $reviewDir = New-Item -ItemType Directory -Force (Join-Path $root 'review')
            $reviewLog = Join-Path $reviewDir.FullName 'eqlog_Testchar_archive.txt'
            $all | Set-Content $reviewLog -Encoding utf8
        }

        $psi = New-Object Diagnostics.ProcessStartInfo $exe
        $psi.UseShellExecute = $false
        $psi.EnvironmentVariables['EQBUDDY_APPDATA'] = $profileDir.FullName
        $psi.EnvironmentVariables['EQBUDDY_OPAQUE'] = '1'
        # THE EVOLVED SHELL COMES UP FOR EVERY SHOT, not just the shell-* ones. The owner's
        # standing order while E-3 is being built: a capture run must not pop a bare v1
        # widget on the monitor the game is on. It rides BEFORE $spec.Env so a shot that
        # names an address ('shell-quests-sky') still gets exactly the room it asked for.
        #
        # It does not change any picture. shot.ps1 uses PrintWindow, so occlusion is
        # already irrelevant, and it now prefers an EXACT title match — which is what keeps
        # the widget's 'EQBuddy' from resolving to the shell's 'EQBuddy — Home' in the same
        # process (trap 24's uncovered half).
        $psi.EnvironmentVariables['EQBUDDY_SHELL'] = '1'
        foreach ($k in $spec.Env.Keys) { $psi.EnvironmentVariables[$k] = $spec.Env[$k] }
        if ($reviewLog) { $psi.EnvironmentVariables['EQBUDDY_REVIEW'] = $reviewLog }
        $proc = [Diagnostics.Process]::Start($psi)
        try {
            # WAIT FOR THE WINDOW THIS SHOT IS ABOUT — which is not what this loop used to do.
            #
            # It asked two questions and the second one answered first, every time. Both
            # `MainWindowTitle` and `MainWindowHandle` describe ONE window: "the first
            # visible, unowned top-level window of the process", which is the widget. So for
            # every shot whose target is a satellite or a room — Options, Drops, the theme
            # windows, the shell — the `MainWindowHandle -ne 0` escape fired as soon as the
            # WIDGET appeared, the 90-second deadline was dead code, and the target window's
            # entire budget was $Settle: eight seconds, shared with the startup replay.
            #
            # Eight seconds is usually plenty and is not a wait. Every hook in DebugHooks
            # opens its window from a `Loaded` handler at DispatcherPriority.ApplicationIdle
            # — deliberately, so the replay lands first — and ApplicationIdle work is
            # starved for exactly as long as the app is busy. E-3 then put a second full
            # window (the shell, on every launch since #316) into that same eight seconds.
            # A budget that used to be generous is now a race, and losing it presents as
            # "no visible window matching …" on whichever row happened to draw the slow
            # launch. The escape hatch's comment was right about satellites not being
            # MainWindowTitle; the conclusion it drew — hand off to shot.ps1 immediately —
            # is what turned a 90-second wait into an 8-second gamble.
            #
            # Now it waits for the same window shot.ps1 will look for, by the same rule, and
            # $Settle goes back to being a settle. On a miss it does NOT throw here: it falls
            # through to the capture exactly as before, so this can only ever wait LONGER
            # than the old code, never fail where the old code succeeded.
            $deadline = (Get-Date).AddSeconds(90)
            $started = Get-Date
            $seen = $null
            while ((Get-Date) -lt $deadline) {
                Start-Sleep -Milliseconds 500
                if ($proc.HasExited) { throw "$exe exited early (code $($proc.ExitCode))." }
                $seen = Find-EqShotWindow $spec.Title $proc.Id
                if ($seen) { break }
            }
            if ($seen) {
                $waited = [int]((Get-Date) - $started).TotalMilliseconds
                Write-Host "  '$($spec.Title)' up after ${waited}ms"
            }
            else {
                # The instrument, not a guess: say what the process DID have on screen.
                # "nothing at all" (the app is still starting, or the hook never fired) and
                # "four windows, none of them this one" (a stale Title — trap 53) are
                # different diagnoses that used to print identically, as nothing.
                $had = @(Get-EqShotWindowTitles $proc.Id)
                $what = if ($had.Count -eq 0) { '(no visible windows at all)' }
                        else { ($had | ForEach-Object { "'$_'" }) -join ', ' }
                Write-Warning ("No window matching '$($spec.Title)' in pid $($proc.Id) after 90s. " +
                    "Visible windows of that process: $what. Capturing anyway so shot.ps1 " +
                    "reports the same failure it always did.")
            }
            # LINES THE APP MUST READ WHILE IT IS RUNNING, not during its startup replay.
            #
            # `Append` writes before launch, which is right for anything whose effect is
            # STATE — a level-up the Progress card lists, a mez the tracker is still holding.
            # It is useless for anything whose effect is an ALERT: replaying today's log at
            # startup deliberately fires no banners, because nobody wants a burst of them for
            # things that happened an hour ago. So a watch rule staged through `Append` is a
            # rule that correctly does nothing, and the shot would be a picture of an empty
            # row that looks exactly like a broken feature (trap 23's shape, arriving through
            # the harness like trap 51's).
            #
            # Written AFTER the target window is up and BEFORE the settle, so the tail's
            # 150 ms poll and the widget's 1 s tick both land inside the settle budget.
            # Append-only, deliberately: the restore-then-append that `Append` does would
            # rewrite the file underneath a running tail.
            if ($spec.AppendLive) { Add-LogLines $spec.AppendLive }

            $backdropForm.Refresh()
            # PARK THE POINTER OFF EVERY WINDOW BEFORE THE SETTLE, or the capture is a
            # picture of where the mouse happened to be. WPF paints :hover from the real
            # cursor whether or not anyone is driving it, so a surface with hover-painted
            # rows photographs one row filled — and a filled row reads as SELECTED, which
            # is a state the shot may be asserting is absent. The career tab's first take
            # showed exactly that: a highlighted sitting beside a detail pane still saying
            # "Pick a sitting on the left", one picture contradicting itself.
            # It is trap 51's shape from outside the app rather than inside it — the same
            # code, the same profile, two different pictures — and the ambient state is the
            # desktop's rather than the fixture's, so no amount of seeding reaches it.
            # Bottom-right of the virtual desktop: outside every window this harness opens,
            # and defined however many monitors are attached.
            $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
            [System.Windows.Forms.Cursor]::Position =
                New-Object System.Drawing.Point (($vs.Right - 1), ($vs.Bottom - 1))
            Start-Sleep -Seconds $Settle

            $png = Join-Path $Out "$name.png"
            # -OwnerPid, so a previous shot's app that is still exiting cannot be
            # photographed under this shot's name: four Progress-theme shots share the
            # title 'EQBuddy Progress', and a title is not an identity.
            & (Join-Path $PSScriptRoot 'shot.ps1') -TitleLike $spec.Title -Out $png -OwnerPid $proc.Id | Write-Host
            $taken += $png
        }
        finally {
            if (-not $proc.HasExited) { Stop-Hard $proc }
            # The return value used to go to Out-Null. A wait that times out and says
            # nothing leaves the next shot launching a SECOND app on one profile — two log
            # tails and two whole-file writers of settings.json, which is trap 13's shape
            # arriving through the harness. Say so, and give it one more push.
            if (-not $proc.WaitForExit(10000)) {
                Write-Warning ("pid $($proc.Id) did not exit within 10s after '$name'; " +
                    "forcing again before the next shot starts on the same profile.")
                Stop-Hard $proc
                if (-not $proc.WaitForExit(5000)) {
                    Write-Warning "pid $($proc.Id) is STILL alive — the next shot shares its profile."
                }
            }
        }
      }
      catch {
        $failed += [pscustomobject]@{ Shot = $name; Error = $_.Exception.Message }
        Write-Warning "SHOT FAILED — $name : $($_.Exception.Message)"
      }
    }
}
finally {
    if ($screenLock) { $screenLock.Dispose() }
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

# The summary is the point of continuing past a failure: every stale row in ONE run,
# and a non-zero exit so nothing reads a partial batch as a green acceptance criterion.
if ($failed.Count -gt 0) {
    Write-Host "`n$($failed.Count) shot(s) FAILED:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  $($_.Shot): $($_.Error)" -ForegroundColor Red }
    exit 1
}
