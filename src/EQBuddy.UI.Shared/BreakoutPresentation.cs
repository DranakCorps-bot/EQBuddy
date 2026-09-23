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
    /// has to pick rather than a switch Options can flip for them — see
    /// <see cref="NeedsPinnedRule"/>, which is what tells that null apart from the others.
    ///
    /// **Damage, Healing and Progress are null since Surface A / SA-1**, and for a
    /// different reason again: the Options tick is the whole switch for those three, and
    /// <c>AppSettings.MigrateHudStatStars</c> is what carries a pre-SA-1 player's star into
    /// <c>DisabledBreakouts</c> before the keys land, so an open window stays open and a
    /// closed one stays closed.
    ///
    /// **They stay null through DRA-81, and that is a decision rather than an oversight.**
    /// <c>dps</c>, <c>hps</c> and <c>xp</c> are <c>MiniStats</c> keys again — the Founder
    /// LOCK put their ★s back — so the old inference "no key, so no star to read" no longer
    /// holds and something has to say what the new star does NOT do. It decides whether the
    /// HUD draws that metric, and nothing else. Re-pointing this table at it would mean
    /// unticking DPS in the Mini dashboard silently closed somebody's Damage window: a
    /// window and a HUD slot are different objects, and one switch quietly doing both is the
    /// "tick box that lies" with the lie on the other side.
    /// </summary>
    public static string? StarKey(string kind) => kind switch
    {
        Pet => "pet",
        Loot => "loot",
        // "buffs" renders no HUD cell at all (MiniBarPresentation.Order skips it), so this
        // key exists only to gate the window. It is the proof that the two concepts are
        // separable — and the reason unticking must not strip a star, since for every
        // OTHER kind that still has one the same key is also a cell on the HUD.
        Buffs => "buffs",
        _ => null,
    };

    /// <summary>Watch, and only Watch: the one kind whose window needs something Options
    /// cannot tick for you.
    ///
    /// It exists because a null <see cref="StarKey"/> used to MEAN "this is Watch" — three
    /// kinds joined that null in SA-1 and the inference stopped holding. Reading it as
    /// Watch would have told a Damage row that it opens for a pinned rule, which is the
    /// "tick box that lies" this screen already had to fix once.</summary>
    public static bool NeedsPinnedRule(string kind) => kind == Watch;

    /// <summary>
    /// The ✕'s own tooltip, on every one of these windows.
    ///
    /// **It named "Options → Floating windows" until DRA-352 D2 (2026-09-23)**, which was the
    /// route to the one persistent switch. The Founder asked for that list off Options, so
    /// the switch moved onto the window as the pin beside this ✕, and the sentence follows
    /// it — a route naming a control the change removed is #219's mechanism inside one
    /// sentence. The ✕ itself is unchanged: a transient close that writes nothing (OE-7,
    /// <c>BreakoutCloseTests</c>).
    /// </summary>
    public const string DismissTip =
        "Close this for now — its HUD chip brings it straight back. "
        + "The pin beside this ✕ is where you stop it opening on its own.";

    /// <summary>
    /// The pin's tooltip while the window DOES open by itself — says what it does now and
    /// what a click changes, because a pin is a glyph and a glyph states nothing.
    /// </summary>
    public const string AutoOpenOnTip =
        "Pinned: opens by itself while EQBuddy is minimised. Click to stop that — its HUD "
        + "chip still brings it up whenever you want it.";

    /// <summary>The pin's tooltip while the window waits to be asked for.</summary>
    public const string AutoOpenOffTip =
        "Not pinned: opens only when you ask (its HUD chip). Click to have it open by itself "
        + "while EQBuddy is minimised.";

    /// <summary>The pin's whole tooltip for a kind in a state: the state sentence, then the
    /// kind's own note about what else the pin does (<see cref="Note"/>).</summary>
    public static string AutoOpenTip(string kind, bool on) =>
        (on ? AutoOpenOnTip : AutoOpenOffTip) + " " + Note(kind);

    /// <summary>The pin's second sentence, keyed on the kind rather than inferred from
    /// whether a star exists (see <see cref="NeedsPinnedRule"/>). These were the Options
    /// list's row tooltips until DRA-352 D2 moved the switch onto the window.</summary>
    public static string Note(string kind) => kind switch
    {
        Watch => WatchNote,
        Damage or Healing => PromotedNote,
        _ => StarNote,
    };

    /// <summary>For a kind that still has a ★. Says the second thing pinning does, since
    /// it is doing it on the player's behalf.</summary>
    public const string StarNote =
        "Pinning also stars the stat, so it shows on the HUD too; unpinning leaves the star.";

    /// <summary>For Damage and Healing, the two kinds whose pin does NOT also set a star.
    ///
    /// **It used to say "DPS and HPS are always-on HUD numbers now, so there is no star to
    /// set"**, which stopped being true with DRA-81's Founder LOCK — there is a star, it is
    /// in Mini dashboard, and this pin deliberately does not touch it. Saying where the
    /// other switch is matters more than it did: two switches that sound like one is how
    /// somebody flips the wrong thing and reports the window as broken.</summary>
    public const string PromotedNote =
        "Whether DPS and HPS show on the HUD is their own star in Options → Mini dashboard; "
        + "neither setting changes the other.";

    /// <summary>The Watch pin's extra sentence: the one window a pin cannot finish
    /// switching on.</summary>
    public const string WatchNote =
        "Watch also needs a rule you have pinned in Options → Watch rules before it appears "
        + "while minimised.";

    /// <summary>The kind for a <c>BreakoutKind</c> member, whichever UI's enum it came
    /// from. The two enums disagree about membership but not about spelling.</summary>
    public static string Kind(Enum breakoutKind) => Kind(breakoutKind.ToString());

    /// <summary>Same, from the enum member's NAME — which is also the key
    /// <c>AppSettings.DisabledBreakouts</c> stores.</summary>
    public static string Kind(string enumMemberName) => enumMemberName.ToLowerInvariant();

    /// <summary>The pet title with the pet's name and charm-hold suffix when there is
    /// one — "Pet damage — Gnoll Pup (held 2:14)". Both UIs built this string themselves.</summary>
    public static string PetTitle(string? petName, DateTime? charmedSince, DateTime now) =>
        petName is { Length: > 0 } name
            ? $"{Title(Pet)} — {name}" + CharmHoldText.Suffix(charmedSince, now)
            : Title(Pet);
}
