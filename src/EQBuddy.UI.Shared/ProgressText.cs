using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// The Progress card's header and summary text, shared so the WPF and Avalonia cards
/// always say the same thing (the LootRows precedent: one builder,
/// two surfaces). LevelUnlocks/LevelUnlockText cover the unlock rows; this covers
/// the prose.
/// </summary>
public static class ProgressText
{
    /// <summary>Header value: "12.3% xp, +1 lvl (3 new), +2 aa". The ding cue
    /// (<paramref name="dingCount"/>, AAs and spells newly available at the
    /// session's latest level) rides here because the header is the only Progress
    /// surface that always shows.</summary>
    public static string Header(StatsSnapshot s, int dingCount) =>
        $"{s.XpPercent:0.0}% xp"
        + (s.Levels.Count > 0
            ? $", +{s.Levels.Count} lvl" + (dingCount > 0 ? $" ({dingCount} new)" : "")
            : "")
        + (s.AaGained > 0 ? $", +{s.AaGained} aa" : "");

    /// <summary>The card's dim summary block, as ONE string with a caller-chosen
    /// separator. The content is <see cref="ProgressPresentation.SummaryLines"/> —
    /// upstream's Gate 5b built the same extraction this fork had, so theirs is the
    /// single source now and this is only the seam the Avalonia build reads through:
    /// it passes " - ", its file-wide plain-ASCII convention under fonts Wine/Linux
    /// may lack, and a wording fix upstream reaches it unchanged.</summary>
    public static string Summary(StatsSnapshot s, string sep = " · ") =>
        string.Join("\n", ProgressPresentation.SummaryLines(s)
            .Select(line => line.Replace(" · ", sep)));

    /// <summary>Which AAs count as "learned this session": announced at or after the
    /// session's start (the ledger holds the character's whole history). One rule for
    /// both UIs — two filters would drift (the trap-4 lesson).</summary>
    public static List<AaAbilityInfo> SessionNewAas(StatsSnapshot s) =>
        s.SessionStart is { } sess
            ? s.AaAbilities.Where(a => a.Time >= sess).ToList()
            : [];

}
