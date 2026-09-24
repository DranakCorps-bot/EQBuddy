using System.Text.RegularExpressions;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// **The words the player reads are the words Helm signed.** Every string
/// <see cref="TelemetryCopy"/> draws is read back out of <c>docs/v2/telemetry.md</c> §8.3 —
/// TEL-A's copy as C-1 / Helm ruled it — so an edit to either side alone reddens the build.
/// These are promises about what leaves a player's machine; a drift between the page and the
/// window is a promise nobody made.
///
/// The page's markdown is layout, not copy: emphasis, backticks, the quote prefix and the
/// "(C-1 / Helm, row N)" attributions are stripped before comparing, and nothing else is.
/// </summary>
public class TelemetryCopyTests
{
    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly Lazy<string> PageText = new(() =>
    {
        var text = File.ReadAllText(Path.Combine(Repo, "docs", "v2", "telemetry.md"));
        var start = text.IndexOf("### §8.3 ", StringComparison.Ordinal);
        var end = text.IndexOf("### §8.4 ", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "§8.3 not found in docs/v2/telemetry.md");
        return Normalize(text[start..end]);
    });

    private static string Normalize(string s)
    {
        s = Regex.Replace(s, @"\s*\*\(C-1 / Helm, row \d+\)\*", "");
        s = Regex.Replace(s, @"(?m)^>\s?", "");
        s = s.Replace("**", "").Replace("*", "").Replace("`", "");
        return Regex.Replace(s, @"\s+", " ");
    }

    private static void OnPage(string copy) =>
        Assert.True(PageText.Value.Contains(Normalize(copy), StringComparison.Ordinal),
            $"Not verbatim in docs/v2/telemetry.md §8.3: \"{copy}\"");

    public static TheoryData<string> PlainStrings() =>
    [
        TelemetryCopy.PromptTitle, TelemetryCopy.PromptLead, TelemetryCopy.PromptListLead,
        TelemetryCopy.PromptRetention, TelemetryCopy.PromptOffLater, TelemetryCopy.PromptOptional,
        TelemetryCopy.PromptLinkLead, TelemetryCopy.PromptDecline, TelemetryCopy.PromptAccept,
        TelemetryCopy.ToggleLabel, TelemetryCopy.OffHeading, TelemetryCopy.OffLead,
        TelemetryCopy.OffRetention, TelemetryCopy.OnRetention,
        TelemetryCopy.OnHeading + " " + TelemetryCopy.OnLead,
        TelemetryCopy.OffDoesLabel + " " + TelemetryCopy.OffDoes,
        TelemetryCopy.OffDoesNot,
        TelemetryCopy.DeleteButton, TelemetryCopy.DeleteDisabledTip,
        TelemetryCopy.DeleteTitle, TelemetryCopy.DeleteLead, TelemetryCopy.DeleteKeeps,
        TelemetryCopy.DeleteAfter, TelemetryCopy.DeleteCancel, TelemetryCopy.DeleteConfirm,
        TelemetryCopy.Deleted,
        TelemetryCopy.StatusNoneYet, TelemetryCopy.StatusSendFailed,
        TelemetryCopy.StatusDeleteFailed,
        TelemetryCopy.StatusLastPrefix + "4 min ago",
    ];

    [Theory]
    [MemberData(nameof(PlainStrings))]
    public void EveryDrawnStringIsVerbatimOnTheSignedPage(string copy) => OnPage(copy);

    [Fact]
    public void TheListRowsAreVerbatim()
    {
        foreach (var (name, text) in TelemetryCopy.PromptFields) OnPage($"{name} — {text}");
        foreach (var (name, text) in TelemetryCopy.OffFields) OnPage($"{name} {text}");
        // The page's ON example shows the id prefix 3a71c04b; the row is otherwise fixed.
        foreach (var (name, text) in TelemetryCopy.OnFields("3a71c04b")) OnPage($"{name} {text}");
        foreach (var item in TelemetryCopy.DeleteItems) OnPage(item);
    }

    /// <summary>§8.3.1 row 7: the footnote names the real path of the day. The only edit to
    /// Bevel's sentence is the inserted "Behavior →", and the ruling that allows it is on the
    /// page too.</summary>
    [Fact]
    public void TheFootnoteIsTheRuledPathFill()
    {
        OnPage(TelemetryCopy.PromptFootnote.Replace("Options → Behavior → ", "Options → "));
        OnPage("Options → Behavior → Help improve EQBuddy");
    }

    /// <summary>The one piece of §B not drawn is Bevel's parenthetical to the implementer; the
    /// label is otherwise the page's. Pinned so a later reader sees it was a decision.</summary>
    [Fact]
    public void TheOffDoesNotLabelDropsOnlyTheImplementerNote()
    {
        OnPage(TelemetryCopy.OffDoesNotLabel.TrimEnd(':')
            + " (say so, don't let it be a surprise): " + TelemetryCopy.OffDoesNot);
    }

    /// <summary>A committed negative: the comparison is not so loose that anything passes.</summary>
    [Fact]
    public void TheComparisonRefusesAWordChange() =>
        Assert.False(PageText.Value.Contains(
            Normalize(TelemetryCopy.PromptOptional.Replace("fully", "better")), StringComparison.Ordinal));

    /// <summary>The C-1 FALSE sentences never ship (§8.3.1 rows 1–3, 5).</summary>
    [Fact]
    public void NoneOfTheRuledFalseSentencesIsDrawn()
    {
        var all = string.Join("\n", typeof(TelemetryCopy)
            .GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!));
        Assert.DoesNotContain("we do not see it", all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nothing has been sent from this computer", all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("this computer never sent anything", all, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No, thanks", all, StringComparison.Ordinal);
        Assert.DoesNotContain("retrying", all, StringComparison.Ordinal);
    }

    private const string BlobMain = "https://github.com/DranakCorps-bot/EQBuddy/blob/main/";

    /// <summary>The repo file a <c>blob/main</c> link opens, or null when it names none.</summary>
    internal static string? RepoFileFor(string url)
    {
        if (!url.StartsWith(BlobMain, StringComparison.Ordinal)) return null;
        var path = Path.Combine(Repo, url[BlobMain.Length..].Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// §8.3.1 row 6 (TEL-PR4, DRA-363): the prompt's one link opens the PLAYER page, and that
    /// page is in the repo the link names. A link to a page nobody committed is a promise with
    /// nothing behind it, and a player who clicks "how to delete it" gets a 404.
    /// </summary>
    [Fact]
    public void ThePromptLinksThePlayerPageAndThePageExists()
    {
        var file = RepoFileFor(TelemetryCopy.PlayerPageUrl);
        Assert.NotNull(file);
        Assert.Equal(Path.Combine(Repo, "docs", "Telemetry.md"), file);
    }

    /// <summary>A committed negative: the resolver refuses a page that is not there.</summary>
    [Fact]
    public void TheLinkResolverRefusesAMissingPage()
    {
        Assert.Null(RepoFileFor(BlobMain + "docs/NoSuchTelemetryPage.md"));
        Assert.Null(RepoFileFor("https://example.com/docs/Telemetry.md"));
    }

    /// <summary>
    /// Every public page that names where heartbeats go names the host the sender actually
    /// dials (TEL-PR4). The host moved once already (DRA-369 set it); a redeploy that moves it
    /// again must move SECURITY.md's host list, the player page and the README's metrics
    /// badges in the same change, or SECURITY.md's "complete list of hosts" is false.
    /// </summary>
    [Fact]
    public void EveryPublicPageNamesTheHostTheSenderDials()
    {
        var host = new Uri(EQBuddy.Core.TelemetrySender.BaseUrl).Host;
        Assert.False(string.IsNullOrEmpty(host));
        foreach (var page in new[] { "SECURITY.md", Path.Combine("docs", "Telemetry.md") })
            Assert.Contains("`" + host + "`", File.ReadAllText(Path.Combine(Repo, page)), StringComparison.Ordinal);
        var readme = File.ReadAllText(Path.Combine(Repo, "README.md"));
        Assert.Contains(Uri.EscapeDataString("https://" + host + "/metrics.json"), readme, StringComparison.Ordinal);
    }
}
