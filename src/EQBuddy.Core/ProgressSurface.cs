namespace EQBuddy.Core;

/// <summary>
/// The four progress surfaces, in the order every UI shows them — the Progress THEME
/// (docs/Themes.md; David ruled themes a direction on 2026-08-19, and this one goes first
/// because Gate 5b had already lifted four of its five cards).
///
/// **Entirely retrospective, which is why it is a window and not an overlay.** Nothing on
/// these tabs has a deadline attached: how much plat this camp has made, what the mote
/// ladder looks like, where faction stands, which raid targets are cleared. By the surface
/// rule that puts all of it on the phone and the desktop and none of it over the game —
/// and it is the theme the all-time-stats direction (#168/#159) plugs into, because
/// "across sessions" is the same question one level up.
///
/// Ordering and labels live here for the reason <see cref="QuestSurface"/> exists: one
/// definition of what the tabs ARE, or the three surfaces drift (#122, #152, #184).
/// </summary>
public enum ProgressTab
{
    /// <summary>XP rate, time to level, what a ding unlocked, skill-ups, AAs.</summary>
    Experience,
    /// <summary>Coin and motes — the two things a session accumulates that spend.</summary>
    Wealth,
    /// <summary>Standing, per faction, with the honest per-kill deltas.</summary>
    Faction,
    /// <summary>Raid targets cleared, witnessed or imported.</summary>
    Raids,
}

/// <summary>A tab as a UI should draw it. <see cref="Value"/> is the tab's headline —
/// "14.2% xp", "5p 1g", "0 / 21" — the number its card used to carry in its header, kept
/// so the tab strip answers at a glance what five separate card headers used to.</summary>
public sealed record ProgressTabHeader(ProgressTab Tab, string Label, string Key, string? Value);

/// <summary>
/// Builds the tab strip shared by the desktop Progress window and EQBuddy Mobile. Pure:
/// takes the already-computed headlines, returns headers.
/// </summary>
public static class ProgressSurface
{
    /// <summary>The canonical label for each tab.
    ///
    /// "Wealth" rather than "Money": the tab carries motes as well as coin, and motes are
    /// currency in Legends — a player who wants to know what the trip was worth should not
    /// have to know which of two cards held which half. That merge is the whole reason
    /// this tab exists rather than two.</summary>
    public static string LabelFor(ProgressTab tab) => tab switch
    {
        ProgressTab.Experience => "Experience",
        ProgressTab.Wealth => "Wealth",
        ProgressTab.Faction => "Faction",
        ProgressTab.Raids => "Raids",
        _ => tab.ToString(),
    };

    /// <summary>The wire/DOM key — lowercase and stable, so a saved tab choice survives a
    /// rename of the human-facing label.
    ///
    /// Experience keeps <c>progress</c>, the key the Progress card has always used in
    /// <c>SectionOrder</c>, <c>HiddenSections</c> and <c>EQBUDDY_EXPAND</c>. Step 5 of the
    /// recipe is "fold the old keys, PRESERVING position and hidden state": the theme
    /// inherits the card slot a player already placed rather than appearing at the bottom
    /// of their list, and a player who had Progress hidden still has it hidden.</summary>
    /// <summary>Inline (Bevel, Helm-signed 2026-08-22). Experience, Wealth and Faction
    /// are one-question rooms that fit a widget; Raids is a cleared/total ledger over six
    /// zones and reads as a line — <c>Raids — 12 / 29</c> — with the window one ⧉ away.
    ///
    /// **Wealth inline is COIN ONLY** (Helm's correction): the four
    /// <c>MoneyPresentation.SummaryLines</c>, no sold ledger and no mote rate. #227 settled
    /// that Wealth is coin and the Motes card owns the rate; the launcher line may still
    /// carry motes/hr, which is a different surface from this body.</summary>
    public static InlineMode InlineModeFor(ProgressTab tab) => tab switch
    {
        ProgressTab.Raids => InlineMode.Glance,
        _ => InlineMode.Full,
    };

    /// <summary>The tab an expanded Progress card opens on: the room that moves while you
    /// play.</summary>
    public const ProgressTab DefaultInlineTab = ProgressTab.Experience;

    public static string KeyFor(ProgressTab tab) => tab switch
    {
        ProgressTab.Experience => "progress",
        ProgressTab.Wealth => "wealth",
        ProgressTab.Faction => "faction",
        ProgressTab.Raids => "raids",
        _ => tab.ToString().ToLowerInvariant(),
    };

    public static ProgressTab? TabForKey(string? key) => key?.Trim().ToLowerInvariant() switch
    {
        "progress" or "experience" or "xp" => ProgressTab.Experience,
        // The two cards Wealth absorbs both resolve to it, so an old saved choice lands
        // somewhere true rather than nowhere.
        "wealth" or "money" or "motes" => ProgressTab.Wealth,
        "faction" => ProgressTab.Faction,
        "raids" => ProgressTab.Raids,
        _ => null,
    };

    /// <summary>The card keys this theme absorbs, in the widget's own vocabulary. The fold
    /// reads this so the list of what disappears lives in ONE place rather than being
    /// spelled again in each UI's settings migration.
    ///
    /// **"motes" LEFT THIS LIST on 2026-09-04 (#252, TiconaX), and the rule it is the first
    /// instance of: a fold may only name keys that are no longer cards.** Motes stopped
    /// being absorbed on 2026-08-21, when David gave it its own top-level card back — but
    /// this list was not told, so <c>FoldThemeSections</c> went on seeing a live catalog key
    /// in every profile's <c>SectionOrder</c>, judged itself stale, and re-ran on EVERY
    /// launch. Each run stripped "motes" out of <c>HiddenSections</c>, so a player who hid
    /// the card found it back the next time they started the app, forever.
    ///
    /// <c>OptionsViewModel.AbsorbedTitles</c> had already dropped Motes for the same reason
    /// and says so in as many words. Two lists describing one fold, and only one of them was
    /// updated — which is the whole hazard, and why
    /// <c>SectionFoldIdempotenceTests.No_fold_absorbs_a_key_that_is_still_a_card</c> now
    /// checks this list against the catalog rather than trusting either comment.
    ///
    /// <see cref="TabForKey"/> still resolves "motes" to Wealth, and must: that is about a
    /// saved TAB choice landing somewhere true, which has nothing to do with whether the
    /// card exists.</summary>
    public static readonly IReadOnlyList<string> AbsorbedCardKeys =
        ["progress", "money", "faction", "raids"];

    /// <summary>The key the folded theme takes — the one card slot the five collapse into.
    /// Deliberately one OF the absorbed keys rather than a new one; see
    /// <see cref="KeyFor"/>.</summary>
    public const string ThemeCardKey = "progress";

    public static IReadOnlyList<ProgressTabHeader> Tabs(
        string? experience = null, string? wealth = null,
        string? faction = null, string? raids = null)
    {
        return
        [
            Header(ProgressTab.Experience, experience),
            Header(ProgressTab.Wealth, wealth),
            Header(ProgressTab.Faction, faction),
            Header(ProgressTab.Raids, raids),
        ];

        static ProgressTabHeader Header(ProgressTab tab, string? value) =>
            new(tab, LabelFor(tab), KeyFor(tab), string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>The launcher card's one-line summary — step 3 of the recipe, and the line
    /// that has to justify replacing five card headers with one. Modelled on the Quests
    /// card's "Epic 0/486 · Sky 0/222": carry the numbers those headers carried, so the
    /// glance they answered still works without opening anything.
    ///
    /// **The line carries what MOVES WHILE YOU PLAY, and the tab badges carry the rest.**
    /// XP leads because it changes every fight; coin and the mote rate change every drop.
    /// Faction and raid clears are review-time facts — they still badge their own tabs,
    /// which is one click and no scrolling. That split is not a width trick, it is what a
    /// glance is FOR, and it was learned the hard way twice in one day: first the line was
    /// long enough to truncate mid-word, then it was trimmed by dropping the mote rate —
    /// and #219 (typical-usual-chaos) arrived within the hour saying motes/hr "was the
    /// most useful stat and the main reason I opened EQBuddy". A summary that replaces
    /// five card headers gets to choose WHICH numbers, not to quietly lose one.
    ///
    /// A part that has nothing to say is omitted rather than printed as a zero — five
    /// zeros would be noise on a fresh character, which is exactly who is looking at a
    /// fresh widget. That is also what keeps this line short for the players who are not
    /// farming motes.</summary>
    public static string LauncherSummary(
        string? xp = null, string? coin = null, int factions = 0, int raidsCleared = 0,
        string? motes = null)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(xp)) parts.Add(xp!);
        if (!string.IsNullOrWhiteSpace(coin)) parts.Add(coin!);
        // Straight after coin: motes ARE currency in Legends, and a farmer watches the
        // rate the way everyone else watches xp/hr.
        if (!string.IsNullOrWhiteSpace(motes)) parts.Add(motes!);
        if (factions > 0) parts.Add($"{factions} faction{(factions == 1 ? "" : "s")}");
        if (raidsCleared > 0) parts.Add($"{raidsCleared} raid{(raidsCleared == 1 ? "" : "s")}");
        return parts.Count > 0 ? string.Join(" · ", parts) : "no progress yet";
    }
}
