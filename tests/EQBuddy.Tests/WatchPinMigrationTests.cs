using EQBuddy.Core;
using EQBuddy.UI.Shared;
using Xunit;

namespace EQBuddy.Tests;

/// <summary>
/// The one-time watch-pin migration (#253, HiramDucky) — added by the v1.99.16 release
/// review, which found the fix had shipped with no test on either lane: the exact
/// regression ("unticked group pin + a pinned rule + relaunch = unticked again") lived in
/// two window constructors where nothing could call it. `WatchPinMigration.Apply` is one
/// home now; the scan at the bottom keeps the lanes from growing hand copies again, which
/// is how #253 happened.
///
/// **In the serial collection because <c>Apply</c> ends in <c>settings.Save()</c>** — it
/// writes the shared throwaway profile's settings.json, so it cannot run beside the tests
/// that assert on that file. It was outside the collection from v1.99.16 until 2026-09-05,
/// and the cost landed in someone else's file: <c>SettingsClobberTests</c> went
/// intermittently red with "Expected 2, Actual 5125" — this migration's default settings
/// written over the two-byte fixture that test had just laid down. See
/// <see cref="SettingsFileCollectionTests"/> for why the curated writer list is the half
/// that needs re-deriving whenever a new <c>Save()</c> appears.
/// </summary>
[Collection(SettingsFileCollection.Name)]
public class WatchPinMigrationTests
{
    /// <param name="retired">Whether the SA-R retirement pass has already run. Defaults to
    /// TRUE for the #253 tests below, which are about the promotion pass alone: leaving it
    /// false would let the retirement unpin every rule underneath their assertions, and a
    /// test that quietly exercises two passes proves neither.</param>
    private static AppSettings Settings(bool migrated, bool groupPin, params TrackedRule[] rules) =>
        Settings(migrated, groupPin, retired: true, rules);

    private static AppSettings Settings(
        bool migrated, bool groupPin, bool retired, params TrackedRule[] rules)
    {
        var s = new AppSettings
        {
            WatchPinsMigrated = migrated,
            PinWatchChips = groupPin,
            WatchChipMasterRetired = retired,
        };
        s.TrackedRules.AddRange(rules);
        return s;
    }

    /// <summary>THE #253 regression: a player who already migrated and then unticked the
    /// group pin keeps it unticked across relaunches, pinned rule or not.</summary>
    [Fact]
    public void AMigratedProfileWithAPinnedRuleStaysUnticked()
    {
        var s = Settings(migrated: true, groupPin: false,
            new TrackedRule { Name = "CC broke", Enabled = true, Pinned = true });

        WatchPinMigration.Apply(s);

        Assert.False(s.PinWatchChips);
    }

    /// <summary>First run of the migration: an old per-rule pin turns the group pin on,
    /// and every enabled rule is pinned so the mini bar shows what it always showed.</summary>
    [Fact]
    public void FirstRunPromotesAPerRulePinToTheGroupPinAndPinsEnabledRules()
    {
        var s = Settings(migrated: false, groupPin: false,
            new TrackedRule { Name = "mine", Enabled = true, Pinned = true },
            new TrackedRule { Name = "also mine", Enabled = true, Pinned = false },
            new TrackedRule { Name = "disabled", Enabled = false, Pinned = false });

        WatchPinMigration.Apply(s);

        Assert.True(s.PinWatchChips);
        Assert.True(s.WatchPinsMigrated);
        Assert.True(s.TrackedRules[1].Pinned, "enabled rules are pinned on migration");
        Assert.False(s.TrackedRules[2].Pinned, "disabled rules are left alone");
    }

    /// <summary>The not-conditioned-on-nothing-pinned case the in-place comment defends:
    /// with the group pin already on, enabled-but-unpinned rules still get pinned even
    /// though the built-in rule was already pinned.</summary>
    [Fact]
    public void GroupPinAlreadyOnStillPinsTheUsersOwnEnabledRules()
    {
        var s = Settings(migrated: false, groupPin: true,
            new TrackedRule { Name = "built-in", Enabled = true, Pinned = true },
            new TrackedRule { Name = "user's own", Enabled = true, Pinned = false });

        WatchPinMigration.Apply(s);

        Assert.True(s.TrackedRules[1].Pinned);
    }

    /// <summary>Running it twice is running it once — the gate is the whole fix.</summary>
    [Fact]
    public void SecondApplyChangesNothing()
    {
        var s = Settings(migrated: false, groupPin: false,
            new TrackedRule { Name = "r", Enabled = true, Pinned = true });
        WatchPinMigration.Apply(s);
        s.PinWatchChips = false;   // the player unticks after the real migration

        WatchPinMigration.Apply(s);

        Assert.False(s.PinWatchChips);
    }

    // ---- Surface A / SA-R: the group pin's retirement ----

    /// <summary>
    /// THE RETIREMENT ITSELF: a player who had the master unticked keeps an empty bar, and
    /// the way that survives the switch's removal is per-rule unpins.
    ///
    /// Without this the setting's disappearance would hand every one of those players their
    /// chips back with nothing having asked them — the SA-1 lesson (read the switch before
    /// stripping it) with a bool instead of a list.
    /// </summary>
    [Fact]
    public void AnUntickedMasterBecomesPerRuleUnpins()
    {
        var s = Settings(migrated: true, groupPin: false, retired: false,
            new TrackedRule { Name = "mine", Enabled = true, Pinned = true },
            new TrackedRule { Name = "also mine", Enabled = false, Pinned = true });

        WatchPinMigration.Apply(s);

        Assert.True(s.WatchChipMasterRetired);
        Assert.All(s.TrackedRules, r => Assert.False(r.Pinned));
    }

    /// <summary>A TICKED master changes nothing at all: those rules keep the pins they had,
    /// so the bar the launch after the upgrade is the bar from the launch before.</summary>
    [Fact]
    public void ATickedMasterLeavesEveryPinAlone()
    {
        var s = Settings(migrated: true, groupPin: true, retired: false,
            new TrackedRule { Name = "shown", Enabled = true, Pinned = true },
            new TrackedRule { Name = "hidden", Enabled = true, Pinned = false });

        WatchPinMigration.Apply(s);

        Assert.True(s.TrackedRules[0].Pinned);
        Assert.False(s.TrackedRules[1].Pinned);
    }

    /// <summary>
    /// **ORDER, and it is the whole correctness argument.** A profile that has never run the
    /// #253 promotion is about to have its master turned ON by it — a brand-new one included,
    /// since <c>ApplyDefaultRules</c> has just added the pinned CC-broke rule — so the
    /// <c>false</c> sitting in <c>PinWatchChips</c> at that moment is a DEFAULT, not a choice.
    /// Retiring first would read it as a choice and empty the mini bar of every fresh install.
    /// </summary>
    [Fact]
    public void AProfileThatNeverMigratedIsPromotedBeforeItIsRetired()
    {
        var s = Settings(migrated: false, groupPin: false, retired: false,
            new TrackedRule { Name = "CC broke", Enabled = true, Pinned = true },
            new TrackedRule { Name = "user's own", Enabled = true, Pinned = false });

        WatchPinMigration.Apply(s);

        Assert.True(s.PinWatchChips, "the promotion ran first and turned the master on");
        Assert.All(s.TrackedRules, r => Assert.True(r.Pinned));
    }

    /// <summary>Running it twice is running it once — for the retirement as much as for the
    /// promotion, and the flag is what makes that true. Re-pinning a rule after the pass must
    /// stick: a second run that re-read <c>PinWatchChips</c> would unpin it again, forever,
    /// which is trap 55's migration-re-deciding-on-its-own-output shape.</summary>
    [Fact]
    public void ASecondRunDoesNotUndoAPinTheRetirementJustCleared()
    {
        var s = Settings(migrated: true, groupPin: false, retired: false,
            new TrackedRule { Name = "r", Enabled = true, Pinned = true });
        WatchPinMigration.Apply(s);
        s.TrackedRules[0].Pinned = true;   // the player pins it again afterwards

        WatchPinMigration.Apply(s);

        Assert.True(s.TrackedRules[0].Pinned);
    }

    /// <summary>
    /// **The setting is retired, so nothing outside these two files may read it.** The
    /// property survives on <c>AppSettings</c> for exactly one reason — this migration has to
    /// read it once — and a setting that outlives its retirement is precisely how a second
    /// switch grows back on the question the retirement existed to leave with one answer.
    ///
    /// **Comment lines are skipped, deliberately.** Four files carry a tombstone naming what
    /// left them and where it went; that is the trap-26 bookkeeping a fold owes, and a scan
    /// that forbade the NAME would make writing one impossible (trap 2's tombstone had to
    /// un-backtick its dead files for the same reason).
    /// </summary>
    [Fact]
    public void NothingOutsideTheMigrationReadsTheRetiredMasterSwitch()
    {
        var src = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var mine = Path.GetFileName(typeof(WatchPinMigration).Name) + ".cs";

        var offenders = Directory
            .EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => Path.GetFileName(f) != mine
                     && Path.GetFileName(f) != $"{nameof(AppSettings)}.cs")
            .Where(f => File.ReadAllLines(f).Any(line =>
                line.Contains(nameof(AppSettings.PinWatchChips), StringComparison.Ordinal)
                && !line.TrimStart().StartsWith("//", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0,
            "PinWatchChips retired in Surface A / SA-R. Only its declaration and this "
            + "migration's one-time translation may read it; these files read it in code, "
            + "which is the second switch coming back: " + string.Join(", ", offenders));
    }

    /// <summary>The widget must CALL the shared migration and may not keep its own copy.
    ///
    /// **Two hand copies of one migration is the condition that produced #253 — and with
    /// one lane the danger changes shape rather than going away.** A migration inlined back
    /// into the widget is invisible to every test above, because `WatchPinMigration` would
    /// go on being correct and simply not be the thing that runs. `Assert.DoesNotContain`
    /// on the setting name is what keeps that honest. (Scanned two lanes until E-2,
    /// 2026-09-04.)</summary>
    [Theory]
    [InlineData("EQBuddy", "MainWindow.xaml.cs")]
    public void TheWidgetUsesTheSharedMigration(string project, string file)
    {
        var src = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var text = File.ReadAllText(Path.Combine(src, project, file));

        Assert.Contains("WatchPinMigration.Apply(", text);
        Assert.DoesNotContain("WatchPinsMigrated", text);
        // Same rule for SA-R's flag: an inlined retirement would be invisible to every test
        // above for exactly the same reason.
        Assert.DoesNotContain("WatchChipMasterRetired", text);
    }
}
