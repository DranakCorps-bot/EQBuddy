<#
.SYNOPSIS
    Builds a drivable copy of the EQBuddy Mobile page so its map can be worked on
    without a PC, a phone, a pairing code or a live log.

.DESCRIPTION
    Two edits, and only two: the pairing token is hard-coded (so boot() runs), and
    WebSocket is stubbed out (so snapshots can be pushed in by hand). Everything under
    test — the map's SVG drawing, the fade curve, the counter-scaling, the panel
    layout — is the shipped code, because the file is a copy of the shipped file.

    Open the result in any browser and push a snapshot from the console:

        __PUSH({ kind: "snapshot", protocol: 3, identity: {...}, offered: ["map"], map: {...} })

    The shape is CompanionSnapshot with camelCase names; CompanionMapSourceTests is
    the authority on what the server actually sends.

    Output lands in dist/ because that folder is gitignored — the harness is a tool,
    not a deliverable, and it must never be mistaken for the page itself.

.NOTES
    Written 2026-08-14 while restoring the breadcrumb trail. It immediately earned its
    keep: driving the real page at zoom exposed that text.poi's CSS font-size had been
    beating the counter-scaling attribute, so every map label had been ballooning on
    zoom since the map shipped. Unit tests could not have seen that.
#>
[CmdletBinding()]
param(
    [string] $OutDir = (Join-Path $PSScriptRoot '..\dist\mobile-harness'),
    # A snapshot JSON to embed and push on load — see ScreenshotFixtureTests, which
    # writes one through the real projection from the game's own map files. With this,
    # opening the harness IS the screenshot; without it, push by hand from the console.
    [string] $Snapshot,
    # Dress the page as a propped-up tablet for a screenshot: the chrome-free (fullscreen)
    # presentation, and no "reconnecting" banner — that banner is a harness artifact,
    # since nothing here is sending the heartbeats a real PC sends every three seconds.
    [switch] $Screenshot,
    # JavaScript run once the snapshot has been rendered — how a shot is posed (zoom in,
    # centre on the player, tap a spawn point to raise the action bar). It drives the
    # page's own controls, so what it produces is what a thumb would produce.
    [string] $After,
    # DRA-60: drive the page's FAILED-CONNECT path — the one a player hits when the QR
    # arrives truncated, when the PC has issued a new code, or when EQBuddy is not
    # running. The socket stub CLOSES without ever opening, which is all a browser can
    # see of any of them (onclose 1006, no readable status), and the page's own
    # `GET /ws?token=…` probe is answered with the status this names:
    #
    #   403         — the PC is there and refused the CODE. The page must STOP dialling
    #                 and show the pairing explainer with the reason on it.
    #   429         — this device burned CompanionServer's auth budget. Keep trying, and
    #                 say which of the two it is.
    #   400         — the code is RIGHT and only the upgrade was missing (a proxy eating
    #                 the Upgrade header). Keep trying.
    #   unreachable — the fetch itself is rejected: EQBuddy is not answering at all.
    #
    # There is no JS runner in tests/EQBuddy.Tests, so CompanionPairingFailureTests reads
    # the shipped source and this is the half that RUNS it. Drive it headless:
    #
    #   msedge --headless=new --virtual-time-budget=40000 --dump-dom <harness.html>#somecode
    #
    # The fragment matters: the harness hard-codes `token`, but `tokenFromMemory` is still
    # computed from location.hash, and it is what picks which sentence the player reads.
    [ValidateSet('403', '429', '400', 'unreachable')]
    [string] $Refuse,
    # DRA-64: drive the page as a device that has ALREADY PAIRED, by seeding the saved
    # screen choice this JSON describes into localStorage before boot() reads it.
    #
    # This is the fixture -Refuse and -Snapshot cannot produce, and it is the one the bug
    # report was actually in. A clean browser profile is the right fixture for trap 67 and
    # the WRONG one here: the Founder's phone was blank because the BROKEN build had
    # already written `{"order":["quests","gear"],"enabled":{"quests":false,"gear":false}}`
    # to it, and every harness run until now started with empty storage — so the run that
    # verified #550 could not see that #550's own gate (`!choice`) excluded exactly that
    # device. Name the state the reporter is in, or the harness only ever re-proves the
    # happy path.
    #
    #   pwsh -NoProfile -File scripts/mobile-harness.ps1 -Snapshot <snap.json> `
    #     -StoredChoice '{"order":["quests","gear"],"enabled":{"quests":false,"gear":false}}'
    #   msedge --headless=new --virtual-time-budget=40000 --dump-dom <harness.html>#somecode
    #
    # Then read #harnessState: `picked` is what this device draws, `noScreens` is whether
    # the page had to explain itself, and `notice` is whether it said it changed anything.
    #
    # NOTE: this deliberately SUPPRESSES the -Snapshot FIRST_RUN rewrite below. That
    # rewrite sets FIRST_RUN to the snapshot's own offered list, which makes the overlap
    # succeed by construction — it is the right thing for posing a screenshot and it
    # dissolves the very mechanism this flag exists to drive.
    [string] $StoredChoice
)

$ErrorActionPreference = 'Stop'

$src = Join-Path $PSScriptRoot '..\src\EQBuddy.Companion\Web\index.html'
if (-not (Test-Path $src)) { throw "Phone page not found at $src" }
$html = Get-Content $src -Raw

# Both anchors are asserted rather than assumed: a silent no-op here would look like a
# page that mysteriously stopped working, which is the worst way to lose an afternoon.
$tokenLine = 'const token = fragmentToken || remembered || "";'
if (-not $html.Contains($tokenLine)) {
    throw "The page's token line has moved — update scripts/mobile-harness.ps1 to match."
}
$html = $html.Replace($tokenLine, 'const token = "harness";')

if ($Refuse) {
    $answer = if ($Refuse -eq 'unreachable') {
        'Promise.reject(new TypeError("harness: EQBuddy is not answering"))'
    } else {
        # None of 403 / 429 / 400 is ok — the page reads `status`, and a stub that
        # reported ok:true for an error status would be lying in the one field a
        # future reader is most likely to trust.
        "Promise.resolve({ status: $Refuse, ok: false })"
    }
    $stub = @"
<script>
// Harness only (-Refuse $Refuse): a PC this device cannot connect to. The socket CLOSES
// without ever opening — all a browser sees of a refused, rate-limited or unanswered
// upgrade alike — and the page's own GET /ws?token=… probe answers $Refuse.
//
// __CLOSES and __PROBES make "it gave up" and "it is still dialling" readable FACTS
// rather than a judgement about a screenshot: a page that stopped stops counting.
window.__SOCK = null;
window.__SENT = [];
window.__CLOSES = 0;
window.__PROBES = [];
window.WebSocket = class {
  constructor(url) {
    this.readyState = 3;
    window.__SOCK = this;
    setTimeout(() => { window.__CLOSES++; this.onclose && this.onclose({ code: 1006 }); }, 0);
  }
  send() {}
  close() {}
};
window.fetch = url => { window.__PROBES.push(String(url)); return $answer; };
// Read after the run: the page's own account of what it did.
window.__STATE = () => ({
  closes: window.__CLOSES,
  probes: window.__PROBES.length,
  probedUrl: window.__PROBES[0] || null,
  banner: document.getElementById("banner").textContent,
  bannerShown: document.getElementById("banner").classList.contains("show"),
  pairShown: document.getElementById("pair").classList.contains("show"),
  pairWhy: (document.getElementById("pairWhy") || {}).textContent || null,
  appHidden: document.getElementById("app").style.display === "none",
});
</script>
<script>
"@
}
else {

$stub = @'
<script>
// Harness only: stand in for the PC so boot() can run. __PUSH(msg) delivers a snapshot
// exactly as the server's WebSocket would.
//
// __PUSH resolves the CURRENT socket when it is CALLED, rather than closing over the one
// that existed when it was defined. The page reconnects on its own schedule, and a
// devtools/automation context can outlive a reload — a captured socket goes quietly
// stale and every push lands on a dead object with no error to show for it.
window.__SOCK = null;
window.__SENT = [];
window.WebSocket = class {
  constructor() {
    this.readyState = 1;
    window.__SOCK = this;
    setTimeout(() => this.onopen && this.onopen(), 0);
  }
  send(s) { try { window.__SENT.push(JSON.parse(s)); } catch { window.__SENT.push(s); } }
  close() {}
};
window.__PUSH = m => {
  const s = window.__SOCK;
  if (!s || !s.onmessage) throw new Error("harness: no socket yet — the page has not booted");
  s.onmessage({ data: JSON.stringify(m) });
};
</script>
<script>
'@

}

if ($StoredChoice) {
    # Written BEFORE the page's script runs, because boot() reads localStorage on its very
    # first lines. The key is the page's own: "eqbuddy-screens-" + token.slice(0, 8), and
    # the harness token is "harness".
    try { $null = $StoredChoice | ConvertFrom-Json }
    catch { throw "-StoredChoice is not valid JSON: $StoredChoice" }
    $seed = @"
<script>
// Harness only (-StoredChoice): this device has paired before and saved these picks.
try { localStorage.setItem("eqbuddy-screens-harness", JSON.stringify($StoredChoice)); } catch (e) {}
</script>
"@
    $stub = $seed + $stub
}

$patched = [regex]::Replace($html, '<script>(\r?\n)"use strict";',
    { param($m) $stub + $m.Groups[1].Value + '"use strict";' }, 1)
if ($patched -eq $html) {
    throw "The page's main <script> opener has moved — update scripts/mobile-harness.ps1 to match."
}

if ($Refuse) {
    # The verdict has to be IN THE DOM, because `--dump-dom` is the whole readout of a
    # headless run: a value left on `window` is invisible to it. Written late enough for
    # the page's 1 s banner tick to have had several goes at overwriting whatever the
    # close handler wrote — which is exactly the shape that hid this bug once already.
    $verdict = @'
<div id="harnessState" style="display:none"></div>
<script>
setTimeout(() => {
  document.getElementById("harnessState").textContent = JSON.stringify(window.__STATE(), null, 1);
}, 20000);
</script>
'@
    $patched = $patched -replace '</body>', ($verdict + "`n</body>")
}

if ($StoredChoice) {
    # Same contract as -Refuse's verdict: the readout has to be IN THE DOM, because
    # --dump-dom is all a headless run gives back. What it reports is the DRA-64 question
    # in three parts — what this device ended up drawing, whether the page had to explain
    # an empty #sections, and whether it TOLD the owner it had changed their picks. The
    # stored choice is read back out of localStorage too, because "the repair was
    # persisted" is a different claim from "the repair happened" and only one of them
    # stops it repeating on every open.
    $verdict = @'
<div id="harnessState" style="display:none"></div>
<script>
// The notice is a 5 s toast and the readout below is later than that on purpose (the page
// needs time to settle), so "is it showing now" would answer the wrong question and answer
// it false. Latch it instead: what matters is whether the player was EVER told.
// Registered IMMEDIATELY, not on DOMContentLoaded: this block is injected at the end of
// <body>, so #notice already exists — and under --virtual-time-budget the snapshot push can
// land before DOMContentLoaded fires, which is exactly how the first cut of this latch
// reported `false` for a notice the page had demonstrably shown.
window.__NOTICED = false;
(function () {
  const n = document.getElementById("notice");
  if (!n) return;
  if (n.classList.contains("show")) window.__NOTICED = n.textContent;
  new MutationObserver(() => {
    if (n.classList.contains("show")) window.__NOTICED = n.textContent;
  }).observe(n, { attributes: true, attributeFilter: ["class"] });
})();
window.__CHOICESTATE = () => {
  let stored = null;
  try { stored = localStorage.getItem("eqbuddy-screens-harness"); } catch (e) {}
  return {
    panels: [...document.querySelectorAll("#sections section.surface > h2 > span:first-child")]
      .map(s => s.textContent),
    noScreens: !!document.getElementById("noScreens"),
    noScreensText: (document.getElementById("noScreens") || {}).textContent || null,
    noticeEverShown: window.__NOTICED,
    // The latch's independent check: textContent SURVIVES the toast hiding itself, and
    // notice() is the only thing that writes it. Two readings of one fact, on purpose —
    // the latch can miss, and a silent miss would read as "the page said nothing".
    noticeText: document.getElementById("notice").textContent || null,
    stored: stored,
  };
};
setTimeout(() => {
  document.getElementById("harnessState").textContent =
    JSON.stringify(window.__CHOICESTATE(), null, 1);
}, 8000);
</script>
'@
    $patched = $patched -replace '</body>', ($verdict + "`n</body>")
}

if ($Snapshot) {
    if (-not (Test-Path $Snapshot)) { throw "Snapshot not found: $Snapshot" }
    $json = Get-Content $Snapshot -Raw
    $offered = ($json | ConvertFrom-Json).offered

    # Stand in for the device's saved picks. FIRST_RUN is evaluated at script load and
    # keyed off innerWidth, so a pane that has not settled at its final size yet picks
    # the PHONE set — and a snapshot offering only the map then renders nothing at all.
    # With a snapshot embedded, the device wants exactly what the snapshot offers.
    # …EXCEPT under -StoredChoice, where the real breakpoint-chosen defaults ARE the thing
    # under test: rewriting FIRST_RUN to the snapshot's own offer makes the overlap succeed
    # by construction, which is how a run can verify a fix for a non-overlap it has just
    # removed (DRA-64).
    if (-not $StoredChoice) {
        $picks = ($offered | ForEach-Object { '"' + $_ + '"' }) -join ', '
        $patched = [regex]::Replace($patched,
            '(?s)const FIRST_RUN = .*?;',
            "const FIRST_RUN = [$picks];   // harness: the embedded snapshot's own surfaces")
    }
    # Pushed once the page has booted; the socket stub raises onopen on a timer, so this
    # waits for __SOCK rather than racing it.
    # 1.5s, not 300ms: the map refits itself when its box settles (a ResizeObserver, so
    # it lands after first layout). Posing before that happens gets the pose thrown away.
    $afterJs = if ($After) { "setTimeout(() => { try { $After } catch (e) { console.error(e); } }, 1500);" } else { '' }
    $auto = @"
<script>
(function tryPush() {
  if (window.__SOCK && window.__SOCK.onmessage) {
    window.__PUSH(__SNAPSHOT);
    $afterJs
    return;
  }
  setTimeout(tryPush, 20);
})();
</script>
"@
    $decl = "<script>window.__SNAPSHOT = $json;</script>"
    $patched = $patched -replace '</body>', ($decl + "`n" + $auto + "`n</body>")
}

if ($Screenshot) {
    $shot = @'
<script>
document.documentElement.classList.add("lean");
addEventListener("DOMContentLoaded", () => {
  const b = document.getElementById("banner");
  if (!b) return;
  b.style.display = "none";
  new MutationObserver(() => { b.style.display = "none"; }).observe(b, { attributes: true });
});
</script>
'@
    $patched = $patched -replace '</body>', ($shot + "`n</body>")
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$out = Join-Path $OutDir 'harness.html'
Set-Content -Path $out -Value $patched -Encoding UTF8
Write-Output (Resolve-Path $out).Path
