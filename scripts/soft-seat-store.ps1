# Shared store for Experiment A' seat claims on EQBuddy (the lab).
# Dot-sourced by claim-seat.ps1 / release-seat.ps1.
# Local mutex only — not a scheduler, not a mailbox, not a Corps standard.
# The live file is gitignored; every worktree of this clone shares the MAIN tree's copy.

$script:SoftSeatExclusive = @('active', 'replacement')
$script:SoftSeatModes = @('active', 'challenger', 'disjoint', 'replacement', 'abandoned')

# DRA-76 (DRA-73 plan SS2.2 / SS8.4): a default claim is refused by ANY live
# seat on the work item, not only an exclusive one. A challenger and a disjoint
# slice are seats doing work on that card; admitting a default one beside them
# is the duplicate executor this store exists to stop (PRs #566/#568 cost one
# full run plus two rulings). The four explicit modes stay the only override.
$script:SoftSeatHolding = @('active', 'challenger', 'disjoint', 'replacement')

# The holding list is the detector, and a detector that drifts out of sync with
# the mode list fails OPEN — a new mode nobody added here would hold a seat that
# refuses nobody (CLAUDE.md trap 78: a guard aimed at nothing is green). Derive
# the expectation from SoftSeatModes and throw at dot-source time if they part.
$script:SoftSeatHoldingExpected = @($script:SoftSeatModes | Where-Object { $_ -ne 'abandoned' })
if (@(Compare-Object $script:SoftSeatHolding $script:SoftSeatHoldingExpected).Count -ne 0) {
    throw ("soft-seat-store: SoftSeatHolding [$($script:SoftSeatHolding -join ', ')] is not " +
        "SoftSeatModes minus 'abandoned' [$($script:SoftSeatHoldingExpected -join ', ')]. " +
        'A mode that holds no seat refuses nobody — add it to both lists or to neither.')
}

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

# ONE number for "old enough to be presumed dead". The claim refusal reads it to
# say so in the refusal text, and the release path reads it to act on it; two
# copies would let the sentence and the behaviour drift apart (trap 4).
$script:SoftSeatStaleAfterHours = 8

# ONE spelling of the DOCUMENTED call form (DRA-106, Helm-signed 2026-09-16).
# A linked worktree shares this clone's store (DRA-90) but carries its OWN
# checkout of claim-seat.ps1, so a RELATIVE call runs whatever copy that
# worktree happens to hold — and a pre-DRA-102 copy consults no registry and
# GRANTS the cross-clone duplicate DRA-102 closed. --git-common-dir answers the
# clone's .git from a linked worktree AND from the main checkout, so one
# invocation is right everywhere and nothing branches on where you are standing.
# Held here because the help text and two refusal lines quote it and three
# copies drift (trap 4). TEXT ONLY: no decision reads it, and the relative form
# is DEMOTED, not removed, so nothing in flight breaks.
$script:SoftSeatCallForm = 'pwsh -NoProfile -File "$(git rev-parse --path-format=absolute --git-common-dir)/../scripts/{0}"'

# Where the ONE store lives, and HOW we decided. The 'how' is returned, not
# just the path, because DRA-90 was reported as "the store is per-working-copy"
# by an executor who looked at a fresh worktree's .claude/soft-seats/ (README +
# claims.template.json are COMMITTED; claims.json is gitignored, so it is absent
# there BY DESIGN) and concluded the seats could not see each other. They can —
# git rev-parse --git-common-dir sends every linked worktree to the main tree's
# copy. A resolution nobody can print is a resolution everybody has to guess at,
# so claim-seat.ps1 -Where prints this.
#
# Resolution kinds: 'explicit' (-StoreDir), 'git-common-dir' (the shared answer),
# 'fallback' (git told us nothing — this copy is on its OWN store and any other
# seat is invisible, which is the failure DRA-90 described).
function Resolve-SoftSeatMainRoot {
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
            $common = ([string] $common).Trim()
            if (-not $common) { continue }
            if (-not [IO.Path]::IsPathRooted($common)) {
                $common = [IO.Path]::GetFullPath((Join-Path $start $common))
            }
            $root = Split-Path $common -Parent
            # The parent of the common dir IS the main working tree, by
            # construction. This used to additionally require a .gitignore
            # there and fall through to the WORKTREE root when it was missing
            # — a proxy for "is this the repo root" (trap 64b) whose failure
            # mode is silent and is exactly the bug: every worktree quietly
            # gets its own store and no seat ever refuses another. Accept the
            # directory git named; only "git named nothing" falls back now.
            if ($root -and (Test-Path -LiteralPath $root -PathType Container)) {
                return [pscustomobject]@{ root = $root; kind = 'git-common-dir'; shared = $true }
            }
        }
    }
    $fallback = if ($PSScriptRoot) { (Split-Path $PSScriptRoot -Parent) } else { (Get-Location).Path }
    return [pscustomobject]@{ root = $fallback; kind = 'fallback'; shared = $false }
}

function Get-SoftSeatMainRoot {
    param([string] $Hint)
    return (Resolve-SoftSeatMainRoot -Hint $Hint).root
}

function Get-SoftSeatStoreOrigin {
    param([string] $StoreDir, [string] $Repo)
    if ($StoreDir) {
        return [pscustomobject]@{
            dir    = [IO.Path]::GetFullPath($StoreDir)
            kind   = 'explicit'
            shared = $false
            root   = $null
        }
    }
    $resolved = Resolve-SoftSeatMainRoot -Hint $Repo
    return [pscustomobject]@{
        dir    = [IO.Path]::GetFullPath((Join-Path $resolved.root '.claude/soft-seats'))
        kind   = $resolved.kind
        shared = $resolved.shared
        root   = $resolved.root
    }
}

function Get-SoftSeatStoreDir {
    param([string] $StoreDir, [string] $Repo)
    return (Get-SoftSeatStoreOrigin -StoreDir $StoreDir -Repo $Repo).dir
}

# --- DRA-95 A' / DRA-102: union-READ, local-WRITE across independent CLONES --
#
# git-common-dir makes every linked WORKTREE of one clone share a store (above).
# Two independent CLONES share nothing, and on this machine the two clones ARE
# the two dispatch lanes: DRA-87's two executors both claimed, 2m29s apart, and
# neither was refused or warned because neither store could see the other.
#
# The signed shape is a machine-level REGISTRY OF PATHS. Each clone registers
# its own resolved store once; the refusal decision reads every registered
# store; the write touches only this clone's. No claim data leaves the repo —
# the registry holds paths and a first-seen stamp and nothing else, which is why
# this is A' and not A (moving the claims themselves out of the tree).
#
# Registry absent, unreadable, or switched off => behave exactly as before.
# That degradation is DELIBERATE and it is also a hole somebody could fall into
# without noticing (trap 68), so a GRANT that was decided without the union read
# says so on the same screen (claim-seat.ps1).
$script:SoftSeatRegistryEnvVar = 'EQBUDDY_SOFT_SEAT_REGISTRY'
$script:SoftSeatRegistryOff = 'off'

function Get-SoftSeatRegistryLocation {
    $override = [Environment]::GetEnvironmentVariable($script:SoftSeatRegistryEnvVar)
    if ($override) {
        $t = ([string] $override).Trim()
        # Exactly that string, and nothing else, is the door (trap 68).
        if ($t -ieq $script:SoftSeatRegistryOff) {
            return [pscustomobject]@{ path = $null; state = 'off'; why = "$($script:SoftSeatRegistryEnvVar)=$($script:SoftSeatRegistryOff)" }
        }
        if ($t) {
            return [pscustomobject]@{ path = [IO.Path]::GetFullPath($t); state = 'ok'; why = "$($script:SoftSeatRegistryEnvVar) points here" }
        }
    }
    $base = [Environment]::GetEnvironmentVariable('LOCALAPPDATA')
    if (-not $base) {
        return [pscustomobject]@{ path = $null; state = 'unavailable'; why = 'LOCALAPPDATA is not set, so there is no machine-level place to keep it' }
    }
    return [pscustomobject]@{
        path  = [IO.Path]::GetFullPath((Join-Path $base 'DranakCorps/soft-seats/stores.json'))
        state = 'ok'
        why   = 'the machine default'
    }
}

# One spelling for "are these two paths the same store". Every comparison in
# this block goes through it, so a trailing slash or a case difference cannot
# make this clone's own store look like a foreign one (it would then read its
# own rows twice and refuse itself).
function Get-SoftSeatComparablePath {
    param([string] $Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return $null }
    $full = $Path
    try { $full = [IO.Path]::GetFullPath($Path) } catch { $full = $Path }
    return ($full.TrimEnd('\', '/')).ToLowerInvariant()
}

function Read-SoftSeatRegistry {
    $loc = Get-SoftSeatRegistryLocation
    $result = [pscustomobject]@{
        path      = $loc.path
        state     = $loc.state
        why       = $loc.why
        stores    = @()
        malformed = @()
    }
    if ($loc.state -ne 'ok') { return $result }
    if (-not (Test-Path -LiteralPath $loc.path -PathType Leaf)) { $result.state = 'absent'; return $result }

    $raw = $null
    try { $raw = [IO.File]::ReadAllText($loc.path) }
    catch { $result.state = 'unreadable'; $result.why = $_.Exception.Message; return $result }
    if ([string]::IsNullOrWhiteSpace($raw)) { $result.state = 'absent'; return $result }

    $obj = $null
    try { $obj = $raw | ConvertFrom-Json }
    catch { $result.state = 'unreadable'; $result.why = $_.Exception.Message; return $result }

    $entries = @()
    $bad = @()
    # trap 80, twice over. (a) Enumerate through the PIPELINE — @($obj.stores)
    # is safe for a property but ForEach-Object unrolls the 1-element case that
    # ConvertTo-Json writes differently on 5.1 and 7 as well. (b) The field we
    # are about to ACT on must be a SCALAR: if 'dir' arrives as an array, then
    # `$dir -ieq $ownDir` is a FILTER returning the matching elements, not a
    # boolean — an empty result is falsy, a non-empty one is truthy, and either
    # way the decision is no longer the one the code reads as. A malformed entry
    # is DROPPED and NAMED, never guessed at.
    foreach ($e in @($obj.stores | ForEach-Object { $_ })) {
        if ($null -eq $e) { continue }
        $dir = $e.dir
        if ($dir -isnot [string] -or [string]::IsNullOrWhiteSpace($dir)) {
            $shape = if ($null -eq $dir) { 'absent' } else { $dir.GetType().Name }
            $bad += "an entry whose 'dir' is not a single path string (it is $shape)"
            continue
        }
        $root = $e.root
        if ($root -isnot [string] -or [string]::IsNullOrWhiteSpace($root)) { $root = $null }
        $entries += [pscustomobject]@{
            dir        = $dir
            root       = $root
            first_seen = (Get-SoftSeatStartedAtIso $e.first_seen)
        }
    }
    $result.stores = @($entries)
    $result.malformed = @($bad)
    return $result
}

function Write-SoftSeatRegistryFile {
    param([string] $Path, $Stores)
    $payload = [ordered]@{
        version    = 1
        updated_at = [datetime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        stores     = @($Stores)
    }
    $json = $payload | ConvertTo-Json -Depth 6
    $dir = Split-Path $Path -Parent
    if ($dir -and -not (Test-Path -LiteralPath $dir -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }
    $tmp = "$Path.tmp"
    [IO.File]::WriteAllText($tmp, $json, [Text.UTF8Encoding]::new($false))
    [IO.File]::Copy($tmp, $Path, $true)
    Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
}

# Register THIS clone's store, once. An explicit -StoreDir is a per-call store —
# claim-seat.ps1 already prints it as "this call only, NOT the machine's shared
# store" — so it never registers and never union-reads. Registering a throwaway
# dir would put a path that is deleted minutes later in front of every future
# refusal.
#
# Never throws: a registry we cannot write is the degraded case, not a failure.
function Register-SoftSeatStore {
    param($Origin)
    if (-not $Origin -or $Origin.kind -eq 'explicit') {
        return [pscustomobject]@{ action = 'skipped'; why = 'an explicit -StoreDir is a per-call store, not a clone'; path = $null }
    }
    $loc = Get-SoftSeatRegistryLocation
    if ($loc.state -ne 'ok') {
        return [pscustomobject]@{ action = $loc.state; why = $loc.why; path = $null }
    }
    try {
        $dir = Split-Path $loc.path -Parent
        if ($dir -and -not (Test-Path -LiteralPath $dir -PathType Container)) {
            New-Item -ItemType Directory -Force -Path $dir | Out-Null
        }
        $lockPath = Join-Path $dir 'stores.lock'
        $lock = $null
        for ($try = 0; $try -lt 4 -and -not $lock; $try++) {
            try {
                $lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            }
            catch {
                $lock = $null
                Start-Sleep -Milliseconds 100
            }
        }
        if (-not $lock) {
            return [pscustomobject]@{ action = 'failed'; why = "another process holds $lockPath"; path = $loc.path }
        }
        try {
            # Re-read UNDER the lock: two clones registering at once must not
            # each write the other away.
            $reg = Read-SoftSeatRegistry
            $own = Get-SoftSeatComparablePath $Origin.dir
            foreach ($s in @($reg.stores)) {
                if ((Get-SoftSeatComparablePath $s.dir) -eq $own) {
                    return [pscustomobject]@{ action = 'present'; why = 'already registered'; path = $loc.path }
                }
            }
            # Malformed rows are dropped by the reader and therefore by this
            # rewrite. A row whose 'dir' is not a path names no store, so there
            # is nothing to preserve — and keeping it would hand the next read
            # the same unusable entry to warn about forever.
            $next = @($reg.stores) + [pscustomobject]@{
                dir        = [IO.Path]::GetFullPath($Origin.dir)
                root       = $Origin.root
                first_seen = [datetime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
            }
            Write-SoftSeatRegistryFile -Path $loc.path -Stores $next
            return [pscustomobject]@{ action = 'registered'; why = $loc.why; path = $loc.path }
        }
        finally { $lock.Dispose() }
    }
    catch {
        return [pscustomobject]@{ action = 'failed'; why = $_.Exception.Message; path = $loc.path }
    }
}

# Every registered store that is NOT this one. Empty for an explicit -StoreDir.
function Get-SoftSeatForeignStores {
    param($Origin, $Registry)
    if (-not $Origin -or $Origin.kind -eq 'explicit') { return @() }
    if (-not $Registry -or $Registry.state -ne 'ok') { return @() }
    $own = Get-SoftSeatComparablePath $Origin.dir
    $out = @()
    foreach ($s in @($Registry.stores)) {
        $cmp = Get-SoftSeatComparablePath $s.dir
        if (-not $cmp -or $cmp -eq $own) { continue }
        $root = $s.root
        if (-not $root) {
            $parent = Split-Path $s.dir -Parent
            if ($parent) { $root = Split-Path $parent -Parent }
        }
        $out += [pscustomobject]@{
            dir    = $s.dir
            root   = $root
            exists = (Test-Path -LiteralPath $s.dir -PathType Container)
        }
    }
    return @($out)
}

# The union READ. Foreign stores are read WITHOUT taking their lock: a lock we
# hold across two stores is a deadlock the moment the other clone claims in the
# other order, and a torn read throws rather than lying, which is reported.
#
# A foreign row is NOT excluded by seat id, unlike a local one. Locally that
# exclusion is what makes a re-claim idempotent; across clones the same seat
# name in another working copy is a second checkout doing the work, and the
# refusal names where its row lives so it can be released THERE.
function Read-SoftSeatForeignHolders {
    param($Stores, [string] $WorkItem)
    $holders = @()
    $unreadable = @()
    foreach ($s in @($Stores)) {
        if (-not $s) { continue }
        if (-not $s.exists) {
            $unreadable += [pscustomobject]@{ dir = $s.dir; reason = 'the directory no longer exists' }
            continue
        }
        $path = Join-Path $s.dir 'claims.json'
        # A registered clone that has never claimed has no store file. That is
        # not a hole — there is nothing there to miss.
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        $store = $null
        try { $store = Read-SoftSeatStoreFile $s.dir }
        catch {
            $unreadable += [pscustomobject]@{ dir = $s.dir; reason = $_.Exception.Message }
            continue
        }
        foreach ($c in @(Get-SoftSeatMatches $store -WorkItem $WorkItem -HoldingOnly)) {
            $c | Add-Member -NotePropertyName store_dir -NotePropertyValue $s.dir -Force
            $c | Add-Member -NotePropertyName store_root -NotePropertyValue $s.root -Force
            $holders += $c
        }
    }
    return [pscustomobject]@{ holders = @($holders); unreadable = @($unreadable) }
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

# "Does this row hold the work item against a DEFAULT claim?" Every mode but
# 'abandoned' does. Kept separate from Test-SoftSeatExclusive on purpose:
# exclusivity is what -Mode replacement takes over, holding is what refuses.
function Test-SoftSeatHolding {
    param([string] $Status)
    return $script:SoftSeatHolding -contains ([string] $Status).ToLowerInvariant()
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
    param(
        $Store,
        [string] $WorkItem,
        [string] $SeatId,
        [string] $NotSeatId,
        [switch] $ExclusiveOnly,
        [switch] $HoldingOnly,
        [switch] $LiveOnly
    )
    $wantItem = Normalize-SoftSeatWorkItem $WorkItem
    $wantSeat = if ($SeatId) { $SeatId.Trim() } else { $null }
    $skipSeat = if ($NotSeatId) { $NotSeatId.Trim() } else { $null }
    $out = @()
    foreach ($c in @($Store.claims)) {
        if ($LiveOnly -and ([string] $c.status).ToLowerInvariant() -eq 'abandoned') { continue }
        if ($ExclusiveOnly -and -not (Test-SoftSeatExclusive $c.status)) { continue }
        if ($HoldingOnly -and -not (Test-SoftSeatHolding $c.status)) { continue }
        if ($wantItem -and ((Normalize-SoftSeatWorkItem $c.work_item) -ne $wantItem)) { continue }
        if ($wantSeat -and ([string] $c.seat_id -ine $wantSeat)) { continue }
        if ($skipSeat -and ([string] $c.seat_id -ieq $skipSeat)) { continue }
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
    $line = ($parts -join ', ')
    # A foreign holder must name the CLONE it is in, or the refusal sends the
    # reader to look for a row in a store that does not have it (DRA-102).
    # store_dir is stamped only by Read-SoftSeatForeignHolders, so a local row
    # prints exactly as it always did.
    if ($Claim.PSObject.Properties['store_dir'] -and $Claim.store_dir) {
        $where = if ($Claim.store_root) { $Claim.store_root } else { $Claim.store_dir }
        $line += " [in another CLONE: $where — store $($Claim.store_dir)]"
    }
    return $line
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
        $ForeignStores,
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

        # Every live seat OTHER than this one holds the work item against a
        # default claim — challengers and disjoint slices included (DRA-76).
        # Excluding our own rows by seat id is what keeps a re-claim idempotent;
        # it is not an exception, this seat is simply not a second executor.
        $holders = @(Get-SoftSeatMatches $store -WorkItem $item -NotSeatId $SeatId -HoldingOnly)

        # ...and every live seat in every OTHER registered clone (DRA-102).
        # This is the whole union-READ half: the decision sees them, the write
        # below never touches their file.
        $foreign = Read-SoftSeatForeignHolders -Stores $ForeignStores -WorkItem $item
        $foreignHolders = @($foreign.holders)
        if ($foreignHolders.Count -gt 0) { $holders = @($holders) + $foreignHolders }

        $ownExclusive = $false
        foreach ($c in $mine) {
            if (Test-SoftSeatExclusive $c.status) { $ownExclusive = $true; break }
        }

        if ($Mode -eq 'active' -and $holders.Count -gt 0) {
            $label = Format-SoftSeatWorkItem $item
            $lines = @($holders | ForEach-Object { "  - $(Format-SoftSeatHolder $_)" })
            # Widening the refusal without making recovery discoverable just
            # manufactures false blocks (the risk row in the store's README):
            # a holder that is gone must say so on the same screen as the refusal.
            $stale = @($holders | Where-Object { Test-SoftSeatStale $_ $script:SoftSeatStaleAfterHours })
            $staleLine = ''
            if ($stale.Count -gt 0) {
                $names = ($stale | ForEach-Object { "'$($_.seat_id)'" }) -join ', '
                $staleLine = "`nLooks stale (age >= $($script:SoftSeatStaleAfterHours)h or a recorded pid that is no longer running): $names."
            }
            # A foreign row cannot be released from here — this seat writes only
            # its own store. Naming the clone is not decoration: without the
            # path, the reader runs release-seat.ps1 in THIS tree, is told "no
            # live claim matches", and concludes the refusal was spurious.
            $foreignLine = ''
            if ($foreignHolders.Count -gt 0) {
                $cmds = @($foreignHolders | ForEach-Object {
                        $r = if ($_.store_root) { $_.store_root } else { (Split-Path (Split-Path $_.store_dir -Parent) -Parent) }
                        "  pwsh -NoProfile -File `"$(Join-Path $r 'scripts/release-seat.ps1')`" -WorkItem $label -SeatId $($_.seat_id)"
                    } | Select-Object -Unique)
                $foreignLine = "`n$($foreignHolders.Count) of those seat(s) live in ANOTHER CLONE on this machine (found through the store registry, DRA-102). This script writes only its own store, so release them where they are:`n$($cmds -join "`n")"
            }
            $msg = @"
REFUSED: $label is already held by $($holders.Count) live seat(s):
$($lines -join "`n")
A default Soft seat on a work item another seat already holds is the duplicate executor this store exists to stop (CLAUDE.md trap 70). A challenger and a disjoint slice HOLD the item too — only an abandoned claim releases it.$staleLine$foreignLine
If this seat is an explicit challenger, disjoint slice, or replacement, pass -Mode challenger|disjoint|replacement.
If the holder is gone: $($script:SoftSeatCallForm -f 'release-seat.ps1') -WorkItem $label -ForceStale
"@.Trim()
            return [pscustomobject]@{
                ok                 = $false
                message            = $msg
                claim              = $holders[0]
                holders            = $holders
                foreign_holders    = $foreignHolders
                foreign_unreadable = @($foreign.unreadable)
                store              = $store
            }
        }

        if ($Check) {
            $msg = "OK: $(Format-SoftSeatWorkItem $item) is claimable as $Mode by seat '$($SeatId.Trim())'."
            if ($ownExclusive) { $msg = "OK: $(Format-SoftSeatWorkItem $item) is already claimed by this seat ($((Format-SoftSeatHolder $mine[0])))." }
            return [pscustomobject]@{
                ok                 = $true
                message            = $msg
                claim              = $(if ($mine) { $mine[0] } else { $null })
                foreign_holders    = $foreignHolders
                foreign_unreadable = @($foreign.unreadable)
                store              = $store
            }
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
        return [pscustomobject]@{
            ok                 = $true
            message            = $msg
            claim              = $claim
            # An explicit mode was ADMITTED beside these; -Mode replacement
            # abandoned the local exclusive row and could not touch theirs.
            # Saying so is the difference between "took over" and "took over
            # here" (local-WRITE is the other half of the signed shape).
            foreign_holders    = $foreignHolders
            foreign_unreadable = @($foreign.unreadable)
            store              = $store
        }
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
        [double] $StaleAfterHours = 0
    )
    if ($StaleAfterHours -le 0) { $StaleAfterHours = $script:SoftSeatStaleAfterHours }
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
