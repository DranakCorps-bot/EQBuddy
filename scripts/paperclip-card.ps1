<#
.SYNOPSIS
  Soft worker Paperclip card update (Phase 0). Call when awake — claim / PR / merge.
.EXAMPLE
  pwsh -File scripts/paperclip-card.ps1 -Issue DRA-7 -Status in_progress -Comment 'seat claimed'
  pwsh -File scripts/paperclip-card.ps1 -Issue DRA-7 -Status in_review -Pr 451
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Issue,
    [ValidateSet('backlog','todo','in_progress','in_review','done')]
    [string] $Status,
    [string] $Comment,
    [int] $Pr,
    [string] $AssigneeAgentId = 'e9b8cf25-a87a-495a-ab53-b72f95b974a1',
    [switch] $Json
)
$ErrorActionPreference = 'Stop'
$env:PAPERCLIP_HOME = 'C:\Users\david\.paperclip'
$env:PAPERCLIP_INSTANCE_ID = 'default'
if (-not $env:PAPERCLIP_API_KEY) {
    $env:PAPERCLIP_API_KEY = [Environment]::GetEnvironmentVariable('PAPERCLIP_API_KEY','User')
}
$pc = 'C:\Users\david\.paperclip\cli\runtime\node_modules\paperclipai\dist\index.js'
$argsList = @('issue','update',$Issue)
if ($Status) { $argsList += @('--status',$Status) }
if ($Status -eq 'in_progress') { $argsList += @('--assignee-agent-id',$AssigneeAgentId) }
$msg = $Comment
if ($Pr) {
    $extra = "PR https://github.com/DranakCorps-bot/EQBuddy/pull/$Pr"
    $msg = if ($msg) { "$msg — $extra" } else { $extra }
}
if ($msg) { $argsList += @('--comment',$msg) }
$argsList += '--json'
$out = & node $pc @argsList 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { Write-Host $out; exit $LASTEXITCODE }
if ($Json) { $out } else {
    $j = $out | ConvertFrom-Json
    Write-Host "$($j.identifier) → $($j.status)"
}
