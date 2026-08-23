using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// What the Motes card says (Gate 5b) — the Potential upgrade-currency ladder, and the
/// per-hour rate farmers actually watch (discussion #49, flipwon).
///
/// The counting is Core's (<see cref="Motes.Summarize"/>, tested there). What lived
/// untested inside <c>RefreshUi</c> is the wording — and specifically the EMPTY wording,
/// which is the sentence that has to explain a card showing nothing without reading as a
/// broken one.
/// </summary>
public static class MotesPresentation
{
    /// <summary>The line under the header. An empty card explains itself rather than
    /// printing "0 motes/hr", which reads as a measurement rather than as "none yet" —
    /// and this card is often empty for an hour at a time, which is the whole reason the
    /// rate is interesting when it isn't.</summary>
    public static string Summary(MotesSummary motes) => motes.Total > 0
        ? $"{motes.PerHour:0.#} motes/hr this session"
        : "No motes yet this session — every Mote of … Potential you loot "
          + "(or store as currency) lands here.";

    /// <summary>
    /// Count AND rate on one line — "3 motes · 0.9/hr". Null when nothing has dropped,
    /// because a rate of nothing is a measurement rather than an answer.
    ///
    /// **One string, three callers, on purpose.** It is the Motes card's own header, the
    /// Progress Experience room's summary line (David, 2026-08-23: *"in Progress, show one
    /// line item only for motes per hour"*), and what the phone's Experience tab is sent.
    /// Writing the Progress line separately would have made it the FOURTH place this app
    /// formats a mote rate, and the three that already existed are how the Wealth chip and
    /// the Wealth body came to disagree (Bevel, 2026-08-22).
    ///
    /// The RATE is the half that matters and the half that went missing in 1.96.0 — #219
    /// (typical-usual-chaos): *"Where'd motes/hour go? That was the most useful stat and
    /// the main reason I opened EQBuddy."* A farmer is measuring a camp against the clock.
    /// </summary>
    public static string? RateLine(MotesSummary motes) => motes.Total > 0
        ? $"{motes.Total} mote{(motes.Total == 1 ? "" : "s")} · {motes.PerHour:0.#}/hr"
        : null;

    /// <summary>The ladder, richest tier first as Core orders it. Motes are items, so they
    /// click through to the wiki and hover their stats like any other.</summary>
    public static List<CardRow> Rows(MotesSummary motes) =>
        [.. motes.Tiers.Select(t => new CardRow(t.Item, $"×{t.Count}", Item: true))];
}
