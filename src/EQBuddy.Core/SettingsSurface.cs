namespace EQBuddy.Core;

/// <summary>
/// The four tabs of the Evolved shell's SETTINGS room, in the order it shows them —
/// Bevel's I-11 IA (`BEVEL.md` 2026-09-05, Helm-signed at #331), built at SR-5.
///
/// **Why this exists at all, rather than the room spelling four labels.** Every other room
/// takes its tab list from a Core surface (<see cref="LootSurface"/>,
/// <see cref="QuestSurface"/>, <see cref="WorldSurface"/>, <see cref="LiveSurface"/>,
/// <see cref="ProgressSurface"/>) and <c>ShellPages.Rooms</c> MAPS that list rather than
/// translating it, so the rail, the Ctrl+K palette and the <c>page:room</c> address grammar
/// cannot come to different ideas about what a room contains. A Settings room whose tabs
/// were four literals inside a WPF file would be the one room the shell could not address,
/// and <c>ShellNavigationTests</c>' round-trip could not be written for it.
///
/// **The count is FOUR and the v1 <c>OptionsWindow</c> keeps FIVE, deliberately.** Bevel §2:
/// *"Watch and Alerts were never two subjects"* — both answer "if X happens, alert me how",
/// which is what <see cref="AlertSurface"/> has modelled as one four-way split since before
/// the pivot. The v1 window is not retired, not renamed and not reshaped by this
/// (I-9's standing rule: landing a room is separate from, and earlier than, retiring the
/// surface it replaces), so the two tab strips differ on purpose and neither is wrong.
///
/// **<see cref="SettingsTab.Hud"/> is the signed word, and this is where it lands.** §4 of
/// `docs/BEVEL-v2-staging-critique.md` bans *"cog menu / Cards &amp; windows (as a finder)"*
/// from shell copy and answers *"Settings, or the nav item"*; the surviving content of the
/// v1 <c>cards</c> tab still needs a name, and Bevel §3 ruled it **HUD** — the noun three
/// other ban rows already resolve to, and what Surface A is actively turning that tab's
/// subject into. The v1 tab goes on saying "Cards &amp; windows" because the ban's own scope
/// line exempts <c>OptionsWindow</c> and renaming shipped copy for no player benefit is the
/// #228 class.
/// </summary>
public enum SettingsTab
{
    /// <summary>Colour theme, sizes, opacity, the alignment grid, the cursor ring.</summary>
    Look,

    /// <summary>Everything that answers "if X happens, alert me how" — the shared
    /// sound/voice defaults over <see cref="AlertTab"/>'s own four families.</summary>
    Alerts,

    /// <summary>What EQBuddy shows: the panel list, what is no longer on the HUD, the
    /// mini dashboard, the floating windows. **Transitional by design** (Bevel §2): Surface
    /// A's star retirements empty it card by card, so do not build for this size.</summary>
    Hud,

    /// <summary>Pairing, the hide-when rules, hotkeys, logs, the tutorial, the readout.</summary>
    Behavior,
}

/// <summary>A tab as a host should draw it. No <c>Count</c> field, unlike
/// <see cref="AlertTabHeader"/>: a settings tab configures the tool rather than listing the
/// player's things, and a badge over "Look" would be counting sliders at somebody.</summary>
public sealed record SettingsTabHeader(SettingsTab Tab, string Label, string Key);

/// <summary>
/// Builds the Settings room's strip. Pure — no counts, no state — so it cannot drift from
/// what the tabs contain.
/// </summary>
public static class SettingsSurface
{
    /// <summary>The canonical label for each tab. Short nouns, parallel with the rail's own
    /// labels, and "HUD" rather than "Cards &amp; windows" per Bevel §3.</summary>
    public static string LabelFor(SettingsTab tab) => tab switch
    {
        SettingsTab.Look => "Look",
        SettingsTab.Alerts => "Alerts",
        SettingsTab.Hud => "HUD",
        SettingsTab.Behavior => "Behavior",
        _ => tab.ToString(),
    };

    /// <summary>The wire key — the second half of a <c>settings:room</c> address, lower-case
    /// and stable. Three of the four are the v1 tab tags (<c>look</c>, <c>alerts</c>,
    /// <c>behavior</c>) on purpose: those strings are already in players' saved
    /// <c>OptionsTab</c>, in `scripts/shoot.ps1` and in docs, and a fresh spelling would make
    /// every one of them land nowhere.</summary>
    public static string KeyFor(SettingsTab tab) => tab switch
    {
        SettingsTab.Look => "look",
        SettingsTab.Alerts => "alerts",
        SettingsTab.Hud => "hud",
        SettingsTab.Behavior => "behavior",
        _ => tab.ToString().ToLowerInvariant(),
    };

    /// <summary>
    /// Resolve a key, or null.
    ///
    /// **It must answer null for every <see cref="AlertSurface"/> key, and that is a
    /// contract rather than an accident.** The room resolves an address through this table
    /// FIRST and falls through to <c>AlertSurface.TabForKey</c>, so <c>settings:crowd</c>
    /// lands on Alerts → Crowd and <c>settings:tracked</c> on Alerts → Watch — reusing the
    /// key grammar those families already have instead of inventing a third address level
    /// (<c>settings:alerts:crowd</c>). A key claimed on both sides would silently swallow the
    /// sub-tab half of that address. <c>SettingsRoomTests</c> asserts the disjointness rather
    /// than trusting this comment.
    ///
    /// <c>cards</c> answers HUD because it is the v1 tab's tag: an old saved
    /// <c>OptionsTab</c>, an old script and an old habit should land somewhere true rather
    /// than nowhere, which is the same reason <c>LootSurface</c> still answers "locker".
    /// </summary>
    public static SettingsTab? TabForKey(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "look" or "appearance" => SettingsTab.Look,
        "alerts" => SettingsTab.Alerts,
        "hud" or "cards" => SettingsTab.Hud,
        "behavior" or "behaviour" => SettingsTab.Behavior,
        _ => null,
    };

    /// <summary>Where the room opens when an address names no tab. Look, which is the v1
    /// window's own fallback (<c>SelectTab</c> sends a stale setting home to "look") and the
    /// least destructive place to arrive.</summary>
    public const SettingsTab DefaultTab = SettingsTab.Look;

    /// <summary>The whole strip, always all four in a fixed order. A tab is never withheld:
    /// a Settings screen that hid a tab because nothing was configured on it would hide it
    /// from precisely the player who has not configured anything.</summary>
    public static IReadOnlyList<SettingsTabHeader> Tabs() =>
    [
        .. Enum.GetValues<SettingsTab>()
            .Select(tab => new SettingsTabHeader(tab, LabelFor(tab), KeyFor(tab))),
    ];
}
