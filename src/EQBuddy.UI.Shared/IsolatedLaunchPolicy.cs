using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// Fail-closed rules for any automated launch that must never touch a player's
/// profile.
///
/// The unit suite has <c>TestProfileIsolation</c> (trap 68 / #428) — that is the
/// HOST process. This is the CHILD-process half: <c>tests/EQBuddy.E2E</c>,
/// <c>scripts/shoot.ps1</c>, drag-verify. Those set <c>EQBUDDY_APPDATA</c> on the
/// launched exe, and until this policy existed a caller dictionary applied AFTER
/// that assignment could point the fixture at a live profile. The host
/// isolation tests would still pass, because they never look at the child.
///
/// No opt-in. An automated seat that "needs" a live profile is the accident
/// this exists to refuse. Product launches
/// (<c>install-local.ps1 -Evolved</c>, <c>Launch-Evolved-Shell.cmd</c>) do not
/// call this; they are how the owner runs Evolved.
/// </summary>
public static class IsolatedLaunchPolicy
{
    public const string ProfileVar = "EQBUDDY_APPDATA";
    public const string V1SourceVar = "EQBUDDY_V1_APPDATA";
    public const string AllowLiveVar = "EQBUDDY_ALLOW_LIVE_APPDATA";

    /// <summary>
    /// Pin the child's profile to <paramref name="isolatedProfile"/> AFTER any
    /// caller dictionary has been applied, and refuse a live v1 import source.
    /// The isolated path itself is refused if it names a player profile, so a
    /// harness constructor that one day pointed <c>ProfileDir</c> at AppData
    /// fails closed before <c>Process.Start</c>.
    /// </summary>
    public static void PinChildProfile(
        IDictionary<string, string?> env, string isolatedProfile)
    {
        if (AppPaths.IsLivePlayerDirectory(isolatedProfile))
            throw new InvalidOperationException(
                "Refused to launch against a live player profile: " + isolatedProfile);
        env[ProfileVar] = isolatedProfile;

        if (env.TryGetValue(V1SourceVar, out var v1) &&
            AppPaths.IsLivePlayerDirectory(v1))
            throw new InvalidOperationException(
                "Refused to import from a live player profile: " + v1);
    }

    /// <summary>
    /// A harness that stood a player's app down must relaunch it without
    /// inheriting the fixture's redirect — or an inherited
    /// <c>EQBUDDY_APPDATA=%AppData%\EQBuddy Evolved</c> points the restored
    /// widget at Evolved even when the stood-down exe was v1.
    /// </summary>
    public static void ClearHarnessOverrides(IDictionary<string, string?> env)
    {
        env.Remove(ProfileVar);
        env.Remove(V1SourceVar);
        env.Remove(AllowLiveVar);
    }
}
