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
        TelemetryCopy.NoteHeading, TelemetryCopy.NoteBody, TelemetryCopy.NotePurpose,
        TelemetryCopy.NoteReturning, TelemetryCopy.NoteWhereOff, TelemetryCopy.NoteLearnMore,
        TelemetryCopy.NoteOpenOptions, TelemetryCopy.NoteDismiss,
        TelemetryCopy.ToggleLabel, TelemetryCopy.ToggleCaption, TelemetryCopy.OffHeading, TelemetryCopy.OffLead,
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

    /// <summary>§8.3.1 row 7, carried over: the note names the real path of the day, and it
    /// is the toggle's own label.</summary>
    [Fact]
    public void TheNoteNamesTheRuledPathToTheToggle() =>
        Assert.Contains("Options → Behavior → " + TelemetryCopy.ToggleLabel + ".",
            TelemetryCopy.NoteWhereOff, StringComparison.Ordinal);

    /// <summary>The short note still carries the entire payload (TEL-001): one plain name per
    /// key <see cref="TelemetryHeartbeat.PayloadKeys"/> sends, and no more keys than that. A
    /// fourth field reddens here as well as in <c>TelemetryHeartbeatTests</c>.</summary>
    [Fact]
    public void TheNoteNamesEveryPayloadField()
    {
        Assert.Equal(["installId", "appVersion", "os"], TelemetryHeartbeat.PayloadKeys);
        Assert.Contains("a random id", TelemetryCopy.NoteBody, StringComparison.Ordinal);
        Assert.Contains("the app version", TelemetryCopy.NoteBody, StringComparison.Ordinal);
        Assert.Contains("your Windows version", TelemetryCopy.NoteBody, StringComparison.Ordinal);
    }

    /// <summary>DRA-385 (Founder, 2026-09-24): the long prompt went unread, and its rule carries
    /// to the note that replaced it (DRA-2010). A regrowth past these reddens and has to argue
    /// for itself.</summary>
    [Fact]
    public void TheNoteStaysShort()
    {
        Assert.True(TelemetryCopy.NoteBody.Length <= 160, TelemetryCopy.NoteBody);
        Assert.True(TelemetryCopy.NotePurpose.Length <= 100, TelemetryCopy.NotePurpose);
        Assert.True(TelemetryCopy.NoteReturning.Length <= 160, TelemetryCopy.NoteReturning);
        Assert.True(TelemetryCopy.NoteWhereOff.Length <= 100, TelemetryCopy.NoteWhereOff);
    }

    /// <summary>
    /// **The cohort sentence is drawn only for a switch-on of a profile that had ANSWERED**
    /// (DRA-2010 §1 protection 2): a past "Not now", a v2.0.6 off or a delete — read off
    /// <c>TelemetryPromptShown</c>. Committed negatives: a never-answered install, and one that
    /// was already on (its flag is true from the yes). Mutants that redden it: gating on the flag
    /// alone (the already-on row); dropping the gate (the never-answered row).
    /// </summary>
    [Theory]
    [InlineData(TelemetryHeartbeat.SwitchOnDecision.SwitchedOn, true, true, TelemetryHeartbeat.NoteKind.Returning)]
    [InlineData(TelemetryHeartbeat.SwitchOnDecision.SwitchedOn, false, true, TelemetryHeartbeat.NoteKind.Generic)]
    [InlineData(TelemetryHeartbeat.SwitchOnDecision.AlreadyOn, true, false, TelemetryHeartbeat.NoteKind.Generic)]
    [InlineData(TelemetryHeartbeat.SwitchOnDecision.AlreadyApplied, true, true, TelemetryHeartbeat.NoteKind.Returning)]
    [InlineData(TelemetryHeartbeat.SwitchOnDecision.AlreadyApplied, false, true, TelemetryHeartbeat.NoteKind.Generic)]
    [InlineData(TelemetryHeartbeat.SwitchOnDecision.AlreadyApplied, true, false, TelemetryHeartbeat.NoteKind.None)]
    [InlineData(TelemetryHeartbeat.SwitchOnDecision.NotTheProductProfile, true, true, TelemetryHeartbeat.NoteKind.None)]
    [InlineData(TelemetryHeartbeat.SwitchOnDecision.NoEndpoint, true, true, TelemetryHeartbeat.NoteKind.None)]
    public void TheCohortSentenceIsGatedOnAPriorAnswer(TelemetryHeartbeat.SwitchOnDecision decision,
        bool promptShown, bool held, TelemetryHeartbeat.NoteKind expected)
    {
        var s = new EQBuddy.Core.AppSettings
        {
            TelemetryPromptShown = promptShown, TelemetryEnabled = true,
            TelemetryInstallId = TelemetryHeartbeat.MintInstallId(), TelemetryFirstBeatHeld = held,
        };
        Assert.Equal(expected, TelemetryHeartbeat.NoteFor(decision, s));
    }

    /// <summary>
    /// **The cohort sentence says THIS version chose, never that the last one failed**
    /// (Challenger condition DRA-2011): v2.0.6 saved the player's off correctly, and a sentence
    /// blaming a save would be false. Read over every drawn string, with a committed negative
    /// proving the scan fires.
    /// </summary>
    [Fact]
    public void TheCohortSentenceSaysThisVersionChoseAndNeverThatASaveFailed()
    {
        Assert.StartsWith("This version turns it back on", TelemetryCopy.NoteReturning, StringComparison.Ordinal);
        Assert.Contains("Not now", TelemetryCopy.NoteReturning, StringComparison.Ordinal);
        Assert.Contains("stays off", TelemetryCopy.NoteReturning, StringComparison.Ordinal);
        Assert.Empty(SaveBlame(AllDrawnText()));
        Assert.NotEmpty(SaveBlame("Your choice could not save on the last version."));
    }

    private static readonly string[] SaveBlamePhrases =
    [
        "could not save", "couldn't save", "failed to save", "did not save", "didn't save",
        "was not saved", "wasn't saved", "not kept", "was lost", "got lost", "a bug",
    ];

    private static List<string> SaveBlame(string text) =>
        SaveBlamePhrases.Where(p => text.Contains(p, StringComparison.OrdinalIgnoreCase)).ToList();

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
            Normalize(TelemetryCopy.NoteBody.Replace("random", "secret")), StringComparison.Ordinal));

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
        // DRA-1980 retired the question; its decline label must not come back as a button. The
        // DRA-2010 cohort sentence QUOTES it, so the rule is that no string IS the label.
        Assert.DoesNotContain(typeof(TelemetryCopy).GetFields().Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!), l => l.Trim().Equals("Not now", StringComparison.OrdinalIgnoreCase));
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
    /// §8.3.1 row 6 (TEL-PR4, DRA-363): the note's one link opens the PLAYER page, and that
    /// page is in the repo the link names. A link to a page nobody committed is a promise with
    /// nothing behind it, and a player who clicks "how to delete it" gets a 404.
    /// </summary>
    [Fact]
    public void TheNoteLinksThePlayerPageAndThePageExists()
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
