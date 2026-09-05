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
/// → **Every host builds its own instance through a factory, and no host hands out a UI
/// object it did not just create.** That is what this scans for.
///
/// **E-2 (2026-09-04) removed the lane the crash was ON, and this file follows the WPF one
/// instead of retiring** — the plan is explicit that the exemptions must not be
/// re-justified as "one lane, so ownership does not matter". Trap 45 was FOUND by Avalonia
/// and is not ABOUT Avalonia: a method returning a long-lived UI object is a transfer of
/// ownership wearing a getter's clothes, a WPF <c>UIElement</c> has exactly one parent too,
/// and there the symptom is not an exception but a surface silently vanishing from
/// whichever host drew it first. E-3's shell is about to become a second host for surfaces
/// the widget still renders, which is the condition that produced this in the first place.
///
/// One correction made in the same pass: the header used to say "the same scan runs over
/// both lanes" while the first four checks read `EQBuddy.Avalonia` and nothing else. The
/// WPF half was a claim, not a scan.
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

    /// <summary>
    /// The accessor shape that was the bug: a host handing out a BUILT body per tab. It
    /// reads like a getter and it is a transfer of ownership between two windows.
    ///
    /// **Re-pointed at the WPF lane in E-2 (2026-09-04), and this is the row where "one
    /// lane, so ownership does not matter" would have been the easy wrong answer.** The
    /// crash was Avalonia's, but the trap is not: a method that returns a long-lived UI
    /// object is a transfer of ownership wearing a getter's clothes, and a WPF UIElement
    /// still has exactly one parent — a shared instance gets torn out of whichever host
    /// drew it last, silently, with no exception to point at. The scan had been declaring
    /// in its own header that it "runs over both lanes" while every path in this group read
    /// `EQBuddy.Avalonia`; now it does what it said.
    ///
    /// It matches the SHAPE rather than four literal signatures, because a WPF host would
    /// spell the return type `UIElement` or `FrameworkElement` where Avalonia spelled it
    /// `Control` — and because E-3's shell is about to add hosts nobody has named yet.
    /// </summary>
    [Theory]
    [InlineData("ProgressWindow.xaml.cs")]
    [InlineData("GearLootWindow.xaml.cs")]
    [InlineData("CreatureWindow.xaml.cs")]
    [InlineData("QuestsWindow.xaml.cs")]
    // The Gate 2 content after E-3 PR 3 lifted it out of the window beside it. Both rows
    // stay: the window is still a host, and the view is where a tab-body accessor would
    // now be written if anyone reached for one.
    [InlineData("QuestsView.xaml.cs")]
    [InlineData("QuestsRoom.cs")]
    [InlineData("WorldWindow.xaml.cs")]
    public void NoHostInterfaceHandsOutATabBody(string file)
    {
        var path = Path.Combine(Src, "EQBuddy", file);
        Assert.True(File.Exists(path),
            $"{file} moved or was folded away — move this row with it rather than letting " +
            "the scan skip a host (a guard that silently skips is not a guard).");
        var text = File.ReadAllText(path);
        var exempt = StillHandingOutBodies.FirstOrDefault(r => r.File == file);

        var accessors = System.Text.RegularExpressions.Regex.Matches(text,
            @"\b(?:UIElement|FrameworkElement|Control)\s+(\w*TabBody)\s*\(")
            .Select(m => m.Value.Trim())
            .Where(a => a != exempt.Accessor)
            .ToList();

        Assert.True(accessors.Count == 0,
            $"{file} hands a built body out per tab ({string.Join(", ", accessors)}). Each " +
            "host builds its own surfaces through a factory instead — trap 45.");
    }

    /// <summary>And the exemptions are real ones. A row that no longer describes the file
    /// is worse than no row — it reads as coverage of something that has moved on, which is
    /// how every stale list in this repo has failed.</summary>
    [Fact]
    public void EveryExemptionStillDescribesItsFile()
    {
        foreach (var (file, accessor, why) in StillHandingOutBodies)
        {
            var text = File.ReadAllText(Path.Combine(Src, "EQBuddy", file));
            Assert.Contains(accessor, text);
            Assert.NotEmpty(why);
        }
    }

    /// <summary>The positive half, per trap 34: forbidding a shape is not the same as
    /// requiring the right one. A future <c>ProgressBody(tab)</c> would sail past the scan
    /// above, so the host is asserted to actually build its own set.</summary>
    [Fact]
    public void TheProgressHostHandsOutAFreshSetInstead()
    {
        var window = File.ReadAllText(Path.Combine(Src, "EQBuddy", "ProgressWindow.xaml.cs"));
        var widget = File.ReadAllText(Path.Combine(Src, "EQBuddy", "MainWindow.xaml.cs"));

        Assert.Contains("ProgressSurfaceSet NewProgressSurfaces()", widget);
        // And the window uses it in its CONSTRUCTOR — a set fetched later, per render,
        // would rebuild the surfaces under the player and lose their fold states.
        Assert.Contains("var surfaces = main.NewProgressSurfaces();", window);
    }

    /// <summary>The other theme windows build their own surfaces in their own constructors,
    /// exactly as Progress does — by calling a factory on the widget, or by constructing
    /// the view outright, which is just as fresh. What none of them may do is take one the
    /// widget is already rendering.</summary>
    [Theory]
    // A field initialiser rather than a constructor line — same thing for ownership, and
    // asserted as it is written rather than as it might have been.
    [InlineData("CreatureWindow.xaml.cs", "KillsCardView _kills = new();")]
    [InlineData("CreatureWindow.xaml.cs", "new DropsCardView(main)")]
    [InlineData("GearLootWindow.xaml.cs", "new LootCardView(main, _settings)")]
    // The gear factory takes the host's list cap since #250 PR 2 (the window's own
    // BodyScroll decides, not a card-sized 320), so the call does not end in "()". The
    // open paren is deliberate and still pins what this guard is FOR.
    [InlineData("GearLootWindow.xaml.cs", "main.NewGearCard(")]
    [InlineData("GearLootWindow.xaml.cs", "new InventoryView(main)")]
    // E-3 PR 3: the Quests surface became a view with TWO hosts, which is precisely the
    // condition trap 45 is about — a WPF UIElement has one parent, so a view shared
    // between the window and the shell's room would be torn out of whichever painted it
    // last, silently. Each host constructs its own, and this is the positive half that
    // says so (forbidding the wrong shape is not the same as requiring the right one).
    [InlineData("QuestsWindow.xaml.cs", "new QuestsView(main)")]
    [InlineData("QuestsRoom.cs", "new QuestsView(main)")]
    public void TheOtherHostsBuildTheirOwnSurfacesToo(string file, string ctorUse)
    {
        var text = File.ReadAllText(Path.Combine(Src, "EQBuddy", file));

        Assert.Contains(ctorUse, text);
    }

    /// <summary>
    /// World PR 1's four separate factories (docs/Themes.md theme 6) — not one combined
    /// set, unlike Progress/Creature/Loot, because MapView/SpawnsView do real
    /// construction-time work (file I/O, ledger reads) a shared factory would fire
    /// needlessly for every sibling. WPF's <c>WorldWindow</c> (PR 2) calls all four from
    /// its own constructor. (The Avalonia row went with the platform in E-2; the four
    /// factories are named here because a World surface built once and shared is the same
    /// ownership transfer as a tab body handed over.)
    /// </summary>
    [Theory]
    [InlineData("EQBuddy", "MainWindow.xaml.cs")]
    public void TheWidgetNamesTheSameFourWorldFactories(string project, string file)
    {
        var text = File.ReadAllText(Path.Combine(Src, project, file));

        Assert.Contains("NewMapView()", text);
        Assert.Contains("NewSpawnsView(", text);
        Assert.Contains("NewTravelView()", text);
        Assert.Contains("NewTravelsView()", text);
    }

    /// <summary>
    /// Every World host builds ITS OWN instance, never a shared one — trap 45's actual
    /// requirement (fresh, not shared). World PR 2 folded both lanes' three standalone
    /// windows into one <c>WorldWindow</c> each, which builds all four surfaces in its
    /// OWN constructor (mirroring Progress/Creature/Loot). Avalonia's <c>MapView</c>/
    /// <c>TravelView</c> are built directly against <c>IZoneHost</c> rather than through
    /// a factory — the shape <c>MapWindow</c>/<c>TravelWindow</c> already used before this
    /// PR, kept rather than "fixed" because <c>ZoneWindowsRenderTests</c> constructs them
    /// against a fake host with no widget at all. Building <c>new MapView(host)</c>/
    /// <c>new TravelView(host)</c> inline is exactly as fresh as calling a factory that
    /// would do the same thing.
    /// </summary>
    [Theory]
    [InlineData("EQBuddy", "WorldWindow.xaml.cs", "main.NewMapView()")]
    [InlineData("EQBuddy", "WorldWindow.xaml.cs", "main.NewSpawnsView(initialZone)")]
    [InlineData("EQBuddy", "WorldWindow.xaml.cs", "main.NewTravelView()")]
    [InlineData("EQBuddy", "WorldWindow.xaml.cs", "main.NewTravelsView()")]
    public void EveryWorldHostBuildsItsOwnFreshView(string project, string file, string ctorUse)
    {
        var text = File.ReadAllText(Path.Combine(Src, project, file));

        Assert.Contains(ctorUse, text);
    }

    /// <summary>No dictionary of pre-built bodies survives on either widget. That field was
    /// the store the accessor read from, and leaving it behind would leave the next person
    /// a loaded gun with the trigger guard removed.</summary>
    [Theory]
    [InlineData("EQBuddy", "MainWindow.xaml.cs")]
    public void NoWidgetKeepsATableOfBuiltBodies(string project, string file)
    {
        var text = File.ReadAllText(Path.Combine(Src, project, file));

        Assert.DoesNotContain("_progressTabBodies", text);
        Assert.DoesNotContain("HandThemeBodyTo", text);
    }

    /// <summary>The widget builds its Progress set from the same factory the window calls,
    /// so the two hosts can never end up sharing one set. (Two lanes until E-2, 2026-09-04
    /// — and E-3 makes this row matter more, not less: the shell is a second host for
    /// surfaces the widget still renders.)</summary>
    [Theory]
    [InlineData("EQBuddy", "MainWindow.xaml.cs")]
    public void TheWidgetBuildsProgressSurfacesThroughAFactory(string project, string file)
    {
        var text = File.ReadAllText(Path.Combine(Src, project, file));

        Assert.Contains("NewProgressSurfaces()", text);
        Assert.Contains("new ProgressSurfaceSet(", text.Replace("=> new(", "=> new ProgressSurfaceSet("));
    }

    /// <summary>And the seam itself still exists, with its three members. Without this the
    /// scans above could all pass on a lane that had quietly stopped having an IWidgetCard
    /// at all (trap 34). One lane since E-2, 2026-09-04.</summary>
    [Theory]
    [InlineData("EQBuddy")]
    public void TheSeamStillHasItsThreeMembers(string project)
    {
        var text = File.ReadAllText(Path.Combine(Src, project, "IWidgetCard.cs"));

        Assert.Contains("interface IWidgetCard", text);
        Assert.Contains("interface ICardContext", text);
        Assert.Contains("record ProgressSurfaceSet(", text);
        Assert.Contains("string Key { get; }", text);
        Assert.Contains("void Render(StatsSnapshot", text);
    }
}
