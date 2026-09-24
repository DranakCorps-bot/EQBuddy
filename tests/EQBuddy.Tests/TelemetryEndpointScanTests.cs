using System.Text.RegularExpressions;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **§9 guard 2: the telemetry endpoint lives in ONE file, and no second HttpClient reaches
/// it** (DRA-362 TEL-PR3). EQBuddy's first off-machine send must have exactly one door a
/// reader can audit: <c>Core/TelemetrySender.cs</c>. A copy of the endpoint in a second file
/// is a second sender waiting to happen — trap 47's two code paths deciding an off-machine
/// question, one of them outside the policy.
///
/// The endpoint is the host (<c>TelemetrySender.BaseUrl</c>, empty until DRA-369 deploys the
/// backend) AND the two paths, so the scan has something to hold while the host is unset.
/// Paired with must-lists (trap 34): the ONE caller of the sender, and the HttpClient
/// constructors the product is allowed to have.
/// </summary>
public class TelemetryEndpointScanTests
{
    private static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    private const string SenderFile = "TelemetrySender.cs";

    private static readonly Lazy<Dictionary<string, string>> Sources = new(() => Directory
        .EnumerateFiles(Src, "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
        .ToDictionary(f => f, File.ReadAllText));

    private static List<string> FilesMatching(string pattern) => Sources.Value
        .Where(kv => Regex.IsMatch(kv.Value, pattern))
        .Select(kv => Path.GetFileName(kv.Key))
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToList();

    /// <summary>The endpoint's literals, read out of the sender itself.</summary>
    public static TheoryData<string> EndpointLiterals()
    {
        var sender = File.ReadAllText(Directory
            .EnumerateFiles(Src, SenderFile, SearchOption.AllDirectories).Single());
        var data = new TheoryData<string> { "\"/heartbeat\"", "\"/delete\"" };
        var host = Regex.Match(sender, @"const string BaseUrl = ""([^""]*)""");
        Assert.True(host.Success, "TelemetrySender.BaseUrl not found — renamed?");
        if (host.Groups[1].Value.Length > 0) data.Add(host.Groups[1].Value);
        return data;
    }

    [Theory]
    [MemberData(nameof(EndpointLiterals))]
    public void TheEndpointLiteralAppearsInExactlyOneSourceFile(string literal) =>
        Assert.Equal([SenderFile], FilesMatching(Regex.Escape(literal)));

    /// <summary>The ONE caller of the sender, as a must-list: the runtime, which asks the
    /// policy before every send. An empty answer would mean the scan is aimed at nothing.</summary>
    [Fact]
    public void OnlyTheRuntimeCallsTheSender() =>
        Assert.Equal(["TelemetryRuntime.cs"], FilesMatching(@"TelemetrySender\.Post\w*Async\("));

    /// <summary>
    /// Every place the product constructs an <c>HttpClient</c>, curated: the wiki reader, the
    /// updater, and the telemetry sender (one each). A new one anywhere is a new network door
    /// and has to be added here on purpose — which is also what makes "no second HttpClient
    /// reaches the endpoint" something a diff shows.
    /// </summary>
    [Fact]
    public void TheHttpClientsTheProductBuildsAreTheKnownOnes()
    {
        var constructed = Sources.Value
            .SelectMany(kv => Regex.Matches(kv.Value,
                    @"new\s+HttpClient\b|HttpClient\s+\w+\s*=\s*new\s*\(")
                .Select(_ => Path.GetFileName(kv.Key)))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["EqlWikiText.cs", SenderFile, "UpdateChecker.cs"], constructed);
    }

    [Fact]
    public void TheSenderSetsNoIdentifyingHeader()
    {
        var sender = Sources.Value.Single(kv => Path.GetFileName(kv.Key) == SenderFile).Value;
        Assert.DoesNotContain("DefaultRequestHeaders", sender);
        Assert.DoesNotContain("UserAgent", sender);
        Assert.DoesNotContain("Cookie", sender);
    }
}
