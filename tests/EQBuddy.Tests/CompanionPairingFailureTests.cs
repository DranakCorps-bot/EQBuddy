using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text.RegularExpressions;
using EQBuddy.Companion;

namespace EQBuddy.Tests;

/// <summary>
/// **DRA-60 — a pairing code the PC will not accept must SAY so, because the socket
/// cannot.** A founder smoke test scanned the QR, the phone navigated to
/// <c>http://10.0.0.84:47859/#&lt;token&gt;</c>, and the page hung. Nothing was wrong with
/// the bind or the address: the PC answered that GET 200 with the whole ~155 KB page,
/// and the same URL on the PC's own browser connected. The hang was the page's, after
/// navigation.
///
/// <para>A fragment token puts the page straight into <c>#app</c> and calls
/// <c>connect()</c>. <c>ws.onclose</c> fires with code 1006 whether the PC REFUSED the
/// code, is rate-limiting this device, or is asleep — the browser will not tell a page
/// which, because the upgrade never happened. The page used to guess, and it only ever
/// guessed for a REMEMBERED code (<c>!everConnected &amp;&amp; tokenFromMemory &amp;&amp;
/// ++refusals &gt;= 3</c>). A code that arrived in the FRAGMENT fell through every
/// branch, so a truncated or stale scan retried forever behind an empty app, and
/// <c>refreshStale()</c> — which runs every second — took the "no news" branch each
/// time and hid the banner. The page was not stuck: it was deciding, once a second, to
/// say nothing.</para>
///
/// <para>**And the silence had teeth.** Five refused connects inside the server's 60 s
/// window rate-limit the device
/// (<c>CompanionServer.AuthFailureLimit</c>), and the 1→2→4→8 s backoff spends that
/// budget in about fifteen seconds — so the loop locked the phone out of the CORRECT
/// code it was about to be shown.
/// <see cref="ARetryLoopLocksTheDeviceOutOfTheCodeItIsAboutToBeShown"/> is that fact
/// against real sockets, and it is why the fix STOPS on a refusal rather than
/// surfacing one and carrying on.</para>
///
/// <para>The instrument the page now uses is in here too: the same-origin
/// <c>GET /ws?token=…</c> the socket would have made answers a STATUS — 403 wrong code,
/// 429 budget burned, 400 the code is right and an upgrade was all that was missing.
/// Those three are asserted against the real server, because the page's message is only
/// honest for as long as they hold.</para>
///
/// <para>There is no JS runner here, so the page half is asserted against the shipped
/// file the way <see cref="CompanionFirstPairingTests"/> does, with a committed NEGATIVE
/// each — a regex that cannot fail reads as coverage (trap 39).</para>
///
/// <para><b>The behavioural proof is the harness, and it is a RECIPE rather than a hand
/// edit</b> — <c>scripts/mobile-harness.ps1 -Refuse</c> closes the socket stub without
/// ever opening it (all a browser sees of any of the three causes) and answers the page's
/// own probe with the status named. The verdict is written into <c>#harnessState</c>, so
/// a headless <c>--dump-dom</c> IS the readout and nobody has to judge a screenshot:</para>
/// <code>
/// pwsh -NoProfile -File scripts/mobile-harness.ps1 -Refuse 403 -OutDir dist/refuse-403
/// msedge --headless=new --virtual-time-budget=45000 --dump-dom dist/refuse-403/harness.html#somecode
/// </code>
/// <para>Measured against the shipped page on 2026-09-11, all four, with a FRAGMENT token
/// (the scanned-QR case, which is the one that hung):</para>
/// <list type="bullet">
/// <item><b>403</b> — <c>closes: 1, probes: 1</c>, app hidden, pairing panel up carrying
/// the scanned-QR sentence. <b>One</b> auth failure spent, not five, which is the whole
/// point of stopping rather than announcing-and-retrying.</item>
/// <item><b>429</b> — still dialling (5 closes over the run), banner reads
/// "Too many pairing attempts from this device."</item>
/// <item><b>400</b> and an outright rejected fetch — still dialling, banner reads
/// "Can't reach EQBuddy on your PC — still trying."</item>
/// </list>
/// <para>And the OTHER blank page, driven the same way with a real snapshot: a PC
/// offering <c>quests</c>/<c>gear</c> against a phone's <c>FIRST_RUN</c> of
/// <c>spawns</c>/<c>session</c> — the Founder's actual configuration — now paints both
/// offered panels instead of nothing. <c>#noScreens</c> is absent, which is the assertion:
/// the page had something to draw, so it did not have to explain itself.</para>
/// </summary>
public class CompanionPairingFailureTests : IDisposable
{
    private const string Token = "0123456789abcdef0123456789abcdef";
    private readonly CompanionServer _server;

    public CompanionPairingFailureTests()
    {
        _server = new CompanionServer(new CompanionServerOptions
        {
            Token = Token,
            Port = 0,                                  // ephemeral — parallel-safe
            Addresses = [IPAddress.Loopback],
            HeartbeatInterval = TimeSpan.FromMilliseconds(200),
        });
        _server.Start();
    }

    public void Dispose() => _server.Dispose();

    private string Root => $"http://127.0.0.1:{_server.Port}";

    private static CancellationToken Deadline(int seconds = 10) =>
        new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

    private static string PageSource()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "EQBuddy.Companion", "Web", "index.html"));
        Assert.True(File.Exists(path), $"the shipped page moved: {path}");
        return File.ReadAllText(path);
    }

    /// <summary>The body of <c>ws.onclose = () =&gt; { … }</c> — every decision the page
    /// makes about a socket that has gone away, or never arrived.</summary>
    private static string OnCloseHandler()
    {
        var m = Regex.Match(PageSource(),
            @"ws\.onclose = \(\) => \{(?<body>.*?)\r?\n    \};", RegexOptions.Singleline);
        Assert.True(m.Success, "index.html no longer has a ws.onclose handler to read.");
        return m.Groups["body"].Value;
    }

    // ---------------- the page: no silent loop ----------------

    /// <summary>THE BUG. A refusal is a refusal whether the code came from the QR's
    /// fragment or from this device's memory, so the close handler must not decide on
    /// the code's PROVENANCE. The negative is the exact condition that shipped.</summary>
    [Fact]
    public void AFragmentCodeTheDesktopRefusesIsNotTreatedDifferentlyFromARememberedOne()
    {
        var page = PageSource();

        // The shipped condition, verbatim. A fragment token could not reach the pair
        // screen through it, because `tokenFromMemory` is false for exactly that case.
        Assert.DoesNotContain("!everConnected && tokenFromMemory", page);
        Assert.DoesNotContain("tokenFromMemory && ++refusals", page);

        // Where the code CAME FROM may decide what to forget and what to say. It may not
        // decide whether the player is told anything at all, which is the bug — so the
        // close handler, which is where that was decided, must not consult it or count.
        var onClose = OnCloseHandler();
        Assert.DoesNotContain("tokenFromMemory", onClose);
        Assert.DoesNotContain("refusals", onClose);

        // A socket that never opened gets diagnosed rather than silently re-dialled.
        Assert.Contains("diagnoseFailedConnect()", onClose);
    }

    /// <summary>A refused code is not retried: only a new code can help, and the retry
    /// itself is what burns the rate-limit budget
    /// (<see cref="ARetryLoopLocksTheDeviceOutOfTheCodeItIsAboutToBeShown"/>).</summary>
    [Fact]
    public void ARefusalStopsTheLoopAndShowsThePairingScreen()
    {
        var page = PageSource();
        var m = Regex.Match(page,
            @"function diagnoseFailedConnect\(\) \{(?<body>.*?)\r?\n  \}", RegexOptions.Singleline);
        Assert.True(m.Success, "the page no longer diagnoses a socket that never opened.");
        var body = m.Groups["body"].Value;

        // It asks the server WHICH refusal this is, rather than guessing from a count.
        Assert.Contains("/ws?token=", body);
        Assert.Contains("403", body);
        Assert.Contains("429", body);
        // And a 403 ends the loop instead of scheduling another dial.
        Assert.Contains("stopAndAskToPair(", body);

        var stop = Regex.Match(page,
            @"function stopAndAskToPair\(\) \{(?<body>.*?)\r?\n  \}",
            RegexOptions.Singleline);
        Assert.True(stop.Success, "the page no longer has a way to stop and ask to pair again.");
        Assert.Contains("$(\"pair\").classList.add(\"show\")", stop.Groups["body"].Value);
        // Retiring the SENTENCE, not just the banner: refreshStale() repaints that banner
        // once a second from `connectionProblem`, so hiding the element without clearing
        // the sentence puts "still trying" back over a pairing screen that says we have
        // stopped, one second later. One producer, one state (trap 4's family).
        Assert.Contains("connectionProblem = null", stop.Groups["body"].Value);
        // The negative: stopping must actually stop. A `connect(` inside this function
        // would be the silent loop wearing the fix's clothes.
        Assert.DoesNotContain("connect(", stop.Groups["body"].Value);
    }

    /// <summary>The banner is repainted once a second from <c>refreshStale()</c>, so any
    /// news the close handler writes straight into it is erased within the second. The
    /// never-connected case has to be a branch IN there or it cannot survive.</summary>
    [Fact]
    public void TheOncePerSecondBannerHasSomethingToSayBeforeTheFirstConnection()
    {
        var m = Regex.Match(PageSource(),
            @"function refreshStale\(\) \{(?<body>.*?)\r?\n  \}", RegexOptions.Singleline);
        Assert.True(m.Success, "index.html no longer has a refreshStale() to read.");
        var body = m.Groups["body"].Value;

        Assert.Contains("!everConnected", body);
        Assert.Contains("connectionProblem", body);

        // The negative: the shipped version's only mention of a never-connected page was
        // `everConnected && !lastMsgAt`, which is false before the first open — so every
        // tick fell through to the else and REMOVED the banner.
        var first = body.IndexOf("!everConnected", StringComparison.Ordinal);
        var hide = body.IndexOf("classList.remove(\"show\")", StringComparison.Ordinal);
        Assert.True(first >= 0 && (hide < 0 || first < hide),
            "a never-connected page still falls through to the branch that hides the banner.");
    }

    /// <summary>Trap 35's shape: an affordance with the right form and the wrong content.
    /// The page must not name the pairing code as the cause when what it measured was an
    /// unreachable PC, so the three statuses map to three different sentences.</summary>
    [Fact]
    public void TheMessageNamesWhatWasMeasuredRatherThanTheMostLikelyCause()
    {
        var page = PageSource();
        Assert.Contains("Too many pairing attempts", page);     // 429
        Assert.Contains("reach EQBuddy on your PC", page);      // unreachable / no answer
        Assert.Contains("accept this pairing code", page);      // 403
    }

    /// <summary>A connected page whose picks draw nothing is the same hang with a
    /// different cause — the PC gates every surface this device picked, the socket is
    /// fine, and <c>#sections</c> is empty under a header that says the character's
    /// name. <c>#screens</c> is <c>display:none</c> until the ⚙, so there is nothing on
    /// screen at all.</summary>
    [Fact]
    public void AConnectedPageWithNothingToDrawSaysSoInsteadOfDrawingNothing()
    {
        var m = Regex.Match(PageSource(),
            @"const wanted = visibleSurfaces\(\);(?<body>.*?)let previous = null;",
            RegexOptions.Singleline);
        Assert.True(m.Success, "render() no longer picks its panels from visibleSurfaces().");
        var body = m.Groups["body"].Value;

        Assert.Contains("wanted.length", body);
        Assert.Contains("nothing", body, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------- the wire facts the page's message rests on ----------------

    /// <summary>The measurement behind the report: the full 32-character code upgrades,
    /// and a code the QR handed over SHORT is a 403 whose body already says what to do.
    /// Same server, same request, one substring apart.</summary>
    [Fact]
    public async Task ATruncatedCodeIsRefusedAndTheFullOneUpgrades()
    {
        using var http = new HttpClient();
        var refused = await http.GetAsync($"{Root}/ws?token={Token[..24]}", Deadline());
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("rescan the QR code", await refused.Content.ReadAsStringAsync());

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{_server.Port}/ws?token={Token}"), Deadline());
        Assert.Equal(WebSocketState.Open, ws.State);
    }

    /// <summary>THE FACT THE DIAGNOSIS RESTS ON. The page's probe is an ordinary GET, so
    /// the RIGHT code answers 400 ("expected a WebSocket upgrade") and never 403 — which
    /// is what lets the page say "your PC isn't reachable" without ever accusing a code
    /// that is actually fine. If the server ever answered these two the same way, the
    /// page's message becomes a guess again and this test is where that is noticed.</summary>
    [Fact]
    public async Task APlainGetWithTheRightCodeIsAnswered400AndWithAWrongOne403()
    {
        using var http = new HttpClient();
        var right = await http.GetAsync($"{Root}/ws?token={Token}", Deadline());
        Assert.Equal(HttpStatusCode.BadRequest, right.StatusCode);

        var wrong = await http.GetAsync($"{Root}/ws?token=nope", Deadline());
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
    }

    /// <summary>Why a surfaced refusal has to STOP. Each silent redial spends one of five
    /// auth failures in the server's 60 s window, and the backoff spends them all in
    /// about fifteen seconds — after which the phone is refused even when it is finally
    /// holding the right code. The player's own remedy (rescan the new QR) is exactly
    /// what the loop had made impossible.</summary>
    [Fact]
    public async Task ARetryLoopLocksTheDeviceOutOfTheCodeItIsAboutToBeShown()
    {
        using var http = new HttpClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var bounce = await http.GetAsync($"{Root}/ws?token=stale-code", Deadline());
            Assert.Equal(HttpStatusCode.Forbidden, bounce.StatusCode);
        }

        var rescanned = await http.GetAsync($"{Root}/ws?token={Token}", Deadline());
        Assert.Equal(HttpStatusCode.TooManyRequests, rescanned.StatusCode);
    }

    /// <summary>The other half of the same arithmetic, and the one that says the fix is
    /// enough: what a STOPPING page spends is one refused socket plus one probe, and two
    /// is inside the budget — so the rescan the pairing screen asks for still connects.
    /// This is the test that would redden if <c>AuthFailureLimit</c> were ever lowered to
    /// two, which would put the fix's own cost over the line.</summary>
    [Fact]
    public async Task WhatAStoppingPageSpendsLeavesRoomForTheRescan()
    {
        using var http = new HttpClient();
        // The socket's refusal…
        var socket = await http.GetAsync($"{Root}/ws?token=stale-code", Deadline());
        Assert.Equal(HttpStatusCode.Forbidden, socket.StatusCode);
        // …and the probe that names it. Then the page stops.
        var probe = await http.GetAsync($"{Root}/ws?token=stale-code", Deadline());
        Assert.Equal(HttpStatusCode.Forbidden, probe.StatusCode);

        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{_server.Port}/ws?token={Token}"), Deadline());
        Assert.Equal(WebSocketState.Open, ws.State);
    }
}
