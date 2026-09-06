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
    /// different reason again: <c>dps</c>, <c>hps</c> and <c>xp</c> are the always-on HUD
    /// numbers now, so no star exists to gate them and the Options tick is the whole
    /// switch. <c>AppSettings.MigratePromotedHudStats</c> is what carries each player's
    /// old star into <c>DisabledBreakouts</c> before the keys go, so an open window stays
    /// open and a closed one stays closed.
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
    /// **The heading this list sits under, said once for both hosts.** It read "Breakout
    /// windows" until 2026-09-05 (SR-3): `\bbreakouts?\b` is a row of the signed terminology
    /// ban, and the Settings block that renders this list serves the Evolved shell as well as
    /// v1 <c>OptionsWindow</c>, so it has ONE string set and that set has to pass in shell
    /// scope. "Floating windows" is the blurb's own first two words.
    ///
    /// It is a const rather than a literal in the block because it is also a ROUTE: three
    /// other surfaces tell a player where to re-enable a window they dismissed, and a heading
    /// renamed without them is #219's mechanism inside a sentence (SR-2 caught the identical
    /// thing in <c>GearChecklistPresentation.EmptyRoute</c>). See <see cref="ReEnableRoute"/>.
    /// </summary>
    public const string Heading = "Floating windows";

    /// <summary>Where a ✕-dismissed window comes back from, in the words the screen actually
    /// shows. Named from <see cref="Heading"/> so the two cannot drift: the alert banner, the
    /// error log and the ✕ tooltip all print this, and none of them can see the heading.</summary>
    public const string ReEnableRoute = "Options → " + Heading;

    /// <summary>The ✕'s own tooltip, on every one of these windows.</summary>
    public const string DismissTip =
        "Hide this window for good (its star chip stays; re-enable under " + ReEnableRoute + ")";

    /// <summary>The row's hover text on the Settings HUD block, keyed on the kind
    /// rather than inferred from whether a star exists (see
    /// <see cref="NeedsPinnedRule"/>).</summary>
    public static string Note(string kind) => kind switch
    {
        Watch => WatchNote,
        Damage or Healing => PromotedNote,
        _ => StarNote,
    };

    /// <summary>For a kind that still has a ★. Says the second thing the tick does, since
    /// it is doing it on the player's behalf.</summary>
    public const string StarNote =
        "Opens while EQBuddy is minimised. Ticking this also stars the stat, so it "
        + "shows on the HUD too.";

    /// <summary>For Damage and Healing, whose stats are on the HUD whatever this says.
    /// Naming the removed toggle rather than only the replacement is the #233 rule.</summary>
    public const string PromotedNote =
        "Opens while EQBuddy is minimised. DPS and HPS are always-on HUD numbers now, "
        + "so there is no star to set and this tick is the whole switch.";

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
        "Floating windows that open while EQBuddy is minimised. Ticking one turns it "
        + "on — where the stat still has a star, it sets that too, so it appears on the "
        + "HUD as well. Untick to stop the window opening; the star stays, so anything "
        + "you keep on the HUD stays put.";

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
