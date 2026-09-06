using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One watch rule that has fired and is still lingering on the HUD chip row.</summary>
/// <param name="RuleId">The rule's stable id, never its name — names collide, and two rules
/// both called "Asaka" already shared one cooldown once for exactly that reason
/// (<see cref="TrackedRule.Id"/>'s own doc comment).</param>
/// <param name="RuleName">What the chicklet is labelled with: the rule's name, or its pattern
/// when it has none — the same string the banner puts before the colon.</param>
/// <param name="Label">What matched. Goes in the tooltip rather than on the face: a Text
/// rule's label is a trimmed log line up to 80 characters, and the chicklet's name column
/// trims at 180px with no way to say it did.</param>
/// <param name="FiredAt">When it last fired. A re-fire moves this rather than adding a second
/// chicklet.</param>
public readonly record struct WatchFire(
    string RuleId, string RuleName, string Label, DateTime FiredAt);

/// <summary>
/// WHICH WATCH RULES ARE STILL ON THE HUD (Surface A / SA-3) — the ledger behind the
/// watch-fire chip family.
///
/// **The chip is the toast's second chance.** <c>AlertWindow</c> holds its banner for six
/// seconds and then it is gone; a player who was looking at the game rather than at the
/// widget has no way back to it. B3's earn table gives Watch alerts the overlay because they
/// are a deadline with an action, and a signal that has already vanished by the time you look
/// up is not one. So the chicklet lingers for <see cref="Linger"/> — long enough to be found,
/// short enough that the row is not a log.
///
/// **One chicklet per RULE, not per firing.** A chatty rule (a Text rule on a raid channel,
/// a Loot rule on a common drop) fires repeatedly, and a ledger keyed on the event would put
/// twenty identical chicklets on the row inside a pull. Re-firing refreshes the countdown and
/// re-labels the existing entry, which is also the honest reading: the chip says "this rule
/// is live right now", and the count that matters is one.
///
/// **No cap, deliberately** (trap 50). The row wraps rather than growing off the monitor, and
/// a "top N" would hide exactly what a player most needs to see — the rule that has started
/// firing constantly. A player with fifteen simultaneously-live rules has something to fix in
/// Options, and the row showing fifteen is what tells them so.
///
/// Framework-free and unit-tested with no window, like everything the row decides.
/// </summary>
public sealed class WatchFireLedger
{
    /// <summary>How long a fired rule keeps its chicklet.
    ///
    /// **A pinned constant, not a setting** (SA-3 item 2, Helm-signed plan): threshold tuning
    /// waits for a player to ask for it, and SA-4 brings the per-family Mute that is the real
    /// answer to "I do not want these". Five times <c>AlertWindow</c>'s six-second banner —
    /// the toast answers "did something just happen" while you are looking at the widget, and
    /// the chip answers "what did I miss" for the half-minute after you look up.</summary>
    public static readonly TimeSpan Linger = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, WatchFire> _live = new(StringComparer.Ordinal);

    /// <summary>A rule fired. Re-firing an already-lingering rule refreshes its countdown and
    /// takes the newer label rather than adding a second chicklet.</summary>
    public void Record(string ruleId, string ruleName, string label, DateTime now)
    {
        if (ruleId.Length == 0) return;   // an id-less rule cannot be told from any other
        _live[ruleId] = new WatchFire(ruleId, ruleName, label, now);
    }

    /// <summary>
    /// A rule fired — record it IF this rule is one the player wants on screen. Returns
    /// whether anything was recorded, so the caller knows whether the row needs a repaint.
    ///
    /// **<see cref="TrackedRule.AlertBanner"/> is the gate, and it is the SA-3 judgement worth
    /// reading twice.** The plan says the chip rides the same event that drives
    /// <c>AlertSoundPlan</c> — which names the EVENT, not the gate. `AlertBanner` is the
    /// switch a player already has for "put this rule on my screen"; a rule with it off is one
    /// somebody turned off on purpose. Until SA-4 brings the per-family Mute, firing
    /// regardless would add an on-screen output with no off-switch anywhere, to rules that
    /// already have one — which is "silent no-ops are broken" with the switch on the other
    /// side. Sound and speech are different channels and neither is touched either way.
    ///
    /// It lives here rather than in the window so it can be asserted at all: the WPF layer has
    /// no unit tests, and this is a decision rather than a wiring detail.
    /// </summary>
    public bool Record(TrackedRule rule, string ruleName, string label, DateTime now)
    {
        if (!rule.AlertBanner || rule.Id.Length == 0) return false;
        Record(rule.Id, ruleName, label, now);
        return true;
    }

    /// <summary>Right-click on the chicklet. The rule is untouched — dismissing is about this
    /// screen right now, exactly as it is for a spawn or slow chip, and the next firing brings
    /// the chicklet back.</summary>
    public void Dismiss(string ruleId) => _live.Remove(ruleId);

    /// <summary>Everything still inside its linger, newest first — the freshest alert is the
    /// one you are most likely to be looking for, and it holds the leftmost place in the
    /// family for the rest of its life rather than sliding along as its neighbours expire.
    ///
    /// Expired entries are dropped here rather than by a timer: the row asks once a second
    /// anyway, and a ledger that needs its own clock is a second thing to keep alive.</summary>
    public List<WatchFire> Snapshot(DateTime now)
    {
        foreach (var id in _live.Where(kv => Expired(kv.Value, now)).Select(kv => kv.Key).ToList())
            _live.Remove(id);
        return [.. _live.Values.OrderByDescending(f => f.FiredAt).ThenBy(f => f.RuleId, StringComparer.Ordinal)];
    }

    /// <summary>Is anything lingering? The cheap emptiness probe, so the row does not build a
    /// list twice a second just to learn it was empty — the same shape
    /// <see cref="ChipStackPlan.FightStack"/>'s callers use.</summary>
    public bool Any(DateTime now)
    {
        foreach (var fire in _live.Values) if (!Expired(fire, now)) return true;
        return false;
    }

    /// <summary>Share of the linger already spent, 0..1 — the chicklet's ELAPSED fraction, so
    /// the draining gauge empties as the chip's own life runs out.</summary>
    public static double Spent(WatchFire fire, DateTime now) =>
        Math.Clamp((now - fire.FiredAt).TotalSeconds / Linger.TotalSeconds, 0, 1);

    /// <summary>Seconds of linger left. Clamped at zero rather than allowed negative: a chip
    /// that is a tick past its welcome reads 0:00 for that tick and then goes.</summary>
    public static double Remaining(WatchFire fire, DateTime now) =>
        Math.Max(0, (fire.FiredAt + Linger - now).TotalSeconds);

    private static bool Expired(WatchFire fire, DateTime now) => now - fire.FiredAt >= Linger;
}
