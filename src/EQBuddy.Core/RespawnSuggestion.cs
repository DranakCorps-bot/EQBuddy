
namespace EQBuddy.Core;

/// <summary>What the pack may say about one creature's respawn timer.</summary>
public enum RespawnVerdictKind
{
    /// <summary>Nothing to say — no evidence, evidence below the bar, or a mob whose
    /// kind has no cycle to suggest. The numbers still travel in the edit summary when
    /// there are any (the SuggestRarity rule verbatim).</summary>
    None,
    /// <summary>The wiki's field is absent or empty and the observed cycles agree —
    /// offer the paste.</summary>
    Suggest,
    /// <summary>The wiki already says what we observed — "nothing to add", said so the
    /// player knows the check HAPPENED (the KnownDrops rule for timers).</summary>
    WikiAgrees,
    /// <summary>The wiki says something materially different — present both for a human
    /// to reconcile. Compare, don't overwrite (the stat-block rule).</summary>
    WikiDisagrees,
}

public sealed record RespawnVerdict(
    RespawnVerdictKind Kind, string Wording, string Note, IReadOnlyList<SpawnCycle> Cycles);

/// <summary>
/// The honesty bar for suggesting a respawn timer to eqlwiki (Fable 5's plan,
/// 2026-08-22). This whole class is the plan's "the bar is the product": too low and
/// EQBuddy becomes the source of wrong timers on the shared reference — uniquely wrong,
/// the thing the match-the-wiki rule exists to prevent; too high and nothing is ever
/// suggested.
///
/// **The bar: at least 3 cycles, all within ±15 % of their median, median ≥ 90 s.**
/// Agreement is the evidence of attention — a player who left the camp produces
/// scattered gaps, not three that agree. Kill-to-kill alone never determines a duration
/// (Scribe's own argument against catalogs), which is why one gap, however clean, is
/// never enough. Below the bar nothing is suggested and the raw cycles travel in the
/// edit summary only — <c>SuggestRarity</c>'s thin-sample rule verbatim.
///
/// **Never for triggered, raid-instanced or multi-spawn entries**, whatever the ledger
/// holds: no cycle exists to suggest (the bees, #109), or sibling noise pollutes the
/// gaps. The caller carries that flag from the catalog entry.
///
/// Everything is computed ONCE here and every consumer (the paste, the row note, the
/// edit summary) reads the result — trap 4's one-fact-one-source rule.
/// </summary>
public static class RespawnSuggestion
{
    public const int MinCycles = 3;
    public const double AgreementTolerance = 0.15;
    public const double MinMedianSeconds = 90;

    /// <summary>The wiki's own idiom — minutes under an hour ("22 min"), hours ("6
    /// hours"), days ("3 days"). Never a variance clause: three cycles cannot measure
    /// variance, and a wrong one is worse than none. <c>SpawnDurationText</c> formats
    /// for OUR chips; this formats for the wiki, and the tests pin the two idioms
    /// apart.</summary>
    public static string WikiWording(double seconds)
    {
        if (seconds >= 86400 && Math.Abs(seconds % 86400) < 1800)
        {
            var days = Math.Round(seconds / 86400);
            return $"{days:g0} day{(days == 1 ? "" : "s")}";
        }
        if (seconds >= 3600 && Math.Abs(seconds % 3600) < 90)
        {
            var hours = Math.Round(seconds / 3600);
            return $"{hours:g0} hour{(hours == 1 ? "" : "s")}";
        }
        var minutes = seconds / 60;
        return $"{(Math.Abs(minutes - Math.Round(minutes)) < 0.05 ? Math.Round(minutes) : Math.Round(minutes, 1)):g} min";
    }

    /// <summary>The observed median, when the bar is met; null otherwise.</summary>
    public static double? AgreedMedianSeconds(IReadOnlyList<SpawnCycle> cycles)
    {
        if (cycles.Count < MinCycles) return null;
        var sorted = cycles.Select(c => c.DurationSeconds).OrderBy(s => s).ToList();
        var median = sorted[sorted.Count / 2];
        if (sorted.Count % 2 == 0) median = (median + sorted[sorted.Count / 2 - 1]) / 2;
        if (median < MinMedianSeconds) return null;
        return cycles.All(c => Math.Abs(c.DurationSeconds - median) <= median * AgreementTolerance)
            ? median : null;
    }

    /// <summary>
    /// The three-way compare: observed cycles · the wiki's own field · the catalog.
    /// </summary>
    /// <param name="suppressed">Triggered / raid-instanced / multi-spawn, from the
    /// catalog entry — no suggestion ever, whatever the ledger holds.</param>
    /// <param name="wikiField">The page's raw <c>respawn_time</c> ("" = absent/empty).
    /// Free prose, matched loosely and never parsed to seconds.</param>
    public static RespawnVerdict Evaluate(
        IReadOnlyList<SpawnCycle> cycles, bool suppressed, string wikiField)
    {
        if (suppressed || cycles.Count == 0)
            return new(RespawnVerdictKind.None, "", "", cycles);

        if (AgreedMedianSeconds(cycles) is not { } median)
            return new(RespawnVerdictKind.None, "",
                cycles.Count == 1
                    ? "1 observed cycle — the bar is 3 that agree"
                    : $"{cycles.Count} observed cycles that do not agree within ±15 % — " +
                      "a scattered sample usually means the camp was not watched end to end",
                cycles);

        var wording = WikiWording(median);
        var note = $"observed {wording} over {cycles.Count} agreeing cycles";

        if (wikiField.Trim().Length == 0)
            return new(RespawnVerdictKind.Suggest, wording, note, cycles);

        return Matches(wikiField, median)
            ? new(RespawnVerdictKind.WikiAgrees, wording,
                $"wiki already says \"{wikiField.Trim()}\" — nothing to add", cycles)
            : new(RespawnVerdictKind.WikiDisagrees, wording,
                $"wiki says \"{wikiField.Trim()}\" · you observed {wording} — compare, don't overwrite",
                cycles);
    }

    /// <summary>Does the wiki's free-prose field plausibly mean our median? Loose on
    /// purpose: "9.5 min", "9.5 minutes", "~10 min" and "570 seconds" all describe one
    /// timer, and calling any of them a DISAGREEMENT would put a false conflict in
    /// front of a player. Anything non-numeric ("Triggered", "unknown") is a
    /// disagreement to show, not to hide.</summary>
    public static bool Matches(string wikiField, double medianSeconds)
    {
        var m = System.Text.RegularExpressions.Regex.Match(wikiField,
            @"(\d+(?:\.\d+)?)\s*(day|hour|hr|min|minute|sec|second|m\b|h\b|s\b)?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success || m.Groups[1].Value.Length == 0) return false;
        var value = double.Parse(m.Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        var unit = m.Groups[2].Value.ToLowerInvariant();
        var seconds = unit switch
        {
            "day" => value * 86400,
            "hour" or "hr" or "h" => value * 3600,
            "sec" or "second" or "s" => value,
            _ => value * 60,   // the wiki's default idiom is minutes
        };
        // The same tolerance as the bar: within ±15 % is the same timer.
        return Math.Abs(seconds - medianSeconds) <= medianSeconds * AgreementTolerance;
    }
}
