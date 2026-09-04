using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// Whether a firing alert may make a noise on EQBuddy Mobile, and the words the Options
/// switch uses to say so (#208, sbaum23).
///
/// **The ask, verbatim:** *"As long as I can turn off alerts/chip in EQBuddy but leave
/// them on in mobile (with the sounds for alerts happening in the browser) I think that
/// will work for my use case."* — sbaum23, 2026-08-20. Cosmic/Wayland will not let the
/// overlay chips land on the monitor he put them on, so the second screen is his overlay;
/// a second screen that cannot make a sound is a screen he has to keep watching.
///
/// **One switch, default Off** (Bevel presentation lock, Helm-signed 2026-09-04). Not a
/// per-event set of pickers: the desktop ALREADY decides which alerts are worth a noise —
/// a muted watch rule resolves to no sound, a spawn row's alert toggle gates its cue — and
/// this decides only whether that decision also reaches the phone. Two sets of pickers
/// deciding one question is trap 10's shape, and the Options volume slider is the worked
/// example of what it costs.
///
/// **It is a POLICY class rather than a bare <c>settings.CompanionSounds</c> read** for
/// trap 47's reason: three code paths ask this question (the two widgets' alert sites and
/// the host that stamps the wire), and a destructive-or-not question answered in three
/// places is a question that will eventually be answered differently in one of them. Here
/// it is answered once, with no audio device and no window in sight.
/// </summary>
public static class MobileAlertSounds
{
    /// <summary>The Options switch's label. Owned here so the two Options windows cannot
    /// drift from each other, the way the chip stacks did (#122, #152).</summary>
    public const string Label = "Mobile sounds";

    /// <summary>The helper line under the switch, exactly as Bevel locked it. It says the
    /// DEFAULT out loud, because "off by default" is the whole answer to "why is my phone
    /// silent" and a player should not have to flip a switch to discover it.</summary>
    public const string HelperText =
        "Off until you turn it on — phone stays quiet when alerts fire.";

    /// <summary>What the Options block adds under the helper, naming what this does NOT
    /// touch. The desktop's own alert sounds have their own controls (Options → Alerts,
    /// per-rule pickers, the volume slider) and a switch that silently took them over
    /// would be the #228 class — a capability removed by a change that read as an
    /// addition.</summary>
    public const string ScopeNote =
        "Only EQBuddy Mobile. Your PC's alert sounds keep their own settings.";

    /// <summary>
    /// May an alert that just fired become a cue on the wire?
    /// </summary>
    /// <param name="companionEnabled">The LAN listener itself (Options → EQBuddy Mobile).
    /// A cue with no server to carry it is a value written and never read (trap 43).</param>
    /// <param name="companionSounds">The master switch this class exists for.</param>
    /// <remarks>Both halves, deliberately: turning EQBuddy Mobile OFF must silence the
    /// phone without the player also having to find this switch, and turning this switch
    /// off must silence it without unpairing. Neither is a substitute for the other.</remarks>
    public static bool ShouldCue(bool companionEnabled, bool companionSounds) =>
        companionEnabled && companionSounds;

    /// <summary>The same question asked of a settings object — the shape both widgets and
    /// the host actually call, so no caller has to remember which two flags to pass.</summary>
    public static bool ShouldCue(AppSettings? settings) =>
        settings is not null && ShouldCue(settings.CompanionEnabled, settings.CompanionSounds);
}
