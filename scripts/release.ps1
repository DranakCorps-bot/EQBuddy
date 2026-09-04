# EQBuddy release: publish exe, sign, compile installer, sign it, refresh zip,
# push to OneDrive (the family's install + auto-update channel).
# Commit + `git push` your source changes too; git is the source-code backup.
param([string]$Tag, [switch]$Prerelease)
$ErrorActionPreference = 'Stop'

# -Prerelease only means anything to `gh release create`, which only runs with a -Tag.
# Without one it would be a switch that silently does nothing on a run that still builds,
# signs, copies to OneDrive and installs locally — and the person who passed it would have
# no way to tell. Refuse here, before the 172 MB publish, rather than after it.
if ($Prerelease -and -not $Tag) { throw '-Prerelease has no effect without -Tag (it is a flag on the GitHub release).' }

$repo = Split-Path $PSScriptRoot -Parent
$oneDrive = 'C:\Users\david\OneDrive\EQBuddyDownload'
. "$PSScriptRoot\signing.ps1"

# Version comes from Directory.Build.props (single source for BOTH apps — issue #30:
# a separate Avalonia version shipped stale Linux builds) so the apps, installer, and
# updater always agree.
$props = Get-Content "$repo\Directory.Build.props" -Raw
if ($props -notmatch '<Version>([\d.]+)</Version>') { throw 'No <Version> in Directory.Build.props' }
$version = $Matches[1]
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
Get-Process EQBuddy -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

dotnet publish "$repo\src\EQBuddy\EQBuddy.csproj" -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$repo\dist\publish"
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

# Sign the app before Inno Setup packages it, so the installer carries a signed
# payload as well as being signed itself. Invoke-EqSign throws on anything short of a
# verified, timestamped signature (scripts\signing.ps1).
Invoke-EqSign "$repo\dist\publish\EQBuddy.exe"

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
          "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup (ISCC.exe) not found' }
& $iscc "/DAppVersion=$version" "$repo\installer\EQBuddy.iss"
if ($LASTEXITCODE -ne 0) { throw 'installer compile failed' }
Invoke-EqSign "$repo\dist\EQBuddySetup.exe"

Compress-Archive -Path "$repo\dist\publish\EQBuddy.exe", "$repo\README.md" `
    -DestinationPath "$repo\dist\EQBuddy-portable.zip" -Force

# Publish SHA-256 alongside the installer; the in-app updater refuses a
# staged installer that doesn't match (UPDATE-003).
(Get-FileHash "$repo\dist\EQBuddySetup.exe" -Algorithm SHA256).Hash |
    Set-Content "$repo\dist\EQBuddySetup.exe.sha256" -NoNewline
# The portable zip gets one too (#119): portable users update by replacing their
# folder, and a future in-place portable updater will demand this hash the same
# way the installer path does.
(Get-FileHash "$repo\dist\EQBuddy-portable.zip" -Algorithm SHA256).Hash |
    Set-Content "$repo\dist\EQBuddy-portable.zip.sha256" -NoNewline

New-Item -ItemType Directory -Force $oneDrive | Out-Null
Copy-Item "$repo\dist\EQBuddySetup.exe", "$repo\dist\EQBuddySetup.exe.sha256", "$repo\dist\EQBuddy-portable.zip" $oneDrive -Force
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
        "$repo\dist\EQBuddySetup.exe", "$repo\dist\EQBuddySetup.exe.sha256",
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
Start-Process "$repo\dist\EQBuddySetup.exe" -ArgumentList '/SILENT'
Write-Host "Installing $version locally (/SILENT); EQBuddy relaunches when it finishes."
