<#
.SYNOPSIS
    The auto-roll can never publish (DRA-705 plan section 4, layer 1).

.DESCRIPTION
    scripts\auto-roll.ps1 runs unattended on the Founder's PC every ten minutes. It must
    install main LOCALLY and do nothing else, so this guard reads its code - the PowerShell
    parser's tokens and command AST, with COMMENTS LEFT OUT, because the file explains at
    length what it never does - and refuses:

      F1  any release script (release.ps1 and its siblings)
      F2  `gh`, as a command or inside a string
      F3  `git push` / `git tag`
      F4  OneDrive, anywhere in code
      F5  a web call that WRITES (Invoke-RestMethod / Invoke-WebRequest with -Body or a
          method other than GET/HEAD), and curl/wget outright
      F6  Invoke-Expression - a command built from a string is a command this scan cannot see
      F7  writing a push URL to anything but DISABLED

    A forbid-scan cannot see a robot that quietly stopped doing its job (trap 34), so it is
    PAIRED with a must-list:

      M1  it calls install-local.ps1 with -Evolved AND -Install
      M2  it calls Assert-AutoRollClone, the refusal that keeps `git clean -fdx` inside the
          push-disabled clone

    -SelfTest drives every rule red against a mutant of the REAL auto-roll.ps1 (and three
    allowed shapes green), so a rule aimed at nothing fails here (trap 78).

    pwsh -NoProfile -File scripts/autoroll-guard.ps1
    pwsh -NoProfile -File scripts/autoroll-guard.ps1 -SelfTest
#>
param([switch] $SelfTest, [string] $Path)
$ErrorActionPreference = 'Stop'
if (-not $Path) { $Path = Join-Path $PSScriptRoot 'auto-roll.ps1' }

function Get-AutoRollViolations {
    param([Parameter(Mandatory)][string] $Text)
    $tokens = $null; $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseInput($Text, [ref]$tokens, [ref]$errors)
    $out = [System.Collections.Generic.List[string]]::new()
    if ($errors -and $errors.Count) { $out.Add("PARSE: $($errors[0].Message)"); return $out }

    $code = @($tokens | Where-Object { $_.Kind -ne 'Comment' })
    foreach ($t in $code) {
        $s = $t.Text
        if ($s -match '(?i)\brelease[\w-]*\.ps1') { $out.Add("F1 release script: $s") }
        if ($s -match '(?i)(^|[\s''"])gh(\.exe)?\s+(release|api|pr|repo|workflow|issue|auth)\b') { $out.Add("F2 gh in a string: $s") }
        if ($s -match '(?i)\bgit\b[^\r\n]*\s(push|tag)\b') { $out.Add("F3 git push/tag in a string: $s") }
        if ($s -match '(?i)onedrive') { $out.Add("F4 OneDrive: $s") }
    }

    $commands = $ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.CommandAst] }, $true)
    foreach ($c in $commands) {
        $name = "$($c.GetCommandName())".ToLowerInvariant()
        $words = @($c.CommandElements | Select-Object -Skip 1 | ForEach-Object {
            if ($_ -is [System.Management.Automation.Language.StringConstantExpressionAst]) { $_.Value.ToLowerInvariant() }
            elseif ($_ -is [System.Management.Automation.Language.CommandParameterAst]) { '-' + $_.ParameterName.ToLowerInvariant() }
        })
        $line = $c.Extent.StartLineNumber
        if ($name -in 'gh', 'gh.exe') { $out.Add("F2 gh command at line ${line}") }
        if ($name -in 'git', 'git.exe') {
            if ($words -contains 'push' -or $words -contains 'tag') { $out.Add("F3 git push/tag at line ${line}") }
            if (($words -contains 'set-url' -or ($words | Where-Object { $_ -like '*pushurl*' })) -and $words -notcontains 'disabled') {
                $out.Add("F7 push URL set to something other than DISABLED at line ${line}")
            }
        }
        if ($name -in 'curl', 'curl.exe', 'wget', 'wget.exe') { $out.Add("F5 $name at line ${line}") }
        if ($name -in 'invoke-restmethod', 'irm', 'invoke-webrequest', 'iwr') {
            $i = [array]::IndexOf($words, '-method')
            $method = if ($i -ge 0 -and $i + 1 -lt $words.Count) { $words[$i + 1] } else { 'get' }
            if ($words -contains '-body' -or $method -notin 'get', 'head') { $out.Add("F5 writing web call ($method) at line ${line}") }
        }
        if ($name -in 'invoke-expression', 'iex') { $out.Add("F6 Invoke-Expression at line ${line}") }
    }

    $installs = @($commands | Where-Object {
        $_.Extent.Text -match '(?i)install-local\.ps1' -and
        @($_.CommandElements | Where-Object { $_ -is [System.Management.Automation.Language.CommandParameterAst] } |
            ForEach-Object { $_.ParameterName.ToLowerInvariant() }) -contains 'evolved' -and
        @($_.CommandElements | Where-Object { $_ -is [System.Management.Automation.Language.CommandParameterAst] } |
            ForEach-Object { $_.ParameterName.ToLowerInvariant() }) -contains 'install'
    })
    if (-not $installs) { $out.Add('M1 missing: no call to install-local.ps1 -Evolved -Install') }
    if (-not @($commands | Where-Object { "$($_.GetCommandName())" -eq 'Assert-AutoRollClone' })) {
        $out.Add('M2 missing: Assert-AutoRollClone is never called')
    }
    return $out
}

if ($SelfTest) {
    $real = Get-Content -LiteralPath $Path -Raw
    $fails = 0
    function Expect([string] $Name, [string] $Text, [string] $Rule) {
        $v = @(Get-AutoRollViolations -Text $Text)
        $hit = if ($Rule) { @($v | Where-Object { $_.StartsWith($Rule) }).Count -gt 0 } else { $v.Count -eq 0 }
        if (-not $hit) {
            $script:fails++
            Write-Host "FAIL: autoroll-guard selftest '$Name' expected $(if ($Rule) { $Rule } else { 'clean' }), got: $($v -join '; ')"
        }
    }
    Expect 'the real auto-roll.ps1 is clean' $real $null
    # Forbid arm: each mutant appends one line to the real file.
    $add = { param($l) $real + "`n" + $l + "`n" }
    Expect 'release.ps1 call'        (& $add '& (Join-Path $repo ''scripts\release.ps1'') -Tag v9.9.9') 'F1'
    Expect 'gh release'              (& $add 'gh release create v9.9.9') 'F2'
    Expect 'gh inside a string'      (& $add '$c = "gh release create v9"') 'F2'
    Expect 'git push'                (& $add '& git -C $repo push origin HEAD') 'F3'
    Expect 'git tag'                 (& $add 'git tag v9.9.9') 'F3'
    Expect 'git push inside a string'(& $add '$c = "git -C x push origin"') 'F3'
    Expect 'OneDrive copy'           (& $add 'Copy-Item a (Join-Path $env:OneDrive ''EQBuddyDownload'')') 'F4'
    Expect 'POST'                    (& $add 'Invoke-RestMethod -Uri https://example.invalid -Method Post') 'F5'
    Expect 'web call with a body'    (& $add 'Invoke-WebRequest -Uri https://example.invalid -Body x') 'F5'
    Expect 'curl'                    (& $add 'curl.exe -X POST https://example.invalid') 'F5'
    Expect 'Invoke-Expression'       (& $add 'Invoke-Expression $c') 'F6'
    Expect 'push URL re-enabled'     (& $add 'git -C $repo remote set-url --push origin https://example.invalid') 'F7'
    # Must arm: each mutant removes what the robot must do.
    Expect 'install-local without -Install' ($real -replace '-Evolved -Install', '-Evolved') 'M1'
    Expect 'install-local never called'     ($real -replace 'install-local\.ps1', 'other.ps1') 'M1'
    Expect 'clone refusal never called'     ($real -replace 'Assert-AutoRollClone -Repo', 'Write-Output -InputObject') 'M2'
    # Allowed shapes: the scan must not refuse these, or it taxes the file's own prose.
    Expect 'a comment naming git push, release.ps1 and OneDrive' (& $add '# never git push, never release.ps1, never OneDrive <# gh release #>') $null
    Expect 'a GET'                          (& $add 'Invoke-RestMethod -Uri https://example.invalid -Method Get') $null
    Expect 'push URL written as DISABLED'   (& $add 'git -C $repo config remote.origin.pushurl DISABLED') $null

    if ($fails) { Write-Host "autoroll-guard selftest: $fails FAILED"; exit 1 }
    Write-Host 'autoroll-guard selftest: 15 mutants refused (F1-F7, M1-M2), real file and 3 allowed shapes clean'
    exit 0
}

if (-not (Test-Path -LiteralPath $Path)) { Write-Host "FAIL: autoroll-guard: $Path does not exist"; exit 1 }
$v = @(Get-AutoRollViolations -Text (Get-Content -LiteralPath $Path -Raw))
if ($v.Count) {
    Write-Host "FAIL: autoroll-guard: scripts/auto-roll.ps1 can publish or has stopped installing (DRA-705 plan section 4):"
    $v | ForEach-Object { Write-Host "  $_" }
    exit 1
}
Write-Host 'autoroll-guard: auto-roll.ps1 publishes nothing and still installs locally'
exit 0
