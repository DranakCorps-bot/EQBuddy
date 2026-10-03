<#
.SYNOPSIS
    The dev-status page: what is live, what is on main, and what needs the Founder's testing
    (DRA-782 D1). One pinned, locked GitHub issue labelled `dev-status`, whose body this
    script rewrites.

.DESCRIPTION
    The Founder asked (2026-10-02) for one place on the repo that shows the live release, the
    current dev build, what merged since live in plain language, and what needs his testing -
    generated, never hand-kept. docs/plans/DRA-782.md is the plan; its section 3 is the page.

    Every fact already lives somewhere, so this script only gathers and renders:

      live          the latest published (non-draft, non-prerelease) GitHub release
      dev build     main's head + Directory.Build.props <Version>
      plain words   WhatsNew.json entries whose version is ABOVE the live tag (written for
                    players, and every player-noticeable change must have one)
      app changes   first-parent merges since the live tag whose files touch src/
      needs testing those app changes + every OPEN PR labelled needs-founder-smoke
      other merges  the rest (plans, ops, docs), folded into a count

    Rendering is ONE pure function, Format-DevStatus: inputs in, markdown out. The two rules
    that decide what the page claims - the WhatsNew version filter and the src/ split - each
    sit on one line tagged `# rule:` so -SelfTest can break them and watch itself go red.

    The issue has ONE writer: .github/workflows/dev-status.yml (push to main, hourly
    schedule, workflow_dispatch). A hand edit is overwritten by the next run, on purpose
    (trap 4). The workflow never runs on pull_request, so no fork code runs near its token.

    pwsh -NoProfile -File scripts/dev-status.ps1               # print the page, write nothing
    pwsh -NoProfile -File scripts/dev-status.ps1 -Publish      # rewrite the issue body
    pwsh -NoProfile -File scripts/dev-status.ps1 -SelfTest     # offline; two prove-fail mutants
#>
param([switch] $Publish, [switch] $SelfTest, [string] $Repo)
$ErrorActionPreference = 'Stop'
$inv = [System.Globalization.CultureInfo]::InvariantCulture

$Label = 'dev-status'
$SmokeLabel = 'needs-founder-smoke'
$IssueTitle = "Dev status: what's on main vs the live release"
$Caveat = 'Merged to main, not released. Nothing here is a commitment about what the next release contains or when.'

function ConvertTo-DevVersion {
    param([string] $Text)
    $v = $null
    if ([version]::TryParse(("$Text" -replace '^[vV]', ''), [ref]$v)) { return $v }
    return $null
}

function Format-DevStamp {
    param($When)
    if ($null -eq $When -or "$When" -eq '') { return 'unknown time' }
    $d = if ($When -is [datetime]) { [datetimeoffset]$When } else { [datetimeoffset]::Parse("$When", $inv) }
    return $d.UtcDateTime.ToString('yyyy-MM-dd HH:mm', $inv) + ' UTC'
}

function Format-DevMergeLine {
    param($Merge, [string] $Repo)
    $title = "$($Merge.Title)".Trim()
    if ($Merge.Number) { return "- [#$($Merge.Number)](https://github.com/$Repo/pull/$($Merge.Number)) $title" }
    return "- ``$("$($Merge.Sha)".Substring(0, [Math]::Min(7, "$($Merge.Sha)".Length)))`` $title"
}

<#
    Inputs (a hashtable):
      Repo         owner/name
      Live         $null, or @{ Tag; PublishedAt; Url; Name }
      Head         @{ Sha; Date }
      NextVersion  <Version> from Directory.Build.props
      WhatsNew     the parsed WhatsNew.json array (version, date, highlights)
      Merges       first-parent merges since the live tag, newest first:
                   @{ Sha; Number; Title; Files = @('src/...', ...) }
      SmokePrs     open PRs labelled needs-founder-smoke: @{ Number; Title; Url }
      GeneratedAt  when this render ran
#>
function Format-DevStatus {
    param([Parameter(Mandatory)][hashtable] $In)
    $repo = $In.Repo
    $sha7 = "$($In.Head.Sha)".Substring(0, [Math]::Min(7, "$($In.Head.Sha)".Length))
    $liveVer = if ($In.Live) { ConvertTo-DevVersion $In.Live.Tag } else { [version]'0.0' }
    $since = if ($In.Live) { $In.Live.Tag } else { 'the first release' }

    $coming = @($In.WhatsNew | Where-Object { $v = ConvertTo-DevVersion $_.version; $v -and $v -gt $liveVer })  # rule: version-filter
    $app = @($In.Merges | Where-Object { @($_.Files | Where-Object { "$_" -like 'src/*' }).Count -gt 0 })  # rule: src-split
    $other = @($In.Merges | Where-Object { $_ -notin $app })
    $smoke = @($In.SmokePrs)

    $o = [System.Collections.Generic.List[string]]::new()
    $o.Add('# Dev status: what''s on `main` vs the live release')
    $o.Add('')
    $o.Add("> $Caveat")
    $o.Add('')

    $o.Add('## Live')
    $o.Add('')
    if ($In.Live) {
        $name = if ($In.Live.Name -and $In.Live.Name -ne $In.Live.Tag) { " - $($In.Live.Name)" } else { '' }
        $o.Add("**[$($In.Live.Tag)]($($In.Live.Url))**$name, published $(Format-DevStamp $In.Live.PublishedAt).")
    } else {
        $o.Add('No published release was found.')
    }
    $o.Add('')

    $o.Add('## Dev build')
    $o.Add('')
    $o.Add("``main`` is at **``$sha7``** (committed $(Format-DevStamp $In.Head.Date)); the next version is **$($In.NextVersion)**.")
    $o.Add("Auto-roll installs ``main`` on the Founder's PC within about ten minutes. When you have this build, Options shows ``dev $sha7`` under its footer.")
    $o.Add('')

    $o.Add('## Coming in the next release')
    $o.Add('')
    if ($coming.Count -eq 0) {
        $o.Add("No What's-new entry yet.")
        $o.Add('')
    } else {
        foreach ($e in $coming) {
            $o.Add("**$($e.version)** ($($e.date))")
            $o.Add('')
            foreach ($h in @($e.highlights)) { $o.Add("- $h") }
            $o.Add('')
        }
    }

    $o.Add("## App changes merged since $since")
    $o.Add('')
    if ($app.Count -eq 0) {
        $o.Add("No app changes merged since $since.")
    } else {
        foreach ($m in $app) { $o.Add((Format-DevMergeLine $m $repo)) }
    }
    $o.Add('')

    $o.Add('## Needs your testing')
    $o.Add('')
    if ($app.Count -eq 0 -and $smoke.Count -eq 0) {
        $o.Add('Nothing is waiting on your testing.')
    } else {
        if ($app.Count -eq 0) {
            $o.Add("- No app changes since $since.")
        } else {
            $o.Add("- **Every app change above** ($($app.Count)) - all of it ships on the next release go, so all of it is untested by you until you say otherwise.")
        }
        if ($smoke.Count -eq 0) {
            $o.Add("- No open pull request is labelled ``$SmokeLabel``.")
        } else {
            $o.Add("- Open pull requests labelled ``$SmokeLabel``:")
            foreach ($p in $smoke) { $o.Add("  - [#$($p.Number)]($($p.Url)) $("$($p.Title)".Trim())") }
        }
    }
    $o.Add('')

    $o.Add('## Other merges since live')
    $o.Add('')
    if ($other.Count -eq 0) {
        $o.Add("No process or docs changes since $since.")
    } else {
        $noun = if ($other.Count -eq 1) { 'change' } else { 'changes' }
        $o.Add('<details>')
        $o.Add("<summary>$($other.Count) process/docs $noun (plans, ops, docs)</summary>")
        $o.Add('')
        foreach ($m in $other) { $o.Add((Format-DevMergeLine $m $repo)) }
        $o.Add('')
        $o.Add('</details>')
    }
    $o.Add('')

    $o.Add('---')
    $o.Add("Generated $(Format-DevStamp $In.GeneratedAt) from ``main`` @ ``$sha7`` by ``.github/workflows/dev-status.yml``.")
    return ($o -join "`n") + "`n"
}

# Which open issue is the page: issues (never PRs) carrying the label, lowest number wins, so
# a duplicate somebody opens by hand never becomes the page.
function Select-DevStatusIssue {
    param($Issues)
    return @($Issues | Where-Object { -not $_.pull_request } | Sort-Object { [int]$_.number }) | Select-Object -First 1
}

# ---------------------------------------------------------------------------------------------
if ($SelfTest) {
    $fails = [System.Collections.Generic.List[string]]::new()

    function New-DevFixture {
        @{
            Repo        = 'o/r'
            Live        = @{ Tag = 'v2.0.3'; PublishedAt = '2026-10-02T00:15:00Z'; Url = 'https://github.com/o/r/releases/tag/v2.0.3'; Name = 'EQBuddy Evolved 0.1 Beta (v2.0.3)' }
            Head        = @{ Sha = 'ce32eb81acf0438feb44f9998bc320055d0941e5'; Date = '2026-10-02T14:00:00Z' }
            NextVersion = '2.0.4'
            WhatsNew    = @(
                [pscustomobject]@{ version = '2.0.4'; date = '2026-10-01'; highlights = @('ABOVE-LIVE entry text') },
                [pscustomobject]@{ version = '2.0.3'; date = '2026-10-01'; highlights = @('AT-LIVE entry text') },
                [pscustomobject]@{ version = '2.0.2'; date = '2026-09-30'; highlights = @('BELOW-LIVE entry text') })
            Merges      = @(
                @{ Sha = 'aaaaaaa1'; Number = 1023; Title = 'feat: an app change'; Files = @('src/EQBuddy.Core/X.cs', 'tests/X.cs') },
                @{ Sha = 'aaaaaaa2'; Number = 1020; Title = 'plan: a plan'; Files = @('docs/plans/src.md', 'FABLE.md') },
                @{ Sha = 'aaaaaaa3'; Number = 1012; Title = 'fix: another app change'; Files = @('src/EQBuddy/Y.cs') },
                @{ Sha = 'aaaaaaa4'; Number = 1019; Title = 'ops: process'; Files = @('scripts/src/z.ps1') })
            SmokePrs    = @(@{ Number = 777; Title = 'smoke me'; Url = 'https://github.com/o/r/pull/777' })
            GeneratedAt = '2026-10-02T15:30:00Z'
        }
    }

    # The assertions, against any renderer by NAME, so a mutant can be run through them.
    function Test-DevRender {
        param([string] $Fn)
        $f = [System.Collections.Generic.List[string]]::new()
        function Want([string] $Why, [bool] $Ok) { if (-not $Ok) { $f.Add($Why) } }

        $md = & $Fn -In (New-DevFixture)
        Want 'caveat line' ($md.Contains("> $Caveat"))
        Want 'live tag linked' ($md.Contains('**[v2.0.3](https://github.com/o/r/releases/tag/v2.0.3)**'))
        Want 'live date' ($md.Contains('published 2026-10-02 00:15 UTC'))
        Want 'head sha7 + next version' ($md.Contains('is at **`ce32eb8`**') -and $md.Contains('next version is **2.0.4**'))
        Want 'options footer sentence' ($md.Contains('Options shows `dev ce32eb8`'))
        Want 'whatsnew ABOVE live included' ($md.Contains('ABOVE-LIVE entry text'))
        Want 'whatsnew AT live excluded' (-not $md.Contains('AT-LIVE entry text'))
        Want 'whatsnew BELOW live excluded' (-not $md.Contains('BELOW-LIVE entry text'))
        $appSec = ($md -split '## Needs your testing')[0]
        $appSec = ($appSec -split '## App changes merged since v2.0.3')[1]
        Want 'app section names #1023' ("$appSec".Contains('#1023'))
        Want 'app section names #1012' ("$appSec".Contains('#1012'))
        Want 'app section keeps a plan out (docs/plans/src.md is not src/)' (-not "$appSec".Contains('#1020'))
        Want 'app section keeps ops out (scripts/src/ is not src/)' (-not "$appSec".Contains('#1019'))
        Want 'app newest first' ("$appSec".IndexOf('#1023') -lt "$appSec".IndexOf('#1012'))
        Want 'testing counts the app changes' ($md.Contains('**Every app change above** (2)'))
        Want 'smoke PR listed' ($md.Contains('[#777](https://github.com/o/r/pull/777) smoke me'))
        Want 'other merges folded into a count' ($md.Contains('<summary>2 process/docs changes (plans, ops, docs)</summary>'))
        Want 'footer' ($md.Contains('Generated 2026-10-02 15:30 UTC from `main` @ `ce32eb8` by `.github/workflows/dev-status.yml`.'))

        # Every empty state is a sentence, never a blank.
        $e = New-DevFixture
        $e.WhatsNew = @($e.WhatsNew | Select-Object -Skip 1)
        $e.Merges = @(); $e.SmokePrs = @()
        $md = & $Fn -In $e
        Want 'empty: whatsnew' ($md.Contains("No What's-new entry yet."))
        Want 'empty: app' ($md.Contains('No app changes merged since v2.0.3.'))
        Want 'empty: testing' ($md.Contains('Nothing is waiting on your testing.'))
        Want 'empty: other' ($md.Contains('No process or docs changes since v2.0.3.'))

        $e = New-DevFixture
        $e.SmokePrs = @()
        $e.Merges = @($e.Merges | Where-Object { $_.Number -in 1020, 1019 })
        $e.SmokePrs = @(@{ Number = 5; Title = 't'; Url = 'u' })
        $md = & $Fn -In $e
        Want 'empty: testing (a) only' ($md.Contains('- No app changes since v2.0.3.'))
        $e.SmokePrs = @(); $e.Merges = @((New-DevFixture).Merges[0])
        $md = & $Fn -In $e
        Want 'empty: testing (b) only' ($md.Contains('- No open pull request is labelled `needs-founder-smoke`.'))

        $e = New-DevFixture
        $e.Live = $null
        $md = & $Fn -In $e
        Want 'no release says so' ($md.Contains('No published release was found.'))
        return , $f
    }

    $real = Test-DevRender 'Format-DevStatus'
    foreach ($x in $real) { $fails.Add("real renderer: $x") }
    Write-Host "  real renderer: $(if ($real.Count) { "$($real.Count) FAILED" } else { 'every assertion green' })"

    # Issue selection: issues only, lowest number.
    $pick = Select-DevStatusIssue @(
        [pscustomobject]@{ number = 40; pull_request = $null },
        [pscustomobject]@{ number = 12; pull_request = [pscustomobject]@{ url = 'x' } },
        [pscustomobject]@{ number = 31; pull_request = $null })
    if (-not $pick -or $pick.number -ne 31) { $fails.Add('issue selection: expected #31 (lowest non-PR)') }
    if ($null -ne (Select-DevStatusIssue @())) { $fails.Add('issue selection: empty list must pick nothing') }

    # Prove-fail (trap 34 / 78): break each rule once and the assertions above must go red.
    # Each mutation must change the text exactly once, or it is a mutant aimed at nothing.
    $tokens = $null; $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($PSCommandPath, [ref]$tokens, [ref]$errors)
    $fnText = $ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $a.Name -eq 'Format-DevStatus' }, $true)[0].Extent.Text
    $mutants = @(
        @{ Name = 'version filter -gt -> -ge (an entry AT the live version leaks in)'; Rule = 'version-filter'; From = '$v -gt $liveVer'; To = '$v -ge $liveVer'; Expect = 'whatsnew AT live excluded' },
        @{ Name = 'src/ split -gt 0 -> -ge 0 (every merge counts as an app change)'; Rule = 'src-split'; From = '.Count -gt 0 })'; To = '.Count -ge 0 })'; Expect = 'app section keeps a plan out' })
    foreach ($m in $mutants) {
        $lines = $fnText -split "`n"
        $hits = @($lines | Where-Object { $_ -match "# rule: $($m.Rule)\b" -and $_.Contains($m.From) })
        if ($hits.Count -ne 1) { $fails.Add("mutant '$($m.Name)': the rule line was not found exactly once - aimed at nothing"); continue }
        $mut = ($lines | ForEach-Object { if ($_ -match "# rule: $($m.Rule)\b") { $_.Replace($m.From, $m.To) } else { $_ } }) -join "`n"
        $mut = $mut.Replace('function Format-DevStatus', 'function Format-DevStatusMutant')
        . ([scriptblock]::Create($mut))
        $red = Test-DevRender 'Format-DevStatusMutant'
        $caught = @($red | Where-Object { $_.StartsWith($m.Expect) })
        if ($caught.Count -eq 0) { $fails.Add("mutant '$($m.Name)' stayed GREEN - the selftest cannot see that rule") }
        else { Write-Host "  mutant RED as expected: $($m.Name) -> $($red -join '; ')" }
    }

    if ($fails.Count) {
        foreach ($x in $fails) { Write-Host "FAIL: $x" }
        exit 1
    }
    Write-Host 'OK: dev-status selftest - renderer, empty states, issue selection, and both mutants red.'
    exit 0
}

# ---------------------------------------------------------------------------------------------
# Gather. Everything below reads git and GitHub; only -Publish writes, and only the one issue.

function Invoke-DevGh {
    $out = & gh @args
    if ($LASTEXITCODE -ne 0) { throw "gh $($args -join ' ') failed (exit $LASTEXITCODE)" }
    return $out
}
function Invoke-DevGit {
    $out = & git @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed (exit $LASTEXITCODE)" }
    return $out
}

$root = Split-Path -Parent $PSScriptRoot
if (-not $Repo) { $Repo = $env:GITHUB_REPOSITORY }
if (-not $Repo) { $Repo = (Invoke-DevGh repo view --json nameWithOwner --jq .nameWithOwner).Trim() }

# Latest published release. /releases/latest already skips drafts and prereleases.
$live = $null
$rel = & gh api "repos/$Repo/releases/latest" 2>$null
if ($LASTEXITCODE -eq 0 -and $rel) {
    $r = ($rel -join "`n") | ConvertFrom-Json
    $live = @{ Tag = $r.tag_name; PublishedAt = $r.published_at; Url = $r.html_url; Name = $r.name }
}

$headSha = (Invoke-DevGit -C $root rev-parse HEAD).Trim()
$headDate = (Invoke-DevGit -C $root log -1 --format=%cI HEAD).Trim()

[xml]$props = Get-Content -Raw (Join-Path $root 'Directory.Build.props')
$next = @($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]

$whatsNew = Get-Content -Raw -Encoding utf8 (Join-Path $root 'src/EQBuddy.Core/Data/WhatsNew.json') | ConvertFrom-Json

$range = if ($live) { "$($live.Tag)..HEAD" } else { 'HEAD' }
$raw = (Invoke-DevGit -C $root log $range --first-parent --merges '--format=%H%x1f%s%x1f%b%x1e') -join "`n"
$merges = foreach ($rec in ($raw -split "\x1e")) {
    $rec = $rec.Trim()
    if (-not $rec) { continue }
    $parts = $rec -split "\x1f"
    $sha = $parts[0].Trim(); $subject = $parts[1]; $body = if ($parts.Count -gt 2) { $parts[2] } else { '' }
    $num = $null; $title = $subject
    if ($subject -match '^Merge pull request #(\d+)\b') {
        $num = [int]$Matches[1]
        $first = @($body -split "`n" | Where-Object { $_.Trim() })[0]
        if ($first) { $title = $first.Trim() }
    }
    $files = @(Invoke-DevGit -C $root diff --name-only "$sha^1" $sha)
    @{ Sha = $sha; Number = $num; Title = $title; Files = $files }
}

$smoke = @((Invoke-DevGh pr list --repo $Repo --state open --label $SmokeLabel --json 'number,title,url' --limit 100) -join "`n" |
    ConvertFrom-Json | ForEach-Object { @{ Number = $_.number; Title = $_.title; Url = $_.url } })

$md = Format-DevStatus -In @{
    Repo = $Repo; Live = $live; Head = @{ Sha = $headSha; Date = $headDate }; NextVersion = $next
    WhatsNew = @($whatsNew); Merges = @($merges); SmokePrs = $smoke; GeneratedAt = [datetimeoffset]::UtcNow.ToString('o')
}

if (-not $Publish) { $md; exit 0 }

# Publish: the label (idempotent), the issue (found or created), its body, and the lock.
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) "dev-status-$([guid]::NewGuid().ToString('N')).md"
[System.IO.File]::WriteAllText($tmp, $md, [System.Text.UTF8Encoding]::new($false))
try {
    Invoke-DevGh label create $Label --repo $Repo --color 0E8A16 --force `
        --description "The auto-generated dev-status page (scripts/dev-status.ps1)" | Out-Null
    $open = (Invoke-DevGh api "repos/$Repo/issues?labels=$Label&state=open&per_page=50") -join "`n" | ConvertFrom-Json
    $issue = Select-DevStatusIssue @($open)
    if ($issue) {
        $n = $issue.number
        Invoke-DevGh issue edit $n --repo $Repo --body-file $tmp | Out-Null
        Write-Host "Updated #$n"
    } else {
        $url = (Invoke-DevGh issue create --repo $Repo --title $IssueTitle --label $Label --body-file $tmp) | Select-Object -Last 1
        $n = [int](("$url".Trim() -split '/')[-1])
        Write-Host "Created #$n ($url)"
    }
    $locked = (Invoke-DevGh api "repos/$Repo/issues/$n" --jq .locked).Trim()
    if ($locked -ne 'true') { Invoke-DevGh issue lock $n --repo $Repo | Out-Null; Write-Host "Locked #$n" }
} finally {
    Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue
}
