namespace EQBuddy.UI.Shared;

/// <summary>
/// What a breakout window calls itself, and which vector it wears (Gate 5c).
///
/// Both UIs typed these titles out, glyph included — "⚔ Your damage" in two files. The
/// icons now match the minimized bar's for the same stat, which is the point of having a
/// design system at all: the thing that means "damage" looks the same wherever it appears.
///
/// **Keyed by string, deliberately.** <c>BreakoutKind</c> is declared separately in each
/// UI and the two do not agree — WPF has Damage, Healing, Pet, Watch, Loot and Buffs;
/// Avalonia has only Damage, Healing, Pet and Buffs. That divergence is real and predates
/// this (the Linux build has no Watch or Loot breakout at all), but it is a FEATURE gap
/// rather than a labelling one, so it is recorded rather than quietly papered over here.
/// A shared enum would have to pick a side.
/// </summary>
public static class BreakoutPresentation
{
    public const string Damage = "damage";
    public const string Healing = "healing";
    public const string Pet = "pet";
    public const string Watch = "watch";
    public const string Loot = "loot";
    public const string Buffs = "buffs";
    public const string Progress = "progress";

    /// <summary>Kind → an <see cref="IconPaths"/> name. Damage, healing, pet and loot
    /// deliberately reuse the minimized bar's icons for the same stat.</summary>
    public static string Icon(string kind) => kind switch
    {
        Damage => "Swords",
        Healing => "Heal",
        Pet => "Paw",
        Watch => "Target",
        Loot => "Bag",
        Buffs => "Timer",
        // The minimized bar's xp icon, like damage/healing/pet/loot reuse theirs.
        Progress => "Chart",
        _ => "Info",
    };

    /// <summary>The window's own title. "Your damage" and "Your healing" say WHOSE — the
    /// one thing EQBuddy will never show is anybody else's, and the title is where a new
    /// player learns that without being lectured.</summary>
    public static string Title(string kind) => kind switch
    {
        Damage => "Your damage",
        Healing => "Your healing",
        Pet => "Pet damage",
        Watch => "Watch list",
        Loot => "Loot",
        Buffs => "Buff set",
        Progress => "Progress",
        _ => "",
    };

    /// <summary>
    /// The <c>MiniStats</c> key whose ★ actually opens this window, or null when a star
    /// is not what gates it.
    ///
    /// This fact lived inline in each widget's <c>UpdateBreakouts</c> gate and nowhere
    /// else, so Options could offer a tick box for a window without being able to turn it
    /// on — and it did exactly that. The "Breakout windows" list only cleared the
    /// ✕-dismissal while the real switch was a ★ on a card, and the blurb admitted it in
    /// passing ("each still needs its ⭐ star"). A tick box that needs a second,
    /// unadvertised step is the "silent no-ops are broken" rule with the switch on the
    /// other side, and it became a recurring Reddit question about how on earth to get
    /// the pet damage window (relayed by David, 2026-08-20).
    ///
    /// Watch is the null: it opens for any 📌-pinned rule, which is a thing the player
    /// has to pick rather than a switch Options can flip for them.
    /// </summary>
    public static string? StarKey(string kind) => kind switch
    {
        Damage => "dps",
        Healing => "hps",
        Pet => "pet",
        Loot => "loot",
        // The Progress card's star is the xp one — the same key gates its mini chip.
        Progress => "xp",
        // "buffs" renders no mini chip at all (MiniStatOrder skips it), so this key
        // exists only to gate the window. It is the proof that the two concepts are
        // separable — and the reason unticking must not strip a star, since for every
        // OTHER kind the same key is also a cell in the minimised pill.
        Buffs => "buffs",
        _ => null,
    };

    /// <summary>The kind for a <c>BreakoutKind</c> member, whichever UI's enum it came
    /// from. The two enums disagree about membership but not about spelling.</summary>
    public static string Kind(Enum breakoutKind) => Kind(breakoutKind.ToString());

    /// <summary>Same, from the enum member's NAME — which is also the key
    /// <c>AppSettings.DisabledBreakouts</c> stores.</summary>
    public static string Kind(string enumMemberName) => enumMemberName.ToLowerInvariant();

    /// <summary>What to say under the list. Names the one condition Options cannot set
    /// for you, rather than the old blanket "each still needs its ⭐ star" — which was
    /// true of every row and therefore explained none of them.</summary>
    public const string Blurb =
        "Floating windows that open while the widget is minimised. Ticking one turns it "
        + "on — it also stars that stat, so it appears in the mini pill too. Untick to "
        + "stop the window opening; the star stays, so anything you keep in the pill "
        + "stays put.";

    /// <summary>The Watch row's extra sentence: the one window a tick cannot finish
    /// switching on.</summary>
    public const string WatchNote =
        "Watch opens for any rule you have pinned in Options → Watch rules — pin one "
        + "there and it appears while minimised.";

    /// <summary>The pet title with the pet's name and charm-hold suffix when there is
    /// one — "Pet damage — Gnoll Pup (held 2:14)". Both UIs built this string themselves.</summary>
    public static string PetTitle(string? petName, DateTime? charmedSince, DateTime now) =>
        petName is { Length: > 0 } name
            ? $"{Title(Pet)} — {name}" + CharmHoldText.Suffix(charmedSince, now)
            : Title(Pet);
}
