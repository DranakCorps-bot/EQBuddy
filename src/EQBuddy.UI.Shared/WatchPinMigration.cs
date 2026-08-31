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
/// </summary>
public static class WatchPinMigration
{
    public static void Apply(AppSettings settings)
    {
        // Once only — gated so deliberately unpinning every rule (or unticking the group
        // pin) isn't undone next launch (#253).
        if (settings.WatchPinsMigrated) return;

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
        settings.Save();
    }
}
