using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One cell of the minimized bar: which icon, and what it currently reads.</summary>
/// <param name="Key">The settings key (<see cref="AppSettings.MiniStats"/>).</param>
/// <param name="Icon">A name from <see cref="IconPaths"/> — never a glyph.</param>
/// <param name="Text">The formatted value.</param>
public sealed record MiniBarCell(string Key, string Icon, string Text);

/// <summary>
/// The minimized bar's contents, decided once (Gate 5c).
///
/// Both widgets carried this table by hand, identically, right down to the comments —
/// which stat gets which glyph, how each value is formatted, and what order they sit in.
/// Two copies of one decision is the shape every drift in this codebase has started from
/// (#184, #122, #152), and it was the densest remaining cluster of glyphs on the surface
/// that is on screen the ENTIRE time a player is farming.
///
/// The glyphs are gone. Every icon here already existed in <see cref="IconPaths"/>, so
/// this cost no new geometry — a fair sign the vectors were being ignored rather than
/// missing. On the Linux and macOS builds a glyph can fail to render altogether (#148,
/// #166), and the minimized bar is precisely where a player is not looking closely enough
/// to notice a box where a skull should be.
///
/// **Deliberately not a size decision.** This says what a cell CONTAINS, never how wide it
/// is. Both widgets are <c>SizeToContent</c>, so a value that changes width on a timer
/// asks the window manager to resize an always-on-top window over a fullscreen game —
/// which cost #173 its keyboard. Reserved widths belong to the bar that draws these, and
/// arrive with #191 (TheMegaSage) when its contents become configurable.
/// </summary>
public static class MiniBarPresentation
{
    /// <summary>The order cells appear in, whichever subset is switched on. Not the
    /// order the player picked them in: a bar that reshuffles as you toggle stats is a
    /// bar you have to re-read every time.
    ///
    /// "buffs" is deliberately absent — it is a valid <see cref="AppSettings.MiniStats"/>
    /// entry that gates the Buffs breakout window and never draws a cell here.
    ///
    /// **"xp", "dps" and "hps" are absent because they are drawn ELSEWHERE, not because
    /// they are unswitchable.** They are <see cref="GlanceKeys"/>: ★s again since DRA-81's
    /// Founder LOCK, listed for the player by <see cref="OptionKeys"/>, and rendered by
    /// <see cref="HudGlance"/> as metric SLOTS ahead of every cell in this list. Keeping
    /// them out of this table is what stops the bar drawing one of them twice — the same
    /// exclusion <see cref="DrawnKeys"/> applies to <see cref="PetKey"/> while it is
    /// inserted, and the reason is identical: a key is a cell OR a slot, never both at once.
    ///
    /// **<see cref="PetKey"/> is here and stays here, because pet damage is drawn
    /// unconditionally only SOMETIMES** (SIGNED #422). It is the one insertable member of
    /// that row: while <see cref="AppSettings.HudGlancePet"/> is set it draws up there
    /// instead, and the exclusion is <see cref="DrawnKeys"/>'s — this table still has to be
    /// able to put a face on the key, because the moment the player drags it back down it
    /// is a cell again.</summary>
    public static readonly IReadOnlyList<string> Order =
        ["kills", "pet", "procs", "loot", "motes", "money", "deaths"];

    /// <summary>
    /// The stats the collapsed HUD draws as METRIC SLOTS on its top row rather than as
    /// cells, in <see cref="HudGlance.Read"/>'s own order — DPS, HPS, then the XP rate.
    ///
    /// **They are ★s like everything else since the FOUNDER LOCK of 2026-09-14** (DRA-81).
    /// Surface A / SA-1 promoted them to "always on" and deleted their switches; the
    /// Founder's smoke is what an unswitchable row costs when the one number they wanted was
    /// the one the app had decided to withhold. So the keys are back in
    /// <see cref="AppSettings.MiniStats"/>, this is the list that names them, and
    /// <see cref="HudGlanceStars.From"/> is the one place a ★ becomes a slot.
    ///
    /// **<see cref="PetKey"/> is deliberately NOT here.** It appears on that row too, but it
    /// is in <see cref="Order"/> because it is a cell that can be MOVED up — one key, one ★,
    /// two possible homes, chosen by a drag (SIGNED #422). These three have exactly one home
    /// and no drag, so listing them here keeps "which row is this stat on" a question with a
    /// single answer per key.
    /// </summary>
    public static readonly IReadOnlyList<string> GlanceKeys = [HudGlance.DpsKey, HudGlance.HpsKey, HudGlance.XpKey];

    /// <summary>
    /// Every ★ the Mini dashboard offers, in the order that screen lists them: the top row's
    /// slots first (<see cref="GlanceKeys"/>), then the cells (<see cref="Order"/>) — which
    /// is the order they appear on the bar itself, read left to right and top row first.
    ///
    /// **It exists because the two lists have different jobs and the SCREEN needs both.**
    /// <see cref="Order"/> is a formatting table ("which stats can this class turn into a
    /// cell"); a screen that listed ★s out of it could only ever offer the stats that happen
    /// to be cells, which is exactly the hole SA-1 left — three switches that existed in the
    /// profile with nothing anywhere to set them. Options walks THIS list, and
    /// <see cref="DrawnKeys"/> still walks the other, so a key can gain a ★ without gaining
    /// a cell.
    /// </summary>
    public static readonly IReadOnlyList<string> OptionKeys = [.. GlanceKeys, .. Order];

    /// <summary>The key the buff set's chip draws under. A <see cref="AppSettings.MiniStats"/>
    /// member since long before it drew anything, and deliberately absent from
    /// <see cref="Order"/> — see <see cref="CanonicalOrder"/> for why it has a PLACE here
    /// without having a row in any table above.</summary>
    public const string BuffsKey = "buffs";

    /// <summary>Pet damage — the one key that can be drawn by the always-on row INSTEAD of
    /// by a cell (SIGNED #422). Named rather than spelled at each of the four places that
    /// ask about it, because "pet" is also a <see cref="HudExpand.Key"/>, a
    /// <c>MiniBarOrder</c> entry and a ★, and a literal cannot say which one is meant.</summary>
    public const string PetKey = "pet";

    /// <summary>
    /// Every key that can sit on the bar, in the order an untouched profile draws them —
    /// the floor <see cref="AppSettings.MiniBarOrder"/> means by "empty".
    ///
    /// **It is <see cref="Order"/> plus "buffs", and the difference between the two lists is
    /// the point.** <see cref="Order"/> is a FORMATTING table: which stats this class can
    /// turn into an icon and a string. "buffs" is not one of them and cannot be — there is
    /// no buff state on <see cref="StatsSnapshot"/> at all, so <c>HudBarView</c> builds that
    /// chip's face from the buff tracker's own count (OE-7). But it is a chip on the bar
    /// like any other, so it has a PLACE, and a place is what an order is about. Its
    /// canonical slot is where it has always drawn: after "deaths".
    ///
    /// The top row's own slots (name, DPS, HPS, XP%/hr) are absent, and **since DRA-81 the
    /// reason is ORDER rather than the absence of a ★**. They have their ★s back
    /// (<see cref="GlanceKeys"/>) and the player sets them in the same list as everything
    /// else — what they do not have is a PLACE to argue about: that row's order is fixed in
    /// <see cref="HudGlance.Read"/> and no drag reaches it, so a stat key in this list would
    /// be an order nothing reads. <see cref="PetKey"/> is the one key on both rows, and it is
    /// here because its cell CAN be carried (SIGNED #422). Pinned watch chips are absent
    /// too — they
    /// are a BLOCK after the cells, one per rule, and per-rule placement would widen this
    /// list by rule id rather than by stat key. Both seams are named rather than built.
    ///
    /// **<see cref="PetKey"/> keeps its place here even while it is drawn up there** (SIGNED
    /// #422). That is the whole of "never lost": <see cref="ResolveOrder"/> is untouched by
    /// <see cref="AppSettings.HudGlancePet"/>, so a pet chip ejected back into the cells
    /// lands where the player last left it rather than where the canonical list would put it.
    /// </summary>
    public static readonly IReadOnlyList<string> CanonicalOrder = [.. Order, BuffsKey];

    /// <summary>
    /// The player's chip order — every key of <see cref="CanonicalOrder"/>, exactly once.
    ///
    /// A key the setting omits is APPENDED in its canonical position rather than dropped, so
    /// a later release's new stat lands ON the bar instead of in a hole, and a stale file
    /// cannot silently lose a cell (trap 20's shape). Unknown names are skipped and
    /// duplicates collapse to their first appearance, so a hand-edited file cannot produce a
    /// bar that draws one chip twice. This is <c>HudChipRow.ResolveOrder</c>'s rule, stated
    /// once more for a list of stat keys rather than of families.
    /// </summary>
    public static IReadOnlyList<string> ResolveOrder(AppSettings settings)
    {
        var order = new List<string>();
        foreach (var name in settings.MiniBarOrder)
        {
            var key = CanonicalOrder.FirstOrDefault(
                k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
            if (key is not null && !order.Contains(key)) order.Add(key);
        }
        foreach (var key in CanonicalOrder)
            if (!order.Contains(key)) order.Add(key);
        return order;
    }

    /// <summary>Writes a new chip order into the profile. The WRITER half of
    /// <see cref="AppSettings.MiniBarOrder"/>, shipping in the same PR as its reader — the
    /// <c>DeadSettingTests</c> posture, which exists because three player-facing bugs came
    /// from data that survived a move and a write path that did not.
    ///
    /// The one caller is the mini bar's DROP. There is deliberately no second writer: two
    /// surfaces editing a brand-new setting on day one is the shape that produced #252.</summary>
    public static void SetOrder(AppSettings settings, IEnumerable<string> order) =>
        settings.MiniBarOrder = [.. order];

    /// <summary>
    /// The keys the bar actually DRAWS, in the player's order: <see cref="ResolveOrder"/>
    /// minus the stats with no ★, minus anything this class cannot put a face on.
    ///
    /// **The one membership decision, so the bar cannot draw a chip the order does not
    /// know about** (trap 4). <c>HudBarView</c> walks this list and asks <see cref="Cell"/>
    /// for each face, except <see cref="BuffsKey"/>, whose face it builds itself.
    ///
    /// **…minus <see cref="PetKey"/> while it is INSERTED into the always-on row** (SIGNED
    /// #422 §2). SA-1's own sentence — *"a key that is drawn unconditionally has no business
    /// in a table whose whole job is 'which subset did the player switch on'"* — now applies
    /// to pet conditionally, and it applies HERE rather than in the view: a view that skipped
    /// the key itself would be a second membership decision beside this one (trap 4, one
    /// layer up), and it is the only thing standing between a player and a bar that draws
    /// their pet's damage twice.
    ///
    /// **The ★ gets no vote while it is inserted, and the asymmetry is deliberate** (§2): an
    /// inserted pet slot is always-on like its neighbours, so un-starring pet changes the
    /// cells and nothing else. Options says so in a sentence, because a click with no visible
    /// effect otherwise reads as broken.
    /// </summary>
    public static IReadOnlyList<string> DrawnKeys(AppSettings settings)
    {
        var on = new HashSet<string>(settings.MiniStats, StringComparer.Ordinal);
        return
        [
            .. ResolveOrder(settings)
                .Where(key => !(settings.HudGlancePet && key == PetKey))
                .Where(on.Contains)
                .Where(key => key == BuffsKey || Icons.ContainsKey(key)),
        ];
    }

    /// <summary>A key list as one space-free token for the <c>EQBUDDY_EXPAND</c> dump
    /// ("money,kills,loot"), or "-" when the bar has no chips at all. The dump is
    /// space-separated key=value, so a value with a space in it would silently become two
    /// keys; "-" rather than "" because a key with an empty value cannot be waited on.</summary>
    public static string OrderKey(IEnumerable<string> keys) =>
        string.Join(",", keys) is { Length: > 0 } key ? key : "-";

    /// <summary>What each cell is CALLED, for the one screen that lists them.
    ///
    /// It had no such screen until 2026-08-21, and that was the hole. A stat's only switch
    /// was the star on its card header, so when the themes folded five cards into windows
    /// the switches went with them — and Options could only reach a star through its
    /// BREAKOUT checkbox, which exists for six kinds. Motes, money and kills have no
    /// breakout, so their stars became reachable only by opening the very window a player
    /// was complaining about ("hidden behind too much other junk I don't care about" -
    /// #228, daetien-lab). Same family as trap 20: the writers survived the fold, the ROUTE
    /// to them did not.</summary>
    public static readonly IReadOnlyDictionary<string, string> Names =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // The three top-row slots (DRA-81). They are in this table and NOT in `Icons`,
            // which is the pair that decides where a key draws: `Cell` refuses anything
            // `Icons` cannot face, so naming them here gives Options a row to list without
            // giving the cell loop a chip to draw. Their icons are HudGlance's own constants.
            [HudGlance.DpsKey] = "DPS",
            [HudGlance.HpsKey] = "HPS (healing)",
            [HudGlance.XpKey] = "XP per hour",
            ["kills"] = "Kills",
            ["pet"] = "Pet damage",
            ["procs"] = "Weapon procs",
            ["loot"] = "Loot",
            ["motes"] = "Motes",
            ["money"] = "Coin",
            ["deaths"] = "Deaths",
        };

    /// <summary>Stat key → <see cref="IconPaths"/> name.</summary>
    public static readonly IReadOnlyDictionary<string, string> Icons =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kills"] = "Skull",
            ["pet"] = "Paw",
            ["procs"] = "Bolt",
            ["loot"] = "Bag",
            ["motes"] = "Sparkle",
            ["money"] = "Coin",
            ["deaths"] = "Skull",
        };

    /// <summary>One cell's face — its icon and what it currently reads — or null when this
    /// table cannot format the key.
    ///
    /// Null is a real answer and has two readings, both of which the caller handles the same
    /// way: a settings file from a LATER version naming a stat this release has never heard
    /// of (which must be skipped rather than drawn blank — a hole in the bar), and
    /// <see cref="BuffsKey"/>, which is a chip the bar builds for itself.</summary>
    public static MiniBarCell? Cell(StatsSnapshot s, string key) =>
        Icons.TryGetValue(key, out var icon) ? new MiniBarCell(key, icon, Text(s, key)) : null;

    /// <summary>What one cell reads. Every format here was already agreed by both
    /// widgets; the point is that it is now agreed in one place.</summary>
    public static string Text(StatsSnapshot s, string key) => key switch
    {
        "kills" => $"{s.YourKillCount}",
        // No "dps"/"hps"/"xp" rows: those three draw as HudGlance SLOTS and HudGlance
        // formats them. They have ★s again since DRA-81, but a ★ decides whether a stat
        // shows and never which class writes its string — a second formatter here would be
        // two sources for one number (trap 4), and the day one of them gained a decimal only
        // the other would move.
        // The VALUE is StatsSnapshot's since SIGNED #422 and the SHAPE is this table's: the
        // always-on row can draw pet damage too now, and two copies of one expression is
        // the very thing the comment above forbids for dps/hps. Compact here, padded into
        // HudGlance's fixed 10-character shape up there.
        PetKey => $"{s.PetDps:0.#} dps",
        // Same denominator as the Procs card: combat minutes, so downtime doesn't
        // flatter the weapon.
        "procs" => $"{s.Procs.Sum(p => p.Count) / Math.Max(1.0 / 60, s.CombatSeconds / 60.0):0.#}/min",
        "loot" => $"{s.LootTotal}",
        "motes" => Motes.Summarize(s.Loot, s.Elapsed) is { Total: > 0 } mo
            ? $"{mo.Total} · {mo.PerHour:0.#}/hr" : "0",
        "money" => StatsSnapshot.FormatCoin(s.Copper),
        "deaths" => $"{s.Deaths.Count}",
        _ => "",
    };
}
