# EQBuddy release: publish exe, sign, compile installer, sign it, refresh zip,
# push to OneDrive (the family's install + auto-update channel).
# Commit + `git push` your source changes too; git is the source-code backup.
param([string]$Tag, [switch]$Prerelease, [switch]$EvolvedLocal, [switch]$SelfTest)
$ErrorActionPreference = 'Stop'

# DRA-925 (DRA-719 v2.0.4). `git push origin main` pushes the LOCAL main ref. From a
# seat worktree that ref can be stale, the push is a rewind, GitHub refuses it, and the
# throw used to land AFTER the OneDrive copy. A tagged release fetches first and refuses
# unless this checkout IS origin/main. Since DRA-4219 nothing here pushes main at all
# (Publish-ReleaseTag, below). These functions are what -SelfTest drives; the live path is
# the one call below the parameter refusals, before any build, sign, or publish.
function Assert-ReleaseHeadMatchesOriginMain {
    param(
        [Parameter(Mandatory)][string] $Head,
        [Parameter(Mandatory)][string] $OriginMain
    )
    if ($Head -ne $OriginMain) {
        throw "refusing release: HEAD $Head is not origin/main $OriginMain. A tagged release must be checked out at origin/main: the tag goes on HEAD, and the release commit must already be on main. Nothing was built."
    }
}

function Invoke-ReleaseHeadPreflight {
    param([Parameter(Mandatory)][string] $Repo)
    # A successful fetch writes progress to stderr. Redirect it so a native-command
    # error preference cannot turn that progress into a throw that skips the exit check.
    # Preference is local to this function; the rest of the script is unchanged.
    $PSNativeCommandUseErrorActionPreference = $false
    git -C $Repo fetch origin --quiet 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "git fetch origin failed (exit $LASTEXITCODE) — refusing to release without a current origin/main. Nothing was built."
    }
    $head = (git -C $Repo rev-parse --verify HEAD 2>$null).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($head)) {
        throw 'git rev-parse HEAD failed — refusing to release. Nothing was built.'
    }
    $originMain = (git -C $Repo rev-parse --verify origin/main 2>$null).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($originMain)) {
        throw 'git rev-parse origin/main failed — refusing to release. Nothing was built.'
    }
    Assert-ReleaseHeadMatchesOriginMain -Head $head -OriginMain $originMain
}

# DRA-4219 (DRA-4191 v2.0.8). The preflight above proves HEAD = origin/main when the run
# STARTS; nothing holds main still for the minute the build takes. v2.0.8 copied to
# OneDrive, then `git push origin HEAD:main` was refused because PR #1099 had merged
# mid-run, and the throw left the family channel live with no tag and no GitHub release.
# So the release never pushes main at all: the commit is already on origin, and a moved
# main only has to still CONTAIN it. This fetches, refuses unless HEAD is an ancestor of
# (or equal to) origin/main, checks the version before any tag exists, then tags HEAD and
# pushes the TAG only. It runs before `gh release create`, and both run before the OneDrive
# copy, so every throw in here lands while nothing has reached anybody.
function Publish-ReleaseTag {
    param(
        [Parameter(Mandatory)][string] $Repo,
        [Parameter(Mandatory)][string] $Tag,
        [Parameter(Mandatory)][string] $Version
    )
    $PSNativeCommandUseErrorActionPreference = $false
    git -C $Repo fetch origin --quiet 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "git fetch origin failed (exit $LASTEXITCODE) before tagging $Tag. Nothing was published."
    }
    $head = (git -C $Repo rev-parse --verify HEAD 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($head)) {
        throw "git rev-parse HEAD failed before tagging $Tag. Nothing was published."
    }
    git -C $Repo merge-base --is-ancestor $head origin/main 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "refusing to tag ${Tag}: HEAD $head is not on origin/main. The release commit must already be on main; this script never pushes main. Nothing was published."
    }
    $headProps = git -C $Repo show "${head}:Directory.Build.props" 2>$null | Out-String
    if ($headProps -notmatch [regex]::Escape("<Version>$Version</Version>")) {
        throw "HEAD $head does not carry <Version>$Version</Version>; refusing to tag a mismatched build. Nothing was published."
    }
    git -C $Repo tag $Tag $head 2>$null
    if ($LASTEXITCODE -ne 0) { throw "git tag $Tag failed (already exists? delete it or pick the next version). Nothing was published." }
    git -C $Repo push --quiet origin "refs/tags/${Tag}" 2>$null
    if ($LASTEXITCODE -ne 0) { throw "pushing tag $Tag failed. Nothing was published (the local tag $Tag is left for you to delete)." }
    $tagProps = git -C $Repo show "${Tag}:Directory.Build.props" 2>$null | Out-String
    if ($tagProps -notmatch [regex]::Escape("<Version>$Version</Version>")) {
        throw "Tag $Tag does not contain <Version>$Version</Version> - refusing to release a mismatched build. The tag is pushed; no GitHub release and no OneDrive copy were made."
    }
}

# Offline. A local bare origin stands in for GitHub. Never fetches this repo's origin,
# never signs, never copies to OneDrive. -Tag on the same command is ignored.
if ($SelfTest) {
    $PSNativeCommandUseErrorActionPreference = $false
    $fail = 0
    function Note-Fail([string] $why) {
        Write-Host "FAIL: $why" -ForegroundColor Red
        $script:fail++
    }
    $root = Join-Path ([IO.Path]::GetTempPath()) ("release-head-selftest-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $root | Out-Null
    try {
        $origin = Join-Path $root 'origin.git'
        $wt = Join-Path $root 'wt'
        git init -q --bare $origin 2>$null | Out-Null
        git init -q -b main $wt 2>$null | Out-Null
        Set-Content -LiteralPath (Join-Path $wt 'Directory.Build.props') -Value '<Project><PropertyGroup><Version>9.9.9</Version></PropertyGroup></Project>'
        git -C $wt add Directory.Build.props 2>$null | Out-Null
        git -C $wt -c user.name=selftest -c user.email=selftest@invalid commit -q -m base 2>$null | Out-Null
        git -C $wt remote add origin $origin 2>$null | Out-Null
        git -C $wt push -q origin main 2>$null | Out-Null
        git -C $wt checkout -q -b seat 2>$null | Out-Null
        git -C $wt -c user.name=selftest -c user.email=selftest@invalid commit -q --allow-empty -m seat 2>$null | Out-Null

        $head = (git -C $wt rev-parse HEAD).Trim()
        $main = (git -C $wt rev-parse origin/main).Trim()
        if ($head -eq $main) { Note-Fail 'fixture HEAD already equals origin/main — the refusal case was not built' }
        $threw = $false
        $msg = ''
        try { Invoke-ReleaseHeadPreflight -Repo $wt } catch { $threw = $true; $msg = $_.Exception.Message }
        if (-not $threw) { Note-Fail 'HEAD != origin/main stayed green' }
        elseif ($msg -notlike "*$head*" -or $msg -notlike "*$main*") {
            Note-Fail "refusal did not name both SHAs: $msg"
        }

        # Equal SHAs, on a branch that is not named main. The check is the commit, not the branch name.
        git -C $wt checkout -q -B at-main $main 2>$null | Out-Null
        try { Invoke-ReleaseHeadPreflight -Repo $wt }
        catch { Note-Fail "HEAD == origin/main threw: $($_.Exception.Message)" }

        # ---- DRA-4219: main MOVES during the run (v2.0.8) ----------------------------------
        # The seat is at origin/main (the preflight passed); then somebody else merges. A
        # second clone pushes a commit, exactly as PR #1099 did mid-run.
        $other = Join-Path $root 'other'
        git clone -q -b main $origin $other 2>$null | Out-Null
        git -C $other -c user.name=selftest -c user.email=selftest@invalid commit -q --allow-empty -m 'merged mid-run' 2>$null | Out-Null
        git -C $other push -q origin HEAD:main 2>$null | Out-Null
        $moved = (git -C $other rev-parse HEAD).Trim()
        git -C $wt fetch -q origin 2>$null | Out-Null
        if ((git -C $wt rev-parse origin/main).Trim() -ne $moved -or $moved -eq $main) {
            Note-Fail 'fixture: origin/main did not move — the v2.0.8 case was not built'
        }
        # The fixture IS the defect: the old order's push of HEAD:main is refused here.
        git -C $wt push -q origin HEAD:main 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { Note-Fail 'fixture: pushing HEAD:main over a moved main succeeded — this is not the case that stranded v2.0.8' }

        $tagThrew = ''
        try { Publish-ReleaseTag -Repo $wt -Tag 'v9.9.9' -Version '9.9.9' } catch { $tagThrew = $_.Exception.Message }
        if ($tagThrew) { Note-Fail "a moved main still refuses the release: $tagThrew" }
        $remoteTag = (git -C $origin rev-parse --verify -q 'refs/tags/v9.9.9^{commit}' 2>$null | Out-String).Trim()
        if ($remoteTag -ne $main) { Note-Fail "origin's v9.9.9 is '$remoteTag', not the released HEAD $main" }
        $remoteMain = (git -C $origin rev-parse main 2>$null | Out-String).Trim()
        if ($remoteMain -ne $moved) { Note-Fail "origin/main is $remoteMain after tagging; the merged-mid-run commit $moved was rewound" }

        # HEAD not on origin/main at all: refused, and no tag reaches origin.
        git -C $wt checkout -q seat 2>$null | Out-Null
        $offThrew = ''
        try { Publish-ReleaseTag -Repo $wt -Tag 'v9.9.8' -Version '9.9.9' } catch { $offThrew = $_.Exception.Message }
        if (-not $offThrew -or $offThrew -notmatch 'not on origin/main') { Note-Fail "HEAD off origin/main did not refuse: '$offThrew'" }
        if (git -C $origin rev-parse --verify -q 'refs/tags/v9.9.8' 2>$null) { Note-Fail 'a tag reached origin for a HEAD that is not on main' }

        # Version mismatch: refused BEFORE any tag exists, locally or on origin.
        git -C $wt checkout -q at-main 2>$null | Out-Null
        $verThrew = ''
        try { Publish-ReleaseTag -Repo $wt -Tag 'v9.9.7' -Version '9.9.7' } catch { $verThrew = $_.Exception.Message }
        if (-not $verThrew -or $verThrew -notmatch 'does not carry') { Note-Fail "a version mismatch did not refuse before tagging: '$verThrew'" }
        if (git -C $wt rev-parse --verify -q 'refs/tags/v9.9.7' 2>$null) { Note-Fail 'a mismatched version was tagged' }

        git -C $wt remote set-url origin (Join-Path $root 'missing.git') 2>$null | Out-Null
        $fetchThrew = $false
        $fetchMsg = ''
        try { Invoke-ReleaseHeadPreflight -Repo $wt } catch { $fetchThrew = $true; $fetchMsg = $_.Exception.Message }
        if (-not $fetchThrew) { Note-Fail 'a dead origin stayed green' }
        elseif ($fetchMsg -notmatch 'git fetch origin failed' -or $fetchMsg -notmatch 'Nothing was built') {
            Note-Fail "fetch failure did not refuse before a build: $fetchMsg"
        }

        # ---- The live wiring, read from the PARSED script (trap 34) -----------------------
        # Commands, not text: a comment cannot satisfy a row, and this selftest's own lines
        # are excluded by extent — the old text scan found its search strings HERE, in the
        # selftest, so its order row compared two lines of itself.
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($PSCommandPath, [ref]$null, [ref]$null)
        $selfIf = @($ast.EndBlock.Statements | Where-Object {
            $_ -is [System.Management.Automation.Language.IfStatementAst] -and $_.Clauses[0].Item1.Extent.Text -eq '$SelfTest' })[0]
        function Test-InFunction($n) {
            for ($p = $n.Parent; $p; $p = $p.Parent) { if ($p -is [System.Management.Automation.Language.FunctionDefinitionAst]) { return $true } }
            $false
        }
        $all = @($ast.FindAll({ $args[0] -is [System.Management.Automation.Language.CommandAst] }, $true) | Where-Object {
            -not ($_.Extent.StartOffset -ge $selfIf.Extent.StartOffset -and $_.Extent.EndOffset -le $selfIf.Extent.EndOffset) })
        $body = @($all | Where-Object { -not (Test-InFunction $_) })
        function Find-Cmd($list, [string] $name, [string] $arg) {
            @($list | Where-Object {
                $_.GetCommandName() -eq $name -and (-not $arg -or (@($_.CommandElements | ForEach-Object { $_.Extent.Text }) -contains $arg)) })
        }

        # Forbid: nothing pushes main, under any spelling. Shown to fire on both old lines.
        $pushesMain = {
            param($c)
            $els = @($c.CommandElements | ForEach-Object { $_.Extent.Text.Trim('"', "'") })
            $c.GetCommandName() -eq 'git' -and $els -contains 'push' -and @($els | Where-Object { $_ -match '^(\S+:)?(refs/heads/)?main$' }).Count -gt 0
        }
        foreach ($s in @('git push origin main', 'git push origin HEAD:main', 'git -C $Repo push origin HEAD:main')) {
            $sc = @([System.Management.Automation.Language.Parser]::ParseInput($s, [ref]$null, [ref]$null).FindAll({ $args[0] -is [System.Management.Automation.Language.CommandAst] }, $true))[0]
            if (-not (& $pushesMain $sc)) { Note-Fail "the push-main forbid does not fire on its own sample: $s" }
        }
        $bad = @($all | Where-Object { & $pushesMain $_ })
        if ($bad.Count) { Note-Fail "release.ps1 still pushes main: $(($bad | ForEach-Object { $_.Extent.Text }) -join ' | ')" }

        $pre = Find-Cmd $body 'Invoke-ReleaseHeadPreflight'
        $sign = Find-Cmd $body 'Initialize-EqSigning'
        $publish = Find-Cmd $body 'dotnet' 'publish'
        $tagCall = Find-Cmd $body 'Publish-ReleaseTag'
        $gh = Find-Cmd $body 'gh' 'release'
        $copy = @(Find-Cmd $body 'Copy-Item' | Where-Object { $_.Extent.Text -match '\$oneDrive' })
        foreach ($row in @(@('preflight', $pre), @('Initialize-EqSigning', $sign), @('dotnet publish', $publish),
                           @('Publish-ReleaseTag', $tagCall), @('gh release create', $gh), @('OneDrive Copy-Item', $copy))) {
            if ($row[1].Count -ne 1) { Note-Fail "expected exactly one live $($row[0]) call, found $($row[1].Count)" }
        }
        if ($pre.Count -eq 1 -and $sign.Count -eq 1 -and $publish.Count -eq 1 -and $copy.Count -eq 1) {
            $p = $pre[0].Extent.StartOffset
            if ($p -gt $sign[0].Extent.StartOffset -or $p -gt $publish[0].Extent.StartOffset -or $p -gt $copy[0].Extent.StartOffset) {
                Note-Fail 'the preflight is not before signing, publish, and the OneDrive copy'
            }
            if ($pre[0].Parent.Parent.Parent -isnot [System.Management.Automation.Language.IfStatementAst] -or
                $pre[0].Parent.Parent.Parent.Clauses[0].Item1.Extent.Text -ne '$Tag') { Note-Fail 'the preflight is not on the -Tag path' }
        }
        # THE DRA-4219 ROW: tag, then GitHub release, then OneDrive — the family copy is last.
        if ($tagCall.Count -eq 1 -and $gh.Count -eq 1 -and $copy.Count -eq 1) {
            if ($tagCall[0].Extent.StartOffset -gt $gh[0].Extent.StartOffset) { Note-Fail 'Publish-ReleaseTag is not before gh release create' }
            if ($gh[0].Extent.StartOffset -gt $copy[0].Extent.StartOffset -or $tagCall[0].Extent.StartOffset -gt $copy[0].Extent.StartOffset) {
                Note-Fail 'the OneDrive copy runs before the tag and the GitHub release: a throw there leaves OneDrive live and untagged (v2.0.8)'
            }
        }
        $ghArgsAst = @($ast.FindAll({ $args[0] -is [System.Management.Automation.Language.AssignmentStatementAst] -and $args[0].Left.Extent.Text -eq '$ghArgs' }, $true))
        if (-not @($ghArgsAst | Where-Object { $_.Right.Extent.Text -match "'--verify-tag'" }).Count) {
            Note-Fail 'gh release create is not given --verify-tag (it could invent a tag on GitHub-side main)'
        }
    }
    finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }

    if ($fail) { Write-Host "release.ps1 selftest: FAIL: $fail check(s)" -ForegroundColor Red; exit 1 }
    Write-Host 'release.ps1 selftest: mismatch refuses and names both SHAs; match passes; a moved main tags without a rewind; off-main and wrong-version refuse untagged; dead origin refuses before a build; nothing pushes main; OneDrive is last' -ForegroundColor Green
    exit 0
}

$repo = Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot\signing.ps1"

# Version comes from Directory.Build.props (single source for BOTH apps — issue #30:
# a separate Avalonia version shipped stale Linux builds) so the apps, installer, and
# updater always agree.
$props = Get-Content "$repo\Directory.Build.props" -Raw
if ($props -notmatch '<Version>([\d.]+)</Version>') { throw 'No <Version> in Directory.Build.props' }
$version = $Matches[1]
$major = [int]($version.Split('.')[0])

# THE EVOLVED CHANNEL IS OPEN (Founder, 2026-09-28: "EQBuddy Evolved 0.1 Beta", tag v2.0.0,
# and GitHub's Latest points at it). Until then this was a throw — Evolved developed
# LOCAL-ONLY and the 2.x line could not be published at all; opening it was always meant to
# be this edit, made on the owner's go, never a switch. What stays locked is the part that
# was never about timing: EQBuddySetup.exe is v1's reserved name and nothing here produces
# it, the installer carries the Evolved identity, and signing is unconditional
# (evolved-channel-guard.ps1 checks 3-5).
#
# -EvolvedLocal is still here and still subtractive: build and sign into dist\ only — no
# OneDrive, no tag, no release, no local install.
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

# DRA-925. Before any build, sign, or publish — the OneDrive copy is the step the
# v2.0.4 rewind reached. -EvolvedLocal already refused -Tag above, so this line is
# reachable only for a real tagged release, and a mismatched checkout never gets as
# far as Initialize-EqSigning.
if ($Tag) { Invoke-ReleaseHeadPreflight -Repo $repo }

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
#
# ...built by scripts\release-notes.ps1, which condenses them only when the joined text
# would exceed GitHub's 125,000-character body limit (2.0.0: 92 highlights, 135k chars —
# gh release create would have failed AFTER the tag was pushed). Called in-process so the
# string never passes through the console encoding (trap 54).
$releaseNotes = (& "$PSScriptRoot\release-notes.ps1" -Version $version) -join "`n"

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
    # Only the EVOLVED copy (2026-09-28): the /SILENT install below replaces {autopf}\EQBuddy
    # Evolved and relaunches THAT, so killing a v1 widget running beside it would end the
    # session (EQBuddy finalizes into history.db on exit) and never bring it back.
    Get-Process EQBuddy -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path -like '*EQBuddy Evolved*' } | Stop-Process -Force
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

# EQBuddyEvolved-portable.zip, not v1's EQBuddy-portable.zip (2026-09-28): both land in the
# family's update folder, and one name for two lines would overwrite the v1 zip there.
# UpdateChecker.EvolvedPortableName is the same fact on the app side.
$portable = "$repo\dist\EQBuddyEvolved-portable.zip"
Compress-Archive -Path "$repo\dist\publish\EQBuddy.exe", "$repo\README.md" `
    -DestinationPath $portable -Force

# The portable zip gets a SHA-256 too (#119): portable users update by replacing their
# folder, and a future in-place portable updater will demand this hash the same
# way the installer path does. It is above the installer now rather than below it so the
# installer block can be one contiguous region — see the next comment.
(Get-FileHash $portable -Algorithm SHA256).Hash |
    Set-Content "$portable.sha256" -NoNewline

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
# of that shape has crept out of it. Since the Evolved channel opened (2026-09-28, see the
# top of this file) this region is REACHABLE at 2.x: -EvolvedLocal is the one switch that
# skips it, and it is the thing this region is conditioned on. A run without it publishes.
# ===================================================================================
if (-not $EvolvedLocal) {

# ORDER IS THE FIX (DRA-4219): tag, then GitHub release, then OneDrive LAST. The OneDrive
# copy is the one step a family widget acts on by itself within 6 hours, so it is the step
# that must never be followed by a throw. Before v2.0.8 it ran first and a moved main left
# it live with no tag; now a failed tag or release stops the run before the family sees it.
if ($Tag) {
    # Issue #56 (sahaq): `gh release create` tags whatever GitHub-side main happens to
    # be — if the release commit was never pushed, the tag lands on the PREVIOUS
    # release's commit and CI ships a stale Linux binary under the new version number.
    # So the tag is made HERE, on HEAD, by Publish-ReleaseTag (top of this file), which
    # never pushes main (DRA-925, DRA-4219) and refuses unless HEAD is on origin/main and
    # carries this version. `--verify-tag` below makes gh refuse rather than invent a tag.
    Publish-ReleaseTag -Repo $repo -Tag $Tag -Version $version
    # --notes-file rather than --generate-notes: the player-facing highlights beat a list
    # of commit subjects, which read as in-jokes to anyone who didn't write them.
    $notesFile = Join-Path ([System.IO.Path]::GetTempPath()) "eqbuddy-notes-$version.md"
    Set-Content -Path $notesFile -Value $releaseNotes -Encoding UTF8
    # The title a player reads carries the release LABEL (Directory.Build.props
    # <ReleaseLabel>, e.g. "EQBuddy Evolved 0.1 Beta (v2.0.0)"); the tag stays the number
    # the updater compares.
    $label = if ($props -match '<ReleaseLabel>([^<]+)</ReleaseLabel>') { " $($Matches[1].Trim())" } else { '' }
    $ghArgs = @($Tag,
        $setupExe, "$setupExe.sha256",
        $portable, "$portable.sha256",
        '--title', "EQBuddy Evolved$label ($Tag)", '--notes-file', $notesFile, '--verify-tag')

    # -Prerelease marks the GitHub release as a prerelease, and that ONE flag is what keeps a
    # v2 milestone away from every v1 client: `UpdateChecker.CheckGitHubAsync` reads
    # `/releases/latest`, and GitHub's latest-release endpoint excludes prereleases and
    # drafts. So a prerelease is invisible to the in-app updater without any client change —
    # which matters because the clients that need protecting are the ones already installed,
    # where no fix of ours can reach them. Charter RELEASE-002 asks for exactly this posture
    # during v2 construction (docs/v2, #275 / P0-1).
    #
    # Two things it does NOT do, both deliberate:
    #  * The OneDrive copy below is a SEPARATE channel — FindBestAsync checks the synced
    #    folder as well as GitHub — so a prerelease still reaches the family's widgets. That
    #    is the point of that folder; it is not covered by this flag.
    #  * It is not the only belt. `ParseRelease` runs `Version.TryParse` on the tag and
    #    returns null when it fails, so a tag shaped `v2.0.0-beta1` offers nothing even if it
    #    were marked latest. Belt, not replacement: a `v2.0.0` tag parses fine.
    # Not a prerelease means Latest, and that is the Founder's call (2026-09-28): installed
    # v1 copies read /releases/latest and are offered Evolved from it. Evolved itself does
    # not depend on Latest (UpdateChecker.PickEvolvedRelease reads the list).
    if ($Prerelease) { $ghArgs += '--prerelease' } else { $ghArgs += '--latest' }

    gh release create @ghArgs
    Remove-Item $notesFile -ErrorAction SilentlyContinue
    if ($LASTEXITCODE -ne 0) { throw 'gh release failed' }
    Write-Host ("GitHub release $Tag published" + $(if ($Prerelease) { ' as a PRERELEASE (excluded from releases/latest, so v1 clients will not be offered it)' } else { '' }))
}

$oneDrive = 'C:\Users\david\OneDrive\EQBuddyDownload'
New-Item -ItemType Directory -Force $oneDrive | Out-Null
Copy-Item $setupExe, "$setupExe.sha256", $portable $oneDrive -Force
Write-Host "Released $version to $oneDrive (family widgets will offer the update within 6 h)"

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
