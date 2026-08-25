using System.Text.RegularExpressions;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// Which pop-out windows the player can resize, and — the load-bearing half — WHY each
/// exclusion is an exclusion.
///
/// David, 2026-08-25: *"please work on allowing all the pop out windows to be resized."*
/// "All" turned out to have three real exceptions, and without this list the next reader
/// finds a window that does not resize and cannot tell a decision from an oversight. Same
/// curated-must-list shape as `DeadSettingTests.Known` and
/// `GameCommandsTests.SurfacesNeedingACommand` (trap 34): a rule with no enumerated
/// exceptions grows silent ones.
///
/// **The deciding question is where the window's SIZE comes from**, not whether resizing
/// would be nice:
/// - Nothing else owns it, and the content is complete at first render → `AllowResize`,
///   which gives minimum sizes and a remembered size.
/// - Something else already owns it → leave it alone. Two writers for one value is trap 4,
///   and `WindowZoom` says so about Width in its own doc comment.
/// - The content arrives AFTER first render → excluded until the window-height V2 fix,
///   because `AllowResize` pins the height at `ContentRendered` and would freeze the
///   window at its "loading" size. That is a REGRESSION, not a missing feature.
/// </summary>
public class ResizableWindowTests
{
    private static string Repo =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static string Wpf(string file) =>
        File.ReadAllText(Path.Combine(Repo, "src", "EQBuddy", file));

    /// <summary>Windows the player may drag, and where their size is kept.</summary>
    public static TheoryData<string, string> Resizable() => new()
    {
        // file, how the size is owned
        { "ProgressWindow.xaml.cs",      "AllowResize" },
        { "CreatureWindow.xaml.cs",      "AllowResize" },
        { "GearLootWindow.xaml.cs",      "AllowResize" },
        { "QuestsWindow.xaml.cs",        "AllowResize" },
        { "SpawnsWindow.xaml.cs",        "AllowResize" },
        { "TravelWindow.cs",             "AllowResize" },
        { "HistoryWindow.xaml.cs",       "AllowResize" },
        { "FightTimelineWindow.xaml.cs", "AllowResize" },
        // Resizable, size deliberately not kept — see the reasons in each file.
        { "ZoneShareWindow.cs",          "CanResize" },
        { "SessionPickerWindow.cs",      "CanResize" },
        // Keeps its own saved size (restores savedW/savedH, Closed += SavePosition).
        { "BreakoutWindow.xaml.cs",      "CanResize" },
        // Resizable by WPF's DEFAULT — they name no ResizeMode at all. Size is not kept:
        // both fetch from eqlwiki after the window is up, so AllowResize's ContentRendered
        // pin would freeze them at the size of an empty lookup. They ride the window-height
        // V2 fix, at which point they can hold a remembered size honestly.
        { "WikiPackWindow.xaml.cs",      "Default" },
        { "MapWindow.cs",                "Default" },
    };

    [Theory]
    [MemberData(nameof(Resizable))]
    public void EveryResizableWindowSaysSo(string file, string mechanism)
    {
        var source = Wpf(file);
        var xaml = file.EndsWith(".xaml.cs", StringComparison.Ordinal)
            ? Wpf(file[..^3])   // "Foo.xaml.cs" -> "Foo.xaml"
            : "";
        var both = source + xaml;

        Assert.DoesNotMatch(@"ResizeMode\s*=\s*""?(?:ResizeMode\.)?NoResize", both);
        if (mechanism == "AllowResize")
            Assert.Contains("WindowZoom.AllowResize", source);
        // "Default" means it relies on WPF's own CanResize. Saying so explicitly would be
        // harmless, but claiming Default while NAMING a mode hides which one it picked.
        if (mechanism == "Default")
            Assert.DoesNotMatch(@"ResizeMode\s*=", both);
    }

    /// <summary>
    /// The exclusions, each with the reason it is one. **A window may only appear here if
    /// its reason is a mechanism, not a preference** — "we did not get to it" is not an
    /// entry, it is a TODO.
    /// </summary>
    public static TheoryData<string, string> NotResizable() => new()
    {
        { "OptionsWindow.xaml",
            "Two owners. It has its own width thumb writing AppSettings.OptionsWidth "
            + "(OnResizeCompleted), and its height is SizeToContent clamped to the work area "
            + "and CHANGES PER TAB — the Alerts tab is ~300px taller than Look. AllowResize "
            + "would pin it to whichever tab opened first and clip the rest." },
        { "ItemInfoWindow.xaml",
            "Content arrives after first render — it fetches the item from eqlwiki (async) "
            + "and grows when the reply lands. Pinning at ContentRendered would freeze it at "
            + "the size of an empty lookup. Rides the window-height V2 fix." },
    };

    [Theory]
    [MemberData(nameof(NotResizable))]
    public void EveryNonResizableWindowIsOnTheListWithAReason(string file, string reason)
    {
        Assert.False(string.IsNullOrWhiteSpace(reason), $"{file}: an exclusion needs a reason");
        var xaml = Wpf(file);

        // ItemInfo and WikiPack are excluded because they fetch; if that stops being true the
        // reason is stale and the window should be resizable. OptionsWindow's reason is
        // structural and has no such tell.
        if (file is "ItemInfoWindow.xaml" or "WikiPackWindow.xaml")
        {
            var code = Wpf(file + ".cs");
            Assert.True(code.Contains("await") || code.Contains("async"),
                $"{file} no longer fetches asynchronously — its exclusion reason is stale, "
                + "so it should now be resizable. Move it to Resizable().");
        }

        Assert.Matches(@"ResizeMode\s*=\s*""?(?:ResizeMode\.)?NoResize", xaml);
    }

    /// <summary>
    /// The negative that stops the pair above going vacuous (trap 39): every window naming
    /// a ResizeMode must be in exactly one of the two lists. A new pop-out that resizes —
    /// or refuses to — cannot slip in unlisted, which is how "all of them" quietly stopped
    /// being all of them the first time.
    /// </summary>
    [Fact]
    public void NoWindowDecidesItsResizeModeWithoutBeingListed()
    {
        var listed = Resizable().Select(r => Path.GetFileNameWithoutExtension((string)r[0]!))
            .Concat(NotResizable().Select(r => Path.GetFileNameWithoutExtension((string)r[0]!)))
            .Select(n => n.EndsWith(".xaml", StringComparison.Ordinal) ? n[..^5] : n)
            .ToHashSet(StringComparer.Ordinal);

        // Overlays and chips are not pop-outs: they are click-through furniture over the
        // game and resizing them is meaningless. Named rather than pattern-matched so a new
        // one has to be thought about.
        var furniture = new HashSet<string>(StringComparer.Ordinal)
        {
            "AlertWindow", "ClickThroughChip", "CursorRingWindow", "GridOverlayWindow",
            "MezChipsWindow", "SpawnChipsWindow", "MainWindow", "TutorialWindow",
            "WhatsNewWindow", "FeedbackWindow", "CompanionWindow", "TextProbeWindow",
            // Not a window: the helper that ASSIGNS ResizeMode for the ones above.
            "WindowZoom",
        };

        var unlisted = Directory
            .EnumerateFiles(Path.Combine(Repo, "src", "EQBuddy"), "*Window*.cs")
            .Concat(Directory.EnumerateFiles(Path.Combine(Repo, "src", "EQBuddy"), "*.xaml"))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(p => Regex.IsMatch(File.ReadAllText(p), @"ResizeMode\s*=\s*""?(?:ResizeMode\.)?(?:No|Can)Resize"))
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .Select(n => n.EndsWith(".xaml", StringComparison.Ordinal) ? n[..^5] : n)
            .Distinct(StringComparer.Ordinal)
            .Where(n => !listed.Contains(n) && !furniture.Contains(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(unlisted.Count == 0,
            "These windows set a ResizeMode but appear in neither list. Add each to "
            + "Resizable() or to NotResizable() with the mechanism that excludes it:"
            + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", unlisted));
    }
}
