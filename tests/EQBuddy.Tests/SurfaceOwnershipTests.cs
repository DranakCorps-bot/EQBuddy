namespace EQBuddy.Tests;

/// <summary>
/// **A control NEVER moves between two windows on Avalonia** — the rule PR A exists to make
/// structural (Fable 5's plan, 2026-08-22), and the one this file stops anybody undoing.
///
/// Re-parenting a control across <c>TopLevel</c>s throws
/// <c>Attempt to call InvalidateArrange on wrong LayoutManager</c>. That is an open upstream
/// bug, not a mistake in our sequencing: avalonia#12753 (2023, "cross-window control
/// reparenting should be supported", still open), #17906 (a regression in 11.2.0 — fine in
/// 11.1.5), #21267 (2026, same message in production on 12.0.x). We ship 12.1.1. In their
/// source both <c>GetLayoutRoot()</c> and <c>GetLayoutManager()</c> read
/// <c>Visual.PresentationSource</c>, and the manager throws when a control's source is not
/// its owner. **Six attempts to sequence the hand-off safely all failed, because the
/// operation is unsupported rather than mis-ordered** — which is also why the one API that
/// would have forced a layout flush turned out to be <c>internal</c>.
///
/// It hid for months because a CLOSED window's presentation source is cleared, so the
/// reopen move passed by null. It stopped hiding twice: the theme windows crashed on close
/// and reopen for Linux and macOS players (every window, two clicks, fixed in 1.99.4), and
/// the inline theme card — the first host alive at the same time as the window — threw on
/// its first run and blocked Inline themes PR 1 outright.
///
/// → **Every host builds its own instance through a factory, and no host interface returns
/// a <c>Control</c> it did not just create.** That is what this scans for. WPF is exempt:
/// it has had the <c>IWidgetCard</c> seam since Gate 5b and its toolkit does not have the
/// bug — but the rule is cheap there too, so the same scan runs over both lanes.
/// </summary>
public class SurfaceOwnershipTests
{
    private static string Src => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    /// <summary>
    /// **The lanes that STILL hand out a built body, and why each is still allowed to.**
    ///
    /// Written as a curated list rather than left out of the scan, because the first run of
    /// this guard found both of them immediately and an exemption nobody can see is not an
    /// exemption, it is a blind spot (trap 34). Fable's plan puts these lifts at the head
    /// of Inline themes PR 2 and PR 3 — they are scheduled work, not accepted risk.
    ///
    /// **What keeps them alive today is a mitigation, not a fix:** since 1.99.4 each theme
    /// window releases what it is holding when it closes, so the reopen re-parent happens
    /// against a control with no presentation source. That is enough while the widget never
    /// hosts these surfaces itself — every one of them is behind a launcher on this lane.
    /// **The day one of them expands in place, it is the Progress crash again**, which is
    /// exactly how PR 1 discovered this class of bug.
    ///
    /// A row leaves this list when its lane gets the seam. Adding one is a deliberate act.
    /// </summary>
    /// <summary>EMPTY since Inline themes PR 2 (2026-08-26): both remaining lanes got
    /// their factories (`NewCreatureSurfaces` / `NewLootSurfaces`), the Kills body became
    /// a view on BOTH lanes (`KillsCardView`), and the widgets' body tables are gone. The
    /// list stays so the day someone needs a new exemption they add a ROW with a reason,
    /// not a blanket skip.</summary>
    public static readonly (string File, string Accessor, string Why)[] StillHandingOutBodies = [];

    /// <summary>The accessor shape that was the bug: <c>Control SomethingTabBody(Tab)</c>.
    /// It reads like a getter and it is a transfer of ownership between two windows.</summary>
    [Theory]
    [InlineData("ProgressWindow.cs")]
    [InlineData("GearLootWindow.cs")]
    [InlineData("CreatureWindow.cs")]
    [InlineData("QuestsWindow.cs")]
    public void NoHostInterfaceHandsOutATabBody(string file)
    {
        var path = Path.Combine(Src, "EQBuddy.Avalonia", file);
        if (!File.Exists(path)) return;
        var text = File.ReadAllText(path);
        var exempt = StillHandingOutBodies.FirstOrDefault(r => r.File == file);

        foreach (var accessor in new[]
        {
            "Control ProgressTabBody(ProgressTab tab);",
            "Control LootTabBody(LootTab tab);",
            "Control CreatureTabBody(CreatureTab tab);",
            "Control QuestTabBody(QuestTab tab);",
        })
        {
            if (exempt.Accessor == accessor) continue;
            Assert.DoesNotContain(accessor, text);
        }
    }

    /// <summary>And the exemptions are real ones. A row that no longer describes the file
    /// is worse than no row — it reads as coverage of something that has moved on, which is
    /// how every stale list in this repo has failed.</summary>
    [Fact]
    public void EveryExemptionStillDescribesItsFile()
    {
        foreach (var (file, accessor, why) in StillHandingOutBodies)
        {
            var text = File.ReadAllText(Path.Combine(Src, "EQBuddy.Avalonia", file));
            Assert.Contains(accessor, text);
            Assert.NotEmpty(why);
        }
    }

    /// <summary>The positive half, per trap 34: forbidding the old name is not the same as
    /// requiring the new shape. A future <c>ProgressBody(tab)</c> would sail past the scan
    /// above, so every lane is asserted to actually have its factory.</summary>
    [Fact]
    public void TheProgressHostHandsOutAFreshSetInstead()
    {
        var text = File.ReadAllText(Path.Combine(Src, "EQBuddy.Avalonia", "ProgressWindow.cs"));

        Assert.Contains("ProgressSurfaceSet NewProgressSurfaces();", text);
        // And the window uses it in its CONSTRUCTOR — a set fetched later, per render,
        // would rebuild the surfaces under the player and lose their fold states.
        Assert.Contains("_surfaces = main.NewProgressSurfaces();", text);
    }

    /// <summary>PR 2's two lanes got the same shape, and the windows build their sets in
    /// their constructors, exactly as Progress does.</summary>
    [Theory]
    [InlineData("CreatureWindow.cs", "CreatureSurfaceSet NewCreatureSurfaces();",
        "var set = main.NewCreatureSurfaces();")]
    [InlineData("GearLootWindow.cs", "LootSurfaceSet NewLootSurfaces();",
        "var set = main.NewLootSurfaces();")]
    public void TheOtherTwoHostsHandOutFreshSetsToo(string file, string factory, string ctorUse)
    {
        var text = File.ReadAllText(Path.Combine(Src, "EQBuddy.Avalonia", file));

        Assert.Contains(factory, text);
        Assert.Contains(ctorUse, text);
    }

    /// <summary>No dictionary of pre-built bodies survives on either widget. That field was
    /// the store the accessor read from, and leaving it behind would leave the next person
    /// a loaded gun with the trigger guard removed.</summary>
    [Theory]
    [InlineData("EQBuddy.Avalonia", "MainWindow.cs")]
    [InlineData("EQBuddy", "MainWindow.xaml.cs")]
    public void NoWidgetKeepsATableOfBuiltBodies(string project, string file)
    {
        var text = File.ReadAllText(Path.Combine(Src, project, file));

        Assert.DoesNotContain("_progressTabBodies", text);
        Assert.DoesNotContain("HandThemeBodyTo", text);
    }

    /// <summary>Both lanes build their Progress set the same way, from a factory named the
    /// same thing. The two seams drifting apart is what carried #122 and #152 to Linux
    /// after Windows had already paid for both.</summary>
    [Theory]
    [InlineData("EQBuddy.Avalonia", "MainWindow.cs")]
    [InlineData("EQBuddy", "MainWindow.xaml.cs")]
    public void BothLanesBuildProgressSurfacesThroughAFactory(string project, string file)
    {
        var text = File.ReadAllText(Path.Combine(Src, project, file));

        Assert.Contains("NewProgressSurfaces()", text);
        Assert.Contains("new ProgressSurfaceSet(", text.Replace("=> new(", "=> new ProgressSurfaceSet("));
    }

    /// <summary>And the seam exists on both lanes, with the same three members. A reader of
    /// one lane should be able to read the other; that was the argument for mirroring the
    /// WPF file name for name rather than inventing an Avalonia shape.</summary>
    [Theory]
    [InlineData("EQBuddy.Avalonia")]
    [InlineData("EQBuddy")]
    public void TheSeamIsTheSameOnBothLanes(string project)
    {
        var text = File.ReadAllText(Path.Combine(Src, project, "IWidgetCard.cs"));

        Assert.Contains("interface IWidgetCard", text);
        Assert.Contains("interface ICardContext", text);
        Assert.Contains("record ProgressSurfaceSet(", text);
        Assert.Contains("string Key { get; }", text);
        Assert.Contains("void Render(StatsSnapshot", text);
    }
}
