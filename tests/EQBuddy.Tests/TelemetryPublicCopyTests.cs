using System.Text.Json;
using System.Text.RegularExpressions;
using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The public pages tell the shipped truth about the heartbeat** (TEL-PR4, DRA-363;
/// <c>docs/v2/telemetry.md</c> §8 and §10). Until v2.0.0 the README and SECURITY.md promised
/// "zero telemetry" and "never phones home", and they were true because nothing sent. The
/// launch release ships the opt-in heartbeat, so those two sentences became false the same
/// day, and a reader with a network monitor can check which one we meant.
///
/// Each rule is a predicate over TEXT, run once on the committed file and once on the
/// pre-launch wording it replaced, so a green run cannot hide a rule that fires on nothing
/// (traps 34 and 78). The host and the payload keys are read from the code that sends them
/// (<see cref="TelemetrySender.BaseUrl"/>, <see cref="TelemetryHeartbeat.PayloadKeys"/>), never
/// spelled here a second time (trap 4).
/// </summary>
public class TelemetryPublicCopyTests
{
    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Repo, .. parts]));

    private static string Flat(string s) => Regex.Replace(s, @"\s+", " ");

    private static string Host => new Uri(TelemetrySender.BaseUrl).Host;

    // ------------------------------------------------------- the DRA-1980 cutover ----

    /// <summary>
    /// **The release that turned telemetry on by default** (DRA-1980 D2), read off the ONE
    /// What's-new entry that announces it, never typed a second time (trap 4). Every public
    /// surface that states or charts the active counts carries a cutover marker naming this
    /// version, so the jump in those counts is not read as growth (plan §3).
    /// </summary>
    internal static string CutoverVersion
    {
        get
        {
            using var doc = JsonDocument.Parse(Read("src", "EQBuddy.Core", "Data", "WhatsNew.json"));
            var versions = doc.RootElement.EnumerateArray()
                .Where(e => e.GetProperty("highlights").EnumerateArray()
                    .Any(h => h.GetString()!.StartsWith("TELEMETRY IS NOW ON BY DEFAULT", StringComparison.Ordinal)))
                .Select(e => e.GetProperty("version").GetString()!)
                .ToList();
            Assert.Single(versions);
            return versions[0];
        }
    }

    /// <summary>The cutover marker's two halves, in a flattened text: the version telemetry went
    /// on by default from, and that the figures before it counted opted-in installs only.</summary>
    internal static IReadOnlyList<string> CutoverViolations(string flat, string version)
    {
        var bad = new List<string>();
        if (!flat.Contains("on by default from v" + version, StringComparison.OrdinalIgnoreCase))
            bad.Add($"no cutover marker naming v{version}, the release that turned telemetry on by default");
        if (!flat.Contains("opted-in installs only", StringComparison.OrdinalIgnoreCase))
            bad.Add("the cutover marker does not say earlier figures counted opted-in installs only");
        return bad;
    }

    /// <summary>The opt-in default's sentences, false of every build from the cutover on.</summary>
    private static readonly string[] OptInDefaultClaims =
    [
        "off until you say yes",
        "off on every install until you",
        "telemetry is off unless a player turns it on",
        "No telemetry unless you turn it on",
    ];

    private static IEnumerable<string> OptInDefaultViolations(string flat) =>
        OptInDefaultClaims.Where(c => flat.Contains(c, StringComparison.OrdinalIgnoreCase))
            .Select(c => $"still says \"{c}\" (opt-in default; on by default since DRA-1980)");

    // ------------------------------------------------------- the DRA-2010 cutover ----

    /// <summary>
    /// **The release that turned telemetry on for every install**, past "Not now" answers
    /// included (DRA-2010 D2), read off the ONE What's-new entry that announces it, like
    /// <see cref="CutoverVersion"/>. The surfaces that state the active counts carry a second
    /// marker naming it, BESIDE the v2.0.6 one rather than instead of it (plan §3).
    /// </summary>
    internal static string SwitchOnVersion
    {
        get
        {
            using var doc = JsonDocument.Parse(Read("src", "EQBuddy.Core", "Data", "WhatsNew.json"));
            var versions = doc.RootElement.EnumerateArray()
                .Where(e => e.GetProperty("highlights").EnumerateArray()
                    .Any(h => h.GetString()!.StartsWith("TELEMETRY IS NOW ON FOR EVERY INSTALL", StringComparison.Ordinal)))
                .Select(e => e.GetProperty("version").GetString()!)
                .ToList();
            Assert.Single(versions);
            return versions[0];
        }
    }

    /// <summary>The second marker's two halves: the version telemetry went on for every install
    /// from, and that from then every install is counted unless it was turned off.</summary>
    internal static IReadOnlyList<string> SwitchOnViolations(string flat, string version)
    {
        var bad = new List<string>();
        if (!flat.Contains("on for every install from v" + version, StringComparison.OrdinalIgnoreCase))
            bad.Add($"no second cutover marker naming v{version}, the release that turned telemetry on for every install");
        if (!flat.Contains("counted unless it was turned off", StringComparison.OrdinalIgnoreCase))
            bad.Add("the second cutover marker does not say every install is counted unless it was turned off");
        return bad;
    }

    /// <summary>v2.0.6's promises, false of every build from <see cref="SwitchOnVersion"/> on:
    /// an earlier "Not now" stays off, the one-time notice and its buttons. Read with markdown
    /// emphasis removed, because SECURITY.md bolds the middle of one.</summary>
    private static readonly string[] PreSwitchOnClaims =
    [
        "earlier build stays off",
        "on an earlier build, it stays off",
        "never asked again",
        "does not ask again",
        "will not ask you again",
        "OK keeps it on",
        "Turn off is the only way",
        "never before the one-time notice",
        "Only an install that never answered",
    ];

    private static IEnumerable<string> PreSwitchOnViolations(string flat)
    {
        var plain = flat.Replace("*", "", StringComparison.Ordinal);
        return PreSwitchOnClaims.Where(c => plain.Contains(c, StringComparison.OrdinalIgnoreCase))
            .Select(c => $"still says \"{c}\" (v2.0.6's promise; every install is on since DRA-2010)");
    }

    /// <summary>The off that stays off, from the switch-on release on, said in the same words
    /// on every page.</summary>
    private static IEnumerable<string> FinalOffViolations(string flat)
    {
        if (!flat.Replace("*", "", StringComparison.Ordinal)
                .Contains("from v" + SwitchOnVersion + " on is final", StringComparison.OrdinalIgnoreCase))
            yield return $"does not say that turning it off from v{SwitchOnVersion} on is final";
    }

    // ------------------------------------------------------------------ README.md ----

    /// <summary>What README.md may not say, and what it must, once the heartbeat ships.</summary>
    internal static IReadOnlyList<string> ReadmeViolations(string readme)
    {
        var bad = new List<string>();
        var flat = Flat(readme);
        if (flat.Contains("never phones home", StringComparison.OrdinalIgnoreCase))
            bad.Add("claims \"never phones home\" (§8.1 removed it: an opted-in heartbeat is one)");
        if (flat.Contains("zero telemetry", StringComparison.OrdinalIgnoreCase))
            bad.Add("claims \"zero telemetry\" beside a build that can send a heartbeat");
        if (!flat.Contains("on by default", StringComparison.OrdinalIgnoreCase))
            bad.Add("does not say the heartbeat is on by default (DRA-1980)");
        bad.AddRange(FinalOffViolations(flat));
        bad.AddRange(OptInDefaultViolations(flat));
        bad.AddRange(PreSwitchOnViolations(flat));
        bad.AddRange(CutoverViolations(flat, CutoverVersion));
        bad.AddRange(SwitchOnViolations(flat, SwitchOnVersion));
        if (!flat.Contains("(docs/Telemetry.md", StringComparison.Ordinal))
            bad.Add("does not link the player page, docs/Telemetry.md");
        if (!flat.Contains("fetches, not people", StringComparison.OrdinalIgnoreCase))
            bad.Add("has no downloads row labelled as fetches, not people (TEL-005)");
        if (!flat.Contains(Uri.EscapeDataString("https://" + Host + "/metrics.json"), StringComparison.Ordinal))
            bad.Add("its metrics badges do not read the host the sender dials");

        // DRA-783 D2: the downloads and hours rows. A row is a table line whose badge reads
        // the key; its LABEL is the first cell, so "estimated" in the prose alone does not count.
        var rows = readme.Split('\n').Select(l => l.TrimEnd('\r'))
            .Where(l => l.StartsWith("| **", StringComparison.Ordinal)).ToList();
        var downloads = rows.FirstOrDefault(r => r.Contains("%24.downloads.", StringComparison.Ordinal));
        if (downloads is null || !downloads.Contains("September 28, 2026", StringComparison.Ordinal))
            bad.Add("the downloads row does not name 2.0's start date, September 28, 2026, beside it");
        var hours = rows.FirstOrDefault(r => r.Contains("%24.usageHours.", StringComparison.Ordinal));
        if (hours is null || !hours.Split('|')[1].Contains("estimated", StringComparison.OrdinalIgnoreCase))
            bad.Add("the hours row does not carry \"estimated\" in its label");
        if (Regex.IsMatch(readme, @"usageHours\.allTime(?!Rounded)"))
            bad.Add("a badge reads usageHours.allTime unrounded (a badge cannot round; read allTimeRounded)");
        return bad;
    }

    [Fact]
    public void TheReadmeTellsTheShippedTruth() =>
        Assert.Empty(ReadmeViolations(Read("README.md")));

    /// <summary>The committed negative: README's principle paragraph as it stood before the
    /// launch release is refused on every arm it breaks.</summary>
    [Fact]
    public void ThePreLaunchReadmeIsRefused()
    {
        const string before =
            "**Your own files, by principle. Zero telemetry, always contribution.** EQBuddy never\n"
            + "reads game memory, never phones home, and never measures other players.";
        var bad = ReadmeViolations(before);
        Assert.Contains(bad, v => v.Contains("never phones home", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("zero telemetry", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("on by default", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("cutover", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("docs/Telemetry.md", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("fetches, not people", StringComparison.Ordinal));
    }

    /// <summary>The committed negatives for DRA-783 D2's three arms, each run against the
    /// shipped README with exactly one thing taken away: the hours row's "estimated", the
    /// downloads row's 2.0 start date, and the rounded key a badge must read.</summary>
    [Fact]
    public void TheReadmeWithoutItsEstimateDateOrRoundingIsRefused()
    {
        var readme = Read("README.md");

        var unestimated = readme.Replace("estimated", "", StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ReadmeViolations(unestimated), v => v.Contains("\"estimated\"", StringComparison.Ordinal));

        var undated = readme.Replace("September 28, 2026", "launch", StringComparison.Ordinal);
        Assert.Contains(ReadmeViolations(undated), v => v.Contains("September 28, 2026", StringComparison.Ordinal));

        var unrounded = readme.Replace("usageHours.allTimeRounded", "usageHours.allTime", StringComparison.Ordinal);
        Assert.Contains(ReadmeViolations(unrounded), v => v.Contains("unrounded", StringComparison.Ordinal));
    }

    /// <summary>DRA-1980 D2's committed negatives: the README's v2.0.0–v2.0.5 principle line, a
    /// cutover marker naming the wrong release, and one that drops its opted-in half are each
    /// refused.</summary>
    [Fact]
    public void TheReadmeWithoutItsCutoverOrWithTheOptInDefaultIsRefused()
    {
        var readme = Read("README.md");

        var optIn = readme + "\nThe one exception is an opt-in heartbeat, **off until you say yes**.\n";
        Assert.Contains(ReadmeViolations(optIn), v => v.Contains("off until you say yes", StringComparison.Ordinal));

        var wrongRelease = readme.Replace("on by default from v" + CutoverVersion, "on by default from v9.9.9",
            StringComparison.Ordinal);
        Assert.NotEqual(readme, wrongRelease);
        Assert.Contains(ReadmeViolations(wrongRelease), v => v.Contains("cutover marker naming", StringComparison.Ordinal));

        var noOptedIn = readme.Replace("opted-in", "earlier", StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ReadmeViolations(noOptedIn), v => v.Contains("opted-in installs only", StringComparison.Ordinal));
    }

    /// <summary>The What's-new lookup finds the release D1 shipped in, so a marker that names
    /// it is naming the right one.</summary>
    [Fact]
    public void TheCutoverVersionIsTheReleaseThatTurnedTelemetryOn() =>
        Assert.Equal("2.0.6", CutoverVersion);

    /// <summary>DRA-2010 D2: the What's-new lookup finds the release D1 shipped in.</summary>
    [Fact]
    public void TheSwitchOnVersionIsTheReleaseThatTurnedEveryInstallOn() =>
        Assert.Equal("2.0.7", SwitchOnVersion);

    /// <summary>DRA-2010 D2's committed negatives for the README: v2.0.6's "Not now stays off"
    /// line, the page without its second marker, and without its final-off sentence, are each
    /// refused.</summary>
    [Fact]
    public void TheReadmeWithV206sPromiseOrWithoutItsSecondCutoverIsRefused()
    {
        var readme = Read("README.md");

        var v206 = readme + "\nAnyone who said \"Not now\" or turned it off on an earlier\nbuild stays off.\n";
        Assert.Contains(ReadmeViolations(v206), v => v.Contains("earlier build stays off", StringComparison.Ordinal));

        var noMarker = readme.Replace("on for every install from v" + SwitchOnVersion, "on for every install",
            StringComparison.Ordinal);
        Assert.NotEqual(readme, noMarker);
        Assert.Contains(ReadmeViolations(noMarker), v => v.Contains("second cutover marker naming", StringComparison.Ordinal));

        var noCounted = readme.Replace("counted unless it was turned off", "counted", StringComparison.Ordinal);
        Assert.NotEqual(readme, noCounted);
        Assert.Contains(ReadmeViolations(noCounted), v => v.Contains("unless it was turned off", StringComparison.Ordinal));

        var noFinal = readme.Replace("is final", "is kept", StringComparison.Ordinal);
        Assert.NotEqual(readme, noFinal);
        Assert.Contains(ReadmeViolations(noFinal), v => v.Contains("is final", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- SECURITY.md ----

    /// <summary>SECURITY.md's "complete list of hosts" has to hold the heartbeat's host, and the
    /// old "Zero telemetry" section must be gone.</summary>
    internal static IReadOnlyList<string> SecurityViolations(string security)
    {
        var bad = new List<string>();
        var flat = Flat(security);
        if (Regex.IsMatch(security, @"(?m)^##\s+Zero telemetry\s*$"))
            bad.Add("still carries the \"## Zero telemetry\" section");
        if (!Regex.IsMatch(security, @"(?m)^##\s+Telemetry: on by default, off with one switch\s*$"))
            bad.Add("has no \"## Telemetry: on by default, off with one switch\" section (§8.5, DRA-1980)");
        if (!flat.Contains("(#telemetry-on-by-default-off-with-one-switch)", StringComparison.Ordinal))
            bad.Add("the host table's link does not reach the telemetry section's anchor");
        bad.AddRange(FinalOffViolations(flat));
        if (!flat.Contains("Help improve EQBuddy", StringComparison.Ordinal))
            bad.Add("does not name Help improve EQBuddy, the one off (DRA-2010)");
        bad.AddRange(OptInDefaultViolations(flat));
        bad.AddRange(PreSwitchOnViolations(flat));
        if (!flat.Contains("| `" + Host + "` |", StringComparison.Ordinal))
            bad.Add($"the host table has no row for `{Host}`, the host the sender dials (§8.4)");
        if (!flat.Contains("1.x and the legacy builds never send anything", StringComparison.Ordinal))
            bad.Add("does not say 1.x and the legacy builds never send anything");
        if (!flat.Contains("(docs/Telemetry.md)", StringComparison.Ordinal))
            bad.Add("does not link the player page, docs/Telemetry.md");
        return bad;
    }

    [Fact]
    public void SecurityMdListsTheHeartbeatAndItsHost() =>
        Assert.Empty(SecurityViolations(Read("SECURITY.md")));

    /// <summary>The committed negative: the pre-launch section, and a host row that names a
    /// host the sender does not dial, are both refused.</summary>
    [Fact]
    public void ThePreLaunchSecurityPageAndADriftedHostAreRefused()
    {
        const string before =
            "## Zero telemetry\n\nThere is no analytics endpoint, no crash reporter, no usage ping.\n";
        var bad = SecurityViolations(before);
        Assert.Contains(bad, v => v.Contains("\"## Zero telemetry\"", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("host table", StringComparison.Ordinal));

        var drifted = Read("SECURITY.md").Replace("`" + Host + "`", "`telemetry.example.invalid`",
            StringComparison.Ordinal);
        Assert.Contains(SecurityViolations(drifted), v => v.Contains("host table", StringComparison.Ordinal));
    }

    // -------------------------------------------------------- docs/Telemetry.md ----

    /// <summary>The player page shows every key the client serializes, and names the host.
    /// A fourth key added to the payload (with its plan re-signed) reddens here until the
    /// player is told about it too.</summary>
    internal static IReadOnlyList<string> PlayerPageViolations(string page)
    {
        var bad = new List<string>();
        foreach (var key in TelemetryHeartbeat.PayloadKeys)
            if (!page.Contains("\"" + key + "\"", StringComparison.Ordinal))
                bad.Add($"the example heartbeat does not show \"{key}\"");
        if (!page.Contains("`" + Host + "`", StringComparison.Ordinal))
            bad.Add($"does not name `{Host}`, the host the sender dials");
        var flat = Flat(page);
        if (!flat.Contains("lower bound", StringComparison.OrdinalIgnoreCase))
            bad.Add("does not say the public numbers are a lower bound");
        if (flat.Contains("Esc and ✕ all say no", StringComparison.Ordinal))
            bad.Add("says Esc and ✕ decline; on the notice closing keeps telemetry on (DRA-1981)");
        bad.AddRange(FinalOffViolations(flat));
        bad.AddRange(OptInDefaultViolations(flat));
        bad.AddRange(PreSwitchOnViolations(flat));
        bad.AddRange(CutoverViolations(flat, CutoverVersion));
        bad.AddRange(SwitchOnViolations(flat, SwitchOnVersion));
        return bad;
    }

    [Fact]
    public void ThePlayerPageShowsEveryKeyTheClientSends() =>
        Assert.Empty(PlayerPageViolations(Read("docs", "Telemetry.md")));

    [Fact]
    public void APlayerPageMissingAKeyIsRefused()
    {
        var page = Read("docs", "Telemetry.md").Replace("\"os\"", "\"platform\"", StringComparison.Ordinal);
        Assert.Contains(PlayerPageViolations(page), v => v.Contains("\"os\"", StringComparison.Ordinal));
        Assert.NotEmpty(PlayerPageViolations("# Telemetry\n\nNothing here."));
    }

    /// <summary>DRA-1980 D2's committed negative: the v2.0.0–v2.0.5 "Being asked" section, where
    /// Esc and ✕ said no, is refused, and so is the page without its cutover.</summary>
    [Fact]
    public void ThePreCutoverPlayerPageIsRefused()
    {
        const string before =
            "It is **off on every install until you say yes.** The two buttons are the same size and "
            + "neither is the default. **Not now**, Esc and ✕ all say no.";
        var bad = PlayerPageViolations(before);
        Assert.Contains(bad, v => v.Contains("Esc and ✕ decline", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("off on every install until you", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("cutover", StringComparison.Ordinal));

        var page = Read("docs", "Telemetry.md");
        var noCutover = page.Replace("on by default from v" + CutoverVersion, "on by default", StringComparison.Ordinal);
        Assert.NotEqual(page, noCutover);
        Assert.Contains(PlayerPageViolations(noCutover), v => v.Contains("cutover marker naming", StringComparison.Ordinal));
    }

    /// <summary>DRA-1980 D2: SECURITY.md's pre-cutover section heading is refused.</summary>
    [Fact]
    public void ThePreCutoverSecuritySectionIsRefused()
    {
        var security = Read("SECURITY.md")
            .Replace("## Telemetry: on by default, off with one switch", "## Telemetry: off unless you turn it on",
                StringComparison.Ordinal);
        Assert.Contains(SecurityViolations(security), v => v.Contains("on by default, off with one switch", StringComparison.Ordinal));
    }

    /// <summary>DRA-2010 D2's committed negatives: the v2.0.6 telemetry paragraph on SECURITY.md
    /// and the v2.0.6 notice section on the player page (both verbatim, as v2.0.6 shipped them)
    /// are refused, and so is the player page without its second marker.</summary>
    [Fact]
    public void TheV206NoticeAndStaysOffWordingIsRefused()
    {
        const string security206 =
            "default**. Before anything is sent, EQBuddy shows a one-time notice naming what the heartbeat "
            + "carries: **OK** keeps it on, and so does closing the notice (Esc, ✕); **Turn off** is the only "
            + "way to stop it there. Anyone who said \"Not now\" or turned it off on an earlier build **stays "
            + "off** and is never asked again; Options is the way back in, either way.";
        var bad = SecurityViolations(security206);
        Assert.Contains(bad, v => v.Contains("OK keeps it on", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("Turn off is the only way", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("earlier build stays off", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("never asked again", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("is final", StringComparison.Ordinal));

        const string page206 =
            "**If you said \"Not now\", or turned it off, on an earlier build, it stays off.** EQBuddy does "
            + "not show you the notice and does not ask again; Options is the way back in if you ever want "
            + "it. Only an install that never answered the old question sees the new notice.";
        bad = PlayerPageViolations(page206);
        Assert.Contains(bad, v => v.Contains("on an earlier build, it stays off", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("does not ask again", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("Only an install that never answered", StringComparison.Ordinal));
        Assert.Contains(bad, v => v.Contains("second cutover", StringComparison.Ordinal));

        var page = Read("docs", "Telemetry.md");
        var noMarker = page.Replace("on for every install from v" + SwitchOnVersion, "on for every install",
            StringComparison.Ordinal);
        Assert.NotEqual(page, noMarker);
        Assert.Contains(PlayerPageViolations(noMarker), v => v.Contains("second cutover marker naming", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------- LEGACY-V1.md ----

    /// <summary>§8.5 / §10's tri-read: the legacy promise is not touched, at TEL-PR4 or ever,
    /// because 1.x never sends. If this sentence moves, somebody edited the one public line the
    /// plan says stays literally true.</summary>
    [Fact]
    public void TheLegacyPromiseStandsVerbatim() =>
        Assert.Contains("Nothing expires, phones home, or switches itself off.",
            Read("LEGACY-V1.md"), StringComparison.Ordinal);
}
