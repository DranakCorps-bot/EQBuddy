using EQBuddy.Core;

namespace EQBuddy;

/// <summary>
/// **THE WIDGET'S ONE ANSWER TO "WHERE DOES THE THING I AM GOING AFTER DROP"** (DRA-216 D5,
/// S13) — <see cref="GearTargets.For"/> behind a memo, and the memo is the whole file.
///
/// <para>It exists because the join is asked on a tick and its inputs are not: the map window
/// refreshes at 1 Hz and the companion host pushes on its own clock, while the tracked list
/// changes only when a player clicks Track in the Helper room. Recomputing would re-walk the
/// catalog for every goal, every second, to produce the same object — the
/// <c>GuideAttachmentMemo</c> idiom one surface along, and its reason.</para>
///
/// <para><b>The key folds the tracked rows by CONTENT, never by count</b> (trap 72). One goal
/// untracked and another tracked between two ticks leaves a count unmoved, and the map would
/// go on ringing the dots of a goal the player has already dropped for as long as they stayed
/// in the zone. The character key is in it because the goals are per character, and
/// <see cref="ItemCatalog.Default"/> is not: it is loaded once per process and cannot move
/// under a running app, so a term for it would be a term that never changes.</para>
///
/// <para><b>One instance per HOST, like every other memo in this folder</b> (trap 45). It is a
/// cache, and a cache two owners invalidate is state rather than a producer — which is why
/// this is a field on the widget and the PRODUCER in Core is what everything shares.</para>
/// </summary>
internal sealed class GearTargetMemo
{
    private string _key = "\0";
    private GearTargetSet _answer = GearTargetSet.None;

    /// <summary>
    /// <b>And the ONE place <see cref="AppSettings.ShowGearTargetsOnMap"/> is applied</b> (D5
    /// Planner review, finding D5-1).
    ///
    /// <para>The gate is here rather than in the two views because this method is already the
    /// single producer both of them read — the desktop map through <c>IZoneHost.GearTargets</c>,
    /// the phone through the companion host's func. Gating each view separately is two callers
    /// answering one question with whichever ran last winning (trap 33), and the rings, the
    /// panel and the phone's copy would be three places it could be answered differently.
    /// Switched off it returns the set every reader already draws nothing on, so no surface
    /// needs a second branch and the catalog is never walked.</para>
    ///
    /// <para>Off touches neither <c>_key</c> nor <c>_answer</c>, so switching the layer back on
    /// recomputes the same key and returns the cached answer rather than rebuilding it.</para>
    /// </summary>
    public GearTargetSet For(AppSettings? settings, string characterKey)
    {
        if (settings?.ShowGearTargetsOnMap != true) return GearTargetSet.None;
        var tracked = TrackedUpgradeStore.For(settings, characterKey);
        var key = characterKey + "§" + string.Join(
            ',', tracked.Select(t => $"{t.Item}:{t.TrackedAt.Ticks}"));
        if (key == _key) return _answer;
        _key = key;
        return _answer = GearTargets.For(tracked, ItemCatalog.Default);
    }
}
