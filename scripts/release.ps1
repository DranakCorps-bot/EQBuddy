# EQBuddy release: publish exe, sign, compile installer, sign it, refresh zip,
# push to OneDrive (the family's install + auto-update channel).
# Commit + `git push` your source changes too; git is the source-code backup.
param([string]$Tag, [switch]$Prerelease, [switch]$EvolvedLocal)
$ErrorActionPreference = 'Stop'

$repo = Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot\signing.ps1"

# Version comes from Directory.Build.props (single source for BOTH apps — issue #30:
# a separate Avalonia version shipped stale Linux builds) so the apps, installer, and
# updater always agree.
$props = Get-Content "$repo\Directory.Build.props" -Raw
if ($props -notmatch '<Version>([\d.]+)</Version>') { throw 'No <Version> in Directory.Build.props' }
$version = $Matches[1]
$major = [int]($version.Split('.')[0])

# EQBuddy Evolved (2.x) develops LOCAL-ONLY until the owner opens the channel, and this
# script is the only thing in the repo that can break that. So the 2.x line cannot be
# published AT ALL: the refusal is here, before the 172 MB publish, and there is
# deliberately no switch that re-enables the channel. Opening it is a future EDIT to this
# file, made when the owner gives the go — the same posture as having no -SkipSign,
# because a protection you can pass a flag to opt out of is a protection nobody has.
#
# -EvolvedLocal is the opposite of an escape hatch: it is the ONLY way to run this script
# on a 2.x tree, and everything it does is subtractive. It skips the family's update
# folder, refuses to tag or publish, and does not install over this machine's v1 copy.
# What it keeps is every signing step, unchanged — an unsigned local build is testing a
# different artifact from the one players get.
if ($major -ge 2 -and -not $EvolvedLocal) {
    throw "EQBuddy $version is the Evolved line and it is LOCAL-ONLY: this script will not publish it. Pass -EvolvedLocal to build and sign into dist\ without touching OneDrive, GitHub or this machine's v1 install — or use scripts\install-local.ps1 -Evolved to build, sign and RUN it on a separate profile."
}
if ($EvolvedLocal -and $major -lt 2) { throw "-EvolvedLocal is for the 2.x Evolved line; $version is 1.x, where the local loop is scripts\install-local.ps1." }
if ($EvolvedLocal -and $Tag)         { throw '-EvolvedLocal refuses -Tag: a tag is a public release, and the Evolved channel is not open. This is the second lock — the publish block is skipped anyway.' }
if ($EvolvedLocal -and $Prerelease)  { throw '-EvolvedLocal refuses -Prerelease: it is a flag on a GitHub release, and -EvolvedLocal makes none. A switch that silently does nothing is the defect the -Prerelease-without-Tag refusal below was written for.' }

# -Prerelease only means anything to `gh release create`, which only runs with a -Tag.
# Without one it would be a switch that silently does nothing on a run that still builds,
# signs, copies to OneDrive and installs locally — and the person who passed it would have
# no way to tell. Refuse here, before the 172 MB publish, rather than after it.
#
# It sits BELOW the -EvolvedLocal refusals, and the order is load-bearing rather than
# cosmetic: above them it made the line before it unreachable. `-EvolvedLocal -Prerelease`
# (no tag) would have been caught here first, so the refusal that names the actual reason
# could never fire — a check that cannot fire is the exact shape this file keeps finding
# (traps 20, 34). Moving four lines makes both reachable and each says its own reason.
if ($Prerelease -and -not $Tag) { throw '-Prerelease has no effect without -Tag (it is a flag on the GitHub release).' }

Write-Host "Releasing EQBuddy $version"

# The in-app "What's new" popup reads embedded notes; a release without an entry
# would show users nothing. Refuse rather than rot.
$whatsNew = Get-Content "$repo\src\EQBuddy.Core\Data\WhatsNew.json" -Raw | ConvertFrom-Json
$entry = $whatsNew | Where-Object { $_.version -eq $version } | Select-Object -First 1
if (-not $entry) {
    throw "No What's-new entry for $version in src\EQBuddy.Core\Data\WhatsNew.json — add one before releasing."
}

# ...and finding an entry is not the same as finding the RIGHT one. The check above searches
# by version, so it is equally satisfied when this release's work was written into a heading
# that already shipped — which happened twice in three releases. That defect cannot be seen
# from inside the file; it is a disagreement with a git tag. scripts/whatsnew-guard.ps1 is
# the only thing here that knows about tags, so it runs before anything is built or signed.
& "$PSScriptRoot\whatsnew-guard.ps1" -Releasing
if ($LASTEXITCODE -ne 0) { throw "What's-new guard failed — see above. Nothing was built." }

# LEGACY-007 (#275): the first 2.x release notes and the README carry a visible
# "Legacy Linux/macOS" section linking to the final v1 release. It is a no-op on the 1.x
# line and it fires exactly once, on the release where forgetting it costs the most — the
# one that makes `releases/latest` a page full of Windows installers.
& "$PSScriptRoot\legacy-notice-guard.ps1"
if ($LASTEXITCODE -ne 0) { throw "Legacy notice guard failed — see above. Nothing was built." }

# EQBuddy Evolved develops LOCAL-ONLY until the owner opens the channel, and this script
# is the only thing in the repo that can break that promise. The guard runs here, before
# anything is built, for the same reason the two above do — and it checks THIS FILE's
# text as well as the family's update folder, so an edit that re-opens the channel fails
# a gate rather than a household.
& "$PSScriptRoot\evolved-channel-guard.ps1"
if ($LASTEXITCODE -ne 0) { throw "Evolved channel guard failed — see above. Nothing was built." }

# The SAME words go on the GitHub release page. --generate-notes produced an empty body
# for v1.80.0 (a merge with no PR behind it has nothing to generate FROM), so anyone who
# hadn't installed yet — the people deciding whether to — landed on a bare changelog
# link. The in-app popup can only reach players who already have EQBuddy and updated;
# this is the same announcement for everyone who doesn't.
$releaseNotes = ($entry.highlights | ForEach-Object { "- $_" }) -join "`n"
$releaseNotes = "## What's new in $version`n`n$releaseNotes`n"

# Resolve the signing toolchain BEFORE the build. Signing used to be discovered at
# the moment of use and to warn-and-continue when it wasn't there, which is how an
# unsigned installer could reach OneDrive with the release reporting success. Now a
# broken toolchain costs one second instead of a 172 MB publish, and it stops the run.
Initialize-EqSigning -Repo $repo

# The kill is loud on purpose (v1.39.0 shipped mid-fight and the widget just
# vanished); the /SILENT install at the end brings the app back — on the NEW build.
#
# Skipped under -EvolvedLocal, because the install that would bring it back is skipped
# too: killing the running v1 widget and then never replacing it would cost David his
# session (EQBuddy finalizes into history.db on exit) in exchange for nothing. An Evolved
# build never touches the installed v1 copy, so it has no reason to close it.
if (-not $EvolvedLocal) {
    Get-Process EQBuddy -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
}

dotnet publish "$repo\src\EQBuddy\EQBuddy.csproj" -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$repo\dist\publish"
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# Sign the app before Inno Setup packages it, so the installer carries a signed
# payload as well as being signed itself. Invoke-EqSign throws on anything short of a
# verified, timestamped signature (scripts\signing.ps1).
#
# Unconditional, including under -EvolvedLocal, where no installer follows it: this is the
# signature the portable exe and the zip carry, and it is the one thing -EvolvedLocal was
# always written to keep. An unsigned local build is testing a different artifact from the
# one players get.
Invoke-EqSign "$repo\dist\publish\EQBuddy.exe"

Compress-Archive -Path "$repo\dist\publish\EQBuddy.exe", "$repo\README.md" `
    -DestinationPath "$repo\dist\EQBuddy-portable.zip" -Force

# The portable zip gets a SHA-256 too (#119): portable users update by replacing their
# folder, and a future in-place portable updater will demand this hash the same
# way the installer path does. It is above the installer now rather than below it so the
# installer block can be one contiguous region — see the next comment.
(Get-FileHash "$repo\dist\EQBuddy-portable.zip" -Algorithm SHA256).Hash |
    Set-Content "$repo\dist\EQBuddy-portable.zip.sha256" -NoNewline

# ---- THE INSTALLER, and the identity that lets -EvolvedLocal build one ---------------
#
# Until 2026-09-07 this block was inside `if (-not $EvolvedLocal)` and -EvolvedLocal built
# no installer AT ALL. The reason was not publishing - the region below already stops that
# - it was that the installer only had to EXIST: installer\EQBuddy.iss carried v1's AppId
# and {autopf}\EQBuddy, so a signed 2.0.0 EQBuddySetup.exe sitting in dist\ was ONE
# double-click from replacing this machine's v1 install in place and inheriting its
# profile (settings.json, history.db, archives), with #158's rollback giving back the
# binary and not the profile. Nothing watches dist\: evolved-channel-guard's check 3
# scans the family's update folder.
#
# TR-2 (FABLE.md §3, signed #399) replaces that mitigation with an IDENTITY, which is the
# stronger form of the same protection: installer\EQBuddyEvolved.iss has its own AppId,
# installs into {autopf}\EQBuddy Evolved and its own Start-menu entry, and is named
# EQBuddyEvolvedSetup.exe - because EQBuddySetup.exe is a RESERVED NAME belonging to the
# v1 line forever (every deployed 1.x updater matches on it; the contract is frozen).
# So the artifact this builds cannot do the thing "never build one" was avoiding, and
# double-clicking it installs BESIDE v1 rather than over it.
#
# Signing is unchanged and unconditional, here as everywhere: an unsigned local build is
# testing a different artifact from the one players get.
#
# One variable, because the artifact's name is one fact and the channel region below
# names it three more times (trap 4). Changing it here changes it everywhere.
$setupExe = "$repo\dist\EQBuddyEvolvedSetup.exe"

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
          "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup (ISCC.exe) not found' }
& $iscc "/DAppVersion=$version" "$repo\installer\EQBuddyEvolved.iss"
if ($LASTEXITCODE -ne 0) { throw 'installer compile failed' }
Invoke-EqSign $setupExe

# Publish SHA-256 alongside the installer; the in-app updater refuses a
# staged installer that doesn't match (UPDATE-003).
(Get-FileHash $setupExe -Algorithm SHA256).Hash |
    Set-Content "$setupExe.sha256" -NoNewline

# ===================================================================================
# THE PUBLISH / INSTALL CHANNEL — everything below here REACHES SOMEBODY.
#
# Three things that leave this machine's dist\ folder, and all three are one decision:
#   * the OneDrive copy, which every family widget checks at startup and every 6 hours;
#   * `gh release create`, which is the public channel;
#   * the /SILENT install, which brings THIS machine current on whatever line this tree
#     is. Under the Evolved identity that is {autopf}\EQBuddy Evolved and its own AppId,
#     so it installs BESIDE an EQBuddy 1.x rather than over it (TR-2) — but it is still
#     an install, and it is still one of the three things that reach somebody.
#
# They live in one region so that skipping them is a single decision rather than three,
# and scripts\evolved-channel-guard.ps1 asserts from the TEXT of this file that nothing
# of that shape has crept out of it. At 2.x this region is unreachable: -EvolvedLocal is
# mandatory there, and it is the thing this region is conditioned on.
# ===================================================================================
if (-not $EvolvedLocal) {

$oneDrive = 'C:\Users\david\OneDrive\EQBuddyDownload'
New-Item -ItemType Directory -Force $oneDrive | Out-Null
Copy-Item $setupExe, "$setupExe.sha256", "$repo\dist\EQBuddy-portable.zip" $oneDrive -Force
Write-Host "Released $version to $oneDrive (family widgets will offer the update within 6 h)"

if ($Tag) {
    # Issue #56 (sahaq): `gh release create` tags whatever GitHub-side main happens to
    # be — if the release commit was never pushed, the tag lands on the PREVIOUS
    # release's commit and CI ships a stale Linux binary under the new version number.
    # So: push first, tag HEAD explicitly, push the tag, and refuse to release unless
    # the tag's own Directory.Build.props agrees with the version being released.
    git push origin main
    if ($LASTEXITCODE -ne 0) { throw 'git push failed - the release commit must be on origin/main' }
    git tag $Tag
    if ($LASTEXITCODE -ne 0) { throw "git tag $Tag failed (already exists? delete it or pick the next version)" }
    git push origin $Tag
    if ($LASTEXITCODE -ne 0) { throw "pushing tag $Tag failed" }
    $tagProps = git show "${Tag}:Directory.Build.props" | Out-String
    if ($tagProps -notmatch [regex]::Escape("<Version>$version</Version>")) {
        throw "Tag $Tag does not contain <Version>$version</Version> - refusing to release a mismatched build"
    }
    # --notes-file rather than --generate-notes: the player-facing highlights beat a list
    # of commit subjects, which read as in-jokes to anyone who didn't write them.
    $notesFile = Join-Path ([System.IO.Path]::GetTempPath()) "eqbuddy-notes-$version.md"
    Set-Content -Path $notesFile -Value $releaseNotes -Encoding UTF8
    $ghArgs = @($Tag,
        $setupExe, "$setupExe.sha256",
        "$repo\dist\EQBuddy-portable.zip", "$repo\dist\EQBuddy-portable.zip.sha256",
        '--title', "EQBuddy $Tag", '--notes-file', $notesFile)

    # -Prerelease marks the GitHub release as a prerelease, and that ONE flag is what keeps a
    # v2 milestone away from every v1 client: `UpdateChecker.CheckGitHubAsync` reads
    # `/releases/latest`, and GitHub's latest-release endpoint excludes prereleases and
    # drafts. So a prerelease is invisible to the in-app updater without any client change —
    # which matters because the clients that need protecting are the ones already installed,
    # where no fix of ours can reach them. Charter RELEASE-002 asks for exactly this posture
    # during v2 construction (docs/v2, #275 / P0-1).
    #
    # Two things it does NOT do, both deliberate:
    #  * The OneDrive copy above is a SEPARATE channel — FindBestAsync checks the synced
    #    folder as well as GitHub — so a prerelease still reaches the family's widgets. That
    #    is the point of that folder; it is not covered by this flag.
    #  * It is not the only belt. `ParseRelease` runs `Version.TryParse` on the tag and
    #    returns null when it fails, so a tag shaped `v2.0.0-beta1` offers nothing even if it
    #    were marked latest. Belt, not replacement: a `v2.0.0` tag parses fine.
    if ($Prerelease) { $ghArgs += '--prerelease' }

    gh release create @ghArgs
    Remove-Item $notesFile -ErrorAction SilentlyContinue
    if ($LASTEXITCODE -ne 0) { throw 'gh release failed' }
    Write-Host ("GitHub release $Tag published" + $(if ($Prerelease) { ' as a PRERELEASE (excluded from releases/latest, so v1 clients will not be offered it)' } else { '' }))
}

# Bring THIS machine current too. Relaunching $runningApp shipped the machine that
# built the release back onto the PREVIOUS version (caught twice on 2026-08-10:
# 1.53.2's release left 1.53.1 running, 1.54.0's left 1.53.2) and left the stale
# app racing its own auto-updater. The installer we just built closes any running
# copy, installs, and relaunches — same path install-local.ps1 uses.
Start-Process $setupExe -ArgumentList '/SILENT'
Write-Host "Installing $version locally (/SILENT); EQBuddy relaunches when it finishes."

}
else {
    Write-Host ''
    Write-Host "EvolvedLocal: $version is built and SIGNED in $repo\dist — and it went nowhere." -ForegroundColor Cyan
    Write-Host '  * OneDrive:  not touched. The family channel still holds whatever v1 build it held.' -ForegroundColor Cyan
    Write-Host '  * GitHub:    not touched. No tag, no release; -Tag and -Prerelease are refused above.' -ForegroundColor Cyan
    Write-Host '  * This PC:   not installed. The silent local install is in the skipped region.' -ForegroundColor Cyan
    Write-Host "  * Installer: BUILT and SIGNED as $setupExe" -ForegroundColor Cyan
    Write-Host '               under its own AppId. It installs to {autopf}\EQBuddy Evolved with its own' -ForegroundColor Cyan
    Write-Host '               Start-menu entry, BESIDE an EQBuddy 1.x install rather than over it, and it' -ForegroundColor Cyan
    Write-Host '               is deliberately NOT called EQBuddySetup.exe - that name belongs to the v1' -ForegroundColor Cyan
    Write-Host '               line, whose deployed updaters match on it. Your v1 install is untouched.' -ForegroundColor Cyan
    Write-Host '  * Profile:   an installed Evolved copy uses whatever %AppData% directory AppPaths names.' -ForegroundColor Cyan
    Write-Host '  To run it:   pwsh -NoProfile -File scripts\install-local.ps1 -Evolved   (portable, no install)' -ForegroundColor Cyan

    # Say it about the FOLDER, not only about this run (trap 43: proving the producer is
    # not proving the effect). A 2.x-stamped EQBuddySetup.exe in dist\ can only have come
    # from a run of this script BEFORE the identity split, and it is exactly the one-way
    # door the split closed: v1's AppId, {autopf}\EQBuddy, the v1 profile inherited in
    # place. Nothing else scans dist\. Named, not deleted: dist\ is build output but it is
    # still David's, and a script that quietly removes signed binaries is a worse habit
    # than one that points at them.
    $staleSetup = "$repo\dist\EQBuddySetup.exe"
    if (Test-Path $staleSetup) {
        $info = (Get-Item $staleSetup).VersionInfo
        if ($info.FileMajorPart -ge 2) {
            Write-Host ''
            Write-Host "  ! $staleSetup is stamped $($info.FileVersion) and is still there." -ForegroundColor Yellow
            Write-Host '    Nothing builds that name any more, so it is left over from a run before TR-2.' -ForegroundColor Yellow
            Write-Host '    It carries v1''s AppId: double-clicking it replaces your v1 install in place and' -ForegroundColor Yellow
            Write-Host '    inherits its profile. Delete it.' -ForegroundColor Yellow
        }
    }
}
