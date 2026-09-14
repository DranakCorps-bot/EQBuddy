# Shared store for Experiment A' seat claims on EQBuddy (the lab).
# Dot-sourced by claim-seat.ps1 / release-seat.ps1.
# Local mutex only — not a scheduler, not a mailbox, not a Corps standard.
# The live file is gitignored; every worktree of this clone shares the MAIN tree's copy.

$script:SoftSeatExclusive = @('active', 'replacement')
$script:SoftSeatModes = @('active', 'challenger', 'disjoint', 'replacement', 'abandoned')

# Paperclip is the tracker for Soft work, so a CLAIM keys on the card id and
# nothing else (EXO-HARDEN-A2 / DRA-50, 2026-09-10). One scope carries two
# names — GitHub #445 IS Paperclip DRA-28 — and the mutex used to key on free
# text, so two seats under two spellings of one scope both started and neither
# was refused. That is the duplicate start trap 70 exists to stop.
#
# We REFUSE a bare issue number rather than map it. An auto-map is a guess
# about which tracker the caller meant, and a wrong guess is a claim key that
# silently misses the holder — the same failure wearing a helpful hat.
$script:SoftSeatKeyPattern = '^DRA-\d+$'

function Get-SoftSeatMainRoot {
    param([string] $Hint)
    $starts = @()
    if ($Hint) { $starts += $Hint }
    if ($PSScriptRoot) { $starts += (Split-Path $PSScriptRoot -Parent) }
    $starts += (Get-Location).Path
    foreach ($start in $starts) {
        if (-not $start) { continue }
        $common = $null
        try { $common = git -C $start rev-parse --path-format=absolute --git-common-dir 2>$null } catch { $common = $null }
        if (-not $common) {
            try { $common = git -C $start rev-parse --git-common-dir 2>$null } catch { $common = $null }
        }
        if ($common) {
            $common = $common.Trim()
            if (-not [IO.Path]::IsPathRooted($common)) {
                $common = [IO.Path]::GetFullPath((Join-Path $start $common))
            }
            $root = Split-Path $common -Parent
            if (Test-Path (Join-Path $root '.gitignore')) { return $root }
        }
    }
    if ($PSScriptRoot) { return (Split-Path $PSScriptRoot -Parent) }
    return (Get-Location).Path
}

function Get-SoftSeatStoreDir {
    param([string] $StoreDir, [string] $Repo)
    if ($StoreDir) { return [IO.Path]::GetFullPath($StoreDir) }
    $root = Get-SoftSeatMainRoot -Hint $Repo
    return [IO.Path]::GetFullPath((Join-Path $root '.claude/soft-seats'))
}

function Normalize-SoftSeatWorkItem {
    param([string] $WorkItem)
    if (-not $WorkItem) { return $null }
    $t = $WorkItem.Trim()
    if ($t.StartsWith('#')) { $t = $t.Substring(1).Trim() }
    # 'dra-28' and 'DRA-28' are ONE claim key. Case is a spelling, and a
    # spelling that does not collide is the whole bug this file guards.
    if ($t -match '^[Dd][Rr][Aa]-(\d+)$') { $t = "DRA-$($Matches[1])" }
    return $t
}

# -cmatch, not -match. The pattern is checked against the NORMALIZED key, so
# the case fold above is what makes 'dra-28' legal — the stored key is always
# the canonical 'DRA-28'. Under case-insensitive -match the fold carried no
# weight (removing it failed nothing), which is a guard that cannot see its
# own subject.
function Test-SoftSeatWorkItemKey {
    param([string] $WorkItem)
    $item = Normalize-SoftSeatWorkItem $WorkItem
    if (-not $item) { return $false }
    return ($item -cmatch $script:SoftSeatKeyPattern)
}

# Returns the normalized key, or throws with the reason. Claims only —
# Invoke-SoftSeatRelease stays permissive on purpose: claims recorded under the
# old free-text keys still have to be releasable, or this change strands them.
function Assert-SoftSeatWorkItemKey {
    param([string] $WorkItem)
    $raw = ([string] $WorkItem).Trim()
    $item = Normalize-SoftSeatWorkItem $WorkItem
    if (-not $item) {
        throw '-WorkItem is required: the Paperclip card id, DRA-<n> (e.g. DRA-28).'
    }
    if ($item -cmatch $script:SoftSeatKeyPattern) { return $item }
    if ($item -match '^\d+$') {
        throw @"
REFUSED: -WorkItem '$raw' is a GitHub issue number, and a Soft seat claims on the Paperclip card id.
GitHub #$item and its DRA-<n> card are two names for ONE scope; two claims under two spellings never collide, so both seats start (CLAUDE.md trap 70).
Pass the card: -WorkItem DRA-<n>. This script will NOT map an issue number onto a card for you — look the card up in Paperclip and pass it yourself.
"@.Trim()
    }
    throw @"
REFUSED: -WorkItem '$raw' is not a Paperclip card id. A Soft seat claim key is DRA-<n> (e.g. DRA-28) and nothing else.
A free-text id is a second spelling of a scope that already has a card, so two seats can hold one scope without colliding (CLAUDE.md trap 70).
"@.Trim()
}

function Format-SoftSeatWorkItem {
    param([string] $WorkItem)
    $n = Normalize-SoftSeatWorkItem $WorkItem
    if (-not $n) { return '' }
    # A claim can no longer be MADE under a bare number, but rows written
    # before DRA-50 still are — keep printing them as '#n' so -List and
    # release-seat.ps1 name them the way they were claimed.
    if ($n -match '^\d+$') { return "#$n" }
    return $n
}

function Get-SoftSeatStartedAtIso {
    param($Value)
    if ($null -eq $Value -or $Value -eq '') { return $null }
    if ($Value -is [datetime]) {
        return ([datetime] $Value).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    }
    $parsed = [datetime]::MinValue
    if ([datetime]::TryParse([string] $Value, [ref] $parsed)) {
        return $parsed.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    }
    return [string] $Value
}

function Test-SoftSeatExclusive {
    param([string] $Status)
    return $script:SoftSeatExclusive -contains ([string] $Status).ToLowerInvariant()
}

function Test-SoftSeatPidAlive {
    param($PidValue)
    if ($null -eq $PidValue -or $PidValue -eq '') { return $false }
    $id = 0
    if (-not [int]::TryParse([string] $PidValue, [ref] $id)) { return $false }
    if ($id -le 0) { return $false }
    try {
        $null = Get-Process -Id $id -ErrorAction Stop
        return $true
    }
    catch { return $false }
}

function Test-SoftSeatStale {
    param($Claim, [double] $StaleAfterHours)
    if (-not $Claim) { return $false }
    $iso = Get-SoftSeatStartedAtIso $Claim.started_at
    $started = [datetime]::MinValue
    if ($iso -and [datetime]::TryParse($iso, [ref] $started)) {
        $age = [datetime]::UtcNow - $started.ToUniversalTime()
        if ($age -ge [TimeSpan]::FromHours($StaleAfterHours)) { return $true }
    }
    if ($null -ne $Claim.pid -and $Claim.pid -ne '' -and -not (Test-SoftSeatPidAlive $Claim.pid)) {
        return $true
    }
    return $false
}

function New-SoftSeatStoreObject {
    return [pscustomobject]@{
        version    = 1
        updated_at = $null
        claims     = @()
    }
}

function Read-SoftSeatStoreFile {
    param([string] $StoreDir)
    $path = Join-Path $StoreDir 'claims.json'
    if (-not (Test-Path $path)) { return (New-SoftSeatStoreObject) }
    $raw = [IO.File]::ReadAllText($path)
    if ([string]::IsNullOrWhiteSpace($raw)) { return (New-SoftSeatStoreObject) }
    $obj = $raw | ConvertFrom-Json
    $claims = @()
    if ($obj.claims) { $claims = @($obj.claims) }
    foreach ($c in $claims) {
        # ConvertFrom-Json turns ISO timestamps into DateTime; keep the file's
        # contract as a UTC string so 5.1 and 7 write the same shape.
        $c.started_at = Get-SoftSeatStartedAtIso $c.started_at
    }
    return [pscustomobject]@{
        version    = 1
        updated_at = (Get-SoftSeatStartedAtIso $obj.updated_at)
        claims     = $claims
    }
}

function Write-SoftSeatStoreFile {
    param([string] $StoreDir, $Store)
    if (-not (Test-Path $StoreDir)) {
        New-Item -ItemType Directory -Force -Path $StoreDir | Out-Null
    }
    $payload = [ordered]@{
        version    = 1
        updated_at = [datetime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        claims     = @($Store.claims)
    }
    $json = $payload | ConvertTo-Json -Depth 6
    $path = Join-Path $StoreDir 'claims.json'
    $tmp = Join-Path $StoreDir 'claims.json.tmp'
    [IO.File]::WriteAllText($tmp, $json, [Text.UTF8Encoding]::new($false))
    [IO.File]::Copy($tmp, $path, $true)
    Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
}

function Lock-SoftSeatStore {
    param([string] $StoreDir)
    if (-not (Test-Path $StoreDir)) {
        New-Item -ItemType Directory -Force -Path $StoreDir | Out-Null
    }
    $lockPath = Join-Path $StoreDir 'claims.lock'
    return [IO.File]::Open(
        $lockPath,
        [IO.FileMode]::OpenOrCreate,
        [IO.FileAccess]::ReadWrite,
        [IO.FileShare]::None)
}

function Get-SoftSeatMatches {
    param($Store, [string] $WorkItem, [string] $SeatId, [switch] $ExclusiveOnly, [switch] $LiveOnly)
    $wantItem = Normalize-SoftSeatWorkItem $WorkItem
    $wantSeat = if ($SeatId) { $SeatId.Trim() } else { $null }
    $out = @()
    foreach ($c in @($Store.claims)) {
        if ($LiveOnly -and ([string] $c.status).ToLowerInvariant() -eq 'abandoned') { continue }
        if ($ExclusiveOnly -and -not (Test-SoftSeatExclusive $c.status)) { continue }
        if ($wantItem -and ((Normalize-SoftSeatWorkItem $c.work_item) -ne $wantItem)) { continue }
        if ($wantSeat -and ([string] $c.seat_id -ine $wantSeat)) { continue }
        $out += $c
    }
    return $out
}

function Format-SoftSeatHolder {
    param($Claim)
    if (-not $Claim) { return '(none)' }
    $parts = @(
        "seat '$($Claim.seat_id)'",
        "$($Claim.status) since $(Get-SoftSeatStartedAtIso $Claim.started_at)"
    )
    if ($Claim.pid) { $parts += "pid $($Claim.pid)" }
    if ($Claim.branch) { $parts += "branch $($Claim.branch)" }
    if ($Claim.worktree) { $parts += "worktree $($Claim.worktree)" }
    return ($parts -join ', ')
}

function New-SoftSeatClaimObject {
    param(
        [string] $WorkItem,
        [string] $SeatId,
        [string] $Status,
        [string] $Branch,
        [string] $Worktree,
        $ExecutorPid
    )
    $pidVal = $null
    if ($null -ne $ExecutorPid -and $ExecutorPid -ne '') { $pidVal = [int] $ExecutorPid }
    return [pscustomobject]@{
        work_item  = (Normalize-SoftSeatWorkItem $WorkItem)
        seat_id    = $SeatId.Trim()
        branch     = $Branch
        worktree   = $Worktree
        started_at = [datetime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        status     = $Status.ToLowerInvariant()
        pid        = $pidVal
    }
}

function Invoke-SoftSeatClaim {
    param(
        [string] $StoreDir,
        [string] $WorkItem,
        [string] $SeatId,
        [string] $Mode = 'active',
        [string] $Branch,
        [string] $Worktree,
        $ExecutorPid,
        [switch] $Check
    )
    $Mode = $Mode.ToLowerInvariant()
    if ($script:SoftSeatModes -notcontains $Mode -or $Mode -eq 'abandoned') {
        throw "Unknown -Mode '$Mode'. Use active (default), challenger, disjoint, or replacement."
    }
    $item = Assert-SoftSeatWorkItemKey $WorkItem
    if (-not $SeatId) { throw '-SeatId is required (a name for this executor / worktree).' }

    $lock = Lock-SoftSeatStore $StoreDir
    try {
        $store = Read-SoftSeatStoreFile $StoreDir
        $mine = @(Get-SoftSeatMatches $store -WorkItem $item -SeatId $SeatId -LiveOnly)
        $exclusive = @(Get-SoftSeatMatches $store -WorkItem $item -ExclusiveOnly -LiveOnly)

        $ownExclusive = $false
        foreach ($c in $mine) {
            if (Test-SoftSeatExclusive $c.status) { $ownExclusive = $true; break }
        }

        if ($Mode -eq 'active' -and $exclusive.Count -gt 0 -and -not $ownExclusive) {
            $holder = Format-SoftSeatHolder $exclusive[0]
            $label = Format-SoftSeatWorkItem $item
            $msg = @"
REFUSED: $label is already claimed ($holder).
A second default Soft seat on the same work item is the collision this store exists to stop (CLAUDE.md trap 70).
If this seat is an explicit challenger, disjoint slice, or replacement, pass -Mode challenger|disjoint|replacement.
If the holder is gone: pwsh -NoProfile -File scripts/release-seat.ps1 -WorkItem $label -ForceStale
"@.Trim()
            return [pscustomobject]@{ ok = $false; message = $msg; claim = $exclusive[0]; store = $store }
        }

        if ($Check) {
            $msg = "OK: $(Format-SoftSeatWorkItem $item) is claimable as $Mode by seat '$($SeatId.Trim())'."
            if ($ownExclusive) { $msg = "OK: $(Format-SoftSeatWorkItem $item) is already claimed by this seat ($((Format-SoftSeatHolder $mine[0])))." }
            return [pscustomobject]@{ ok = $true; message = $msg; claim = $(if ($mine) { $mine[0] } else { $null }); store = $store }
        }

        if ($Mode -eq 'replacement') {
            $kept = @()
            foreach ($c in @($store.claims)) {
                $sameItem = (Normalize-SoftSeatWorkItem $c.work_item) -eq $item
                $isMe = [string] $c.seat_id -ieq $SeatId.Trim()
                if ($sameItem -and -not $isMe -and (Test-SoftSeatExclusive $c.status)) {
                    $c.status = 'abandoned'
                }
                $kept += $c
            }
            $store.claims = $kept
        }

        $updated = $false
        $claim = $null
        $next = @()
        foreach ($c in @($store.claims)) {
            $sameItem = (Normalize-SoftSeatWorkItem $c.work_item) -eq $item
            $isMe = [string] $c.seat_id -ieq $SeatId.Trim()
            if ($sameItem -and $isMe -and ([string] $c.status).ToLowerInvariant() -ne 'abandoned') {
                $c.status = $Mode
                if ($Branch) { $c.branch = $Branch }
                if ($Worktree) { $c.worktree = $Worktree }
                if ($null -ne $ExecutorPid -and $ExecutorPid -ne '') { $c.pid = [int] $ExecutorPid }
                $claim = $c
                $updated = $true
            }
            $next += $c
        }
        if (-not $updated) {
            $claim = New-SoftSeatClaimObject -WorkItem $item -SeatId $SeatId -Status $Mode -Branch $Branch -Worktree $Worktree -ExecutorPid $ExecutorPid
            $next += $claim
        }
        $store.claims = $next
        Write-SoftSeatStoreFile $StoreDir $store
        $verb = if ($updated) { 'refreshed' } else { 'claimed' }
        $msg = "OK: $verb $(Format-SoftSeatWorkItem $item) as $Mode for seat '$($SeatId.Trim())' ($(Format-SoftSeatHolder $claim))."
        return [pscustomobject]@{ ok = $true; message = $msg; claim = $claim; store = $store }
    }
    finally {
        $lock.Dispose()
    }
}

function Invoke-SoftSeatRelease {
    param(
        [string] $StoreDir,
        [string] $WorkItem,
        [string] $SeatId,
        [switch] $ForceStale,
        [double] $StaleAfterHours = 8
    )
    $item = Normalize-SoftSeatWorkItem $WorkItem
    if (-not $item -and -not $SeatId) {
        throw 'Pass -WorkItem and/or -SeatId (or -ForceStale with -WorkItem to recover a dead holder).'
    }

    $lock = Lock-SoftSeatStore $StoreDir
    try {
        $store = Read-SoftSeatStoreFile $StoreDir
        $targets = @(Get-SoftSeatMatches $store -WorkItem $item -SeatId $SeatId -LiveOnly)
        if ($targets.Count -eq 0) {
            $label = if ($item) { Format-SoftSeatWorkItem $item } else { "seat '$SeatId'" }
            return [pscustomobject]@{
                ok      = $false
                message = "REFUSED: no live claim matches $label."
                store   = $store
            }
        }

        $released = @()
        $blocked = @()
        foreach ($c in $targets) {
            $mine = $SeatId -and ([string] $c.seat_id -ieq $SeatId.Trim())
            $stale = Test-SoftSeatStale $c $StaleAfterHours
            if ($mine -or ($ForceStale -and $stale)) {
                $c.status = 'abandoned'
                $released += $c
            }
            else {
                $blocked += $c
            }
        }

        if ($released.Count -eq 0) {
            $holder = Format-SoftSeatHolder $blocked[0]
            $why = 'it is not this seat'
            if ($ForceStale) {
                $why = 'it is not stale (still inside the age threshold and its pid is alive, or no pid was recorded)'
            }
            else {
                $why = "$why — pass -SeatId of the holder, or -ForceStale after the age threshold / a dead pid"
            }
            return [pscustomobject]@{
                ok      = $false
                message = "REFUSED: cannot release ($holder) because $why."
                store   = $store
            }
        }

        Write-SoftSeatStoreFile $StoreDir $store
        $names = ($released | ForEach-Object { "$($_.seat_id)/$(Format-SoftSeatWorkItem $_.work_item)" }) -join ', '
        return [pscustomobject]@{
            ok      = $true
            message = "OK: abandoned $names."
            store   = $store
        }
    }
    finally {
        $lock.Dispose()
    }
}

function Write-SoftSeatList {
    param([string] $StoreDir)
    $store = Read-SoftSeatStoreFile $StoreDir
    $live = @($store.claims | Where-Object { ([string] $_.status).ToLowerInvariant() -ne 'abandoned' })
    if ($live.Count -eq 0) {
        Write-Host 'No live Soft seat claims.'
        return 0
    }
    foreach ($c in $live) {
        Write-Host ("  {0,-12} {1,-12} {2}" -f (Format-SoftSeatWorkItem $c.work_item), $c.status, (Format-SoftSeatHolder $c))
    }
    return 0
}
