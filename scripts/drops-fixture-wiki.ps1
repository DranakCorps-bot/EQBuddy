# The offline eqlwiki seed every capture harness stages a Drops surface from, and the
# writer that puts it on disk. Dot-sourced by `scripts/shoot.ps1` (stills) and
# `scripts/record-tray-gifs.ps1` (the landing's interaction GIFs).
#
# IT LIVES HERE BECAUSE IT IS ONE PRODUCER. The seed was written inside `shoot.ps1` with a
# comment saying it is "a variable rather than three copies because a staging list is code
# a compiler cannot check (trap 30)" — and DRA-62 then needed the same list in a SECOND
# harness. Copying it across would have made exactly the two-current-answers shape trap 33
# names, with the failure mode the original comment already spells out below: a partial or
# drifted seed does not fail, it makes the app correctly fetch the rest from the live wiki,
# so the capture becomes a picture of whatever eqlwiki said that minute.
#
# Nothing here fetches. Both harnesses run fully offline against this.

# EVERY creature the fixture drops from, seeded once and shared by every shot that
# photographs a Drops surface. It is a variable rather than three copies because a staging
# list is code a compiler cannot check (trap 30) — and this one has a specific way of going
# wrong: a PARTIAL seed does not fail, it makes the app correctly fetch the rest from the
# live wiki, so the capture becomes a picture of whatever eqlwiki said that minute. That is
# trap 23, and it cost two wrong 'wiki-pack' shots before anyone noticed.
# Skeleton is deliberately five days old: inside the 7-day lifetime (so it is not re-fetched)
# and outside the 30-second "just now" rule, which is the only way a shot can show the
# freshness caption doing anything at all.
$DropsFixtureWiki = @{
    'Orc pawn' = @()
    'Puma' = @('Chunk of Meat')
    'Giant spider' = @('Spider Silk', 'Spider Legs')
    'Skeleton' = @{ Loot = @('Bone Chips', 'Rusty Scimitar'); AgeDays = 5 }
    'Asp' = @('Giant Snake Fang', 'Giant Snake Rattle', 'Snake Meat')
    'Large rattlesnake' = @('Snake Egg', 'Snake Fang')
    'Rattlesnake' = @('Snake Fang')
    'Willowisp' = @('Burned Out Lightstone')
    'Young kodiak' = @('Bear Meat', 'Chunk of Meat', 'Thick Grizzly Bear Skin')
    'Zombie' = @('Cloth Cape', 'Zombie Skin')
    'Ghoul' = @('Mote of Infinitesimal Potential')
    'Lesser mummy' = @('Rusty Morning Star', 'Splintering Club')
    'Plains cat' = @('Ruined Cat Pelt')
}

# Write the seed into an isolated profile's mob cache. `$profileDir` is passed rather than
# read from the caller's scope, because the two harnesses name that variable differently and
# a function that reaches for an ambient one is a second way to be wrong about which profile
# is being staged.
function Write-EqWikiCacheTo([string]$profileDir, [hashtable]$pages) {
    $dir = Join-Path $profileDir 'wiki-cache/mobs'
    if ($null -eq $pages) { Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue; return }
    New-Item -ItemType Directory -Force $dir | Out-Null
    foreach ($title in $pages.Keys) {
        # The service's own filename rule: non-alphanumerics become underscores, lowercased.
        $file = ((($title.ToCharArray() | ForEach-Object {
            if ([char]::IsLetterOrDigit($_)) { [char]::ToLowerInvariant($_) } else { '_' }
        }) -join '') + '.json')
        # Drops are read from the {{Namedmobpage}} template's known_loot field, NOT from
        # free wikitext — EqlWikiMobs.Parse only ever looks inside known_loot/common_loot.
        # Free "== Loot ==" prose parses to a page with no drops at all, which is a real
        # state (PageHasNoLoot) and therefore renders perfectly plausibly: the first run
        # of this staging showed all thirteen creatures as "page lists no loot" and looked
        # like a correct screenshot of a wrong app.
        # A value is either the loot list, or @{ Loot = @(...); AgeDays = N } to stage a
        # page read N days ago — how the Drops tab's freshness caption (#226) gets a
        # state worth photographing. Keep it INSIDE the 7-day lifetime: a page older than
        # that is expired, the app re-fetches it live, and the caption reads "just now" --
        # a real state, and a picture of the wrong one (trap 23).
        $entry = $pages[$title]
        $items = if ($entry -is [hashtable]) { $entry.Loot } else { $entry }
        $age = if ($entry -is [hashtable] -and $entry.AgeDays) { [int]$entry.AgeDays } else { 0 }
        $loot = (($items | ForEach-Object { "{{:$_}}" }) -join ' ')
        $wikitext = "{{Namedmobpage`n|name=$title`n|zone=Test Zone`n|known_loot=$loot`n}}"
        @{
            Title = $title
            Wikitext = $wikitext
            FetchedAt = (Get-Date).ToUniversalTime().AddDays(-$age).ToString('o')
        } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $dir $file) -Encoding UTF8
    }
}
