using System.Reflection;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The guardrails that keep the codebase shaped the way the docs claim it is:
/// Core and UI.Shared stay UI-toolkit-free (they're the seam the Avalonia app and
/// every port builds on), and the known god-files stop growing.
/// </summary>
public class ArchitectureTests
{
    // ---- layer purity ----

    /// <summary>Assembly names no Core/UI.Shared code may pull in. WPF and
    /// Avalonia types belong to the two UI projects; the moment one leaks into a
    /// shared layer, the Linux build and every downstream port breaks quietly.</summary>
    private static readonly string[] ForbiddenUiAssemblies =
    [
        "PresentationCore", "PresentationFramework", "WindowsBase",
        "System.Xaml", "System.Windows.Forms", "Avalonia",
    ];

    public static TheoryData<Assembly> SharedAssemblies => new()
    {
        typeof(EQBuddy.Core.LogParser).Assembly,        // EQBuddy.Core
        typeof(EQBuddy.UI.Shared.GameCommands).Assembly, // EQBuddy.UI.Shared
        // The companion server must stay hostable from the Avalonia lane too —
        // a WPF type leaking in here would quietly kill that.
        typeof(EQBuddy.Companion.CompanionServer).Assembly, // EQBuddy.Companion
    };

    [Theory]
    [MemberData(nameof(SharedAssemblies))]
    public void SharedLayersReferenceNoUiToolkit(Assembly assembly)
    {
        var offending = assembly.GetReferencedAssemblies()
            .Where(r => ForbiddenUiAssemblies.Any(f =>
                r.Name is { } n &&
                (n.Equals(f, StringComparison.OrdinalIgnoreCase) ||
                 n.StartsWith(f + ".", StringComparison.OrdinalIgnoreCase))))
            .Select(r => r.Name)
            .ToList();

        Assert.True(offending.Count == 0,
            $"{assembly.GetName().Name} must stay UI-toolkit-free but references: " +
            string.Join(", ", offending));
    }

    // ---- file-length ratchet ----

    /// <summary>
    /// THE RATCHET CONTRACT. These are the current line counts of the files that
    /// have historically absorbed every feature (re-measured 2026-08-15). The test
    /// fails when any of them grows more than 10% past its baseline.
    ///
    /// Shrink freely — and when you do, lower the baseline here in the same PR so
    /// the headroom doesn't quietly refill. Growth past the limit needs a
    /// deliberate baseline bump in the same PR, which makes "this file gets
    /// bigger" a reviewed decision instead of a drift. New logic usually belongs
    /// in Core or UI.Shared anyway, where it's testable without a window.
    ///
    /// **A path may be a glob**, and when it is, the lines of every file it matches
    /// are SUMMED. That is deliberate: MainWindow.xaml.cs could otherwise be brought
    /// under its limit by splitting it into `MainWindow.Something.xaml.cs` and
    /// changing nothing, which leaves exactly as much untestable window logic as
    /// before — the thing this gate exists to push back on. Splitting for
    /// readability stays fine; it just doesn't buy headroom. Only two things do:
    /// moving logic out of the WPF layer, or lifting a surface into a component of
    /// its own (QuestChecklistView, 2026-08-15, is what the current MainWindow
    /// baseline reflects).
    /// </summary>
    private static readonly (string RelativePath, int BaselineLines)[] Hotspots =
    [
        // Baseline history: lowered 4274 → 4422 on 2026-08-19 — no, LOWERED is the wrong
        // word for a number that went up, and that is the point. The file had drifted to
        // 4,622 against a 4,274 baseline: legal (under the +10% limit) but only 79 lines
        // from failing, and it had been within ~100 for days. The Watch card came out to
        // WatchCardView.cs (231 lines) and this is the new true count, so the 10% is
        // headroom that has been earned rather than headroom left over from a stale
        // number. Re-baselining without the lift would have been raising the ceiling.
        //
        // Measured before reaching for the lift: 177 private/internal methods in that
        // file and not one unreferenced. There was no free room to find.
        //
        // Then lowered 4422 → 4355 on 2026-08-19 with the Progress card's lift into
        // ProgressCardView.cs — in the SAME commit as the lift, because room that is
        // freed and not claimed quietly refills. The card took its two unlock MEMOS with
        // it rather than leaving them behind as internals, which is the difference
        // between lifting a surface and moving some lines.
        //
        // Then lowered 4355 → 4324 on 2026-08-19 with the PROGRESS THEME (docs/Themes.md):
        // RenderRaids left for RaidsCardView.cs and the other four Progress-theme cards
        // stopped being fields here entirely, since ProgressWindow builds its own.
        //
        // **Only 31 lines, and the small number is the honest part.** The fold removed
        // ~150 and added back ~75 of window plumbing — ShowProgressWindow,
        // NewProgressSurfaces, SetMiniStat — because that is where this file already keeps
        // every satellite's launcher. Consolidating cards buys much less headroom than
        // lifting one does; the surfaces move, the doors to them stay.
        // 4,324 → 4,214 on 2026-08-20: the Gear card body lifted into GearCardView.cs
        // for the Loot & Items theme. Lowered in the same commit as the lift, or the
        // room quietly refills — which is the whole contract above.
        (@"EQBuddy/MainWindow*.xaml.cs", 4214),
        // A GLOB, like MainWindow's above, and for the same reason — but this one was a
        // literal path until 2026-08-18 and SessionStats is a partial class, so
        // SessionStats.Tracked.cs (207 lines) was never counted at all. The entry read
        // 2,559 for a class that is 2,766, and the "split it into another partial"
        // escape this test exists to refuse was standing open on the one file that had
        // just needed a baseline bump. Globbing costs nothing today and shuts it.
        //
        // Baseline history: 2324 → 2559 on 2026-08-17 for #135's sixth confirmed cause
        // (a charm cast by an ITEM, which prints no cast line) — the file had 22 lines
        // of headroom and the fix needed 25. Then → 2766 here, which is not a third
        // grant: it is the same code, finally all being counted.
        //
        // Lowered 2766 → 2375 on 2026-08-18: the charm state machine came out into
        // CharmTracker.cs, as the two notes above said it should. 391 lines, verified
        // behaviour-preserving by replaying all seven of bjstrange's #135 logs and
        // diffing every charm-state transition — byte for byte identical.
        (@"EQBuddy.Core/SessionStats*.cs", 2375),
        (@"EQBuddy/OptionsWindow.xaml.cs", 1547),
        (@"EQBuddy.Core/LogParser.cs", 853),
        // THE LARGEST FILE IN THE REPO, and until 2026-08-19 the only big one with no
        // ratchet at all — 5,127 lines, ~700 more than the WPF widget this list was
        // written for. It was missed because the hotspots were chosen while the WPF
        // decomposition was the work in front of us, and nothing since has looked at the
        // list itself: the Avalonia twin grew unwatched the whole time the Windows one
        // was being pulled apart.
        //
        // Entered at its CURRENT size rather than at some aspirational number, because a
        // ratchet's job is to stop growth today; the 10% is the same grant every other
        // entry gets. It should come down the way SessionStats did — the widget's card
        // bodies here have never been lifted, and LootCardView.cs is the worked example
        // of doing it (the Avalonia card that was a whole feature behind, Gate 4).
        //
        // NOT bumped for the Progress theme on 2026-08-19, and worth saying why, because
        // the file GREW: 5,127 → 5,291, still legal inside the 10% grant. Its twin came
        // down 31 lines for the same change and this went up 164, because the WPF fold
        // moved five card VIEWS to a window while this one only re-parented five card
        // BODIES — the bodies are still built and rendered here, and the IProgressHost
        // implementation is new on top. That was the right call for the fold itself (it
        // keeps "the tabs draw what the cards drew" literally true, with no rewrite to
        // review) but it is not decomposition, and this entry should not be allowed to
        // read as though it were. The headroom this file has left is now ~350 lines.
        //
        // 2026-08-20: the GEAR & LOOT fold took the same shape and cost another ~90 lines
        // (IGearLootHost, the launcher, ShowGearLootWindow), so the headroom is ~100 and
        // this note is the warning the next fold needs. **The next theme on this build must
        // be preceded by a LIFT, not followed by one.** The obvious candidate is already
        // named by its WPF twin: the gear checklist here is ~275 contiguous lines
        // (BuildGearSection, RenderGearChecklist, GearRow, the auto-check high-water marks)
        // and Windows lifted exactly that into GearCardView.cs. Note this build has no E2E
        // suite, so CLAUDE.md's "pin the behaviour before the move" has to be paid in
        // WidgetRenderTests instead — write the assertions first.
        (@"EQBuddy.Avalonia/MainWindow.cs", 5127),
    ];

    private const double AllowedGrowth = 1.10;

    public static TheoryData<string, int> HotspotData()
    {
        var data = new TheoryData<string, int>();
        foreach (var (path, baseline) in Hotspots) data.Add(path, baseline);
        return data;
    }

    [Theory]
    [MemberData(nameof(HotspotData))]
    public void HotspotFilesDoNotGrowPastTheRatchet(string relativePath, int baselineLines)
    {
        var src = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src");
        var full = Path.GetFullPath(Path.Combine(src, relativePath));
        var dir = Path.GetDirectoryName(full)!;
        var pattern = Path.GetFileName(full);
        // A literal name matches exactly one file; a glob sums its whole family, so a
        // partial can't be used to duck the limit (see the contract above).
        var files = Directory.Exists(dir)
            ? Directory.GetFiles(dir, pattern).OrderBy(f => f, StringComparer.Ordinal).ToList()
            : [];
        Assert.True(files.Count > 0, $"Ratchet hotspot moved or vanished: {full} — " +
            "update the path (or drop the entry) in ArchitectureTests.Hotspots.");

        var perFile = files.ToDictionary(f => Path.GetFileName(f)!, f => File.ReadLines(f).Count());
        var lines = perFile.Values.Sum();
        var limit = (int)(baselineLines * AllowedGrowth);
        var breakdown = perFile.Count > 1
            ? " (" + string.Join(" + ", perFile.Select(kv => $"{kv.Key} {kv.Value}")) + ")"
            : "";

        Assert.True(lines <= limit,
            $"{relativePath} is {lines} lines{breakdown} — past its ratchet limit of {limit} " +
            $"(baseline {baselineLines} + 10%). Extract the new logic into Core/UI.Shared, " +
            "or lift a whole surface into its own class the way QuestChecklistView was. " +
            "Splitting the file into another partial will not help: this entry sums them. " +
            "Failing that, bump the baseline in ArchitectureTests.Hotspots as a " +
            "deliberate, reviewed decision in this same PR.");

        // The other direction: a file that shrank well below baseline means someone
        // did the hard work — bank it. Warning-only would be invisible in CI, so
        // this fails too, asking for the baseline to be lowered to match.
        var slack = (int)(baselineLines * 0.85);
        Assert.True(lines >= slack,
            $"{relativePath} is {lines} lines{breakdown}, well under its {baselineLines} " +
            "baseline. Nice. Lower the baseline in ArchitectureTests.Hotspots so the " +
            "freed headroom can't quietly refill.");
    }
}
