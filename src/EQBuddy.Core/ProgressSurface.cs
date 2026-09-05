namespace EQBuddy.Core;

/// <summary>
/// The progress surfaces, in the order every UI shows them — the Progress THEME
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

    /// <summary>
    /// **Your career: every sitting this character has stored, and the ladders they add
    /// up to.** The Progress half of <c>HistoryWindow</c>'s merge (Bevel's History
    /// pre-design §1, Helm-signed 2026-09-05 ~10:10 AM CT) — the *"which past sessions"*
    /// browse and the cross-session level/AA step charts, which are the two career jobs
    /// the signed disposition table names outright.
    ///
    /// **It is the first tab in this enum that is not on every surface**, which is a new
    /// kind of row rather than a fifth of the same kind — see
    /// <see cref="DesktopShellOnly"/>, which is where that difference is decided and the
    /// only place it may be.
    /// </summary>
    History,
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
        // "History", the word the v1 window and its one context-menu door already use.
        // A new word here ("Sessions", "Career") would be a second name for a surface a
        // player can still reach under the old one, which is trap 33 lifted into naming.
        ProgressTab.History => "History",
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
        // <see cref="ProgressTab.History"/> never reaches an inline card — the widget's
        // Progress card filters <see cref="DesktopShellOnly"/> out before it builds a chip
        // — so this answer is unreachable rather than chosen. It is left as the total
        // function's default deliberately: inventing a Glance body for a tab no inline
        // host draws would be a second description of a surface with no host to describe.
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
        ProgressTab.History => "history",
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
        // "sessions" is what the v1 window's door is called ("Session history…"), so the
        // word a player would type lands on the room that answers it.
        "history" or "sessions" => ProgressTab.History,
        _ => null,
    };

    /// <summary>
    /// **Which of these tabs the Evolved reshape hands to another room** — today exactly
    /// one, <see cref="ProgressTab.Raids"/>, which belongs to <see cref="LiveTab.Raids"/>
    /// ("Progress (Experience / Wealth / Faction / Raids) — Reshape", the Helm-signed IA
    /// table).
    ///
    /// **A TOTAL FUNCTION over the enum rather than a second list of tabs, and that is the
    /// whole point.** The obvious shape here is a `ShellTabs` array beside
    /// <see cref="Tabs"/> — and two hand-maintained lists describing one arrangement is
    /// precisely trap 55, which cost #252: <c>AbsorbedCardKeys</c> and
    /// <c>OptionsViewModel.AbsorbedTitles</c> described one fold, only one of them was told
    /// when Motes stopped being absorbed, and every launch afterwards un-hid a card the
    /// player had hidden. Written this way a fifth Progress tab appears in the Evolved room
    /// automatically, because the default answer is "no, it stayed".
    ///
    /// **It says nothing about the v1 surfaces, deliberately.** <c>ProgressWindow</c> and
    /// the widget's inline Progress card still draw four tabs and must: taking Raids off
    /// them would be a v1 SUBTRACTION, which is gated per item on the shell room existing,
    /// a HUD chip shipping and a screenshot — a later PR by construction. So this predicate
    /// is read by the two hosts that follow the ROOM (the shell's Progress room and the
    /// phone's Progress screen, which Bevel's §3 requires move in the same commit) and by
    /// nothing else. <see cref="TabForKey"/> still resolves <c>"raids"</c>, and must: that
    /// is about an old saved tab choice landing somewhere true, exactly as <c>"motes"</c>
    /// still resolves to Wealth long after Motes became a card again.
    /// </summary>
    public static bool MovedToLive(ProgressTab tab) => tab == ProgressTab.Raids;

    /// <summary>
    /// **The second kind of row this module carries, and it is genuinely a new kind rather
    /// than a fifth tab** — Bevel's History pre-design §4 named the gap in as many words:
    /// <see cref="MovedToLive"/> is a filter for *"this tab left Progress entirely,
    /// everywhere"*, and there was no filter for *"this tab exists on Progress but only on
    /// the Evolved desktop shell"*. The signed disposition table's Why column asks for
    /// exactly that — *"History studio depth stays desktop-only"* — and Helm signed the new
    /// row kind on 2026-09-05 (~10:10 AM CT).
    ///
    /// **True means EXACTLY ONE host draws it: the Evolved shell's Progress room.** Four
    /// hosts read this module and three of them filter this predicate OUT:
    ///
    ///  * <c>ProgressRoom</c> (the Evolved shell) — DRAWS it. The only one.
    ///  * <c>CompanionProjection</c> (the phone) — refuses it. This is the half Bevel's §4
    ///    is about: a session browser with a filterable list, per-session detail and two
    ///    step charts is desk work, and the phone is the look-away surface.
    ///  * <c>ProgressWindow</c> (the v1 pop-out) — refuses it. Not because it is not a
    ///    desktop, but because <c>HistoryWindow</c> is already the desktop's career surface
    ///    and it keeps its one context-menu door this pass (Helm, same sign, item 5). A
    ///    third host for one surface is trap 58's shape before it is anything else.
    ///  * <c>ProgressThemeCard</c> (the widget's inline card) — refuses it. A 320-unit card
    ///    cannot hold a list beside a detail pane, and the ⧉ on its header opens the window
    ///    rather than losing anything.
    ///
    /// **A TOTAL FUNCTION over the enum, for <see cref="MovedToLive"/>'s own reason** (trap
    /// 55): the default answer is "no, every host draws it", so a sixth Progress tab
    /// appears everywhere automatically and only a deliberate row here holds it back.
    ///
    /// **The two predicates are independent and must stay so.** A tab could in principle be
    /// both; nothing here assumes otherwise, and each host applies the one that is about
    /// it. <c>ShellPages.Rooms(Progress)</c> filters <see cref="MovedToLive"/> only — the
    /// shell's address grammar must reach every room the shell draws, and
    /// <c>progress:history</c> is one.
    /// </summary>
    public static bool DesktopShellOnly(ProgressTab tab) => tab == ProgressTab.History;

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
        string? faction = null, string? raids = null, string? history = null)
    {
        return
        [
            Header(ProgressTab.Experience, experience),
            Header(ProgressTab.Wealth, wealth),
            Header(ProgressTab.Faction, faction),
            Header(ProgressTab.Raids, raids),
            // Last, and not by accident: the other four are what this sitting is doing and
            // History is what every sitting before it added up to. A career browser between
            // Wealth and Faction would put the review surface inside the live ones.
            Header(ProgressTab.History, history),
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
