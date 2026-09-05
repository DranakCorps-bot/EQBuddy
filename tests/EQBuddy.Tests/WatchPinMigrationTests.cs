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
    private static AppSettings Settings(bool migrated, bool groupPin, params TrackedRule[] rules)
    {
        var s = new AppSettings { WatchPinsMigrated = migrated, PinWatchChips = groupPin };
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
    }
}
