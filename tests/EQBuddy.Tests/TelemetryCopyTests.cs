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
        TelemetryCopy.PromptTitle, TelemetryCopy.PromptBody, TelemetryCopy.PromptCloseKeepsOn,
        TelemetryCopy.PromptChangeLater,
        TelemetryCopy.PromptLearnMore, TelemetryCopy.PromptTurnOff, TelemetryCopy.PromptOk,
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
        foreach (var (name, text) in TelemetryCopy.OffFields) OnPage($"{name} {text}");
        // The page's ON example shows the id prefix 3a71c04b; the row is otherwise fixed.
        foreach (var (name, text) in TelemetryCopy.OnFields("3a71c04b")) OnPage($"{name} {text}");
        foreach (var item in TelemetryCopy.DeleteItems) OnPage(item);
    }

    /// <summary>§8.3.1 row 7: the prompt names the real path of the day, and it is the
    /// toggle's own label.</summary>
    [Fact]
    public void ThePromptNamesTheRuledPathToTheToggle() =>
        Assert.EndsWith("Options → Behavior → " + TelemetryCopy.ToggleLabel + ".",
            TelemetryCopy.PromptChangeLater, StringComparison.Ordinal);

    /// <summary>The short prompt still carries the entire payload (TEL-001): one plain name
    /// per key <see cref="TelemetryHeartbeat.PayloadKeys"/> sends, and no more keys than
    /// that. A fourth field reddens here as well as in <c>TelemetryHeartbeatTests</c>.</summary>
    [Fact]
    public void TheShortPromptNamesEveryPayloadField()
    {
        Assert.Equal(["installId", "appVersion", "os"], TelemetryHeartbeat.PayloadKeys);
        Assert.Contains("a random id", TelemetryCopy.PromptBody, StringComparison.Ordinal);
        Assert.Contains("the app version", TelemetryCopy.PromptBody, StringComparison.Ordinal);
        Assert.Contains("your Windows version", TelemetryCopy.PromptBody, StringComparison.Ordinal);
    }

    /// <summary>DRA-385 (Founder, 2026-09-24): the long prompt went unread. The notice is a
    /// title and three short lines; a regrowth past this reddens and has to argue for itself.
    /// The two buttons are OK and Turn off (DRA-1980).</summary>
    [Fact]
    public void ThePromptStaysShort()
    {
        Assert.True(TelemetryCopy.PromptBody.Length <= 160, TelemetryCopy.PromptBody);
        Assert.True(TelemetryCopy.PromptCloseKeepsOn.Length <= 90, TelemetryCopy.PromptCloseKeepsOn);
        Assert.True(TelemetryCopy.PromptChangeLater.Length <= 90, TelemetryCopy.PromptChangeLater);
        Assert.Equal("OK", TelemetryCopy.PromptOk);
        Assert.Equal("Turn off", TelemetryCopy.PromptTurnOff);
    }

    /// <summary>
    /// **The Challenger condition (DRA-1981), in the words:** the notice says that closing it
    /// leaves telemetry on, and names its own Turn off button as the only way to stop it — so
    /// a close is an answer the player was told about before giving it. Mutant that reddens it:
    /// a notice that only says "you can change this later".
    /// </summary>
    [Fact]
    public void TheNoticeSaysClosingLeavesItOnAndTurnOffIsTheOnlyOff()
    {
        Assert.Contains("Closing this leaves it on", TelemetryCopy.PromptCloseKeepsOn, StringComparison.Ordinal);
        Assert.Contains(TelemetryCopy.PromptTurnOff + " is the only way", TelemetryCopy.PromptCloseKeepsOn,
            StringComparison.Ordinal);
        Assert.Contains("turn it off", TelemetryCopy.PromptChangeLater, StringComparison.Ordinal);
    }

    /// <summary>
    /// **No drawn word says "anonymous"** (plan §2): the copy lists what is sent instead,
    /// because the id IS kept in stored heartbeats (C-1 row 1). Read over every literal AND
    /// every list the class draws, so a field row cannot carry it either.
    /// </summary>
    [Fact]
    public void NoDrawnStringCallsItAnonymous()
    {
        var all = AllDrawnText();
        Assert.NotEmpty(all);
        Assert.DoesNotContain("anonym", all, StringComparison.OrdinalIgnoreCase);
        // The committed negative: the scan reads the lists, not only the constants.
        Assert.Contains("a random number we create", all, StringComparison.Ordinal);
    }

    private static string AllDrawnText()
    {
        var parts = new List<string>();
        foreach (var f in typeof(TelemetryCopy).GetFields())
        {
            switch (f.GetValue(null))
            {
                case string text: parts.Add(text); break;
                case IEnumerable<(string, string)> rows: parts.AddRange(rows.Select(r => r.Item1 + " " + r.Item2)); break;
                case IEnumerable<string> items: parts.AddRange(items); break;
            }
        }
        parts.AddRange(TelemetryCopy.OnFields("3a71c04b").Select(r => r.Name + " " + r.Text));
        return string.Join('\n', parts);
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
            Normalize(TelemetryCopy.PromptBody.Replace("random", "secret")), StringComparison.Ordinal));

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
        // DRA-1980 retired the question; its decline label must not come back as a button.
        Assert.DoesNotContain("Not now", all, StringComparison.Ordinal);
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
