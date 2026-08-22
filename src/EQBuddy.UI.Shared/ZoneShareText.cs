using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>Words the share-import preview uses, in one place so the two desktops cannot
/// spell them differently.</summary>
public static class ZoneShareText
{
    /// <summary>Why a row cannot be imported at all. Both reasons are the catalog saying
    /// this mob has no kill-to-respawn cycle, and the player's next action differs, so the
    /// two are named rather than merged (the same split as the Spawns row's
    /// "triggered" vs "instance").</summary>
    public static string RefusedReason(ZoneShare.TimerDiff diff) =>
        "the catalog says this mob is triggered or a raid-instance boss";
}
