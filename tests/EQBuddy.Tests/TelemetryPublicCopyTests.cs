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
        if (!flat.Contains("off until you say yes", StringComparison.OrdinalIgnoreCase))
            bad.Add("does not say the heartbeat is off until the player says yes");
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
        Assert.Contains(bad, v => v.Contains("off until", StringComparison.Ordinal));
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

    // ---------------------------------------------------------------- SECURITY.md ----

    /// <summary>SECURITY.md's "complete list of hosts" has to hold the heartbeat's host, and the
    /// old "Zero telemetry" section must be gone.</summary>
    internal static IReadOnlyList<string> SecurityViolations(string security)
    {
        var bad = new List<string>();
        var flat = Flat(security);
        if (Regex.IsMatch(security, @"(?m)^##\s+Zero telemetry\s*$"))
            bad.Add("still carries the \"## Zero telemetry\" section");
        if (!Regex.IsMatch(security, @"(?m)^##\s+Telemetry: off unless you turn it on\s*$"))
            bad.Add("has no \"## Telemetry: off unless you turn it on\" section (§8.5)");
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
        if (!Flat(page).Contains("lower bound", StringComparison.OrdinalIgnoreCase))
            bad.Add("does not say the public numbers are a lower bound");
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

    // ------------------------------------------------------------- LEGACY-V1.md ----

    /// <summary>§8.5 / §10's tri-read: the legacy promise is not touched, at TEL-PR4 or ever,
    /// because 1.x never sends. If this sentence moves, somebody edited the one public line the
    /// plan says stays literally true.</summary>
    [Fact]
    public void TheLegacyPromiseStandsVerbatim() =>
        Assert.Contains("Nothing expires, phones home, or switches itself off.",
            Read("LEGACY-V1.md"), StringComparison.Ordinal);
}
