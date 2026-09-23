<#
.SYNOPSIS
    The prove-fail for `challenge-line-guard.ps1`: every refusal driven RED at least once,
    every shape the rule ALLOWS driven green, and both pattern lists shown to be load-bearing.

.DESCRIPTION
    DRA-309 S3. A guard nobody has watched refuse is a guard aimed at nothing - trap 34 (a
    forbid-scan cannot see a missing thing) and trap 78 (a detector whose pattern list
    silently collapsed matched nothing and reported clean for a month). This is the copy of
    that lesson for the challenge-line guard, and it runs in CI beside the guard itself.

    It builds throwaway plan corpora under the run's temp directory - never `docs/plans` -
    and drives the guard over them. Where a case needs a must-list other than the committed
    one, it runs a MUTATED COPY of the guard with that list substituted, which is the only
    honest way to exercise a curated list without pretending the live one is a parameter.

    THE GREEN HALF IS NOT FILLER. Three of the cases below assert the guard does NOT fire:
    on a plan that never reached the C-test, on prose that merely mentions the field, and on
    a keyed line quoting ANOTHER card's walk outside a slice. Those are the shapes a
    too-eager guard would refuse, and refusing them would re-impose exactly the every-card
    tax that DRA-306's PROCEED-WITH condition removed (merged SPEC §4.1, DRA-305 §1.4).
    A guard that demanded the line universally would be red across the whole corpus for a
    reason nobody changed, which is trap 74 - a gate nobody believes.

    BOTH LISTS ARE PROVEN LOAD-BEARING, by mutation rather than by inspection: emptying the
    must-list and emptying the keyed-line pattern list must each make the guard REFUSE TO
    REPORT rather than pass. Deleting the `challenge-overrule` row must stop that form being
    detected. A row nobody has watched matter is a row that can rot silently.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false) } catch { }

$guard = Join-Path $PSScriptRoot 'challenge-line-guard.ps1'
if (-not (Test-Path $guard)) { throw "challenge-line-selftest: cannot find $guard" }
$guardSource = [System.IO.File]::ReadAllText($guard, [System.Text.UTF8Encoding]::new($false))

$root = Join-Path ([System.IO.Path]::GetTempPath()) ("eqb-challenge-selftest-" + [guid]::NewGuid().ToString('n').Substring(0, 8))
New-Item -ItemType Directory -Path $root -Force | Out-Null

$failures = 0
$checks = 0

function Write-Result {
    param([bool]$Ok, [string]$Label, [string]$Detail = '')
    $script:checks++
    if ($Ok) {
        Write-Host "  [ok]   $Label"
    } else {
        $script:failures++
        Write-Host "  [FAIL] $Label"
        if ($Detail) { foreach ($l in ($Detail -split "`n")) { Write-Host "         $l" } }
    }
}

# A throwaway corpus directory holding the named plan files.
function New-Corpus {
    param([Parameter(Mandatory)][hashtable]$Files)
    $dir = Join-Path $root ([guid]::NewGuid().ToString('n').Substring(0, 8))
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    foreach ($name in $Files.Keys) {
        $p = Join-Path $dir $name
        [System.IO.File]::WriteAllText($p, [string]$Files[$name], [System.Text.UTF8Encoding]::new($false))
    }
    return $dir
}

# Run the guard - or a mutated copy of it - over a corpus. `MustList`/`LineKeys` substitute
# the curated lists; `$null` means "leave the committed list alone". The substitution is a
# regex over the source block, so a rename of either variable makes these cases fail loudly
# rather than silently testing a stale copy.
function Invoke-Guard {
    param(
        [Parameter(Mandatory)][string]$PlansDir,
        [string[]]$MustList,
        [string[]]$LineKeys,
        [switch]$EmptyMustList,
        [switch]$EmptyLineKeys
    )

    $src = $guardSource
    $script = $guard

    function Set-Block {
        param([string]$Text, [string]$Var, [string[]]$Values)
        $body = if ($Values.Count -eq 0) { "`$$Var = @(" + "`n)" }
                else { "`$$Var = @(" + "`n" + (($Values | ForEach-Object { "    '$_'" }) -join "`n") + "`n)" }
        $pattern = "(?s)\`$$Var = @\(.*?\r?\n\)"
        if ($Text -notmatch $pattern) { throw "challenge-line-selftest: could not find the `$$Var block in the guard source - the guard was renamed or reshaped and this self-test is testing nothing." }
        return [regex]::Replace($Text, $pattern, { param($m) $body }, 1)
    }

    $mutated = $false
    if ($EmptyMustList)      { $src = Set-Block -Text $src -Var 'MustList' -Values @();        $mutated = $true }
    elseif ($MustList)       { $src = Set-Block -Text $src -Var 'MustList' -Values $MustList;  $mutated = $true }
    if ($EmptyLineKeys)      { $src = Set-Block -Text $src -Var 'LineKeys' -Values @();        $mutated = $true }
    elseif ($LineKeys)       { $src = Set-Block -Text $src -Var 'LineKeys' -Values $LineKeys;  $mutated = $true }

    if ($mutated) {
        $script = Join-Path $root ("guard-" + [guid]::NewGuid().ToString('n').Substring(0, 8) + '.ps1')
        [System.IO.File]::WriteAllText($script, $src, [System.Text.UTF8Encoding]::new($false))
    }

    $out = & pwsh -NoProfile -File $script -PlansDir $PlansDir 2>&1 | Out-String
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $out }
}

# ---------------------------------------------------------------------------------------
# Fixture prose. A plan body with a header region, an ordinary body section, and a slice
# sequence - the three regions the guard partitions a file into.
$HEADER = @'
# A plan (DRA-900)

**Class:** V2 · **Card:** DRA-900
route: hard
'@

$KEYED_OWN   = 'challenge: dra-900-some-slug -> PROCEED-WITH (C2) as of 2026-09-22'
$KEYED_OTHER = 'challenge: dra-901-other-slug -> PROCEED-WITH (C2)'

$BODY = @'

## 1. What this is

Ordinary argument, no slices here.
'@

$SLICES = @'

### 2. Slices (one SIGN authorizes the declared sequence)

- **D1** - the first slice.
- **D2** - the second slice.
'@

# A plan that satisfies its own must-list row. Every corpus below that substitutes a
# must-list carries this file and names it, so the row under test is LIVE - otherwise the
# dead-row arm fires as well and a case could go red for a reason that is not its own.
$SATISFIED = $HEADER + "`n" + $KEYED_OWN + "`n" + $BODY + $SLICES

Write-Host ''
Write-Host 'challenge-line-guard self-test'
Write-Host ''
Write-Host '  Refusals (each must fire):'

# --- 1. A gate-evaluated plan with no keyed line at all.
$c = New-Corpus @{ 'DRA-900.md' = ($HEADER + $BODY + $SLICES) }
$r = Invoke-Guard -PlansDir $c -MustList @('DRA-900')
Write-Result (($r.ExitCode -eq 1) -and ($r.Output -match 'carries no keyed line')) `
    'a plan on the must-list with NO keyed line is refused' $r.Output

# --- 2. The discriminator: a keyed line, but keyed to ANOTHER card. This is the live shape
#       in DRA-305 §1.3, which quotes DRA-304's walk as evidence; it must not satisfy the row.
$c = New-Corpus @{ 'DRA-900.md' = ($HEADER + "`n" + $KEYED_OTHER + "`n" + $BODY + $SLICES) }
$r = Invoke-Guard -PlansDir $c -MustList @('DRA-900')
Write-Result (($r.ExitCode -eq 1) -and ($r.Output -match 'carries no keyed line') -and ($r.Output -match 'dra-901-other-slug')) `
    "a keyed line for ANOTHER card does not satisfy this plan's row, and the refusal names it" $r.Output

# --- 3. Right line, wrong place: below the first section heading rather than at the top.
$c = New-Corpus @{ 'DRA-900.md' = ($HEADER + $BODY + "`n" + $KEYED_OWN + "`n" + $SLICES) }
$r = Invoke-Guard -PlansDir $c -MustList @('DRA-900')
Write-Result (($r.ExitCode -eq 1) -and ($r.Output -match 'not at the top of the plan body')) `
    'a keyed line below the first heading is refused as misplaced' $r.Output

# --- 4. §3.2's defect: a keyed line inside a slice sequence. The offending plan is NOT on
#       the must-list, which proves this arm is independent of the curated list; the
#       satisfied plan beside it keeps the list live, so the single reported problem is
#       this one and not a dead row.
$c = New-Corpus @{
    'DRA-900.md' = $SATISFIED
    'DRA-902.md' = ($HEADER + $BODY + $SLICES + "`n" + 'challenge: dra-902-slice-drift -> PROCEED-WITH (C2)' + "`n")
}
$r = Invoke-Guard -PlansDir $c -MustList @('DRA-900')
Write-Result (($r.ExitCode -eq 1) -and ($r.Output -match 'inside a slice sequence') -and ($r.Output -match 'found 1 problem')) `
    'a keyed line inside a slice sequence is refused (§3.2, the D(n+1) drift)' $r.Output

# --- 5. The same defect under a `(D2)`-style heading rather than a "Slices" one.
$dSlice = "`n### 3. The second lane (D2)`n`nchallenge: dra-903-handoff -> PROCEED-WITH (C2)`n"
$c = New-Corpus @{
    'DRA-900.md' = $SATISFIED
    'DRA-903.md' = ($HEADER + $BODY + $dSlice)
}
$r = Invoke-Guard -PlansDir $c -MustList @('DRA-900')
Write-Result (($r.ExitCode -eq 1) -and ($r.Output -match 'inside a slice sequence') -and ($r.Output -match 'found 1 problem')) `
    'a keyed line under a D(n)-named heading is refused too' $r.Output

# --- 6. The `challenge-overrule:` form is detected as well. Trap 78: a pattern row nobody
#       has watched fire is a row that can be deleted without symptom - so fire it, and then
#       delete the row and assert the detection STOPS.
$ovl = "`n### 3. The second lane (D2)`n`nchallenge-overrule: dra-904-handoff -> PROCEED-WITH (C2)`n"
$c = New-Corpus @{
    'DRA-900.md' = $SATISFIED
    'DRA-904.md' = ($HEADER + $BODY + $ovl)
}
$r = Invoke-Guard -PlansDir $c -MustList @('DRA-900')
Write-Result (($r.ExitCode -eq 1) -and ($r.Output -match 'inside a slice sequence') -and ($r.Output -match 'found 1 problem')) `
    'a challenge-overrule: line in a slice sequence is refused' $r.Output

$r = Invoke-Guard -PlansDir $c -MustList @('DRA-900') -LineKeys @('challenge')
Write-Result ($r.ExitCode -eq 0) `
    "...and removing the 'challenge-overrule' row stops it being detected, so that row is load-bearing" $r.Output

# --- 7. A must-list row naming a plan that does not exist: a dead row, which has no symptom
#       unless something reports it (trap 78).
$c = New-Corpus @{ 'DRA-900.md' = ($HEADER + "`n" + $KEYED_OWN + "`n" + $BODY) }
$r = Invoke-Guard -PlansDir $c -MustList @('DRA-900', 'DRA-999')
Write-Result (($r.ExitCode -eq 1) -and ($r.Output -match 'must-list row names no plan')) `
    'a must-list row naming a missing plan file is refused as a dead row' $r.Output

# --- 8. Both lists emptied must REFUSE TO REPORT, not pass. This is trap 78 exactly: the
#       guard whose list collapsed matched nothing and reported clean.
$c = New-Corpus @{ 'DRA-900.md' = ($HEADER + $BODY) }
$r = Invoke-Guard -PlansDir $c -EmptyMustList
Write-Result (($r.ExitCode -ne 0) -and ($r.Output -match 'must-list is EMPTY')) `
    'an EMPTY must-list makes the guard refuse to report a result' $r.Output

$c = New-Corpus @{ 'DRA-900.md' = ($HEADER + $BODY + $SLICES + "`n" + $KEYED_OWN + "`n") }
$r = Invoke-Guard -PlansDir $c -EmptyLineKeys
Write-Result (($r.ExitCode -ne 0) -and ($r.Output -match 'pattern list is EMPTY')) `
    'an EMPTY keyed-line pattern list makes the guard refuse to report a result' $r.Output

Write-Host ''
Write-Host '  Allowed shapes (each must stay green - a guard that refused these would'
Write-Host '  re-impose the every-card tax DRA-306 removed):'

# --- 9. A plan that never reached the C-test writes nothing and is asked for nothing. The
#       satisfied plan beside it keeps the must-list live, so this is genuinely "not on the
#       list" rather than "no list at all".
$c = New-Corpus @{
    'DRA-900.md' = $SATISFIED
    'DRA-905.md' = ($HEADER + $BODY + $SLICES)
}
$r = Invoke-Guard -PlansDir $c -MustList @('DRA-900')
Write-Result ($r.ExitCode -eq 0) `
    'a plan NOT on the must-list, with no keyed line, passes (the scoped-line condition)' $r.Output

# --- 10. Prose that merely mentions the field is not a record of a walk. Both the bare
#        field name and the ellipsis form the SPEC itself uses appear in the live corpus.
#
#        THE PROSE SITS INSIDE THE SLICE SECTION ON PURPOSE. That is the only region where
#        the slug requirement changes a verdict: a plan's slice sequence is exactly where
#        someone would write ABOUT the rule, and if the guard read `challenge: ... ->` as a
#        keyed line it would report a §3.2 drift that is not there. Put this fixture in the
#        header or the ordinary body instead and no check reads it, so the case passes on a
#        guard whose slug requirement has been deleted - measured, as a surviving mutant.
$proseSlices = @'

### 2. Slices (one SIGN authorizes the declared sequence)

- **D1** - the first slice. A plan that trips C1-C5 carries its `challenge:` line
  before its LIVE ASK goes up, and the gate fires once at the plan's SIGN.
- **D2** - where the C-test was evaluated and nothing fired, Planner writes
  `challenge: ... -> NOT-ENGAGED (no C-test fires)` - a gate-status line, not a
  fifth verdict. The `challenge-overrule:` line and the NO-RETURN rule are the
  ops SPEC's and are inherited verbatim.
'@
$c = New-Corpus @{
    'DRA-900.md' = $SATISFIED
    'DRA-906.md' = ($HEADER + $BODY + $proseSlices)
}
$r = Invoke-Guard -PlansDir $c -MustList @('DRA-900')
Write-Result ($r.ExitCode -eq 0) `
    'prose mentioning challenge: inside a slice section is not read as a keyed line' $r.Output

# --- 11. The live DRA-305 §1.3 shape: another card's keyed line, quoted as evidence, in the
#        ordinary body. Outside a slice, that is a citation and not a drift.
$c = New-Corpus @{ 'DRA-900.md' = ($HEADER + "`n" + $KEYED_OWN + "`n" + $BODY + "`n" + $KEYED_OTHER + "`n" + $SLICES) }
$r = Invoke-Guard -PlansDir $c -MustList @('DRA-900')
Write-Result ($r.ExitCode -eq 0) `
    "another card's keyed line quoted OUTSIDE a slice is allowed (DRA-305 §1.3's live shape)" $r.Output

# --- 12. The reachable positive for the corpus as a whole: a correct plan with a slice
#        sequence present and clean. Without this, every red row above could be going red
#        for a reason that has nothing to do with the rule.
$c = New-Corpus @{ 'DRA-900.md' = ($HEADER + "`n" + $KEYED_OWN + "`n" + $BODY + $SLICES) }
$r = Invoke-Guard -PlansDir $c -MustList @('DRA-900')
Write-Result (($r.ExitCode -eq 0) -and ($r.Output -match 'none inside a slice sequence')) `
    'a correctly placed keyed line with a clean slice sequence passes' $r.Output

# --- 13. The committed corpus itself, through the committed lists. This is the one case
#        with no mutation at all, and it is what CI would otherwise be asserting alone.
$repoPlans = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs/plans'
if (Test-Path $repoPlans) {
    $r = Invoke-Guard -PlansDir $repoPlans
    Write-Result ($r.ExitCode -eq 0) 'the committed docs/plans corpus passes the committed lists' $r.Output
}

Write-Host ''
try { Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue } catch { }

if ($failures -gt 0) {
    Write-Host "FAIL: $failures of $checks challenge-line-guard self-test checks failed."
    exit 1
}
Write-Host "OK: all $checks challenge-line-guard self-test checks passed (refusals fired, allowed shapes stayed green, both curated lists proven load-bearing)."
exit 0
