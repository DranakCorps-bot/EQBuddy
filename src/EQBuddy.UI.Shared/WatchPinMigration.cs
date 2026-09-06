using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// The one-time watch-pin migration both widgets run at startup — chips became per-rule
/// again, and someone who had them on was seeing every enabled rule, so pin what they
/// already had rather than silently emptying their mini bar.
///
/// ONE home for it since the v1.99.16 release review: until then both MainWindows carried
/// the block verbatim, and #253 (HiramDucky) is what per-lane hand copies of a migration
/// cost — the "any per-rule pin turns on the group pin" line sat ABOVE the
/// <see cref="AppSettings.WatchPinsMigrated"/> gate on both lanes, re-ran every launch,
/// and flipped the Options tick-box back on for anyone who had turned it off while a rule
/// stayed pinned. The fix moved it inside the gate; this class is the guard that keeps it
/// there, because a test can call this where it could never reach a window constructor.
///
/// Deliberately NOT in <see cref="AppSettings"/>.Load's own migration pass: that path has
/// a <c>persistMigrations: false</c> caller (the --textprobe lock-skip, trap 13) and this
/// migration must save, so it stays a startup step the widgets own.
///
/// <b>Since Surface A / SA-R it carries a SECOND pass — the group pin's retirement</b> — and
/// the two are here together because the order between them is the whole correctness
/// argument. See <see cref="RetireGroupPin"/>.
/// </summary>
public static class WatchPinMigration
{
    public static void Apply(AppSettings settings)
    {
        // Non-short-circuiting: both passes are gated separately and a profile can owe
        // either, both, or neither. ORDER MATTERS — see RetireGroupPin.
        var changed = PromoteToGroupPin(settings) | RetireGroupPin(settings);
        if (changed) settings.Save();
    }

    /// <summary>The original #253 pass, unchanged: chips became per-rule, so pin what the
    /// player was already seeing rather than silently emptying their mini bar.</summary>
    private static bool PromoteToGroupPin(AppSettings settings)
    {
        // Once only — gated so deliberately unpinning every rule (or unticking the group
        // pin) isn't undone next launch (#253).
        if (settings.WatchPinsMigrated) return false;

        // Any per-rule pin from older versions turns on the group pin.
        if (!settings.PinWatchChips && settings.TrackedRules.Any(r => r.Pinned))
            settings.PinWatchChips = true;
        // Not conditioned on "nothing is pinned": AppSettings.Load may already have added
        // the built-in CC-broke rule, which is pinned by default, and that made this pass
        // skip itself and leave the user's own rules invisible.
        if (settings.PinWatchChips)
            foreach (var rule in settings.TrackedRules.Where(r => r.Enabled))
                rule.Pinned = true;
        settings.WatchPinsMigrated = true;
        return true;
    }

    /// <summary>
    /// <b>Surface A / SA-R: <c>AppSettings.PinWatchChips</c> retires, and its state has to
    /// land somewhere before it does.</b> The master switch and the per-rule 📌 both answered
    /// "does this chip show" — two switches, one question — and Helm's #341 sign was to reduce
    /// them to ONE. The pin is the survivor, so the master's OFF has to become per-rule OFF or
    /// every player who unticked that box gets their chips back with nothing having asked
    /// them. This is <c>AppSettings.MigratePromotedHudStats</c>'s order of operations exactly:
    /// read the switch, translate it, then stop reading it.
    ///
    /// <b>It runs AFTER <see cref="PromoteToGroupPin"/>, and that is not tidiness.</b> A
    /// profile that has never migrated is about to have its master turned ON by that pass —
    /// including a brand-new one, where <c>ApplyDefaultRules</c> has just added the pinned
    /// CC-broke rule — so a <c>false</c> read before it runs is a DEFAULT, not a choice, and
    /// unpinning on it would empty the mini bar of every fresh install. Ordering is what buys
    /// the <c>hadFile</c> guard that migration needed a whole parameter for.
    ///
    /// Nothing is unpinned when the master was on: those rules keep exactly the pins they had,
    /// so what is on the bar the launch after the upgrade is what was on it the launch before.
    /// </summary>
    private static bool RetireGroupPin(AppSettings settings)
    {
        if (settings.WatchChipMasterRetired) return false;
        settings.WatchChipMasterRetired = true;
        if (settings.PinWatchChips) return true;

        foreach (var rule in settings.TrackedRules) rule.Pinned = false;
        return true;
    }
}
