using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One row of an under-bar peek: a name, the value beside it, and how full its
/// gauge is.</summary>
/// <param name="Share">0–1, for the row's under-bar. It is a SHARE of the biggest row
/// rather than of the total, which is what every other breakdown list in the app draws —
/// EXCEPT <see cref="HudExpandPeek.Money"/>, whose rows are parts of one figure and are
/// therefore drawn against the session total. That difference is named on the builder, and
/// a builder that draws no gauge at all says 0 there: the host clamps a bar to a 1% floor,
/// so **0 is a visible sliver rather than nothing** and is only honest when the row has no
/// share to state.</param>
public sealed record PeekRow(string Name, string Value, double Share, string? Tooltip = null);

/// <summary>
/// A peek body: the line under the header, the rows, the empty explanation when there are
/// none, and the repaint gate's key.
/// </summary>
/// <param name="Empty">Non-null means there is nothing to draw and this says why. The rows
/// list is empty in that case; a caller never has to check both.</param>
/// <param name="Signature">The repaint gate. **Nothing in it ticks** (trap 8): a key that
/// carried a countdown would rebuild the panel every second and throw away whatever the
/// pointer was over.</param>
public sealed record PeekBody(
    string Subtext, IReadOnlyList<PeekRow> Rows, string? Empty, string Signature);

/// <summary>
/// WHAT THE UNDER-BAR PANEL SHOWS for the three kinds that are not a meter — Watch, Loot
/// and the buff set (OE-7).
///
/// Damage, Healing and Pet already had one builder for this: <see cref="LivePresentation.Meter"/>,
/// which the float and the panel both ask. These three had none, because before OE-7 they
/// had no panel — so the choice was to write their rows inline in <c>HudExpandWindow</c> or
/// to put them here. Inline would have been a second producer of "what does the Watch
/// surface mean" (trap 33) and, worse, would have been unreachable by a test: the WPF layer
/// has no unit tests (docs/TestPlan.md §5), so a rule that only exists in a window is a rule
/// nothing can check.
///
/// <see cref="Watch"/> is read by the FLOAT as well as by the panel, which is what makes it
/// a shared decision rather than a second copy with a shorter list. <see cref="Loot"/> reads
/// the same <c>MainWindow.TargetDropsContent</c>/<c>TargetEmptyNote</c> calls the float's own
/// Target scope makes, for the same reason — a mini-bar chip and its pop-out must answer
/// "what can this creature drop" identically (#392, which landed the target re-scope while
/// OE-9 was building — that half is Bevel's and this seat took it as it stands). And
/// <see cref="Procs"/> is read by the Damage float's new procs block. Only
/// <see cref="Buffs"/> is deliberately NOT what its float draws, and that is named rather
/// than implied — see it below.
///
/// **OE-9 added five more, and none of them has a float of its own.** Motes, Kills, Procs,
/// Money is the rest of <see cref="MiniBarPresentation.Order"/>, and the owner's
/// ~1:29 PM CT amend (2026-09-07) is that every cell on the bar peeks and pops out. Each one
/// takes the SAME numbers its full surface already shows and the SAME denominators — the
/// motes summary's own hours, the Procs card's combat minutes (#85), the Wealth tab's coin
/// facts — because a peek that computed a rate its own ⧉ then disagreed with would be trap
/// 4 across two windows a click apart.
///
/// **Framework-free**, like everything else in this folder: rows are data, and the host
/// turns them into controls.
/// </summary>
public static class HudExpandPeek
{
    /// <summary>Rows sorted, capped and gauged from a name→count table. The three builders
    /// below differ only in what they count, so the arithmetic is written once — a second
    /// copy of "share of the biggest" is how two lists start disagreeing about which row is
    /// full.</summary>
    private static List<PeekRow> Gauged<T>(
        IEnumerable<T> source, Func<T, double> weight,
        Func<T, string> name, Func<T, string> value, Func<T, string?>? tooltip = null)
    {
        var ordered = source.OrderByDescending(weight).ToList();
        if (ordered.Count == 0) return [];
        var top = Math.Max(1e-9, ordered.Max(weight));
        return [.. ordered.Select(x =>
            new PeekRow(name(x), value(x), weight(x) / top, tooltip?.Invoke(x)))];
    }

    /// <summary>
    /// The Watch peek: every 📌-pinned rule, its count and its per-hour rate.
    ///
    /// **The FLOAT reads this too** (<c>BreakoutWindow.UpdateWatch</c>). The pinned-rule
    /// filter, the subtext's phrasing and the empty line were written in that window and
    /// nowhere else; giving the panel its own copy would have meant two answers to "which
    /// rules does the Watch surface mean", both current, differing the day the pin rule
    /// changes — which is trap 33 exactly, and SA-R changed that pin rule once already.
    /// </summary>
    public static PeekBody Watch(
        IEnumerable<TrackedRule> rules, IReadOnlyList<TrackedRuleResult> tracked)
    {
        var pinned = rules.Where(r => r.Enabled && r.Pinned)
            .Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var hits = tracked.Where(t => pinned.Contains(t.Id)).ToList();
        var total = hits.Sum(r => r.TotalQuantity);
        var subtext = $"Session · {hits.Count} pinned rule{(hits.Count == 1 ? "" : "s")} · {total} total";

        if (hits.Count == 0)
            return new PeekBody(subtext, [], "Pin a watch rule in Options to track it here.",
                "watch|empty");

        var rows = Gauged(hits, r => r.TotalQuantity, r => r.Name,
            r => $"{r.TotalQuantity} · {r.PerHour:0.#}/hr",
            r => r.LastItem is { Length: > 0 } li ? $"last: {li}" : null);
        return new PeekBody(subtext, rows, null,
            "watch|" + string.Join(",", hits.Select(r => $"{r.Id}:{r.TotalQuantity}:{r.LastItem}")));
    }

    /// <summary>
    /// The Loot peek: what your CURRENT TARGET can drop — your observed counts leading,
    /// the wiki's behind — never the session list.
    ///
    /// **This used to be session loot, and that was the wrong fact for a bar chip named
    /// "Loot" to answer.** <c>LootBreakoutView</c>'s own pop-out already defaults to Target
    /// scope (David's spec for that window from the start), so a peek that showed Session
    /// while its float defaults to Target was the same chip naming two different questions
    /// depending on which surface you opened — trap 33's shape one level up, and the reason
    /// the three parameters below come from the SAME <c>MainWindow.TargetDropsContent</c>
    /// call <c>LootBreakoutView.Render</c> makes for its own Target scope, rather than
    /// re-deriving "what can this creature drop" a second time.
    ///
    /// The gauge is deliberately flat (every row shares 1.0): an observed count
    /// ("4 this session · 30%") and a wiki rarity word ("common") are not one unit, and
    /// drawing a proportional bar across them would assert a comparison that is not there —
    /// the same reasoning <see cref="Buffs"/> uses to sort an unknown duration last rather
    /// than guess it forward.
    /// </summary>
    /// <param name="names">Who you're fighting (or last considered), or "" for no target —
    /// <c>MainWindow.TargetDropsContent</c>'s own shape.</param>
    /// <param name="detail">The kill count / wiki-state suffix beside <paramref name="names"/>.</param>
    /// <param name="rows">Observed counts first, then the wiki's known drops.</param>
    /// <param name="emptyNote">What to say when there IS a target but nothing is known yet —
    /// <c>MainWindow.TargetEmptyNote</c>'s own wording, so a wiki-offline or no-page state
    /// reads the same here as it does on the float.</param>
    public static PeekBody Loot(
        string names, string detail, IReadOnlyList<(string Name, string Value)> rows,
        string emptyNote)
    {
        if (names.Length == 0)
            return new PeekBody("No target", [], LootPresentation.NoTargetNote, "loot|empty");

        var subtext = LootPresentation.TargetSubtitle(names, detail);
        if (rows.Count == 0)
            return new PeekBody(subtext, [], emptyNote, "loot|" + subtext + "|empty");

        var peekRows = rows.Select(r => new PeekRow(r.Name, r.Value, 1.0)).ToList();
        return new PeekBody(subtext, peekRows, null,
            "loot|" + subtext + "|" + string.Join(",", rows.Select(r => $"{r.Name}:{r.Value}")));
    }

    /// <summary>
    /// The buff-set peek: what is up right now, soonest to fade at the top.
    ///
    /// **Not what the float draws, and this one is a product difference rather than a
    /// density one.** The Buffs window is the SET EDITOR — per-class buckets, the honesty
    /// statuses, the suggestion rows, an add box. A peek cannot edit anything and must not
    /// pretend to, so it answers the one question a glance is for: what is running and how
    /// long have I got. The same split <c>BuffRosterPresentation</c>'s own header describes
    /// between the roster and the HUD chicklets.
    ///
    /// **The countdown IS in the signature, which is the opposite of what trap 8 usually
    /// asks for, and the exception is bought rather than assumed.** Trap 8's rule — keep
    /// values that drift every tick out of a gate — exists because a ticking key means a
    /// rebuild every second, and on a <c>SizeToContent</c> surface a rebuild is a RESIZE
    /// (trap 12 / #173). The panel that draws this has one fixed width as of OE-7, so a
    /// rebuild repaints identical geometry and costs five text blocks. The alternative was
    /// worse in kind rather than in degree: a clock left out of the gate is a clock that
    /// never updates, and a frozen countdown on a surface whose whole job is "how long have
    /// I got" is not a saving, it is a wrong answer.
    /// </summary>
    public static PeekBody Buffs(IReadOnlyList<BuffState> active, DateTime now)
    {
        var subtext = $"{active.Count} buff{(active.Count == 1 ? "" : "s")} up";
        if (active.Count == 0)
            return new PeekBody(subtext, [],
                "No buffs are up. They appear here as the log sees them land.", "buffs|empty");

        // Soonest to fade FIRST, which is the opposite of every other peek here — the rows
        // below are ordered by "biggest", and the urgent buff is the one with the least left.
        // A buff whose duration was never learned sorts last rather than first: "?" is not a
        // deadline, and putting an unknown at the top of a list about time running out would
        // be the surface asserting something it does not know.
        var ordered = active
            .OrderBy(b => b.RemainingSeconds(now) ?? double.MaxValue)
            .ThenBy(b => b.Label, StringComparer.Ordinal)
            .ToList();
        var rows = ordered.Select(b => new PeekRow(
            b.Label,
            BuffRosterPresentation.Clock(b.RemainingSeconds(now), b.Estimated),
            // The gauge is what is LEFT, so a full bar means a fresh buff — the same
            // direction the roster's own chip gauge runs.
            1 - (BuffRosterPresentation.ElapsedShare(b, now) ?? 0),
            BuffRosterPresentation.Detail(b))).ToList();

        return new PeekBody(subtext, rows, null,
            "buffs|" + string.Join(",", rows.Select(r => $"{r.Name}:{r.Value}")));
    }

    // ================================================================== OE-9 ====
    // The rest of the tray. Every cell on the minimized bar peeks and pops out now
    // (the owner's ~1:29 PM CT amend, 2026-09-07), and these five are the ones that
    // had no builder because they had no float to borrow one from.

    /// <summary>
    /// The Motes peek: one row per tier, count and rate, with #154's weighting on the
    /// subtext.
    ///
    /// **The per-tier rate is derived from the summary's OWN denominator rather than from a
    /// second division.** <c>Motes.Summarize</c> computes <c>PerHour = Total / hours</c> with
    /// a floor of one minute on <c>hours</c>; <c>PerHour × count ÷ Total</c> is
    /// <c>count ÷ hours</c> exactly, so this cannot drift from the card's rate the day that
    /// floor changes — which is what taking <c>elapsed</c> here and dividing again would have
    /// risked (trap 4 as arithmetic).
    ///
    /// **The signature is tier COUNTS only** (trap 8). The rates are in the values and the
    /// events are in the key, which is the shipped Watch-peek precedent: a per-hour figure
    /// drifts on every tick, so a key that carried one would rebuild the panel every second
    /// and throw away whatever the pointer was over.
    /// </summary>
    public static PeekBody Motes(MotesSummary motes)
    {
        var subtext = $"Session · {motes.Total} · {motes.PerHour:0.#}/hr"
            + (motes.PotencyPerHour > 0 ? $" · {motes.PotencyPerHour:0.#} potency/hr" : "");
        if (motes.Total == 0)
            return new PeekBody(subtext, [],
                "No motes yet this session. They appear here as you loot them.", "motes|empty");

        var rows = Gauged(motes.Tiers, t => t.Count, t => t.Item,
            t => $"{t.Count} · {Share(motes.PerHour, t.Count, motes.Total):0.#}/hr");
        return new PeekBody(subtext, rows, null,
            "motes|" + string.Join(",", motes.Tiers.Select(t => $"{t.Item}:{t.Count}")));
    }

    /// <summary>One row's share of a whole-list rate — see <see cref="Motes"/> for why the
    /// rate is apportioned rather than recomputed.</summary>
    private static double Share(double wholeRate, int part, int whole) =>
        whole <= 0 ? 0 : wholeRate * part / whole;

    /// <summary>
    /// The Kills peek: kills per creature, biggest first — literally the list the expanded
    /// Kills card fills (<c>StatsSnapshot.YourKills</c>, already ordered by Core).
    ///
    /// The rate on the subtext is <c>StatsSnapshot.KillsPerHour</c>, the session's own, and
    /// not a division performed here: the Kills card, the session summary and this must not
    /// be able to quote three numbers for one session.
    /// </summary>
    public static PeekBody Kills(IReadOnlyList<NameCount> kills, int total, double perHour)
    {
        var subtext = $"Session · {total} kill{(total == 1 ? "" : "s")} · {perHour:0.#}/hr";
        if (kills.Count == 0)
            return new PeekBody(subtext, [], "Nothing has died yet this session.", "kills|empty");

        var rows = Gauged(kills, k => k.Count, k => k.Name, k => $"{k.Count}");
        return new PeekBody(subtext, rows, null,
            "kills|" + string.Join(",", kills.Select(k => $"{k.Name}:{k.Count}")));
    }

    /// <summary>
    /// The Procs peek: each proc's count, its rate and its damage.
    ///
    /// **THE DENOMINATOR IS COMBAT MINUTES (#85, Kerdude), the same one the Procs block and
    /// the mini-bar cell already use** — so downtime does not flatter the weapon, and the
    /// three places a player can read a proc rate cannot disagree. That is also why this
    /// builder is what the DAMAGE FLOAT's new procs block reads: the card and the Live room
    /// each built proc rows inline, which was two producers before this made it one.
    ///
    /// **There is no healing here, and that is a fact about Core rather than a scope call.**
    /// <c>StatsSnapshot.Procs</c> is <c>(Name, Count, Damage)</c> — no healing field exists.
    /// Bevel #371 asked for "damage/healing/pertinent stats"; what ships is the stats the app
    /// tracks today, which is exactly what the Procs card shows. If the log distinguishes
    /// healing procs, teaching Core that is its own item and never a silent ride-along here.
    /// </summary>
    public static PeekBody Procs(
        IReadOnlyList<(string Name, int Count, long Damage)> procs, double combatSeconds)
    {
        var minutes = Math.Max(1.0 / 60, combatSeconds / 60.0);
        var count = procs.Sum(p => p.Count);
        var damage = procs.Sum(p => p.Damage);
        var subtext = $"Session · {count} proc{(count == 1 ? "" : "s")} · "
            + $"{count / minutes:0.#}/min · {damage:N0} dmg";
        if (procs.Count == 0)
            return new PeekBody(subtext, [],
                "No weapon procs yet this session.", "procs|empty");

        // Gauged by COUNT, because "/min" is the headline this surface exists for — a bar
        // drawn off damage would rank a rare heavy proc above the one actually firing.
        //
        // **The tooltip carries the untruncated row, and the first shot is why.** A proc's
        // name is "<spell> · <item>" whenever an item-proc line named the vehicle, and that
        // plus three facts does not fit the panel's one fixed 300 width — `hud-expand-procs`
        // came back "Exaltation Strike · Polished Mithril…" over "0.1/min · 42 d…". The ⧉
        // is the real answer (the Damage float's procs block draws these at full width), but
        // a hover costs nothing and a row a player cannot read is trap 14's family: correct,
        // clipped, and invisible to every test. Nothing but the picture says so.
        var rows = Gauged(procs, p => p.Count, p => p.Name,
            p => $"×{p.Count} · {p.Count / minutes:0.#}/min · {p.Damage:N0} dmg",
            p => $"{p.Name} — ×{p.Count} · {p.Count / minutes:0.#}/min · {p.Damage:N0} damage");
        return new PeekBody(subtext, rows, null,
            "procs|" + string.Join(",", procs.Select(p => $"{p.Name}:{p.Count}:{p.Damage}")));
    }

    /// <summary>
    /// The Money peek: the Wealth tab's coin facts — looted, vendor, total, per hour.
    ///
    /// **THE GAUGE IS A SHARE OF THE SESSION TOTAL, not of the biggest row** (the owner's
    /// ~4:32 PM CT shot, 2026-09-07). This builder shipped with every row at
    /// <c>Share = 0</c> and a comment arguing that four facts are not a ranking, so no bar
    /// belonged — but the host floors a bar at 1% (<c>BreakdownRows.Row</c>), so "no gauge"
    /// did not render as nothing. It rendered as four identical stubs on the left of four
    /// rows that plainly are parts of one figure, which reads as a broken gauge rather than
    /// as an absent one. The owner's call: they draw.
    ///
    /// **The denominator is <paramref name="total"/>, and it is the right one because Core
    /// makes it exact.** <c>StatsSnapshot.Copper</c> is <c>_copper + _vendorCopper</c> —
    /// looted plus sold, by construction — so Looted and Sold are true parts of it and their
    /// two bars fill the Total bar between them. Share-of-the-biggest (what <see cref="Gauged"/>
    /// draws everywhere else) would have made the LARGER of the two full and Total full as
    /// well, saying "these are equal" about a part and its whole.
    ///
    /// **Per hour keeps no gauge, and that is the honest answer rather than the tidy one.**
    /// It is a RATE: it has no share of a total, it is not bounded by one, and on any session
    /// under an hour it exceeds it. Drawing it against <paramref name="total"/> would assert
    /// a comparison that does not exist (<see cref="Loot"/>'s flat gauge is refused for the
    /// same reason, one dimension over). Its tooltip says so, so the one row without a bar
    /// answers "why" on hover instead of looking like the bug this change fixes.
    ///
    /// **The per-item <c>SoldItems</c> breakdown stays in the window.** The peek is row-capped
    /// at five and the ⧉ is one click from the full Wealth tab, which is what "the same
    /// content" can honestly mean at peek density — flagged rather than assumed.
    /// </summary>
    public static PeekBody Money(long total, long looted, long vendor, long perHour)
    {
        var subtext = $"Session · {StatsSnapshot.FormatCoin(total)} · "
            + $"{StatsSnapshot.FormatCoin(perHour)}/hr";
        if (total == 0)
            return new PeekBody(subtext, [],
                "No coin yet this session — looted or sold.", "money|empty");

        List<PeekRow> rows =
        [
            new("Looted", StatsSnapshot.FormatCoin(looted), CoinShare(looted, total),
                $"Looted from corpses — {Percent(looted, total)} of this session's coin"),
            new("Sold to vendors", StatsSnapshot.FormatCoin(vendor), CoinShare(vendor, total),
                $"Sold to vendors — {Percent(vendor, total)} of this session's coin"),
            // The whole, and the reference the two bars above are drawn against.
            new("Total", StatsSnapshot.FormatCoin(total), 1.0,
                "Looted + sold — the whole the shares above are drawn against"),
            new("Per hour", StatsSnapshot.FormatCoin(perHour), 0,
                "A rate, not a part of the total — there is no share to draw"),
        ];
        return new PeekBody(subtext, rows, null, $"money|{looted}|{vendor}|{total}|{perHour}");
    }

    /// <summary>One coin row's fraction of the session total, clamped.
    ///
    /// **The clamp is not decoration.** Core makes <c>Copper = CorpseCopper + VendorCopper</c>,
    /// so a part can never exceed the whole today — but this builder takes four independent
    /// longs from a caller, and a bar is drawn from whatever it is handed. A second producer
    /// of those numbers (a replay, a history row, a future surface) that disagreed by a copper
    /// would otherwise paint a gauge past its own track, which is the kind of thing nothing
    /// notices until it is in a screenshot.</summary>
    private static double CoinShare(long part, long total) =>
        total <= 0 ? 0 : Math.Clamp((double)part / total, 0, 1);

    /// <summary>The same fraction as a percentage, for the row's hover. Whole numbers: the
    /// gauge carries the comparison and the tooltip is there to name it, so a decimal point
    /// would be precision the bar cannot show.</summary>
    private static string Percent(long part, long total) =>
        $"{CoinShare(part, total) * 100:0}%";

}
