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

    /// <summary>The pet title with the pet's name and charm-hold suffix when there is
    /// one — "Pet damage — Gnoll Pup (held 2:14)". Both UIs built this string themselves.</summary>
    public static string PetTitle(string? petName, DateTime? charmedSince, DateTime now) =>
        petName is { Length: > 0 } name
            ? $"{Title(Pet)} — {name}" + CharmHoldText.Suffix(charmedSince, now)
            : Title(Pet);
}
