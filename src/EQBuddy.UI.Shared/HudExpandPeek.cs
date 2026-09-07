using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One row of an under-bar peek: a name, the value beside it, and how full its
/// gauge is.</summary>
/// <param name="Share">0–1, for the row's under-bar. It is a SHARE of the biggest row
/// rather than of the total, which is what every other breakdown list in the app draws.</param>
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
/// "what can this creature drop" identically. Only <see cref="Buffs"/> is deliberately NOT
/// what its float draws, and that is named rather than implied — see it below.
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
}
