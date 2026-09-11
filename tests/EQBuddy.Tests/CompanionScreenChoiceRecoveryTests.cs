using System.Text.RegularExpressions;
using EQBuddy.Companion;

namespace EQBuddy.Tests;

/// <summary>
/// **DRA-64 — the repair for a blank phone could not reach the one phone that was blank.**
///
/// #550 shipped the rescue for DRA-60: when a device's <c>FIRST_RUN</c> defaults miss every
/// surface the PC is willing to share, <c>wanted</c> comes back empty and the page draws a
/// live header over nothing. The Founder rescanned the QR on the republished Desktop and the
/// phone was still blank, while pasting the SAME full URL into the PC's own browser worked.
///
/// <para>That asymmetry is not the network, and it is not the token. It is
/// <c>innerWidth</c>. At &gt;= 900 the page's <c>FIRST_RUN</c> is
/// <c>map, spawns, mez, session, quests</c> and this PC offers <c>quests, gear</c> — they
/// overlap on <c>quests</c>, so the PC's browser paints. Below 900 it is
/// <c>spawns, session</c>, which overlaps nothing. The working paste and the blank phone are
/// the same build, the same token and the same server, one breakpoint apart.</para>
///
/// <para>**And the rescue was gated on <c>!choice</c>**, i.e. "this device has no stored
/// choice". That was standing in for *"nobody has chosen yet"*, and they are different
/// claims (trap 64b: a proxy is a claim about the world). The Founder's phone had already
/// paired against the BROKEN build, and the first snapshot of that session wrote
/// <c>{"order":["quests","gear"],"enabled":{"quests":false,"gear":false}}</c> into
/// <c>localStorage</c> under <c>eqbuddy-screens-&lt;token8&gt;</c> — through the
/// <c>if (offerChanged) { … saveChoice(); }</c> line, which fires on every first snapshot
/// because <c>offered</c> starts empty. So the device that needed the rescue was the one
/// device it could not fire for, and rescanning the same QR reloads the same key and the
/// same all-off choice. Nothing on the PC could fix it; nothing in the page said why.</para>
///
/// <para>The gate is now the fact — has a human ever touched the ⚙ — recorded by
/// <c>commitChoice()</c>, the one door a screen pick comes through. An all-off choice a
/// player MADE still survives; one that only the defaults produced is repaired once, and
/// the page says it did.</para>
///
/// <para>There is no JS runner in this suite, so the page half is asserted against the
/// shipped file with a committed NEGATIVE each, the way <see cref="CompanionFirstPairingTests"/>
/// and <see cref="CompanionPairingFailureTests"/> do — a regex that cannot fail reads as
/// coverage (trap 39).</para>
///
/// <para><b>The behavioural proof is the harness #552 built, driven on the fixture it could
/// not previously produce.</b> <c>-StoredChoice</c> seeds a device that has ALREADY PAIRED,
/// and it deliberately suppresses the <c>-Snapshot</c> rewrite of <c>FIRST_RUN</c> — that
/// rewrite sets <c>FIRST_RUN</c> to the snapshot's own offer, which makes the overlap succeed
/// by construction and is why the run that verified #550 could not see this:</para>
/// <code>
/// pwsh -NoProfile -File scripts/mobile-harness.ps1 -OutDir dist/dra64 `
///   -Snapshot &lt;snap.json offering quests+gear&gt; `
///   -StoredChoice '{"order":["quests","gear"],"enabled":{"quests":false,"gear":false}}'
/// msedge --headless=new --virtual-time-budget=45000 --dump-dom "file:///&lt;abs&gt;/harness.html#somecode"
/// </code>
/// <para>Measured 2026-09-11, reading <c>#harnessState</c>, all three through the same door.
/// <b>Prove-failed rather than green-only</b> (trap 34) — BEFORE is the page at
/// <c>cc020280</c>, which is the page the Founder actually rescanned:</para>
/// <list type="bullet">
/// <item><b>BEFORE, Founder's state</b> — <c>panels: []</c>, <c>noScreens: true</c> reading
/// "No screens picked on this device. Tap ⚙ at the top to choose what to show.", stored
/// choice still <c>{quests:false,gear:false}</c>. So his phone was not hanging by then: it
/// was showing that one sentence and no data, for good.</item>
/// <item><b>AFTER</b> — <c>panels: ["Quests","Gear checklist"]</c>, <c>noScreens: false</c>,
/// the notice shown, and <b><c>stored</c> now <c>{quests:true,gear:true}</c></b> — the
/// repair reached localStorage, which is what stops it repeating on every open.</item>
/// <item><b>AFTER, the negative</b> (<c>playerPicked:true</c> seeded — a player who turned
/// them all off deliberately) — <c>panels: []</c>, nothing overridden, nothing announced,
/// and the #550 sentence explaining it. An over-eager repair reddens here.</item>
/// </list>
///
/// <para>A second, cheaper instrument runs the same decision without a browser:
/// <c>node scripts/dra64-choice-probe.mjs [olderPage.html]</c> lifts the shipped
/// <c>ensureChoice()</c> out of <c>index.html</c> and runs it over a fake <c>localStorage</c>
/// across six scenarios, and takes an older page as an argument so the prove-fail is one
/// command. Neither is a CI step — Helm ACKed leaving node out of CI on #550.</para>
/// </summary>
public class CompanionScreenChoiceRecoveryTests
{
    private static string PageSource()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "EQBuddy.Companion", "Web", "index.html"));
        Assert.True(File.Exists(path), $"the shipped page moved: {path}");
        return File.ReadAllText(path);
    }

    private static string Body(string pattern, string what)
    {
        var m = Regex.Match(PageSource(), pattern, RegexOptions.Singleline);
        Assert.True(m.Success, $"index.html no longer has {what} to read.");
        return m.Groups["body"].Value;
    }

    private static string EnsureChoice() =>
        Body(@"function ensureChoice\(\) \{(?<body>.*?)\r?\n  \}", "an ensureChoice()");

    private static string CommitChoice() =>
        Body(@"function commitChoice\(\) \{(?<body>.*?)\r?\n  \}", "a commitChoice()");

    // ---------------- the gate is the fact, not the proxy ----------------

    /// <summary>THE BUG. The rescue must not ask whether a stored choice EXISTS — that is
    /// true of every device that has ever connected, including the one in the report. The
    /// negatives are the two forms the shipped gate took.</summary>
    [Fact]
    public void TheRescueAsksWhetherAHumanEverPickedNotWhetherAChoiceWasStored()
    {
        var body = EnsureChoice();

        // The shipped condition, verbatim. A phone that had already paired could not reach
        // the rescue through it, because `choice` is non-null for exactly that device.
        Assert.DoesNotContain("firstPairing", body);
        Assert.DoesNotContain("const firstPairing = !choice", PageSource());

        // What it asks instead, and that the answer still has to be "nothing is showing".
        Assert.Contains("playerHasPicked()", body);
        Assert.Contains("!offered.some(s => choice.enabled[s])", body);
    }

    /// <summary>A rescue the caller cannot see is a rescue that never reaches
    /// <c>localStorage</c>, so the next load repeats it — and repeats the sentence that
    /// explains it. <c>ensureChoice()</c> has to REPORT, and the save has to read it.</summary>
    [Fact]
    public void TheRepairIsPersistedSoItHappensOnceRatherThanEveryTimeThePageOpens()
    {
        Assert.Contains("return true;", EnsureChoice());

        var page = PageSource();
        // The negative: the shipped save looked only at the offer list. A rescue on a push
        // whose offer did not move would have been forgotten.
        Assert.DoesNotContain("if (offerChanged) { renderScreens(); saveChoice(); }", page);
        Assert.Contains("if (offerChanged || rescued) { renderScreens(); saveChoice(); }", page);
    }

    /// <summary>Trap 4's shape. "The player has chosen" is ONE fact with ONE producer:
    /// <c>commitChoice()</c>, which both the checkboxes and the ▲▼ reorder go through. The
    /// other two <c>saveChoice()</c> callers — the offer-changed push and the fullscreen
    /// flag — are not screen picks, and a second writer there is what would make an
    /// accidental all-off choice permanent all over again.</summary>
    [Fact]
    public void OnlyTheGearPanelRecordsThatThePlayerChose()
    {
        var page = PageSource();
        Assert.Contains("choice.playerPicked = true", CommitChoice());

        // Exactly one assignment in the whole page.
        Assert.Equal(1, Regex.Matches(page, @"playerPicked\s*=\s*true").Count);

        // And not from either of the saves that are not a pick. `fullscreen` is the
        // fullscreen flag's own line; neither it nor the offer-changed branch may stamp.
        foreach (var line in page.Split('\n'))
        {
            if (!line.Contains("playerPicked = true")) continue;
            Assert.DoesNotContain("fullscreen", line);
            Assert.DoesNotContain("offerChanged", line);
        }
    }

    /// <summary>An all-off choice the player MADE is a decision, and the repair must leave
    /// it alone — otherwise the fix for a blank page becomes a page that will not stay
    /// configured. The gate reads the stamp, so this is the same assertion from the other
    /// side: the only thing standing between a rescue and a player's picks.</summary>
    [Fact]
    public void AnAllOffChoiceThePlayerActuallyMadeSurvives()
    {
        var page = PageSource();
        var m = Regex.Match(page, @"const playerHasPicked = [^\r\n]*");
        Assert.True(m.Success, "the page no longer has a playerHasPicked() to read.");
        Assert.Contains("choice.playerPicked", m.Value);
        // It must be a live read, not a boot-time snapshot: commitChoice() sets the flag
        // during the same page life, and a captured boolean would go on rescuing after the
        // player had just turned everything off.
        Assert.Contains("=> !!(choice && choice.playerPicked)", m.Value);
    }

    // ---------------- silent no-ops are broken, both ways ----------------

    /// <summary>Turning a device's screens back on behind the owner is a change to their
    /// settings, so it says so — but ONLY when it overrode picks the device arrived
    /// holding. On a first pairing the same branch is the default, and announcing a default
    /// is noise.</summary>
    [Fact]
    public void OverridingPicksTheDeviceArrivedWithIsAnnouncedAndADefaultIsNot()
    {
        var page = PageSource();
        Assert.Contains("const hadStoredChoice = !!choice;", page);
        Assert.Contains("if (rescued && hadStoredChoice) notice(RESCUED_PICKS);", page);

        // The sentence names what happened and the door back, rather than a cause.
        var m = Regex.Match(page, @"const RESCUED_PICKS =(?<body>.*?);\r?\n", RegexOptions.Singleline);
        Assert.True(m.Success, "the page no longer has a sentence for a rescued choice.");
        Assert.Contains("turned them on", m.Groups["body"].Value);
        Assert.Contains("⚙", m.Groups["body"].Value);
    }

    // ---------------- the premise, measured rather than assumed ----------------

    /// <summary>Why the PC's browser worked while the phone did not, as two facts rather
    /// than a story: the narrow <c>FIRST_RUN</c> and the wide one, read off the shipped
    /// page, against a PC that offers only <c>quests</c> and <c>gear</c>. The wide list
    /// overlaps; the narrow one does not. If either list ever changes so that the narrow
    /// one overlaps too, this test is where the explanation in the class comment stops
    /// being true.</summary>
    [Fact]
    public void TheNarrowFirstRunMissesASharedQuestsAndGearPcWhileTheWideOneDoesNot()
    {
        var m = Regex.Match(PageSource(),
            @"const FIRST_RUN = innerWidth >= 900\s*\r?\n\s*\?(?<wide>[^\r\n]*)\r?\n\s*:(?<narrow>[^\r\n]*);",
            RegexOptions.Singleline);
        Assert.True(m.Success, "the page no longer picks FIRST_RUN off the viewport width.");

        string[] Names(string s) =>
            [.. Regex.Matches(s, "\"(?<n>[a-z]+)\"").Select(x => x.Groups["n"].Value)];

        var wide = Names(m.Groups["wide"].Value);
        var narrow = Names(m.Groups["narrow"].Value);
        Assert.NotEmpty(wide);
        Assert.NotEmpty(narrow);

        // The Founder's PC gate, as CompanionHost computes it: everything minus the hidden.
        string[] offered = [CompanionSurfaces.Quests, CompanionSurfaces.Gear];
        Assert.All(offered, s => Assert.Contains(s, CompanionSurfaces.All));

        Assert.NotEmpty(wide.Intersect(offered));      // the PC browser paste painted
        Assert.Empty(narrow.Intersect(offered));       // the phone had nothing to draw
    }
}
