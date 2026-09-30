using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>One slice of a meter's mix strip: a kind and its share of the meter's total.</summary>
/// <param name="Share">0..1 of the rows' summed total. The segments of one mix sum to 1.</param>
public sealed record OutputKindSegment(OutputKind Kind, string Label, double Share);

/// <summary>
/// **What a meter row's KIND looks like and is called** — the Founder's option A,
/// 2026-09-29: DPS/HPS rows keep damage order and each wears its type's colour (a square
/// before the name, the underline bar), with a thin stacked "mix" strip and a legend at the
/// top of the meter.
///
/// Every surface that draws a meter reads THIS file — the widget's Combat and Healing cards,
/// the breakout floats, the Live room, the HUD's expand panel and the phone — so the words,
/// the order and the shares cannot drift between them (trap 4, trap 33). The COLOURS are
/// <see cref="ThemeTones"/>' derived keys (<see cref="BrushKey"/>), so a theme picks its
/// light or dark set once and every host, the phone included, rides it.
///
/// The words are the Founder's: "DoT" and "HoT", never "over time".
/// </summary>
public static class OutputKindPresentation
{
    /// <summary>
    /// The FIXED kind order: the legend, the mix strip and the derived theme keys all follow
    /// it. **The mix is drawn in this order and not in damage order, deliberately**: the rows
    /// under it are already in damage order, and a strip whose segments swapped places every
    /// time two kinds crossed would read as movement where nothing but a share moved. The
    /// share is carried by each segment's WIDTH; its position is what makes the legend
    /// readable at a glance, fight after fight. Other is last — it is the "we don't know"
    /// bucket, and it should never be the first thing the eye lands on.
    /// </summary>
    public static readonly IReadOnlyList<OutputKind> Order =
    [
        OutputKind.Melee, OutputKind.Skill, OutputKind.Ranged, OutputKind.Spell, OutputKind.DoT,
        OutputKind.DamageShield, OutputKind.Proc, OutputKind.Pet, OutputKind.Heal, OutputKind.HoT,
        OutputKind.Other,
    ];

    /// <summary>The legend word for a kind.</summary>
    public static string Label(OutputKind kind) => kind switch
    {
        OutputKind.Melee => "Melee",
        OutputKind.Skill => "Skills",
        OutputKind.Ranged => "Ranged",
        OutputKind.Spell => "Spells",
        OutputKind.DoT => "DoT",
        OutputKind.DamageShield => "Damage shield",
        OutputKind.Proc => "Procs",
        OutputKind.Pet => "Pet",
        OutputKind.Heal => "Direct heals",
        OutputKind.HoT => "HoT",
        _ => "Other",
    };

    /// <summary>The derived theme resource that paints a kind — "Kind&lt;Name&gt;Brush", one
    /// per <see cref="Order"/> entry, in <see cref="ThemeTones.Keys"/>.</summary>
    public static string BrushKey(OutputKind kind) => $"Kind{Name(kind)}Brush";

    /// <summary>
    /// The kind as a WIRE token for the phone: a key, never a sentence (trap 32). It names
    /// both the page's CSS class (<c>kind-damage-shield</c>) and the theme token that paints
    /// it (<c>kindDamageShield</c> → <c>--kind-damage-shield</c>).
    /// </summary>
    public static string Token(OutputKind kind) => "kind" + Name(kind) switch
    {
        // The enum spells these with a capital inside the acronym; a CSS custom property is
        // kebab-cased off capitals, so "DoT" would become "do-t". The token spells them flat.
        "DoT" => "Dot",
        "HoT" => "Hot",
        var n => n,
    };

    /// <summary>
    /// The meter's mix: one segment per kind PRESENT, in <see cref="Order"/>, each with its
    /// share of the rows' summed total. Empty when there is nothing to explain — no rows, no
    /// total, or every row <see cref="OutputKind.Other"/> (an archived session, whose rows
    /// draw grey and whose strip would be one grey bar labelled "Other": a legend for a
    /// colour that says nothing).
    /// </summary>
    public static IReadOnlyList<OutputKindSegment> Mix(IEnumerable<SourceDamage> rows)
    {
        var byKind = new Dictionary<OutputKind, long>();
        long total = 0;
        foreach (var r in rows)
        {
            if (r.Total <= 0) continue;
            byKind[r.Kind] = byKind.GetValueOrDefault(r.Kind) + r.Total;
            total += r.Total;
        }
        if (total == 0 || byKind.Keys.All(k => k == OutputKind.Other)) return [];
        return [.. Order.Where(byKind.ContainsKey)
            .Select(k => new OutputKindSegment(k, Label(k), (double)byKind[k] / total))];
    }

    /// <summary>A legend entry's words: the label and its rounded share ("Melee 62%"). A share
    /// that rounds to nothing says "&lt;1%" rather than a "0%" beside a visible colour.</summary>
    public static string LegendText(OutputKindSegment s)
    {
        var pct = Math.Round(100 * s.Share);
        return pct < 1 ? $"{s.Label} <1%" : $"{s.Label} {pct:0}%";
    }

    private static string Name(OutputKind kind) => kind.ToString();
}
