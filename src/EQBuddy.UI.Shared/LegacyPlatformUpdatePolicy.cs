using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>Which desktop this copy is running on. Architecture rides along because
/// macOS ships two builds and handing an Intel Mac the arm64 zip is the same class of
/// mistake as handing it the Linux tarball (#93, Amatyr).
///
/// It lives here rather than inside the Avalonia lane's <c>UpdateOffer</c> because the
/// question "which platform is this?" now has TWO consumers — the artifact routing that
/// has always been Avalonia's, and <see cref="LegacyPlatformUpdatePolicy"/>, which both
/// lanes call. One enum, one mapping, one place to be wrong.</summary>
public enum Desktop { Windows, Linux, MacArm64, MacX64 }

/// <summary>What the update check should DO, as four separate answers rather than a bool.
///
/// A bool is what produced #93 — every decision in the old <c>UpdateOffer</c> took a single
/// <c>isWindows</c>, so "not Windows" silently meant "Linux" and a Mac user was handed the
/// Linux tarball. The same reasoning applies one level up: "offer the update" and "show the
/// final legacy notice" are not two ends of one switch, and neither is "record that the
/// player has seen it" (which is the affordance Bevel may yet want to move).</summary>
/// <param name="ShowUpdateOffer">Behave exactly as EQBuddy always has: offer the found
/// update, with whatever wording and artifact the lane already picks.</param>
/// <param name="ShowFinalLegacyNotice">Show the one-time notice instead. Never true at the
/// same time as <paramref name="ShowUpdateOffer"/>.</param>
/// <param name="RecordAcknowledgement">Persist <c>AppSettings.LegacyFinalNoticeAcknowledged</c>.
/// Separate from <paramref name="BrowserTarget"/> on purpose: whether the click means "open
/// the page", "I have read this", or both is a wiring decision, not a redesign.</param>
/// <param name="BrowserTarget">Where a click on the notice sends the browser — the FINAL
/// LEGACY RELEASE page, never <c>releases/latest</c>. Null when there is no notice.</param>
public readonly record struct LegacyUpdateDecision(
    bool ShowUpdateOffer,
    bool ShowFinalLegacyNotice,
    bool RecordAcknowledgement,
    string? BrowserTarget);

/// <summary>
/// May this copy of EQBuddy offer the player the update it just found?
///
/// **The one-way door.** EQBuddy v2 is Windows-only (owner-approved, 2026-09-04). The v1
/// update channel is a single feed: <c>UpdateChecker</c> reads the repository's newest
/// release and every lane offers it. The moment a <c>2.x</c> release is `latest`, every
/// Linux and macOS install still running v1 is being steered toward an installer that
/// cannot run on their machine — and nothing shipped afterwards can reach them, because
/// the thing that would have carried the fix is the update they were told to take.
///
/// So the answer is decided ONCE, here, for both widgets. This is trap 47's shape exactly
/// — never let two code paths decide one question with a consequence — with "destructive"
/// replaced by "may we tell this player to install something that cannot run". The four
/// copies of the log-janitor rule disagreed in the direction that destroyed data; six copies
/// of this rule would disagree in the direction that strands a platform.
///
/// **What it deliberately does NOT change.** Windows behaves as it always has, in every
/// case — a Phase 0 change that alters the Windows update banner has widened its own blast
/// radius for nothing. And a non-Windows copy offered a further <c>1.99.x</c> LEGACY patch
/// still takes it: `legacy-v1` existing does not mean it will never be touched.
///
/// Charter LEGACY-002 / #275, Fable plan P0-2.
/// </summary>
public static class LegacyPlatformUpdatePolicy
{
    /// <summary>The running platform. Kept here rather than at the call site so the
    /// mapping is one decision with one place to be wrong.</summary>
    public static Desktop Current() =>
        OperatingSystem.IsWindows() ? Desktop.Windows
        : !OperatingSystem.IsMacOS() ? Desktop.Linux
        : System.Runtime.InteropServices.RuntimeInformation.OSArchitecture
            == System.Runtime.InteropServices.Architecture.Arm64
            ? Desktop.MacArm64
            : Desktop.MacX64;

    public static bool IsWindows(Desktop platform) => platform == Desktop.Windows;

    /// <summary>The major version at which EQBuddy stops being cross-platform. Named
    /// rather than inlined because the widgets must never test it themselves — a lane
    /// that knows this number is a lane that can drift from the other one.</summary>
    public const int WindowsOnlyMajor = 2;

    /// <summary>
    /// The whole rule, as a pure function.
    ///
    /// 1. <b>Windows → unchanged, in every case.</b> The policy answers "behave as today"
    ///    and the Windows diff is a call site, not a behaviour change.
    /// 2. <b>Non-Windows and the found release is v2 or later</b> → never offer it.
    ///    - automatic (<paramref name="manual"/> false): show the notice once, i.e. iff it
    ///      has not been acknowledged, and record that it has been.
    ///    - manual (the Help menu): ALWAYS answer with the same notice. A player who asks
    ///      "check for updates" and gets silence has hit a silent no-op, which this repo
    ///      treats as broken. It never re-arms the automatic nag — showing the notice can
    ///      only ever set the acknowledgement, never clear it.
    /// 3. <b>Non-Windows and the release is still 1.x</b> → today's behaviour, byte for
    ///    byte. A later legacy patch must still be offerable.
    /// </summary>
    /// <param name="info">The update just found. Callers ask only after
    /// <c>UpdateChecker.IsNewer</c> has said this is newer than what is running.</param>
    /// <param name="acknowledged">
    /// <c>AppSettings.LegacyFinalNoticeAcknowledged</c>: has this player already been told?
    /// </param>
    public static LegacyUpdateDecision Decide(UpdateInfo info, Desktop platform,
        bool manual, bool acknowledged)
    {
        if (IsWindows(platform) || info.Latest.Major < WindowsOnlyMajor)
            return new(ShowUpdateOffer: true, ShowFinalLegacyNotice: false,
                RecordAcknowledgement: false, BrowserTarget: null);

        // Non-Windows, and the feed is offering v2 or later. The offer is off from here
        // down; the only question left is whether we say anything.
        var say = manual || !acknowledged;
        return new(
            ShowUpdateOffer: false,
            ShowFinalLegacyNotice: say,
            // Showing it is what spends the nag, on EITHER path. Recording on the manual
            // path cannot re-arm anything — the flag only ever goes false→true — and it
            // keeps one meaning for one field instead of two.
            RecordAcknowledgement: say,
            // Never the release page: `releases/latest` IS the v2 release page the moment
            // v2 ships, and its most prominent asset is EQBuddySetup.exe. A correct-looking
            // notice that ends there is LEGACY-002 arriving through the back door, and it
            // would read as a working feature in every screenshot.
            BrowserTarget: say ? UpdateChecker.GitHubLegacyReleasePage : null);
    }

    /// <summary>What the notice says. Two sentences, no bare URL: both widgets are 320 px
    /// and <c>SizeToContent</c>, so an unbreakable token is a geometry change on a
    /// transparent always-on-top window over a fullscreen game (trap 12 / #173). The link
    /// lives behind the click, which is the only gesture either banner has.</summary>
    public static string FinalLegacyNoticeText(UpdateInfo info, Desktop platform) =>
        $"EQBuddy v{info.Latest} is Windows-only. This {PlatformName(platform)} copy stays "
        + "on v1 and keeps working - click for the final v1 release page.";

    /// <summary>What the banner says once the browser is open.</summary>
    public static string FinalLegacyOpenedText() =>
        "Release page opened - keep this copy, it will not be updated again.";

    private static string PlatformName(Desktop platform) => platform switch
    {
        Desktop.Linux => "Linux",
        Desktop.MacArm64 or Desktop.MacX64 => "macOS",
        _ => "Windows",
    };
}
