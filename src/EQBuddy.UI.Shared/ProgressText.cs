using EQBuddy.Core;

namespace EQBuddy.UI.Shared;

/// <summary>
/// The Progress card's header and summary text, shared so the card and the Progress
/// breakout window always say the same thing (the LootRows precedent: one builder,
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

    /// <summary>Whether the Progress surface has anything at all to show — its empty
    /// test, kept beside the header so the two can never disagree: every counter the
    /// header can name (xp ticks, AA points, dings) plus the body's own lists must
    /// count here, or a window says "+2 aa" above "nothing seen yet" (the round-3
    /// review catch — AaGained accrues with no xp tick and no purchase). AaAbilities
    /// is the character's whole durable ledger, deliberately: a veteran with a quiet
    /// session still has their All-AA fold to read, which is content, not emptiness.</summary>
    public static bool HasContent(StatsSnapshot s) =>
        s.XpTicks > 0 || s.AaGained > 0 || s.Levels.Count > 0
        || s.SkillUps.Count > 0 || s.AaAbilities.Count > 0;

    /// <summary>Which AAs count as "learned this session": announced at or after the
    /// session's start (the ledger holds the character's whole history). One rule for
    /// the card and the breakout — two filters would drift (the trap-4 lesson).</summary>
    public static List<AaAbilityInfo> SessionNewAas(StatsSnapshot s) =>
        s.SessionStart is { } sess
            ? s.AaAbilities.Where(a => a.Time >= sess).ToList()
            : [];

}
