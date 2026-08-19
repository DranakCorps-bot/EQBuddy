namespace EQBuddy.UI.Shared;

/// <summary>
/// What a breakdown list can be ordered by, and what the strip that offers it says.
///
/// This is the app's most-rebuilt control after the chip itself: "sort: total dps hits
/// avg", written by hand as bare TextBlocks with a Tag and a click handler in the Combat
/// card, again in the damage-taken block, again for healing, and again in every breakout
/// window. Gate 5 spends `EqSegmentedStrip` on all of them, and this is the half that
/// isn't a control — which metrics exist, in what order, and what each is called on a
/// surface that measures damage versus one that measures healing.
///
/// The wording matters and was getting it wrong quietly: healing counts CASTS, not hits,
/// and its rate is HPS, not DPS. Both UIs derived that from a substring test on the
/// heading text ("does the title contain 'Heal'?"), in two places, which is a rule you
/// cannot find by searching for either word.
/// </summary>
public static class SortStrip
{
    /// <summary>The metric a list is ordered by. Named here rather than in each UI so the
    /// strip, the row builder and the persisted setting all mean the same four things.</summary>
    public enum Metric { Total, Rate, Hits, Avg }

    /// <param name="Metric">What it sorts by.</param>
    /// <param name="Label">The chip's word — lower case; a strip is navigation.</param>
    /// <param name="Tip">Hover copy, or null. Only the rate needs one: "dps" on a
    /// per-ability row is not the DPS people expect, and saying so is the difference
    /// between a number and a misleading number.</param>
    public readonly record struct Option(Metric Metric, string Label, string? Tip);

    /// <summary>What a strip over DAMAGE offers.</summary>
    public static IReadOnlyList<Option> ForDamage { get; } =
    [
        new(Metric.Total, "total", null),
        new(Metric.Rate, "dps",
            "Per-ability DPS: that ability's damage ÷ total time in combat"),
        new(Metric.Hits, "hits", null),
        new(Metric.Avg, "avg", null),
    ];

    /// <summary>What a strip over HEALING offers. Casts, not hits; hps, not dps.</summary>
    public static IReadOnlyList<Option> ForHealing { get; } =
    [
        new(Metric.Total, "total", null),
        new(Metric.Rate, "hps",
            "Per-spell HPS: that spell's healing ÷ total time in combat"),
        new(Metric.Hits, "casts", null),
        new(Metric.Avg, "avg", null),
    ];

    /// <summary>Damage TAKEN has no rate column: incoming damage per second of your own
    /// combat time is a number with no meaning, and offering it would invite the reading
    /// that it is somebody's DPS on you.</summary>
    public static IReadOnlyList<Option> ForDamageTaken { get; } =
        [.. ForDamage.Where(o => o.Metric != Metric.Rate)];

    /// <summary>An option on a strip that orders ROWS rather than a metric column — the
    /// Watch card's manual / a–z / total / recent. Its key is the stored setting value,
    /// because unlike <see cref="Metric"/> these were never an enum: both UIs held the
    /// same four <c>(mode, label)</c> tuples inline and compared them to
    /// <c>AppSettings.WatchSortMode</c> by string.</summary>
    /// <param name="Key">The value persisted in <c>AppSettings.WatchSortMode</c>.</param>
    /// <param name="Label">The chip's word — lower case; a strip is navigation.</param>
    /// <param name="Tip">Hover copy, or null.</param>
    public readonly record struct ModeOption(string Key, string Label, string? Tip);

    /// <summary>What a strip over WATCH RULES offers (#105, wizen).
    ///
    /// "manual" is the odd one and is the reason this needs a tooltip at all: it is not a
    /// sort the card performs, it is the order the rules already sit in over in Options.
    /// The wording says where to change it, and says it WITHOUT naming a glyph — the
    /// arrows it used to point at are tofu on a Wine prefix (#148, #166).</summary>
    public static IReadOnlyList<ModeOption> ForWatchRules { get; } =
    [
        new("manual", "manual",
            "The order your rules sit in over in Options — reorder them there"),
        new("alpha", "a–z", null),
        new("total", "total", null),
        new("recent", "recent", "Most recently matched first; never-matched rules sink"),
    ];

    /// <summary>The caption a strip carries when it needs one.
    ///
    /// It does NOT always need one, and Gate 5's first screenshot is why: on the widget a
    /// strip sits on the same row as the heading that already names the list ("Damage by
    /// attack"), and four chips plus "sort:" left that heading trimmed to "Damage b…" in a
    /// 342px window. Chips reading total / dps / hits / avg are self-evidently a sort.
    ///
    /// **A caption earns its place when two strips share a row** — the Loot card's
    /// "show:" and "sort:" would be one undifferentiated line of pills without them — and
    /// is redundant when a single strip sits beside its own heading.</summary>
    public const string Caption = "sort:";

    /// <summary>Settings round-trip. Anything unrecognised is Total — the default every
    /// one of these strips has always started on.</summary>
    public static Metric Parse(string? stored) => stored switch
    {
        "hits" => Metric.Hits,
        "avg" => Metric.Avg,
        "rate" => Metric.Rate,
        _ => Metric.Total,
    };

    public static string Key(Metric metric) => metric switch
    {
        Metric.Hits => "hits",
        Metric.Avg => "avg",
        Metric.Rate => "rate",
        _ => "total",
    };
}
