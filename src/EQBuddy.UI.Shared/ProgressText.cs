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

    // Summary(s, sep) lived here as the seam the v1 Avalonia card read the summary block
    // through (with an ASCII " - " separator). Its one caller left in E-2c; the WPF card
    // reads ProgressPresentation.SummaryLines directly.

    /// <summary>Which AAs count as "learned this session": announced at or after the
    /// session's start (the ledger holds the character's whole history). One rule for
    /// the card and the breakout — two filters would drift (the trap-4 lesson).</summary>
    public static List<AaAbilityInfo> SessionNewAas(StatsSnapshot s) =>
        s.SessionStart is { } sess
            ? s.AaAbilities.Where(a => a.Time >= sess).ToList()
            : [];

}
