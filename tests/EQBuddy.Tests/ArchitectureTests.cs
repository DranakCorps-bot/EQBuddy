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
        // 4214, RESTORED by the v1.99.12 Fable review after a same-day re-anchor to
        // 4519: the convention is now KEEP-IF-IT-FITS — a lift banks into the old
        // baseline unless the post-lift sum still exceeds the old cap, because a
        // re-anchor that raises the ceiling erases the pressure that drives the next
        // lift. Raising a baseline is the one move this table exists to make someone
        // argue for out loud; make the argument in the commit TITLE.
        //
        // 4214 → 4273 on 2026-09-04 (P0-2, LEGACY-002, #275). **The argument, out loud:**
        // main was sitting at 4,635 of a 4,635 limit, so the ratchet was already full
        // before this PR — ANY WPF change would have failed it. What P0-2 adds here is 64
        // lines: a policy call, a browser-open branch and a guarded settings write. The
        // decision itself went to `UI.Shared/LegacyPlatformUpdatePolicy` and is unit
        // tested there, which is exactly what the message above asks for; what is left is
        // window plumbing that cannot leave without moving the update banner, and moving
        // the update banner is precisely what Phase 0 was told not to do.
        // Bumped to the MINIMUM that fits (4273 × 1.1 = 4700 against 4,699 lines), so
        // this grants one line and no more. **The next WPF change must lift a surface** —
        // that is the pressure this number exists to keep, and it is now at its maximum.
        //
        // 4273 → 4158 on 2026-09-04 (E-3 PR 1, the Evolved shell host). **Lowered, and
        // the lift came first**, which is the sentence the row below has been waiting to
        // be able to say. The sixteen EQBUDDY_* window hooks — 135 contiguous lines of
        // `if (env) Loaded += … call a method`, sharing one job and owing nothing to the
        // widget's own state — went to `DebugHooks.cs`. That paid for what the shell
        // needs here, which is one field. Registration ORDER was preserved exactly: these
        // are Loaded handlers that open windows which stack, so a re-order would be
        // invisible in a diff and would surface as a screenshot of the wrong window on
        // top. Bumped down to the MINIMUM that fits (4158 × 1.1 = 4573 against 4,572
        // lines), by the same "one line and no more" rule the bump above used — so the
        // NEXT E-3 move must lift again, which is the whole point of the pressure.
        //
        // 4158 → 4123 on 2026-09-05 (E-3 PR 5, the Live room). **The lift came first
        // again, and this time the ratchet is what forced it**: the file sat at 4,535
        // against a 4,573 cap, so the Live room's ~35 lines of factory and accessors could
        // not have been added at all without one. That is the pressure working exactly as
        // the note above intended — "the NEXT E-3 move must lift again".
        //
        // What came out: `FillList` (the plain name/value row builder, ~70 lines) and
        // `FillStatList` beside it, into `BreakdownRows.FillPairRows`/`FillStatRows`. **It
        // is a lift and not a move because it has a SECOND CONSUMER** — the Live room's
        // Damage tab draws the same procs, stances, area-spell and damage-taken lists the
        // Combat card draws, and two builders for one row shape is trap 33's shape. The
        // ~40 call sites in this file read unchanged through a one-line forwarder, which is
        // what kept the diff to the extraction rather than to every caller.
        //
        // Bumped down to the MINIMUM that fits (4123 × 1.1 = 4535.3 against 4,535 lines),
        // by the same "one line and no more" rule the two entries above used.
        //
        // 4123 → 4106 on 2026-09-05 (HUD subtraction cut 1, the Quests card). **Not a lift
        // this time — a SUBTRACTION**, which is the first entry here that got its room by
        // deleting a surface rather than moving one, and the room has to be claimed the
        // same way or it quietly refills.
        //
        // **The honest number is 19 lines, and it is smaller than the deletion.** The card
        // build block, the SectionMap row, the EQBUDDY_EXPAND branch, the render call, the
        // header line and the two card-sync calls came to about 40; roughly half came back
        // as comments saying where the card went and why the host outlived it. That trade
        // is deliberate — CLAUDE.md's rule is that a fold or a cut must be findable
        // afterwards, and a tombstone that costs ten lines is cheaper than the next session
        // re-deriving why `_questsHost` has no card. `QuestsThemeCard.cs` (97 lines) and
        // `Core/QuestInline.cs` (63) left the repo entirely and are not in this sum at all.
        //
        // Bumped down to the MINIMUM that fits (4106 × 1.1 = 4516.6 against 4,516 lines),
        // by the same "one line and no more" rule as every entry above.
        //
        // 4106 → 4100 on 2026-09-05 (HUD subtraction cut 2, the World card). **Six lines,
        // and the small number is the interesting part.** The code that left is 19 lines —
        // the card build block, the widget's own `TravelsView` field and construction, the
        // `EQBUDDY_EXPAND` member, the SectionMap row, the `_worldCard` field, the render
        // call, the `MiscHeader` launcher line and three `_worldCard?.Sync()` calls — and
        // the first pass of tombstones came to MORE than that: the file grew 13 lines and
        // **the ratchet failed the change**, which is the only reason anyone measured.
        //
        // That is the guard doing a job it was not obviously written for. Cut 1's own note
        // above already said "the honest number is 19 lines, and it is much smaller than
        // the deletion" because half of what left came back as commentary; here the same
        // habit went past break-even. The tombstones stayed — a cut that cannot be found
        // afterwards is what CLAUDE.md's "three ways back" and trap 55 both refuse — but
        // they were compressed to a line or two each, with the reasoning kept HERE and in
        // the surface files rather than repeated at every call site. `WorldThemeCard.cs`
        // (60 lines) left the repo outright and is not in this sum; so did
        // `WorldSurface.LauncherSummary`/`InlineModeFor` and `WorldTheme`'s glance family,
        // in Core and UI.Shared.
        //
        // Minimum that fits again: 4100 × 1.1 = 4510 against 4,509 lines.
        //
        // 4100 → 3964 on 2026-09-05 (Surface A / SA-1). **A LIFT again**, and the entry
        // had ZERO headroom when the pass started — 4,516 against 4,516.6 — which is why
        // the collapsed HUD bar left rather than being edited in place. `HudBarView.cs`
        // took the chip builder, the divider trim and the per-tick rebuild; what stayed
        // is WHEN the bar is on screen, which is the host's (trap 15). It is a VIEW CLASS
        // and not a `MainWindow.Hud.xaml.cs` partial, because this glob SUMS its matches
        // on purpose and a partial would have bought nothing.
        //
        // Bumped down to the MINIMUM that fits the MERGED tree (3964 × 1.1 = 4360
        // against 4,360 lines), by the same "one line and no more" rule as every entry
        // above. Cut 2 landed on `main` while this branch was in flight and the two
        // changes touch different parts of the file, so the number is the one the MERGE
        // produces rather than either branch's own — which is also why it is set once,
        // here, instead of per commit.
        // 3964 → 3895 on 2026-09-05 (Surface A / SA-2). **A DELETION this time, not a
        // lift**, which is the better of the two and the one this ratchet exists to buy:
        // `SpawnChipsWindow` and `MezChipsWindow` folded into one HUD chip row, and their
        // two construction blocks, the `CloseChips` teardown, the two window fields and
        // the four chip-BUILDING helpers (`MezChips`, `SlowChips`, `FightChips`,
        // `SlowChipsVisible`) left with them — the builders into `UI.Shared/HudChipRow.cs`,
        // where the mez numbering that had never been assertable now is, and the rest into
        // `HudChipRowWindow`.
        //
        // The entry had zero headroom AGAIN when the pass started (4,360 against 3964 ×
        // 1.1 = 4,360), exactly as F2 predicted, which is why SA-2's scope was two window
        // deletions rather than an edit that squeezes under the bar. Set to the MINIMUM
        // that fits, by the same "one line and no more" rule as every entry above:
        // 3895 × 1.1 = 4,284.5 against 4,284 lines.
        //
        // 3895 → 3839 on 2026-09-06 (OE-1, the mini-bar expand). **A LIFT, in the same
        // commit, because the entry had ONE line of headroom** — 4,283 against 4,284 — and
        // the feature needed six lines of widget-side wiring. `UpdateBreakouts`,
        // `ToggleBreakout` and the `_breakouts` dictionary went to `BreakoutHost.cs`
        // verbatim: it is the surface OE-1 extends (the under-bar panel's ⧉ pops into one of
        // those windows), which is what makes it the honest one to take rather than whatever
        // block happened to be longest. Everything OE-1 adds went to `HudExpand` (UI.Shared,
        // unit-tested), `HudExpandBar` and `HudExpandWindow`.
        //
        // Set to the MINIMUM that fits by the same "one line and no more" rule as every
        // entry above: 3839 × 1.1 = 4,222.9 against 4,222 lines. OE-3 and OE-4 are queued
        // behind this in the same lane and both touch this file — so they inherit zero
        // headroom on purpose, and OE-4's plan already names the roster lift that pays for
        // it. A baseline left high is headroom nobody argued for.
        (@"EQBuddy/MainWindow*.xaml.cs", 3839),
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
        // Lowered 1547 → 689 on 2026-09-05 with SR-4, in the same commit as the lift, which
        // is the standing rule — room that is freed and not claimed quietly refills. The four
        // alert blocks (the rules editor, the buff-set builder, the mez and spawn boxes and
        // the shared sound/voice header) left for SettingsAlertsView.cs, host-neutral, so the
        // Evolved shell's Settings room can compose the SAME controls instead of growing a
        // second copy of forty wirings to drift against this one. What is left here is window
        // chrome — width persistence, monitor clamping, the tab links — plus the Look,
        // Behavior and Cards tabs, which SR-1 and SR-3 take next.
        //
        // Lowered 689 → 393 on 2026-09-05 with SR-1, in the same commit as the lift, by the
        // same standing rule. The palette picker and its Custom rows, the four size/opacity
        // sliders, the alignment grid, the cursor ring, EQBuddy Mobile's pairing panel, the
        // three hide-when rules, keep-above, the hotkey rows, the regen override, auto-empty
        // + archive, the tutorial and the perf readout left for SettingsLookView.cs and
        // SettingsBehaviorView.cs. What is left is window chrome and the Cards tab, which
        // SR-3 takes.
        //
        // Lowered 393 → 337 on 2026-09-05 with SR-2, in the same commit as the move, by the
        // same standing rule. This one is not a lift into a shared block: the gear checklist
        // import (the website link, the file picker, Clear and the status line) left Options
        // ENTIRELY for GearCardView, the surface its result appears on. An import workflow is
        // a domain action, not a setting.
        //
        // Lowered 337 → 326 on 2026-09-05 with SR-3, in the same commit as the lift, by the
        // same standing rule — and this row is the one that shows why the ratchet counts
        // `.xaml.cs` and not the surface. The `cards` tab was almost all XAML: 46 lines of it
        // left `OptionsWindow.xaml` for `SettingsHudView.cs` along with the three stray
        // handlers here, and the eleven-line net is what remains after the block's own
        // construction and its doc block arrived. **The last tenant is gone**: what is left in
        // this file is window chrome — width persistence, the monitor clamp, the tab links, the
        // resize grips — plus the four block constructions and the one key-press route.
        (@"EQBuddy/OptionsWindow.xaml.cs", 326),
        (@"EQBuddy.Core/LogParser.cs", 853),
        // ---- The Avalonia widget's row (5,229 lines) left this table with the platform
        // in E-2 (2026-09-04). It was the largest file in the repo and it carried the
        // longest baseline history here, most of it about lifts that were forced by a
        // toolkit bug (trap 45) rather than by size — which is exactly why the row is not
        // simply forgotten: **the WPF row above did NOT inherit its headroom.** A
        // deletion that quietly raises someone else's ceiling is the "re-anchor erases
        // the pressure" move this table exists to make somebody argue for out loud, and
        // nobody is arguing for it. 4,273 stood, one line from its cap, and E-3's shell
        // had to be preceded by a lift. It was (see the 4154 note above).
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
