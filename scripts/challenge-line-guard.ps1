<#
.SYNOPSIS
    Every gate-evaluated plan carries its keyed `challenge:` line, at the top of its body
    and never inside a slice sequence.

.DESCRIPTION
    DRA-309 S3, off the DRA-305 SPEC (its §5 edit table, §3.2 seam and §7 item 4). The
    Challenger gate fires ONCE, at a plan's SIGN, and the durable record of that walk is a
    keyed line at the top of `docs/plans/DRA-<n>.md`, beside `route:`. This is the guard
    that makes those two sentences checkable instead of believed.

    THE PAIRING IS THE POINT (trap 34). A forbid-scan alone cannot see a plan that quietly
    skipped the gate - "no keyed line in a slice section" is green on a repository with no
    keyed lines at all, which is also what a repository that stopped walking the gate looks
    like. So the forbid-scan below is PAIRED with a curated must-list naming the plans that
    reached the C-test, and the must-list is the half that can see an absence.

    SCOPE IS "EVALUATED", NOT "EVERY PLAN" (merged SPEC §4.1; DRA-306's PROCEED-WITH
    condition; DRA-305 §1.4 item 2). A plan that never reached the C-test writes nothing,
    and this guard demands nothing of it. A guard that required the line universally would
    re-impose precisely the every-card tax that condition removed - so the must-list is
    curated and short by design, and its shortness is not a gap.

    WHAT "KEYED" MEANS, AND WHY THAT IS THE WHOLE DISCRIMINATOR. A keyed line is
    `challenge: <slug> -> <disposition>`, where `<slug>` names the card the walk was about.
    A plan's OWN line is one whose slug is keyed to its OWN card number. That single rule
    is what separates a durable record from prose ABOUT the rule, and the corpus already
    proves it in both directions:

      - `docs/plans/DRA-305.md` §1.3 carries `challenge: dra-304-process-gate -> ...`, a
        real keyed line quoted as evidence about a DIFFERENT card. It must not satisfy
        DRA-305's own must-list row, and it does not, because the key is `dra-304-`.
      - The same file says `challenge:` a dozen times in prose - in §3.1, §5's edit table,
        §6 and §7 - with no keyed slug. None of those is a record of a walk, and none of
        them matches.

    THE VERDICT VOCABULARY IS DELIBERATELY NOT READ. The four SIGNed verdicts are byte-
    frozen in the ops `purpose/CHALLENGER_PROCESS_GATE_SPEC.md`, and `CLAUDE.md` is
    explicit that this repository holds a POINTER to that file and "must never become a
    copy" - for the SPEC's own stated reason, that a copy either tracks the original or
    goes stale, and the stale one is what somebody reads. A guard that enumerated the four
    verdicts here would BE that copy, and it would fail closed on the day the ops repo
    added a fifth. So this guard reads the KEY and the PRESENCE of a disposition; the
    disposition's spelling is the ops SPEC's to own. That is a deliberate limit, not an
    oversight, and it is why a malformed verdict word is a human-review matter.

    THE TWO REGIONS, AND WHY THEY CANNOT CONTRADICT EACH OTHER. A plan file is partitioned
    at its first SLICE heading:

      - HEADER  - everything before the first `##`-or-deeper heading. This is "the top of
                  the plan body, beside `route:`" (§5), and it is where a must-list plan's
                  own keyed line is required to be.
      - SLICE   - everything from the first heading that names a slice (`D<n>`, `S<n>`, or
                  a "Slices" section) onward. No keyed line of ANY card may appear here.

    HEADER is a subset of not-SLICE by construction - the header ends at the first heading
    of any kind, and a slice heading is a heading - so a correctly placed line satisfies
    both checks and no file can be caught between them. §3.2 is why the SLICE half exists
    at all: a keyed line on a D(n+1) hand-off is the gate having drifted into the slice
    sequence, which would re-create one layer down the 84% of parked wait that the
    signed-plan-authorizes-every-slice cutover bought out. It is reported as a DEFECT.

.PARAMETER PlansDir
    The directory of plan bodies. Defaults to `docs/plans` beside this script's repository
    root; parameterised only so the self-test can drive it over a throwaway corpus.

.EXAMPLE
    pwsh -NoProfile -File scripts/challenge-line-guard.ps1
#>
[CmdletBinding()]
param(
    [string]$PlansDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Trap 54: read the file's bytes, not the host's decode of them. Plan bodies are full of
# em-dashes and section signs, and a mojibaked path in a refusal is a refusal nobody can act on.
try { [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false) } catch { }

if (-not $PlansDir) {
    $PlansDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs/plans'
}

# ---------------------------------------------------------------------------------------
# THE CURATED MUST-LIST - trap 34's half of this guard.
#
# One row per plan whose C1-C5 test was EVALUATED. A row here asserts "this plan walked the
# gate, so its keyed line must be present and correctly placed". A plan that never reached
# the C-test gets no row and is asked for nothing (merged SPEC §4.1).
#
# Each element is its own quoted string on its own line. Trap 78: a computed array literal
# whose elements are not parenthesised collapses into ONE joined string, because PowerShell
# binds `,` tighter than `+` - and the detector that happened to then matched nothing and
# reported clean for a month. These are literals, and the count is asserted below regardless.
$MustList = @(
    # DRA-305 - the EQBuddy Soft-loop gate SPEC, and the first EQBuddy-lane walk of the gate
    # it specifies. Verdict PROCEED-WITH (C2) returned by the live Challenger seat on the
    # DRA-315 wake card, 2026-09-22T05:26:14Z, ~1 h into the 6-hour window. The walk is
    # recorded in that file's own "Gate walk (§7.2)" block, which is the fixture this row
    # reads: revert the line and this guard goes red, which is the placement working.
    'DRA-305'

    # DRA-336 - TEL for launch (the TEL-001 first-open-prompt amendment). C1 fired: it
    # re-opens a signed clause of the Fable TEL plan (PR #320). Verdict PROCEED-WITH (C1)
    # returned by the Challenger on the DRA-358 wake card, 2026-09-23T22:42Z, inside the
    # 6-hour window; condition C-1 (a human read of the consent copy before TEL-PR1
    # merges) is restated in the plan header beside the keyed line this row reads.
    'DRA-336'

    # DRA-373 - the Evolved landing launch streamlining plan (PR #871). C1 fired: the brief
    # reverses the DRA-48 IA lock of 2026-09-10, Founder-directed on the card. Verdict
    # PROCEED-WITH (C1) returned by the Challenger on the DRA-375 wake card (retargeted by
    # Helm from closed #872 to PR #871 at 0c19ab5a), 2026-09-24T17:07Z; its three conditions
    # (dated variant-B trigger, named fallback stills, D2-merge re-verify gate) are folded
    # into the plan body the keyed line points at.
    'DRA-373'

    # DRA-251 - Base Dmg: admittance in ItemStatsBlock. The C-test was EVALUATED and no
    # test fired: NOT-ENGAGED, written by Planner 2026-09-25 per the merged SPEC's v1
    # scope (a Planner gate-status line, not a Challenger verdict; no wake, seat billed
    # nothing). It gets a row because it reached the C-test - "evaluated, nothing fired"
    # and "never assessed" must not render identically, which is this guard's own reason.
    'DRA-251'
)

if ($MustList.Count -eq 0) {
    throw 'challenge-line-guard: the must-list is EMPTY, so the only half of this guard that can see a MISSING keyed line would assert nothing. Refusing to report a result (traps 34 and 78).'
}

# ---------------------------------------------------------------------------------------
# THE PATTERNS. Both forms of keyed line the gate produces, per `CLAUDE.md` ("carries its
# keyed `challenge:` line (and its `challenge-overrule:` ... line, where one applies)").
#
# `<key>: dra-<digits>-<slug> -> <something non-empty>`. The `dra-<digits>-` prefix is the
# discriminator described above; `->` plus a non-empty tail is what makes it a RECORD of a
# disposition rather than a mention of the field. The verdict word itself is not read.
$LineKeys = @(
    'challenge'
    'challenge-overrule'
)

if ($LineKeys.Count -eq 0) {
    throw 'challenge-line-guard: the keyed-line pattern list is EMPTY, so this guard would match nothing and report clean. Refusing to report a result (trap 78).'
}

# Built from $LineKeys so the list above is load-bearing rather than decorative: delete a
# row and the corresponding form stops being detected, which the self-test proves.
$keyAlternation = ($LineKeys | ForEach-Object { [regex]::Escape($_) }) -join '|'
$KeyedLineRegex = "(?i)\b(?<key>$keyAlternation):\s*(?<slug>dra-(?<card>\d+)-[a-z0-9-]+)\s*->\s*(?<tail>\S.*)$"

# A heading that opens a slice sequence. `##`-or-deeper, whose text names a slice (`D1`,
# `S2`, `D3.1`) or is a "Slices" section. Surveyed against the live corpus: DRA-179 uses
# `### 1. The routing rule (D1)` and `### Slices`, DRA-180 uses `### 2. Slices (...)`, and
# DRA-305 - which is §-numbered throughout - has no slice heading at all, so its whole body
# is non-slice territory and its §7 prose about D(n+1) is not mistaken for a hand-off.
$SliceHeadingRegex = '(?i)^#{2,6}\s.*(\b[DS]\d+(\.\d+)?\b|\bslices?\b)'

# Any `##`-or-deeper heading closes the header region.
$AnyHeadingRegex = '^#{2,6}\s'

if (-not (Test-Path $PlansDir)) {
    Write-Host "SKIPPED: challenge-line-guard found no plans directory at '$PlansDir'. Nothing judged."
    exit 0
}

$planFiles = @(Get-ChildItem -Path $PlansDir -Filter '*.md' -File -ErrorAction SilentlyContinue | Sort-Object Name)
if ($planFiles.Count -eq 0) {
    Write-Host "EMPTY: challenge-line-guard found no plan bodies under '$PlansDir'. Nothing judged."
    Write-Host '       (A pass over zero files, said out loud so it cannot be mistaken for one over many.)'
    exit 0
}

# Read one file into the shape both checks ask about: its keyed lines, each tagged with the
# region it sits in. One producer for both checks (trap 4) - two scans would be two answers
# to "where is this line", and the last one would win.
function Read-PlanLines {
    param([Parameter(Mandatory)][string]$Path)

    $text = [System.IO.File]::ReadAllText($Path, [System.Text.UTF8Encoding]::new($false))
    $lines = $text -split "`r?`n"

    $firstHeading = -1
    $firstSlice = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($firstHeading -lt 0 -and $lines[$i] -match $AnyHeadingRegex) { $firstHeading = $i }
        if ($firstSlice -lt 0 -and $lines[$i] -match $SliceHeadingRegex) { $firstSlice = $i }
    }

    $found = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $m = [regex]::Match($lines[$i], $KeyedLineRegex)
        if (-not $m.Success) { continue }

        $inHeader = ($firstHeading -lt 0) -or ($i -lt $firstHeading)
        $inSlice = ($firstSlice -ge 0) -and ($i -ge $firstSlice)

        $found += [pscustomobject]@{
            LineNo   = $i + 1
            Key      = $m.Groups['key'].Value.ToLowerInvariant()
            Slug     = $m.Groups['slug'].Value.ToLowerInvariant()
            Card     = [int]$m.Groups['card'].Value
            InHeader = $inHeader
            InSlice  = $inSlice
            Text     = $lines[$i].Trim()
        }
    }

    return [pscustomobject]@{
        Path            = $Path
        Name            = [System.IO.Path]::GetFileName($Path)
        KeyedLines      = $found
        FirstSliceLine  = $(if ($firstSlice -ge 0) { $firstSlice + 1 } else { 0 })
    }
}

$plans = @()
foreach ($f in $planFiles) { $plans += (Read-PlanLines -Path $f.FullName) }

$failures = @()

# ---------------------------------------------------------------------------------------
# CHECK A - the must-list. Every plan that reached the C-test carries its OWN keyed line,
# in the header region. This is the half that can see an absence.
foreach ($card in $MustList) {
    $expectFile = "$card.md"
    $plan = $plans | Where-Object { $_.Name -eq $expectFile } | Select-Object -First 1

    if (-not $plan) {
        # A must-list row naming a file that does not exist is a dead row, and a dead row has
        # no symptom (trap 78). It is a failure of the list, reported as one.
        $failures += [pscustomobject]@{
            Kind   = 'must-list row names no plan'
            File   = $expectFile
            Detail = "The must-list names $card, but '$expectFile' is not in $PlansDir. Either the plan was renamed and this row is stale, or the row was written for a plan that was never committed."
        }
        continue
    }

    if ($card -notmatch '(?i)^DRA-(\d+)$') {
        $failures += [pscustomobject]@{
            Kind   = 'must-list row is not a card id'
            File   = $expectFile
            Detail = "The must-list row '$card' is not of the form DRA-<n>, so no keyed slug can be derived from it."
        }
        continue
    }
    $cardNumber = [int]$Matches[1]

    $own = @($plan.KeyedLines | Where-Object { $_.Key -eq 'challenge' -and $_.Card -eq $cardNumber })

    if ($own.Count -eq 0) {
        $others = @($plan.KeyedLines | Where-Object { $_.Card -ne $cardNumber })
        $hint = ''
        if ($others.Count -gt 0) {
            $hint = " The file does carry $($others.Count) keyed line(s), but keyed to another card ($(($others | ForEach-Object { $_.Slug } | Select-Object -Unique) -join ', ')) - a quotation of somebody else's walk is not a record of this one."
        }
        $failures += [pscustomobject]@{
            Kind   = 'gate-evaluated plan carries no keyed line'
            File   = $plan.Name
            Detail = "$card reached the C-test, so its plan body must carry 'challenge: dra-$cardNumber-<slug> -> <disposition>' at the top of the body, beside route:.$hint"
        }
        continue
    }

    $inHeader = @($own | Where-Object { $_.InHeader })
    if ($inHeader.Count -eq 0) {
        $where = ($own | ForEach-Object { "line $($_.LineNo)" }) -join ', '
        $failures += [pscustomobject]@{
            Kind   = 'keyed line is not at the top of the plan body'
            File   = $plan.Name
            Detail = "$card's keyed line is present ($where) but sits below the first section heading. DRA-305 §5: the keyed line lives at the TOP of the plan body, beside route: - that placement is what makes it the durable record rather than a remark inside an argument."
        }
    }
}

# ---------------------------------------------------------------------------------------
# CHECK B - the forbid-scan. ZERO keyed lines in a slice sequence, in ANY plan, keyed to any
# card. DRA-305 §3.2: the gate fires once at plan SIGN and never per slice, so a keyed line
# on a D(n+1) hand-off is the gate having drifted into the slice sequence. That is the
# defect this arm reports, and §7 item 4 makes it greppable acceptance.
foreach ($plan in $plans) {
    foreach ($line in ($plan.KeyedLines | Where-Object { $_.InSlice })) {
        $failures += [pscustomobject]@{
            Kind   = 'keyed line inside a slice sequence'
            File   = $plan.Name
            Detail = "Line $($line.LineNo) carries a keyed line ($($line.Slug)) at or below the slice heading on line $($plan.FirstSliceLine). The gate fires ONCE, at the plan's SIGN, and never per slice (DRA-305 §3.2) - a keyed line on a D(n+1) hand-off is the gate having drifted into the slice sequence, and a signed plan already authorizes every slice it declares.`n    > $($line.Text)"
        }
    }
}

# ---------------------------------------------------------------------------------------
$totalKeyed = ($plans | ForEach-Object { $_.KeyedLines.Count } | Measure-Object -Sum).Sum
if (-not $totalKeyed) { $totalKeyed = 0 }

if ($failures.Count -eq 0) {
    Write-Host "OK: $($plans.Count) plan body/bodies scanned; $($MustList.Count) on the gate-evaluated must-list, each carrying its own keyed line at the top of the body; $totalKeyed keyed line(s) found in total and none inside a slice sequence."
    exit 0
}

Write-Host ''
Write-Host "FAIL: challenge-line-guard found $($failures.Count) problem(s) across $($plans.Count) plan body/bodies."
Write-Host ''
foreach ($f in $failures) {
    Write-Host "  [$($f.Kind)]  $($f.File)"
    foreach ($l in ($f.Detail -split "`n")) { Write-Host "      $l" }
    Write-Host ''
}
Write-Host 'The rule, in one place: DRA-305 §5 (the keyed line is the durable record, at the top of'
Write-Host 'the plan body), §3.2 (the gate fires once at plan SIGN and never per slice) and §7 item 4'
Write-Host '(the acceptance this guard makes greppable). Verdict semantics are the ops'
Write-Host 'purpose/CHALLENGER_PROCESS_GATE_SPEC.md''s and are deliberately not read here.'
exit 1
